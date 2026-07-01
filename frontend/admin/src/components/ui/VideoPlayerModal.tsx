"use client";

import React, { useEffect, useRef, useState } from 'react';
import { X, Loader2, Video } from 'lucide-react';
import Hls from 'hls.js';
import Plyr from 'plyr';
import 'plyr/dist/plyr.css';
import type { MediaAssetDto } from '@/lib/api';

interface VideoPlayerModalProps {
    asset: MediaAssetDto | null;
    onClose: () => void;
}

export function VideoPlayerModal({ asset, onClose }: VideoPlayerModalProps) {
    const videoRef = useRef<HTMLVideoElement>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (!asset || !videoRef.current) return;

        const video = videoRef.current;
        setIsLoading(true);
        setError(null);

        let videoSrc = asset.hlsPath || asset.filePath;

        if (!videoSrc) {
            setError("Bu video için oynatılabilir bir dosya bulunamadı.");
            setIsLoading(false);
            return;
        }

        if (videoSrc.startsWith('/')) {
            const baseUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5292/api/v1";
            try {
                const backendHost = baseUrl.replace('/api/v1', '');
                videoSrc = `${backendHost}${videoSrc}`;
            } catch (e) {
                console.error(e);
            }
        }

        let hls: Hls | null = null;
        let player: Plyr | null = null;

        const initPlyr = (qualities: number[] = []) => {
            const vttPath = videoSrc.includes("master.m3u8") 
                ? videoSrc.replace("master.m3u8", "thumbnails.vtt")
                : undefined;

            const plyrOptions: any = {
                controls: ['play-large', 'play', 'progress', 'current-time', 'duration', 'mute', 'volume', 'captions', 'settings', 'pip', 'airplay', 'fullscreen'],
                settings: ['captions', 'quality', 'speed'],
                speed: { selected: 1, options: [0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.25, 2.5] },
                previewThumbnails: vttPath ? { enabled: true, src: vttPath } : { enabled: false },
                keyboard: { focused: true, global: true }
            };

            if (qualities.length > 0) {
                plyrOptions.quality = {
                    default: qualities[0],
                    options: qualities,
                    forced: true,
                    onChange: (q: number) => {
                        if (hls) {
                            const levelIndex = hls.levels.findIndex(l => l.height === q);
                            if (levelIndex !== -1) {
                                hls.nextLevel = levelIndex;
                            }
                        }
                    }
                };
            }

            player = new Plyr(video, plyrOptions);
        };

        if (asset.hlsPath && Hls.isSupported()) {
            hls = new Hls({ maxBufferLength: 30, enableWorker: true });
            hls.loadSource(videoSrc);
            hls.attachMedia(video);
            
            hls.on(Hls.Events.MANIFEST_PARSED, (event, data) => {
                const availableQualities = data.levels.map((l: any) => l.height).sort((a: number, b: number) => b - a);
                setIsLoading(false);
                initPlyr(availableQualities);
                video.play().catch(console.error);
            });

            hls.on(Hls.Events.ERROR, (event, data) => {
                if (data.fatal) {
                    setError("HLS Error: " + data.details);
                    setIsLoading(false);
                }
            });
        } else if (video.canPlayType('application/vnd.apple.mpegurl') || asset.filePath) {
            video.src = videoSrc;
            video.addEventListener('loadedmetadata', () => {
                setIsLoading(false);
                initPlyr();
                video.play().catch(console.error);
            });
        } else {
            setError("Tarayıcınız bu videoyu desteklemiyor.");
            setIsLoading(false);
        }

        return () => { 
            if (hls) hls.destroy(); 
            if (player) player.destroy();
        };
    }, [asset]);

    if (!asset) return null;

    return (
        <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/90 backdrop-blur-xl animate-in fade-in duration-300">
            <div className="absolute top-6 right-6">
                <button onClick={onClose} className="p-3 bg-white/10 hover:bg-white/20 text-white rounded-full transition-all hover:scale-105 backdrop-blur-md border border-white/10" title="Kapat">
                    <X size={24} />
                </button>
            </div>

            <div className="w-full max-w-6xl mx-auto px-6 flex flex-col items-center">
                <div className="w-full mb-6 flex justify-between items-end">
                    <div>
                        <h2 className="text-white text-2xl font-semibold tracking-tight">{asset.title}</h2>
                        <p className="text-gray-400 text-sm mt-1">
                            {asset.status === 'Ready' ? '✅ Hazır' : 
                             asset.status === 'Uploading' ? '⏳ Yükleniyor' : 
                             asset.status === 'Processing' ? '⚙️ İşleniyor' : 
                             '❌ Hata'} 
                            {asset.durationSeconds ? ` • ${Math.floor(asset.durationSeconds / 60)}:${(asset.durationSeconds % 60).toString().padStart(2, '0')} dk` : ''}
                        </p>
                    </div>
                </div>

                <div className="relative w-full aspect-video bg-black rounded-3xl overflow-hidden border border-white/10 shadow-[0_0_100px_rgba(0,0,0,0.5)] ring-1 ring-white/5">
                    {isLoading && !error && (
                        <div className="absolute inset-0 flex items-center justify-center bg-[#09090B] z-10">
                            <div className="flex flex-col items-center gap-4">
                                <div className="p-4 bg-white/5 rounded-2xl animate-pulse">
                                    <Loader2 className="w-8 h-8 text-blue-500 animate-spin" />
                                </div>
                                <span className="text-gray-400 text-sm font-medium tracking-wide">Yükleniyor...</span>
                            </div>
                        </div>
                    )}
                    
                    {error && (
                        <div className="absolute inset-0 flex items-center justify-center bg-[#09090B] z-10 p-6 text-center">
                            <div className="bg-red-500/10 text-red-400 px-6 py-4 rounded-2xl border border-red-500/20 font-medium">
                                {error}
                            </div>
                        </div>
                    )}

                    <video ref={videoRef} className="w-full h-full" poster={asset.thumbnailPath} crossOrigin="anonymous" />
                </div>
            </div>
        </div>
    );
}
