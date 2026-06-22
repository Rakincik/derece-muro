"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { courseApi, sessionRecordingApi, videoApi, getFileUrl, getVideoPlaybackDetails, type RecordingDto, type VideoNoteDto, type CourseMediaDto } from "@/lib/api";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { PremiumPlayer } from "@/components/video/PremiumPlayer";
import { initSecurityKiosk } from "@/lib/security/antiDebug";
import {
    ChevronLeft, Play, SkipBack, SkipForward,
    List, StickyNote, Clock, Trash2, Send,
    CheckCircle2, Loader2, Check, ShieldAlert
} from "lucide-react";

// ─── Format helpers ──────────────────────────────────────────────────────────
const fmtTime = (sec: number) => {
    const h = Math.floor(sec / 3600);
    const m = Math.floor((sec % 3600) / 60);
    const s = Math.floor(sec % 60);
    return h > 0 ? `${h}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}` : `${m}:${s.toString().padStart(2, "0")}`;
};

const fmtClockTime = (iso: string) => {
    try { return new Date(iso).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" }); }
    catch { return "--:--"; }
};

// ─── Main Watch Page ─────────────────────────────────────────────────────────
export default function WatchPage() {
    const router = useRouter();
    const { token, currentTenantId: tenantId } = useAuth();
    const params = useParams();
    const courseId = params.courseId as string;
    const recordingId = params.recordingId as string;

    // State
    const [allRecordings, setAllRecordings] = useState<RecordingDto[]>([]);
    const [currentRec, setCurrentRec] = useState<RecordingDto | null>(null);
    const [courseTitle, setCourseTitle] = useState("");
    const [notes, setNotes] = useState<VideoNoteDto[]>([]);
    const [noteText, setNoteText] = useState("");
    const [loading, setLoading] = useState(true);
    const [sideTab, setSideTab] = useState<"playlist" | "notes">("playlist");
    const [sidebarOpen, setSidebarOpen] = useState(true);
    const [securityViolation, setSecurityViolation] = useState(false);

    // ── Watch tracking ──
    const [watchedMap, setWatchedMap] = useState<Record<string, boolean>>({});
    const watchTimerRef = useRef<NodeJS.Timeout | null>(null);
    const watchedKey = `muro_watched_${courseId}`;

    // Load watched map from localStorage
    useEffect(() => {
        try {
            const saved = localStorage.getItem(watchedKey);
            if (saved) setWatchedMap(JSON.parse(saved));
        } catch { /* ignore */ }
    }, [watchedKey]);

    const markWatched = useCallback((recId: string) => {
        setWatchedMap(prev => {
            const next = { ...prev, [recId]: true };
            try { localStorage.setItem(watchedKey, JSON.stringify(next)); } catch { /* ignore */ }
            return next;
        });
    }, [watchedKey]);

    // ── Watch timer: 30 sn sonra izlendi olarak işaretle ──
    useEffect(() => {
        if (!currentRec) return;

        watchTimerRef.current = setTimeout(() => {
            const targetId = currentRec.mediaAssetId || currentRec.id;
            markWatched(targetId);
        }, 30000);

        return () => {
            if (watchTimerRef.current) clearTimeout(watchTimerRef.current);
        };
    }, [currentRec?.id, markWatched]);

    // ── Security Kiosk (Anti-Debug) ──
    useEffect(() => {
        const cleanup = initSecurityKiosk(() => {
            setSecurityViolation(true);
        });
        return cleanup;
    }, []);

    // Load data
    useEffect(() => {
        if (!token || !tenantId || !courseId) return;

        const loadData = async () => {
            setLoading(true);
            try {
                const headers = { "Authorization": `Bearer ${token}`, "X-Tenant-Id": tenantId };
                const courseRes = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/courses/${courseId}`, { headers });
                const courseData = courseRes.ok ? await courseRes.json() : null;
                if (courseData) setCourseTitle(courseData.title || "");

                const recs = await sessionRecordingApi.list(token, tenantId).catch(() => []);
                const courseMedias = await courseApi.getCourseMedias(token, tenantId, courseId).catch(() => []);
                const sessions = courseData?.sessions ?? [];

                const typedRecs = recs as RecordingDto[];

                const readyCourseMedias = courseMedias.filter((cm: CourseMediaDto) => {
                    if (cm.sessionId) {
                        const matchRec = typedRecs.find(r => r.sessionId === cm.sessionId);
                        if (matchRec && matchRec.status !== 'Ready') {
                            return false;
                        }
                    }
                    return true;
                });

                const courseRecs: RecordingDto[] = readyCourseMedias
                    .filter((cm: CourseMediaDto) => cm.type !== "Exam")
                    .map((cm: CourseMediaDto) => {
                        const matchRec = typedRecs.find(r => r.sessionId === cm.sessionId);
                        const matchSession = sessions.find(s => s.id === cm.sessionId);
                        return {
                            id: cm.id,
                            sessionId: cm.sessionId || cm.id,
                            sessionTitle: cm.mediaAsset?.title || cm.sessionTitle || cm.examTitle || 'İçerik',
                            courseId: cm.courseId,
                            courseTitle: courseData?.title || '',
                            playbackUrl: matchRec?.playbackUrl || '',
                            hlsPath: cm.mediaAsset?.hlsPath,
                            thumbnailPath: cm.mediaAsset?.thumbnailPath,
                            durationSeconds: cm.mediaAsset?.durationSeconds || matchRec?.durationSeconds || 0,
                            participantsCount: 0,
                            status: "Ready",
                            createdAt: cm.createdAt,
                            scheduledStart: cm.sessionScheduledStart || undefined,
                            type: cm.type === 'Video' || cm.mediaAsset?.hlsPath ? 'Video' : 'Recording',
                            videoUrl: matchSession?.videoUrl || null,
                            mediaAssetId: cm.mediaAssetId || matchRec?.mediaAssetId || null
                        };
                    });

                setAllRecordings(courseRecs);

                const current = courseRecs.find((r: RecordingDto) => r.id === recordingId) || courseRecs[0];
                setCurrentRec(current);
            } catch (e) { console.error(e); }
            setLoading(false);
        };
        loadData();
    }, [token, tenantId, courseId, recordingId]);

    // ── Load notes from backend when recording changes ──
    useEffect(() => {
        if (!currentRec || !token || !tenantId) return;
        const targetId = currentRec.mediaAssetId || currentRec.id;
        videoApi.getNotes(token, tenantId, targetId)
            .then(fetchedNotes => { if (Array.isArray(fetchedNotes)) setNotes(fetchedNotes); })
            .catch(() => setNotes([]));
    }, [currentRec?.id, currentRec?.mediaAssetId, token, tenantId]);

    // Navigate to next recording
    const playNext = useCallback(() => {
        const idx = allRecordings.findIndex(r => r.id === currentRec?.id);
        if (idx < allRecordings.length - 1) {
            const next = allRecordings[idx + 1];
            router.push(`/dashboard/courses/${courseId}/watch/${next.id}`);
        }
    }, [allRecordings, currentRec, courseId, router]);

    const switchRecording = (rec: RecordingDto) => {
        router.push(`/dashboard/courses/${courseId}/watch/${rec.id}`);
    };

    // ── Add note (persisted to backend, timestamp = creation time) ──
    const addNote = async () => {
        if (!noteText.trim() || !token || !tenantId || !currentRec) return;
        const text = noteText.trim();
        setNoteText("");

        const targetId = currentRec.mediaAssetId || currentRec.id;
        // Optimistic local add
        const now = new Date().toISOString();
        const tempNote: VideoNoteDto = {
            id: crypto.randomUUID(),
            mediaAssetId: targetId,
            timestampSeconds: 0,
            timestampFormatted: fmtClockTime(now),
            text,
            createdAt: now,
        };
        setNotes(prev => [...prev, tempNote]);

        // Persist to backend
        try {
            const saved = await videoApi.addNote(token, tenantId, targetId, 0, text);
            if (saved?.id) {
                setNotes(prev => prev.map(n => n.id === tempNote.id ? { ...saved, timestampFormatted: fmtClockTime(saved.createdAt) } : n));
            }
        } catch { /* keep optimistic note */ }
    };

    // ── Delete note ──
    const deleteNote = async (noteId: string) => {
        setNotes(prev => prev.filter(n => n.id !== noteId));
        if (token && tenantId) {
            try { await videoApi.deleteNote(token, tenantId, noteId); } catch { /* ignore */ }
        }
    };

    if (loading) {
        return (
            <div className="fixed inset-0 bg-[#0A0A0F] flex items-center justify-center z-50">
                <div className="text-center">
                    <Loader2 size={40} className="text-indigo-400 animate-spin mx-auto mb-3" />
                    <p className="text-white/50 text-sm">Yükleniyor...</p>
                </div>
            </div>
        );
    }

    if (securityViolation) {
        return (
            <div className="fixed inset-0 bg-red-950/90 flex items-center justify-center z-50">
                <div className="text-center bg-black/50 p-10 rounded-2xl border border-red-500/30 max-w-md backdrop-blur-md">
                    <ShieldAlert size={64} className="text-red-500 mx-auto mb-4" />
                    <h2 className="text-2xl font-bold text-white mb-2">Güvenlik İhlali</h2>
                    <p className="text-red-200 text-sm">
                        Sistem kaynaklarını izinsiz kopyalama veya izleme girişimi tespit edildi. <br/><br/>
                        Geliştirici araçlarını (DevTools) kapatıp sayfayı yenileyin.
                    </p>
                </div>
            </div>
        );
    }

    const currentIndex = allRecordings.findIndex(r => r.id === currentRec?.id);

    return (
        <div className="fixed inset-0 bg-[#0A0A0F] z-50 flex flex-col">
            {/* ── Top Bar ── */}
            <div className="h-12 bg-[#12121A] border-b border-white/5 flex items-center px-4 gap-3 shrink-0">
                <Link
                    href={`/dashboard/courses/${courseId}`}
                    className="flex items-center gap-1.5 text-white/40 hover:text-white/80 transition-colors text-xs"
                >
                    <ChevronLeft size={14} /> {courseTitle || "Derse Dön"}
                </Link>
                <div className="h-4 w-px bg-white/10" />
                <h1 className="text-white/80 text-sm font-medium truncate flex-1">
                    {currentRec?.sessionTitle || "Video"}
                </h1>
                <div className="flex items-center gap-2 text-white/30 text-[10px]">
                    {currentIndex >= 0 && (
                        <span className="px-2 py-0.5 rounded-full bg-indigo-500/20 text-indigo-300 font-bold">
                            {currentIndex + 1} / {allRecordings.length}
                        </span>
                    )}
                </div>
                <button
                    onClick={() => setSidebarOpen(!sidebarOpen)}
                    className="text-white/40 hover:text-white/80 transition-colors p-1 md:hidden"
                    title="İçerik panelini aç/kapa"
                >
                    <List size={16} />
                </button>
            </div>

            {/* ── Content ── */}
            <div className="flex-1 flex overflow-hidden">
                {/* ── Video Area ── */}
                <div className="flex-1 flex flex-col min-w-0">
                    {/* Video Player */}
                    <div className="flex-1 bg-black relative flex items-center justify-center overflow-hidden select-none">
                        {(() => {
                            if (currentRec?.videoUrl) {
                                const details = getVideoPlaybackDetails(currentRec.videoUrl);
                                if (details.type === "iframe") {
                                    return (
                                        <iframe 
                                            src={details.url} 
                                            className="w-full h-full border-0 absolute inset-0"
                                            allowFullScreen
                                            //@ts-ignore
                                            webkitallowfullscreen="true"
                                            //@ts-ignore
                                            mozallowfullscreen="true"
                                            allow="autoplay; fullscreen"
                                        />
                                    );
                                } else {
                                    return (
                                        <PremiumPlayer 
                                            src={details.url} 
                                            poster={currentRec?.thumbnailPath ? getFileUrl(currentRec.thumbnailPath) : undefined}
                                            autoplay={true} 
                                        />
                                    );
                                }
                            }
                            const src = currentRec?.hlsPath || currentRec?.playbackUrl;
                            if (!src) return null;
                            
                            // Check if it's a BigBlueButton presentation URL
                            if (src.includes("/playback/presentation/") || src.includes("meetingId=")) {
                                return (
                                    <iframe 
                                        src={src} 
                                        className="w-full h-full border-0"
                                        allowFullScreen
                                        //@ts-ignore
                                        webkitallowfullscreen="true"
                                        //@ts-ignore
                                        mozallowfullscreen="true"
                                        allow="camera; microphone; display-capture; fullscreen"
                                    />
                                );
                            }

                            return (
                                <PremiumPlayer 
                                    src={src} 
                                    poster={currentRec?.thumbnailPath ? getFileUrl(currentRec.thumbnailPath) : undefined}
                                    autoplay={true} 
                                />
                            );
                        })()}
                    </div>

                    {/* ── Bottom Info Bar ── */}
                    <div className="h-14 bg-[#12121A] border-t border-white/5 flex items-center px-5 gap-4 shrink-0">
                        <div className="flex items-center gap-2 text-white/60 text-xs">
                            <Clock size={12} />
                            <span className="font-medium">{currentRec?.sessionTitle}</span>
                            {currentRec?.durationSeconds && currentRec.durationSeconds > 0 && (
                                <>
                                    <span className="text-white/20">•</span>
                                    <span className="font-mono text-white/30">{fmtTime(currentRec.durationSeconds)}</span>
                                </>
                            )}
                        </div>
                        <div className="flex-1" />
                        {/* Watch status badge */}
                        {currentRec && watchedMap[currentRec.id] && (
                            <span className="text-[10px] text-emerald-400 flex items-center gap-1 bg-emerald-500/10 px-2 py-1 rounded-full">
                                <CheckCircle2 size={10} /> İzlendi
                            </span>
                        )}
                        {/* Nav buttons */}
                        <button
                            onClick={() => { if (currentIndex > 0) switchRecording(allRecordings[currentIndex - 1]); }}
                            disabled={currentIndex <= 0}
                            className="text-white/40 hover:text-white/80 disabled:opacity-20 transition-colors p-1.5"
                            title="Önceki"
                        >
                            <SkipBack size={16} />
                        </button>
                        <button
                            onClick={playNext}
                            disabled={currentIndex >= allRecordings.length - 1}
                            className="text-white/40 hover:text-white/80 disabled:opacity-20 transition-colors p-1.5"
                            title="Sonraki"
                        >
                            <SkipForward size={16} />
                        </button>
                    </div>
                </div>

                {/* ── Sidebar (responsive) ── */}
                <div className={`bg-[#12121A] border-l border-white/5 flex flex-col shrink-0 transition-all duration-300 ${sidebarOpen ? 'w-80 max-md:fixed max-md:inset-y-12 max-md:right-0 max-md:z-40 max-md:w-72 max-md:shadow-2xl' : 'w-0 overflow-hidden border-l-0'}`}>
                    {/* Sidebar Tabs */}
                    <div className="flex border-b border-white/5">
                        <button
                            onClick={() => setSideTab("playlist")}
                            className={`flex-1 py-3 text-xs font-semibold flex items-center justify-center gap-1.5 transition-colors ${sideTab === "playlist" ? "text-indigo-400 border-b-2 border-indigo-400" : "text-white/30 hover:text-white/60"}`}
                        >
                            <List size={13} /> Oturumlar
                        </button>
                        <button
                            onClick={() => setSideTab("notes")}
                            className={`flex-1 py-3 text-xs font-semibold flex items-center justify-center gap-1.5 transition-colors ${sideTab === "notes" ? "text-indigo-400 border-b-2 border-indigo-400" : "text-white/30 hover:text-white/60"}`}
                        >
                            <StickyNote size={13} /> Notlarım
                            {notes.length > 0 && (
                                <span className="px-1.5 py-0.5 rounded-full bg-indigo-500/20 text-indigo-300 text-[9px] font-bold">{notes.length}</span>
                            )}
                        </button>
                    </div>

                    {/* Sidebar Content */}
                    <div className="flex-1 overflow-y-auto custom-scroll">
                        {sideTab === "playlist" ? (
                            <div className="p-2">
                                {allRecordings.map((rec, idx) => {
                                    const isActive = rec.id === currentRec?.id;
                                    const isWatched = watchedMap[rec.id];
                                    return (
                                        <button
                                            key={rec.id}
                                            onClick={() => switchRecording(rec)}
                                            className={`w-full text-left p-3 rounded-xl mb-1 transition-all group ${isActive
                                                ? "bg-indigo-500/15 border border-indigo-500/30"
                                                : "hover:bg-white/5 border border-transparent"
                                                }`}
                                        >
                                            <div className="flex items-start gap-3">
                                                <div className={`w-7 h-7 rounded-lg flex items-center justify-center shrink-0 text-[10px] font-bold mt-0.5 ${isActive
                                                    ? "bg-indigo-500 text-white"
                                                    : isWatched
                                                        ? "bg-emerald-500/20 text-emerald-400"
                                                        : "bg-white/5 text-white/30 group-hover:bg-white/10"
                                                    }`}>
                                                    {isActive ? <Play size={12} className="ml-0.5" /> : isWatched ? <Check size={12} /> : idx + 1}
                                                </div>
                                                <div className="flex-1 min-w-0">
                                                    <p className={`text-xs font-medium truncate ${isActive ? "text-indigo-300" : "text-white/60 group-hover:text-white/80"}`}>
                                                        {rec.sessionTitle}
                                                    </p>
                                                    <div className="flex items-center gap-2 mt-1">
                                                        {rec.durationSeconds && rec.durationSeconds > 0 && (
                                                            <span className="text-[10px] text-white/20 flex items-center gap-0.5">
                                                                <Clock size={8} /> {fmtTime(rec.durationSeconds)}
                                                            </span>
                                                        )}
                                                        {rec.scheduledStart && (
                                                            <span className="text-[10px] text-white/20">
                                                                {new Date(rec.scheduledStart).toLocaleDateString("tr-TR", { day: "numeric", month: "short" })}
                                                            </span>
                                                        )}
                                                        {isWatched && (
                                                            <span className="text-[9px] text-emerald-400/60 flex items-center gap-0.5">
                                                                <CheckCircle2 size={8} /> İzlendi
                                                            </span>
                                                        )}
                                                    </div>
                                                </div>
                                            </div>
                                        </button>
                                    );
                                })}
                                {allRecordings.length === 0 && (
                                    <div className="text-center py-12 text-white/20 text-xs">
                                        <List size={20} className="mx-auto mb-2 opacity-50" />
                                        Henüz kayıt yok
                                    </div>
                                )}
                            </div>
                        ) : (
                            <div className="p-3">
                                {notes.length === 0 && (
                                    <div className="text-center py-8 text-white/20 text-xs">
                                        <StickyNote size={20} className="mx-auto mb-2 opacity-50" />
                                        <p>Henüz not yok</p>
                                        <p className="mt-1 text-white/10">Video izlerken not almaya başlayın</p>
                                    </div>
                                )}
                                {notes.map(note => (
                                    <div
                                        key={note.id}
                                        className="group p-3 mb-2 rounded-xl bg-white/[0.03] hover:bg-white/[0.06] border border-white/5 transition-all"
                                    >
                                        <div className="flex items-center justify-between mb-1.5">
                                            <span className="text-[10px] font-mono text-indigo-400 bg-indigo-500/10 px-1.5 py-0.5 rounded">
                                                🕐 {fmtClockTime(note.createdAt)}
                                            </span>
                                            <button
                                                onClick={() => deleteNote(note.id)}
                                                className="opacity-0 group-hover:opacity-100 text-white/20 hover:text-red-400 transition-all"
                                            >
                                                <Trash2 size={11} />
                                            </button>
                                        </div>
                                        <p className="text-xs text-white/50 leading-relaxed">{note.text}</p>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>

                    {/* Note Input (always visible when notes tab active) */}
                    {sideTab === "notes" && (
                        <div className="p-3 border-t border-white/5">
                            <div className="flex items-center gap-1.5 mb-2">
                                <span className="text-[10px] text-white/20">📝 Not ekle</span>
                            </div>
                            <div className="flex gap-2">
                                <input
                                    type="text"
                                    value={noteText}
                                    onChange={e => setNoteText(e.target.value)}
                                    onKeyDown={e => e.key === "Enter" && addNote()}
                                    placeholder="Not yazın..."
                                    className="flex-1 bg-white/5 border border-white/10 rounded-lg px-3 py-2 text-xs text-white placeholder:text-white/20 focus:outline-none focus:border-indigo-500/50 transition-colors"
                                />
                                <button
                                    onClick={addNote}
                                    disabled={!noteText.trim()}
                                    className="px-3 py-2 bg-indigo-500 hover:bg-indigo-600 disabled:opacity-30 text-white rounded-lg transition-all"
                                >
                                    <Send size={12} />
                                </button>
                            </div>
                        </div>
                    )}
                </div>

                {/* Mobile sidebar backdrop */}
                {sidebarOpen && (
                    <div
                        className="fixed inset-0 bg-black/50 z-30 md:hidden"
                        onClick={() => setSidebarOpen(false)}
                    />
                )}
            </div>
        </div>
    );
}
