"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import { videoApi, type VideoNoteDto, type RecordingDto } from "@/lib/api";

/**
 * Custom hook — video player state management.
 * Extracts watch tracking, fullscreen, keyboard shortcuts, and resume logic
 * from CourseDetailPage to reduce its monolithic state.
 */
export function useVideoPlayer(
    courseId: string,
    recordings: RecordingDto[],
    token: string | null,
    tenantId: string | null,
    activeTab: string,
) {
    const [selectedRec, setSelectedRec] = useState<RecordingDto | null>(null);
    const [isFullscreen, setIsFullscreen] = useState(false);
    const [sidebarOpen, setSidebarOpen] = useState(true);
    const [iframeLoaded, setIframeLoaded] = useState(false);
    const [watchedMap, setWatchedMap] = useState<Record<string, boolean>>({});

    const playerContainerRef = useRef<HTMLDivElement>(null);
    const watchTimerRef = useRef<NodeJS.Timeout | null>(null);
    const heartbeatRef = useRef<NodeJS.Timeout | null>(null);
    const watchStartRef = useRef<number>(0);

    // Sorted recordings for playlist (now trusts the order passed from page.tsx which respects CourseMedia OrderIndex)
    const sortedRecordings = [...recordings];

    // localStorage key helpers
    const watchedKey = `muro_watched_${courseId}`;
    const lastWatchedKey = `muro_lastWatched_${courseId}`;

    // Progress calculations
    const watchedCount = sortedRecordings.filter(r => watchedMap[r.id]).length;
    const progressPercent = sortedRecordings.length > 0 ? Math.round((watchedCount / sortedRecordings.length) * 100) : 0;
    const lastWatchedId = typeof window !== "undefined" ? localStorage.getItem(lastWatchedKey) : null;
    const lastWatchedRec = lastWatchedId ? sortedRecordings.find(r => r.id === lastWatchedId) : null;

    // ── Load watched map from localStorage ──
    useEffect(() => {
        try {
            const saved = localStorage.getItem(watchedKey);
            if (saved) setWatchedMap(JSON.parse(saved));
        } catch { /* ignore */ }
    }, [watchedKey]);

    // ── Save watched map to localStorage ──
    const markWatched = useCallback((recId: string) => {
        setWatchedMap(prev => {
            const next = { ...prev, [recId]: true };
            try { localStorage.setItem(watchedKey, JSON.stringify(next)); } catch { /* ignore */ }
            return next;
        });
    }, [watchedKey]);

    // ── Fullscreen toggle ──
    const toggleFullscreen = useCallback(() => {
        if (!playerContainerRef.current) return;
        const isNative = !!(document.fullscreenElement || (document as any).webkitFullscreenElement);
        
        if (!isNative && !isFullscreen) {
            const elem = playerContainerRef.current as any;
            const reqFs = elem.requestFullscreen || elem.webkitRequestFullscreen || elem.mozRequestFullScreen || elem.msRequestFullscreen;
            if (reqFs) {
                reqFs.call(elem).catch(() => {
                    setIsFullscreen(true); // CSS fallback
                });
            } else {
                setIsFullscreen(true); // CSS fallback
            }
        } else {
            if (isNative) {
                const exitFs = document.exitFullscreen || (document as any).webkitExitFullscreen || (document as any).mozCancelFullScreen || (document as any).msExitFullscreen;
                if (exitFs) {
                    exitFs.call(document).catch(() => setIsFullscreen(false));
                }
            } else {
                setIsFullscreen(false); // Disable CSS fallback
            }
        }
    }, [isFullscreen]);

    // ── Fullscreen change listener ──
    useEffect(() => {
        const handler = () => {
            const isNative = !!(document.fullscreenElement || (document as any).webkitFullscreenElement);
            // If the event fired and we are no longer native fullscreen, exit fullscreen state.
            // If we are native fullscreen, ensure state is true.
            setIsFullscreen(isNative);
        };
        document.addEventListener("fullscreenchange", handler);
        document.addEventListener("webkitfullscreenchange", handler);
        return () => {
            document.removeEventListener("fullscreenchange", handler);
            document.removeEventListener("webkitfullscreenchange", handler);
        };
    }, []);

    // ── Watch timer: mark as watched after 30s on same video ──
    useEffect(() => {
        if (!selectedRec) return;
        watchStartRef.current = Date.now();
        setIframeLoaded(false);
        try { localStorage.setItem(lastWatchedKey, selectedRec.id); } catch { /* ignore */ }

        watchTimerRef.current = setTimeout(() => {
            markWatched(selectedRec.id);
        }, 30000);

        return () => {
            if (watchTimerRef.current) clearTimeout(watchTimerRef.current);
        };
    }, [selectedRec?.id, markWatched, lastWatchedKey]);

    // ── Heartbeat: send progress every 60s + flush on unmount ──
    // 🚀 10K optimization: 60s interval = half the API calls vs 30s
    useEffect(() => {
        if (!selectedRec || !token || !tenantId) return;

        const sendProgress = () => {
            const elapsed = Math.floor((Date.now() - watchStartRef.current) / 1000);
            if (elapsed < 5) return; // 5sn'den az izlemediyse gönderme
            const payload = {
                watchedSeconds: elapsed,
                totalSeconds: selectedRec.durationSeconds || 0,
                lastPosition: elapsed,
            };
            const targetId = selectedRec.mediaAssetId || selectedRec.id;
            // localStorage'a yaz — tab kapansa bile kaybolmasın
            try { localStorage.setItem(`muro_progress_${targetId}`, JSON.stringify(payload)); } catch { }
            videoApi.updateProgress(token, tenantId, targetId, payload).catch(() => { });
        };

        heartbeatRef.current = setInterval(sendProgress, 60_000);

        // Tab kapanırken/video değişirken son veriyi gönder (sendBeacon ile)
        const flushOnUnload = () => {
            const elapsed = Math.floor((Date.now() - watchStartRef.current) / 1000);
            if (elapsed < 5) return;
            const apiBase = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5292/api/v1";
            const body = JSON.stringify({
                watchedSeconds: elapsed,
                totalSeconds: selectedRec.durationSeconds || 0,
                lastPosition: elapsed,
            });
            const targetId = selectedRec.mediaAssetId || selectedRec.id;
            // sendBeacon tab kapanırken bile isteği tamamlar
            navigator.sendBeacon?.(
                `${apiBase}/videos/${targetId}/progress?t=${token}&tenant=${tenantId}`,
                new Blob([body], { type: "application/json" })
            );
        };
        window.addEventListener("beforeunload", flushOnUnload);

        return () => {
            if (heartbeatRef.current) clearInterval(heartbeatRef.current);
            window.removeEventListener("beforeunload", flushOnUnload);
            sendProgress(); // video değiştiğinde son durumu kaydet
        };
    }, [selectedRec?.id, token, tenantId]);

    // ── Keyboard shortcuts ──
    useEffect(() => {
        const handler = (e: KeyboardEvent) => {
            if ((e.target as HTMLElement).tagName === "INPUT" || (e.target as HTMLElement).tagName === "TEXTAREA") return;
            if (activeTab !== "videos" || sortedRecordings.length === 0) return;

            const currentRec = selectedRec || sortedRecordings[0];
            if (!currentRec) return;
            const idx = sortedRecordings.findIndex(r => r.id === currentRec.id);

            switch (e.key.toLowerCase()) {
                case "n":
                    e.preventDefault();
                    if (idx < sortedRecordings.length - 1) setSelectedRec(sortedRecordings[idx + 1]);
                    break;
                case "p":
                    e.preventDefault();
                    if (idx > 0) setSelectedRec(sortedRecordings[idx - 1]);
                    break;
                case "f":
                    e.preventDefault();
                    toggleFullscreen();
                    break;
                case "s":
                    e.preventDefault();
                    setSidebarOpen(prev => !prev);
                    break;
            }
        };
        window.addEventListener("keydown", handler);
        return () => window.removeEventListener("keydown", handler);
    }, [selectedRec?.id, activeTab, sortedRecordings, toggleFullscreen]);

    return {
        selectedRec, setSelectedRec,
        isFullscreen, toggleFullscreen,
        sidebarOpen, setSidebarOpen,
        iframeLoaded, setIframeLoaded,
        watchedMap, markWatched,
        playerContainerRef,
        sortedRecordings,
        watchedCount, progressPercent,
        lastWatchedRec,
    };
}

/**
 * Custom hook — player notes management via backend API.
 */
export function usePlayerNotes(
    selectedRecId: string | null,
    token: string | null,
    tenantId: string | null,
) {
    const [notes, setNotes] = useState<VideoNoteDto[]>([]);
    const [noteText, setNoteText] = useState("");
    const [playerTime, setPlayerTime] = useState(0);
    const [sideTab, setSideTab] = useState<"playlist" | "notes">("playlist");

    // Load notes when video changes
    useEffect(() => {
        if (!selectedRecId || !token || !tenantId) return;
        videoApi.getNotes(token, tenantId, selectedRecId)
            .then(n => { if (Array.isArray(n)) setNotes(n); })
            .catch(() => setNotes([]));
    }, [selectedRecId, token, tenantId]);

    const addNote = useCallback(async () => {
        const targetId = selectedRecId; // Already resolved to mediaAssetId in page.tsx / useVideoPlayer caller
        if (!noteText.trim() || !targetId || !token || !tenantId) return;
        try {
            const note = await videoApi.addNote(token, tenantId, targetId, playerTime, noteText.trim());
            setNotes(prev => [...prev, note].sort((a, b) => a.timestampSeconds - b.timestampSeconds));
            setNoteText("");
        } catch { /* ignore */ }
    }, [noteText, selectedRecId, token, tenantId, playerTime]);

    const deleteNote = useCallback(async (noteId: string) => {
        if (!token || !tenantId) return;
        try {
            await videoApi.deleteNote(token, tenantId, noteId);
            setNotes(prev => prev.filter(n => n.id !== noteId));
        } catch { /* ignore */ }
    }, [token, tenantId]);

    return {
        notes, noteText, setNoteText,
        playerTime, setPlayerTime,
        sideTab, setSideTab,
        addNote, deleteNote,
    };
}
