"use client";

import React, { useEffect, useRef, useState } from "react";
import { Plyr, APITypes, PlyrProps } from "plyr-react";
import Hls from "hls.js";

interface PremiumPlayerProps {
    src: string;
    mediaId?: string;
    onLoaded?: () => void;
    autoplay?: boolean;
    poster?: string | null;
}

export const PremiumPlayer = React.memo(function PremiumPlayer({ src, mediaId, onLoaded, autoplay = false, poster }: PremiumPlayerProps) {
    const plyrRef = useRef<APITypes>(null);
    const hlsRef = useRef<Hls | null>(null);
    const lastValidSrcRef = useRef<string>(src);
    const [supported, setSupported] = useState(true);
    const [qualities, setQualities] = useState<number[]>([]);

    if (src) {
        lastValidSrcRef.current = src;
    }
    const currentSrc = src || lastValidSrcRef.current || "";

    if (!currentSrc) return null;


    // Tam URL'yi oluştur
    const fullSrc = currentSrc.startsWith("/") 
        ? `${process.env.NEXT_PUBLIC_API_URL?.replace("/api/v1", "") || "http://localhost:5292"}${currentSrc}`
        : currentSrc;

    const isHls = fullSrc.includes(".m3u8");

    useEffect(() => {
        let videoElementCleanup: HTMLVideoElement | null = null;
        let handleLoadedMetadataCleanup: (() => void) | null = null;
        let handleTimeUpdateCleanup: (() => void) | null = null;

        // HLS Kurulumu
        const initializePlayer = () => {
            const plyrInstance = plyrRef.current?.plyr;
            if (!plyrInstance) return;

            const videoElement = plyrInstance.media as HTMLVideoElement;
            if (!videoElement) return;
            videoElementCleanup = videoElement;

            const handleLoadedMetadata = () => {
                if (mediaId) {
                    const savedTimeStr = localStorage.getItem(`muro_video_time_${mediaId}`);
                    const savedTime = savedTimeStr ? parseFloat(savedTimeStr) : 0;
                    if (savedTime > 3 && Math.abs(videoElement.currentTime - savedTime) > 1) {
                        videoElement.currentTime = savedTime;
                    }
                }
                if (onLoaded) onLoaded();
            };
            handleLoadedMetadataCleanup = handleLoadedMetadata;

            const handleTimeUpdate = () => {
                if (mediaId && videoElement.currentTime > 0 && !videoElement.seeking) {
                    const duration = videoElement.duration || 1;
                    const percent = (videoElement.currentTime / duration) * 100;
                    if (percent > 98) {
                        localStorage.removeItem(`muro_video_time_${mediaId}`);
                    } else {
                        localStorage.setItem(`muro_video_time_${mediaId}`, videoElement.currentTime.toString());
                    }
                }
            };
            handleTimeUpdateCleanup = handleTimeUpdate;

            videoElement.addEventListener("loadedmetadata", handleLoadedMetadata);
            videoElement.addEventListener("timeupdate", handleTimeUpdate);

            if (isHls) {
                if (Hls.isSupported()) {
                    if (hlsRef.current) {
                        hlsRef.current.destroy();
                    }

                    const hls = new Hls({ maxBufferLength: 30 });
                    hls.loadSource(fullSrc);
                    hls.attachMedia(videoElement);
                    
                    hls.on(Hls.Events.MANIFEST_PARSED, (event, data) => {
                        const availableQualities = data.levels.map((l) => l.height).sort((a, b) => b - a);
                        setQualities(availableQualities);
                        
                        if (mediaId) {
                            const savedTimeStr = localStorage.getItem(`muro_video_time_${mediaId}`);
                            const savedTime = savedTimeStr ? parseFloat(savedTimeStr) : 0;
                            if (savedTime > 3 && Math.abs(videoElement.currentTime - savedTime) > 1) {
                                videoElement.currentTime = savedTime;
                            }
                        }

                        if (onLoaded) onLoaded();
                        if (autoplay) {
                            const playPromise = videoElement.play();
                            if (playPromise !== undefined) {
                                playPromise.catch(() => { /* Autoplay engellendi */ });
                            }
                        }
                    });

                    hlsRef.current = hls;

                } else if (videoElement.canPlayType("application/vnd.apple.mpegurl")) {
                    // Native HLS (Safari vb.)
                    videoElement.src = fullSrc;
                    videoElement.addEventListener("loadedmetadata", handleLoadedMetadata);
                } else {
                    setSupported(false);
                }
            } else {
                // Native MP4 (Direct File)
                videoElement.src = fullSrc;
                videoElement.addEventListener("loadedmetadata", handleLoadedMetadata);
            }
        };

        const timer = setTimeout(() => {
            initializePlayer();
        }, 50);

        return () => {
            clearTimeout(timer);
            if (videoElementCleanup) {
                if (handleLoadedMetadataCleanup) {
                    videoElementCleanup.removeEventListener("loadedmetadata", handleLoadedMetadataCleanup);
                }
                if (handleTimeUpdateCleanup) {
                    videoElementCleanup.removeEventListener("timeupdate", handleTimeUpdateCleanup);
                }
            }
            if (hlsRef.current) {
                try { hlsRef.current.destroy(); } catch (e) { console.error("HLS destroy error", e); }
                hlsRef.current = null;
            }
        };
    }, [fullSrc, isHls, onLoaded, autoplay, mediaId]);

    if (!supported) {
        return (
            <div className="w-full h-full flex items-center justify-center bg-[#0A1931] text-white/50 text-sm">
                Tarayıcınız bu video formatını desteklemiyor.
            </div>
        );
    }

    const vttPath = fullSrc.includes("master.m3u8") 
        ? fullSrc.replace("master.m3u8", "thumbnails.vtt")
        : undefined;

    const plyrOptions: PlyrProps["options"] = {
        ratio: "16:9",
        fullscreen: { enabled: true, fallback: true, iosNative: true },
        controls: [
            "play-large", "play", "progress", "current-time", "duration", 
            "mute", "volume", "captions", "settings", "pip", "airplay", "fullscreen"
        ],
        settings: ["quality", "speed"],
        quality: qualities.length > 0 ? {
            default: qualities[0],
            options: qualities,
            forced: true,
            onChange: (q: number) => {
                if (hlsRef.current) {
                    const levelIndex = hlsRef.current.levels.findIndex(l => l.height === q);
                    if (levelIndex !== -1) {
                        hlsRef.current.currentLevel = levelIndex;
                    }
                }
            }
        } : {
            default: 720,
            options: [1080, 720, 480, 360],
            forced: false
        },
        speed: { selected: 1, options: [0.5, 0.75, 1, 1.25, 1.5, 2] },
        disableContextMenu: true,
        keyboard: { focused: true, global: true },
        tooltips: { controls: true, seek: true },
        previewThumbnails: vttPath ? { enabled: true, src: vttPath } : { enabled: false },
    };

    const posterFullUrl = poster 
        ? (poster.startsWith("/") ? `${process.env.NEXT_PUBLIC_API_URL?.replace("/api/v1", "") || "http://localhost:5292"}${poster}` : poster)
        : undefined;

    return (
        <div className="w-full h-full premium-player-wrapper bg-black" onContextMenu={e => e.preventDefault()}>
            <Plyr
                ref={plyrRef}
                source={{
                    type: "video",
                    poster: posterFullUrl,
                    sources: [
                        {
                            src: fullSrc,
                            type: isHls ? "application/x-mpegURL" : "video/mp4",
                        }
                    ]
                }}
                options={plyrOptions}
            />
        </div>
    );
}, (prevProps, nextProps) => {
    return prevProps.src === nextProps.src && 
           prevProps.poster === nextProps.poster && 
           prevProps.autoplay === nextProps.autoplay;
});
