"use client";
import { useState, useMemo, useCallback, useEffect, useRef } from "react";
import {
    BookOpen, Plus, Search, Edit3, Trash2, Eye, X, Users, Clock, Video,
    Monitor, Layers, Play, ArrowLeft, Calendar as Cal,
    ChevronRight, Radio, CheckCircle2, FileText,
    BarChart3, TrendingUp, Target, Award, Zap,
    FolderOpen, Settings, Loader2, StopCircle, ChevronLeft,
    Flame, PlayCircle, LayoutGrid, List as ListIcon, ArrowUpDown,
    Upload, Image as ImageIcon, UploadCloud
} from "lucide-react";
import { useToast } from "@/components/toast";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { useAuth } from "@/contexts/AuthContext";
import { sessionApi, courseApi, recordingApi, userApi, type CourseListDto, type SessionDto, type CourseDetailDto, type RecordingDto, type GroupSummaryDto, type CourseMaterialDto, type UserDto } from "@/lib/api";
import { API_URL } from "@/lib/api/core";
import { VideoUploaderModal } from "@/components/ui/VideoUploaderModal";
import { CourseMediaTab } from "./CourseMediaTab";
import { CourseStudentTab } from "./CourseStudentTab";
import { PremiumTabs } from "@/components/ui/PremiumTabs";
import { CustomSelect } from "@/components/ui/CustomSelect";
import { ResponsiveList } from "@/components/ui/ResponsiveList";
import { Tooltip } from "@/components/ui/Tooltip";

// ─── HLS Player Component ──────────────────────────────────────────────────
function HlsVideoPlayer({ src }: { src: string }) {
    const videoRef = useRef<HTMLVideoElement>(null);
    useEffect(() => {
        const video = videoRef.current;
        if (!video || !src) return;
        const fullSrc = src.startsWith("/") 
            ? `${API_URL.replace("/api/v1", "")}${src}`
            : src;
            
        const load = async () => {
            if (video.canPlayType("application/vnd.apple.mpegurl")) {
                video.src = fullSrc;
            } else {
                const Hls = (await import("hls.js")).default;
                if (Hls.isSupported()) {
                    const hls = new Hls();
                    hls.loadSource(fullSrc);
                    hls.attachMedia(video);
                    return () => hls.destroy();
                }
            }
        };
        load();
    }, [src]);
    return <video ref={videoRef} controls autoPlay className="w-full h-full outline-none" />;
}

// ── Interfaces ────────────────────────────────────────────────────────────────
interface MappedSession {
    id: string; title: string; date: string; time: string; duration: string;
    status: "scheduled" | "live" | "ended"; attendees: number; hasRecording: boolean;
    scheduledStart?: string; durationMinutes?: number; recordingEnabled?: boolean;
    videoUrl?: string;
}

interface CourseGroup { id: string; name: string; mode: string; }

interface MappedCourse {
    id: string; title: string; description: string; type: string; thumbnailUrl: string | null;
    sessionCount: number; isPublished: boolean; createdAt: string; color: string;
    sessions: MappedSession[]; groups: CourseGroup[]; groupCount: number; order: number;
    instructorId: string | null; instructorName: string | null;
}

type DTab = "overview" | "sessions" | "media" | "recordings" | "docs" | "settings" | "students";

const typeIcons: Record<string, React.ElementType> = { Online: Monitor, Offline: Video, "Canlı": Radio, Hibrit: Layers };

const tabDef: { key: DTab; label: string; icon: any }[] = [
    { key: "overview", label: "Genel Bakış", icon: BarChart3 },
    { key: "media", label: "Videolar", icon: Play },
    { key: "recordings", label: "Kayıtlar", icon: PlayCircle },
    { key: "docs", label: "Dokümanlar", icon: FolderOpen },
    { key: "settings", label: "Ayarlar", icon: Settings }
];

// ── Helpers ────────────────────────────────────────────────────────────────────
const mapSession = (s: SessionDto): MappedSession => ({
    id: s.id, title: s.title,
    date: s.scheduledStart ? s.scheduledStart.split("T")[0] : "-",
    time: s.scheduledStart ? new Date(s.scheduledStart).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" }) : "-",
    duration: s.durationMinutes ? `${s.durationMinutes} dk` : "-",
    status: (s.status?.toLowerCase() ?? "scheduled") as MappedSession["status"],
    attendees: 0, hasRecording: s.status?.toLowerCase() === "ended",
    scheduledStart: s.scheduledStart ?? undefined,
    durationMinutes: s.durationMinutes ?? undefined,
    recordingEnabled: s.recordingEnabled,
    videoUrl: s.videoUrl ?? undefined,
});

const getFileUrl = (path: string | null) => {
    if (!path) return "";
    if (path.startsWith("http")) return path;
    const base = process.env.NEXT_PUBLIC_API_URL?.replace("/api/v1", "") || "http://localhost:5292";
    return `${base}${path.startsWith("/") ? "" : "/"}${path}`;
};

const mapCourse = (c: CourseListDto, sessions: MappedSession[] = [], detail?: CourseDetailDto): MappedCourse => ({
    id: c.id, title: c.title, description: c.description ?? "",
    type: c.courseType ?? "Online", thumbnailUrl: c.thumbnailUrl ? getFileUrl(c.thumbnailUrl) : null,
    sessionCount: c.sessionCount, isPublished: c.isPublished,
    createdAt: c.createdAt.split("T")[0], color: "#6366f1",
    sessions, groups: detail?.groups?.map(g => ({ id: g.groupId, name: g.groupName, mode: g.mode })) ?? [],
    groupCount: c.groupCount ?? 0, order: c.order ?? 0,
    instructorId: c.instructorId ?? detail?.instructorId ?? null,
    instructorName: c.instructorName ?? detail?.instructorName ?? null,
});

// ── Main Component ────────────────────────────────────────────────────────────
export default function CoursesPage() {
    const { success, error: toastError } = useToast();
    const { user, token, currentTenantId: tenantId } = useAuth();
    const isInstructor = user?.role === "Instructor" || user?.tenants?.find((t: any) => t.tenantId === tenantId)?.role === "Instructor";
    const [courses, setCourses] = useState<MappedCourse[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState("");
    const [showWizard, setShowWizard] = useState(false);
    const [isSavingWizard, setIsSavingWizard] = useState(false);
    const [editCourse, setEditCourse] = useState<MappedCourse | null>(null);
    const [detail, setDetail] = useState<MappedCourse | null>(null);
    const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
    const [tab, setTab] = useState<DTab>("overview");
    const [actionLoading, setActionLoading] = useState<string | null>(null);
    const [recordings, setRecordings] = useState<RecordingDto[]>([]);
    const [attendanceModalOpen, setAttendanceModalOpen] = useState(false);
    const [attendanceData, setAttendanceData] = useState<any | null>(null);
    const [attendanceLoading, setAttendanceLoading] = useState(false);
    const [liveStartModal, setLiveStartModal] = useState<{ courseId: string; courseName: string } | null>(null);
    const [liveStartTopic, setLiveStartTopic] = useState("");
    const [recPage, setRecPage] = useState(0);
    const [recSearch, setRecSearch] = useState("");
    const [recPerPage, setRecPerPage] = useState(20);
    const [recSortColumn, setRecSortColumn] = useState<"title" | "date" | "duration">("date");
    const [recSortDesc, setRecSortDesc] = useState(true);
    const [previewVideo, setPreviewVideo] = useState<{ url: string; type: "video" | "iframe"; title: string } | null>(null);
    const [viewMode, setViewMode] = useState<"grid" | "list">("list");
    const [vodModalOpen, setVodModalOpen] = useState(false);
    const [statusFilter, setStatusFilter] = useState("all");
    const [sortBy, setSortBy] = useState<"name" | "date" | "sessions" | "status">("date");
    const [courseSortDesc, setCourseSortDesc] = useState(true);
    const [coursePage, setCoursePage] = useState(0);
    const [coursesPerPage, setCoursesPerPage] = useState(15);

    // ── Fetch courses from API ────────────────────────────────────────────────
    const fetchCourses = useCallback(async () => {
        if (!token || !tenantId) return;
        setLoading(true);
        try {
            const r = await courseApi.list(token, tenantId, { pageSize: 100 });
            setCourses(r.items.map(c => mapCourse(c)));
        } catch { /* silent */ }
        finally { setLoading(false); }
    }, [token, tenantId]);

    useEffect(() => { fetchCourses(); }, [fetchCourses]);

    // ── Auto-refresh detail when tab becomes visible (sync BBB status) ────────
    useEffect(() => {
        const onVisible = async () => {
            if (document.visibilityState !== "visible" || !detail || !token || !tenantId) return;
            try {
                const d = await courseApi.get(token, tenantId, detail.id);
                const mapped = mapCourse(
                    { ...d, sessionCount: detail.sessionCount, groupCount: d.groups.length, order: d.order, createdAt: d.createdAt },
                    d.sessions.map(mapSession), d
                );
                setDetail(mapped);
                setCourses(prev => prev.map(c => c.id === detail.id ? mapped : c));
            } catch { /* ignore */ }
        };
        document.addEventListener("visibilitychange", onVisible);
        return () => document.removeEventListener("visibilitychange", onVisible);
    }, [token, tenantId, detail?.id]);

    // ── Open course detail ────────────────────────────────────────────────────
    const openDetail = useCallback(async (course: MappedCourse, targetTab: DTab = "overview") => {
        setDetail(course); setTab(targetTab);
        if (!token || !tenantId) return;
        try {
            const [d, recs] = await Promise.all([
                courseApi.get(token, tenantId, course.id),
                recordingApi.list(token, tenantId),
            ]);
            const mapped = mapCourse(
                { ...d, sessionCount: course.sessionCount, groupCount: d.groups.length, order: d.order, createdAt: d.createdAt },
                d.sessions.map(mapSession), d
            );
            setDetail(mapped);
            setRecordings(recs.filter(r => r.courseTitle === d.title));
            setCourses(prev => prev.map(c => c.id === course.id ? mapped : c));
        } catch { /* ignore */ }
    }, [token, tenantId]);

    // ── CRUD Handlers ─────────────────────────────────────────────────────────
    const handleDelete = useCallback(async (id: string) => {
        if (!token || !tenantId) return;
        try {
            await courseApi.delete(token, tenantId, id);
            setCourses(p => p.filter(c => c.id !== id));
            setDeleteTarget(null);
            if (detail?.id === id) setDetail(null);
            success("Ders silindi");
        } catch { toastError("Hata", "Ders silinemedi."); }
    }, [token, tenantId, detail]);

    const handleTogglePublish = useCallback(async (id: string, current: boolean) => {
        if (!token || !tenantId) return;
        try {
            await courseApi.update(token, tenantId, id, { isPublished: !current });
            setCourses(p => p.map(c => c.id === id ? { ...c, isPublished: !current } : c));
            if (detail?.id === id) setDetail(p => p ? { ...p, isPublished: !current } : null);
            success(!current ? "Ders yayına alındı" : "Ders taslağa çekildi");
        } catch { toastError("Hata", "Yayın durumu güncellenemedi."); }
    }, [token, tenantId, detail]);

    const handleSettingsSave = useCallback(async (id: string, data: { title: string; description: string; courseType: string; instructorId?: string }) => {
        if (!token || !tenantId) return;
        try {
            const payload = { ...data, instructorId: data.instructorId || null };
            await courseApi.update(token, tenantId, id, payload);
            setCourses(p => p.map(c => c.id === id ? { ...c, ...payload, type: data.courseType } : c));
            if (detail?.id === id) setDetail(p => p ? { ...p, ...payload, type: data.courseType } : null);
            success("Değişiklikler kaydedildi");
        } catch { toastError("Hata", "Güncellenemedi."); }
    }, [token, tenantId, detail]);

    const handleWizardSave = useCallback(async (data: {
        title: string; description: string; courseType: string; isPublished: boolean;
        coverImage?: File; startDate?: string; instructorId?: string;
        session?: { title: string; scheduledStart: string; durationMinutes: number; recordingEnabled: boolean };
    }) => {
        if (!token || !tenantId) return;
        setIsSavingWizard(true);
        try {
            const created = await courseApi.create(token, tenantId, {
                title: data.title || "Yeni Ders",
                description: data.description,
                courseType: data.courseType || "Online",
                instructorId: data.instructorId || undefined,
            });
            // Upload cover image if provided
            if (data.coverImage) {
                try {
                    await courseApi.uploadCover(token, tenantId, created.id, data.coverImage);
                } catch { /* cover upload failure is not critical */ }
            }
            // Publish if needed
            if (data.isPublished) {
                await courseApi.update(token, tenantId, created.id, { isPublished: true });
            }
            // Create first session if provided
            if (data.session) {
                await courseApi.createSession(token, tenantId, created.id, {
                    title: data.session.title,
                    scheduledStart: data.session.scheduledStart,
                    durationMinutes: data.session.durationMinutes,
                    recordingEnabled: data.session.recordingEnabled,
                });
            }
            await fetchCourses();
            success("Ders oluşturuldu! 🎉");
            setShowWizard(false); setEditCourse(null);
        } catch { toastError("Hata", "Ders oluşturulamadı."); }
        finally { setIsSavingWizard(false); }
    }, [token, tenantId, fetchCourses]);

    // ── BBB Handlers ──────────────────────────────────────────────────────────
    const handleStartSession = useCallback(async (courseId: string, sessionId: string) => {
        if (!token || !tenantId) return;
        setActionLoading(sessionId);
        try {
            const result = await sessionApi.start(token, tenantId, courseId, sessionId);
            window.open(result.moderatorJoinUrl, "_blank", "noopener");
            setCourses(prev => prev.map(c => c.id === courseId ? {
                ...c, sessions: c.sessions.map(s => s.id === sessionId ? { ...s, status: "live" as const } : s)
            } : c));
            if (detail?.id === courseId) setDetail(prev => prev ? {
                ...prev, sessions: prev.sessions.map(s => s.id === sessionId ? { ...s, status: "live" as const } : s)
            } : null);
            success("Ders başlatıldı!");
        } catch { toastError("Hata", "BBB bağlantısı kurulamadı."); }
        finally { setActionLoading(null); }
    }, [token, tenantId, detail]);

    const handleEndSession = useCallback(async (courseId: string, sessionId: string) => {
        if (!token || !tenantId) return;
        setActionLoading(sessionId);
        try {
            await sessionApi.end(token, tenantId, courseId, sessionId);
            setCourses(prev => prev.map(c => c.id === courseId ? {
                ...c, sessions: c.sessions.map(s => s.id === sessionId ? { ...s, status: "ended" as const } : s)
            } : c));
            if (detail?.id === courseId) setDetail(prev => prev ? {
                ...prev, sessions: prev.sessions.map(s => s.id === sessionId ? { ...s, status: "ended" as const } : s)
            } : null);
            success("Oturum bitirildi.");
        } catch { toastError("Hata", "Oturum bitirilemedi."); }
        finally { setActionLoading(null); }
    }, [token, tenantId, detail]);



    const handleDeleteSession = useCallback(async (courseId: string, sessionId: string) => {
        if (!token || !tenantId) return;
        try {
            await courseApi.deleteSession(token, tenantId, courseId, sessionId);
            setCourses(prev => prev.map(c => c.id === courseId ? {
                ...c, sessionCount: Math.max(0, c.sessionCount - 1), sessions: c.sessions.filter(s => s.id !== sessionId)
            } : c));
            if (detail?.id === courseId) setDetail(prev => prev ? {
                ...prev, sessionCount: Math.max(0, prev.sessionCount - 1), sessions: prev.sessions.filter(s => s.id !== sessionId)
            } : null);
            success("Oturum silindi.");
        } catch { toastError("Hata", "Oturum silinemedi."); }
    }, [token, tenantId, detail]);

    const handleCreateSession = useCallback(async (courseId: string) => {
        if (!token || !tenantId) return;
        try {
            const s = await courseApi.createSession(token, tenantId, courseId, {
                title: `Oturum ${(detail?.sessions.length ?? 0) + 1}`,
                scheduledStart: new Date(Date.now() + 86400000).toISOString(),
                durationMinutes: 60, recordingEnabled: true,
            });
            const ns = mapSession(s);
            setCourses(prev => prev.map(c => c.id === courseId ? { ...c, sessionCount: c.sessionCount + 1, sessions: [...c.sessions, ns] } : c));
            if (detail?.id === courseId) setDetail(prev => prev ? { ...prev, sessionCount: prev.sessionCount + 1, sessions: [...prev.sessions, ns] } : null);
            success("Oturum oluşturuldu!");
        } catch { toastError("Hata", "Oturum oluşturulamadı."); }
    }, [token, tenantId, detail]);

    // ── Quick Start: Create session with topic + immediately start BBB ────────
    const handleQuickStart = useCallback(async (courseId: string, topic: string, videoUrl?: string) => {
        if (!token || !tenantId || !topic.trim()) return;
        
        // Tarayıcının pop-up engelleyicisine takılmamak için yeni sekmeyi asenkron işlemden ÖNCE açıyoruz
        // Sadece BigBlueButton derslerinde (yani videoUrl yoksa) sekme açacağız!
        const newTab = !videoUrl ? window.open("about:blank", "_blank") : null;
        
        setActionLoading("quickstart");
        try {
            // 1. Create session with topic as title
            const today = new Date();
            const dateStr = today.toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric" });
            const sessionTitle = `${topic.trim()} — ${dateStr}`;
            const s = await courseApi.createSession(token, tenantId, courseId, {
                title: sessionTitle,
                durationMinutes: 60,
                recordingEnabled: true,
            });

            // 2. If videoUrl is provided, update the session with videoUrl
            if (videoUrl && videoUrl.trim()) {
                await courseApi.updateSession(token, tenantId, courseId, s.id, {
                    title: sessionTitle,
                    videoUrl: videoUrl.trim(),
                });
                s.videoUrl = videoUrl.trim();
            }
            
            // 3. Start the session immediately
            const result = await sessionApi.start(token, tenantId, courseId, s.id);
            
            if (!videoUrl) {
                // Açtığımız sekmeyi şimdi BBB URL'sine yönlendiriyoruz
                if (newTab) {
                    newTab.location.href = result.moderatorJoinUrl;
                } else {
                    // Eğer her şeye rağmen engellendiyse (veya newTab null dönerse) mevcut sayfada aç
                    window.location.href = result.moderatorJoinUrl;
                }
            } else {
                // Harici Canlı Yayın linki ise, yeni sekmede açalım
                window.open(videoUrl.trim(), "_blank");
            }
            
            // 4. Update local state
            const ns = mapSession({ ...s, status: "Live" });
            setCourses(prev => prev.map(c => c.id === courseId ? { ...c, sessionCount: c.sessionCount + 1, sessions: [...c.sessions, ns] } : c));
            if (detail?.id === courseId) setDetail(prev => prev ? { ...prev, sessionCount: prev.sessionCount + 1, sessions: [...prev.sessions, ns] } : null);
            success("Canlı ders başlatıldı! 🔴");
            setLiveStartModal(null);
            setLiveStartTopic("");
        } catch { 
            if (newTab) newTab.close(); // Hata durumunda boş açılan sekmeyi kapat
            toastError("Hata", "Canlı ders başlatılamadı."); 
        }
        finally { setActionLoading(null); }
    }, [token, tenantId, detail]);

    const handleViewAttendance = useCallback(async (sessionId: string) => {
        if (!token || !tenantId) return;
        setAttendanceModalOpen(true);
        setAttendanceData(null);
        setAttendanceLoading(true);
        try {
            const data = await sessionApi.getAttendance(token, tenantId, sessionId);
            setAttendanceData(data);
        } catch { toastError("Hata", "Yoklama alınamadı."); }
        finally { setAttendanceLoading(false); }
    }, [token, tenantId]);

    const handleSort = useCallback((col: "name" | "date" | "sessions" | "status") => {
        if (sortBy === col) {
            setCourseSortDesc(prev => !prev);
        } else {
            setSortBy(col);
            setCourseSortDesc(true);
        }
    }, [sortBy]);

    const filtered = useMemo(() => {
        let result = courses.filter(c => {
            const ms = !search || c.title.toLocaleLowerCase('tr-TR').includes(search.toLocaleLowerCase('tr-TR'));
            const mst = statusFilter === "all" || (statusFilter === "published" ? c.isPublished : !c.isPublished);
            return ms && mst;
        });
        if (sortBy === "name") result = [...result].sort((a, b) => courseSortDesc ? b.title.localeCompare(a.title, "tr") : a.title.localeCompare(b.title, "tr"));
        else if (sortBy === "sessions") result = [...result].sort((a, b) => courseSortDesc ? b.sessionCount - a.sessionCount : a.sessionCount - b.sessionCount);
        else if (sortBy === "status") result = [...result].sort((a, b) => {
            const val = a.isPublished === b.isPublished ? 0 : a.isPublished ? -1 : 1;
            return courseSortDesc ? val : -val;
        });
        else result = [...result].sort((a, b) => courseSortDesc ? b.createdAt.localeCompare(a.createdAt) : a.createdAt.localeCompare(b.createdAt));
        return result;
    }, [courses, search, statusFilter, sortBy, courseSortDesc]);

    const totalCoursePages = Math.ceil(filtered.length / coursesPerPage);
    const pagedCourses = filtered.slice(coursePage * coursesPerPage, (coursePage + 1) * coursesPerPage);

    const stats = {
        total: courses.length,
        published: courses.filter(c => c.isPublished).length,
        liveNow: courses.reduce((s, c) => s + c.sessions.filter(se => se.status === "live").length, 0),
    };

    // ════════════════════════════════════════════════════════════════════════════
    // DETAIL VIEW
    // ════════════════════════════════════════════════════════════════════════════
    if (detail) {
        const c = detail;
        const liveSessions = c.sessions.filter(s => s.status === "live").length;
        const endedSessions = c.sessions.filter(s => s.status === "ended").length;
        const TI = typeIcons[c.type] ?? Monitor;

        return (
            <div className="space-y-6 animate-fade-in">
                <button onClick={() => { setDetail(null); setTab("overview"); }} className="flex items-center gap-2 text-sm text-[#A9A9A9] hover:text-[#0A1931] transition-colors group">
                    <ArrowLeft size={16} className="group-hover:-translate-x-0.5 transition-transform" /> Ders Listesine Dön
                </button>

                {/* Hero */}
                <div className="relative rounded-3xl md:rounded-[2.5rem] overflow-hidden group shadow-xl sm:shadow-2xl shadow-indigo-900/10 bg-[#0A1931]">
                    {c.thumbnailUrl ? (
                        <>
                            <img src={c.thumbnailUrl} alt="Cover" className="absolute inset-0 w-full h-full object-cover opacity-80" />
                            <div className="absolute inset-0 bg-black/60" />
                        </>
                    ) : (
                        <div className="absolute inset-0 bg-gradient-to-br from-[#1B3B6F] via-violet-600 to-blue-700" />
                    )}
                    <div className="absolute inset-0 bg-gradient-to-t from-[#0A1931]/90 via-transparent to-white/5" />
                    <div className="relative p-6 sm:p-8 md:p-10 flex flex-col md:flex-row items-center gap-5 sm:gap-6 md:gap-8">
                        <div className="w-16 h-16 sm:w-20 sm:h-20 md:w-24 md:h-24 rounded-2xl md:rounded-3xl bg-white/10 backdrop-blur-xl flex items-center justify-center shadow-2xl ring-1 ring-white/20 overflow-hidden shrink-0">
                            {c.thumbnailUrl ? (
                                <img src={c.thumbnailUrl} alt="Cover" className="w-full h-full object-cover" />
                            ) : (
                                <BookOpen className="text-white w-8 h-8 md:w-10 md:h-10" />
                            )}
                        </div>
                        <div className="flex-1 text-center md:text-left">
                            <div className="flex flex-wrap items-center justify-center md:justify-start gap-2 mb-2 sm:mb-3">
                                {c.isPublished
                                    ? <span className="text-[9px] md:text-[10px] font-bold uppercase tracking-widest px-2.5 py-1 md:px-3 rounded-full bg-emerald-500/20 text-emerald-200 border border-emerald-400/20">✓ Yayında</span>
                                    : <span className="text-[9px] md:text-[10px] font-bold uppercase tracking-widest px-2.5 py-1 md:px-3 rounded-full bg-amber-500/20 text-amber-200 border border-amber-400/20">Taslak</span>
                                }
                            </div>
                            <h1 className="text-2xl sm:text-3xl md:text-4xl font-black text-white tracking-tight mb-1 sm:mb-2">{c.title}</h1>
                            <p className="text-[11px] sm:text-sm font-medium text-white/60 max-w-xl leading-relaxed">{c.description}</p>
                        </div>
                        <div className="flex flex-col w-full md:w-auto gap-3 shrink-0">
                            <div className="grid grid-cols-1 gap-3">
                                <div className="p-3 md:p-4 rounded-xl md:rounded-3xl bg-white/5 backdrop-blur-md border border-white/10 text-center min-w-[120px]">
                                    <Users className="text-white/40 mx-auto mb-1 md:mb-2 w-4 h-4 md:w-5 md:h-5" />
                                    <p className="text-lg md:text-xl font-black text-white tabular-nums">{c.groupCount}</p>
                                    <p className="text-[8px] md:text-[9px] font-bold text-white/40 uppercase tracking-widest">Kayıtlı Grup</p>
                                </div>
                            </div>
                            {(() => {
                                const liveSession = c.sessions.find(s => s.status === "live");
                                return liveSession ? (
                                    <button onClick={() => handleEndSession(c.id, liveSession.id)} 
                                        className="w-full px-4 py-3 md:px-6 md:py-4 text-xs md:text-sm font-black bg-[#E50914] text-white rounded-xl md:rounded-[1.25rem] hover:bg-red-700 transition-all shadow-lg md:shadow-[0_8px_25px_rgba(229,9,20,0.35)] flex items-center justify-center gap-2 md:gap-2.5 active:scale-95 border border-red-500/30">
                                        <StopCircle size={16} className="animate-pulse" /> DERSİ SONLANDIR
                                    </button>
                                ) : (
                                    <button onClick={() => setLiveStartModal({ courseId: c.id, courseName: c.title })} 
                                        className="w-full px-4 py-3 md:px-6 md:py-4 text-xs md:text-sm font-black bg-gradient-to-r from-red-500 to-rose-600 text-white rounded-xl md:rounded-[1.25rem] hover:from-red-600 hover:to-rose-700 transition-all shadow-lg md:shadow-[0_8px_25px_rgba(239,68,68,0.35)] flex items-center justify-center gap-2 md:gap-2.5 active:scale-95 animate-pulse-slow border border-red-400/30">
                                        <Radio size={16} className="animate-pulse" /> CANLI DERS BAŞLAT
                                    </button>
                                );
                            })()}
                        </div>
                    </div>
                </div>

                {/* Tabs */}
                <div className="bg-white/80 backdrop-blur-xl rounded-[2.5rem] border border-[#E2E8F0]/60 overflow-hidden shadow-2xl shadow-indigo-900/5">
                    <div className="flex flex-col sm:flex-row sm:items-center justify-between border-b border-[#E2E8F0]/60 p-4 sm:p-6 bg-[#E2E8F0]/20/50 gap-4">
                        <PremiumTabs 
                            tabs={[
                                { id: "overview", label: "Genel Bakış", icon: <BarChart3 size={14} /> },
                                { id: "recordings", label: "Kayıtlar", icon: <PlayCircle size={14} /> },
                                !isInstructor ? { id: "students", label: "Bireysel Öğrenciler", icon: <Users size={14} /> } : null,
                                { id: "docs", label: "Dokümanlar", icon: <FileText size={14} /> },
                                { id: "settings", label: "Ayarlar", icon: <Settings size={14} /> }
                            ].filter(Boolean) as any} 
                            activeTab={tab} 
                            onChange={(id) => setTab(id as typeof tab)} 
                            className="flex-1 min-w-0 pr-4"
                        />
                        {/* Publish toggle in tab bar */}
                        <div className="flex items-center">
                            <button onClick={() => handleTogglePublish(c.id, c.isPublished)}
                                className={`w-full sm:w-auto px-4 py-2.5 sm:py-2 text-[10px] font-bold uppercase tracking-widest rounded-xl transition-all flex items-center justify-center gap-2 ${c.isPublished ? "bg-emerald-50 text-emerald-600 hover:bg-emerald-100 ring-1 ring-emerald-200" : "bg-amber-50 text-amber-600 hover:bg-amber-100 ring-1 ring-amber-200"}`}>
                                {c.isPublished ? "✓ Yayında" : "Yayına Al"}
                            </button>
                        </div>
                    </div>

                    <div className="p-6">
                        {/* ── OVERVIEW ────────────────────────────────────────── */}
                        {tab === "overview" && (
                            <div className="space-y-6">
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div className="p-4 rounded-2xl bg-white border border-[#E2E8F0]/60 hover:shadow-lg transition-all group">
                                        <Users size={14} className="text-[#A0AEC0] mb-2 group-hover:text-[#A0AEC0] transition-colors" />
                                        <p className="text-xl font-bold text-[#0A1931] tracking-tight">{c.groupCount}</p>
                                        <p className="text-[10px] font-bold uppercase tracking-widest text-[#A0AEC0] mt-0.5">Kayıtlı Grup</p>
                                    </div>
                                </div>


                                {/* Hızlı İşlemler + Bilgi */}
                                <div className="grid grid-cols-2 gap-4">
                                    <div className="p-5 rounded-2xl bg-[#E2E8F0]/20 border border-[#E2E8F0]/60">
                                        <p className="text-[10px] font-bold uppercase tracking-widest text-[#A0AEC0] mb-1">Oluşturulma</p>
                                        <p className="text-sm font-bold text-[#0A1931]">{c.createdAt}</p>
                                    </div>
                                    <div className="p-5 rounded-2xl bg-[#E2E8F0]/20 border border-[#E2E8F0]/60">
                                        <p className="text-[10px] font-bold uppercase tracking-widest text-[#A0AEC0] mb-1">Kayıt Sayısı</p>
                                        <p className="text-sm font-bold text-[#0A1931]">{recordings.length}</p>
                                    </div>
                                </div>

                                {/* Hızlı İşlemler */}
                                <div>
                                    <h3 className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest mb-4">Hızlı İşlemler</h3>
                                    <div className="flex flex-wrap gap-3">
                                        <button onClick={() => setTab("recordings")} className="px-5 py-3 text-xs font-bold bg-white border border-[#E2E8F0] rounded-xl hover:bg-[#E2E8F0]/20 transition-all flex items-center gap-2"><PlayCircle size={14} /> Kayıtları Gör</button>
                                        <button onClick={() => handleTogglePublish(c.id, c.isPublished)}
                                            className={`px-5 py-3 text-xs font-bold rounded-xl transition-all flex items-center gap-2 ${c.isPublished ? "bg-amber-50 border border-amber-200 text-amber-600 hover:bg-amber-100" : "bg-emerald-50 border border-emerald-200 text-emerald-600 hover:bg-emerald-100"}`}>
                                            <Eye size={14} /> {c.isPublished ? "Taslağa Çek" : "Yayına Al"}
                                        </button>
                                    </div>
                                </div>
                            </div>
                        )}



                        {/* ── RECORDINGS ──────────────────────────────────────── */}
                        {tab === "recordings" && (
                            <CourseMediaTab 
                                courseId={detail.id} 
                                recordings={recordings} 
                                sessions={detail.sessions}
                                onViewAttendance={handleViewAttendance}
                                onPlay={(title, url, type) => setPreviewVideo({ title, url, type })}
                                onRefreshDetail={async () => {
                                    if (detail) openDetail(detail, "recordings");
                                }}
                            />
                        )}

                        {/* ── STUDENTS ────────────────────────────────────────── */}
                        {tab === "students" && <CourseStudentTab courseId={detail.id} />}

                        {/* ── DOCS ──────────────────────────────────────────────── */}
                        {tab === "docs" && <DocsTab courseId={c.id} />}

                        {/* ── SETTINGS ────────────────────────────────────────── */}
                        {tab === "settings" && <SettingsTab course={c} onSave={handleSettingsSave} onDelete={() => { handleDelete(c.id); setDetail(null); }} />}
                    </div>
                </div>

                {/* ── Video Preview Modal ────────────────────────────────────── */}
                {previewVideo && (
                    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-md animate-fade-in p-4" onClick={() => setPreviewVideo(null)}>
                        <div className="bg-white rounded-3xl w-full max-w-5xl overflow-hidden shadow-2xl animate-slide-up flex flex-col" onClick={e => e.stopPropagation()}>
                            {/* Header */}
                            <div className="flex items-center justify-between px-6 py-4 border-b border-[#E2E8F0]/60 bg-[#E2E8F0]/20">
                                <h2 className="text-sm font-black text-[#0A1931] tracking-tight flex items-center gap-2">
                                    <PlayCircle size={16} className="text-[#1B3B6F]" />
                                    {previewVideo.title}
                                </h2>
                                <button onClick={() => setPreviewVideo(null)} className="p-2 rounded-xl bg-white hover:bg-red-50 hover:text-red-600 transition-all shadow-sm border border-[#E2E8F0]/60"><X size={16} /></button>
                            </div>
                            {/* Body */}
                            <div className="bg-black aspect-video relative flex items-center justify-center w-full">
                                {previewVideo.type === "video" ? (
                                    <HlsVideoPlayer src={previewVideo.url} />
                                ) : (
                                    <iframe src={previewVideo.url} className="w-full h-full border-0" allowFullScreen />
                                )}
                            </div>
                        </div>
                    </div>
                )}

                {/* Live Start Modal (Detail Scope) */}
                {liveStartModal && (
                    <LiveStartModal
                        modal={liveStartModal}
                        topic={liveStartTopic}
                        setTopic={setLiveStartTopic}
                        onClose={() => { setLiveStartModal(null); setLiveStartTopic(""); }}
                        onStart={handleQuickStart}
                        loading={actionLoading === "quickstart"}
                    />
                )}

                {vodModalOpen && detail && (
                    <VideoUploaderModal 
                        isOpen={vodModalOpen}
                        courseId={detail.id} 
                        onClose={() => setVodModalOpen(false)} 
                        onSuccess={() => {
                            // Let the component auto-refresh via visibility or global state
                        }} 
                    />
                )}

                {/* Attendance Modal */}
                {attendanceModalOpen && (
                    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
                        <div className="bg-white rounded-[2rem] shadow-2xl w-full max-w-3xl max-h-[90vh] flex flex-col overflow-hidden animate-fade-in-up">
                            <div className="flex items-center gap-4 p-6 border-b border-[#E2E8F0]/60 bg-[#E2E8F0]/15">
                                <div className="w-12 h-12 rounded-2xl bg-indigo-100 flex items-center justify-center text-indigo-600"><Users size={24} /></div>
                                <div className="flex-1">
                                    <h2 className="text-xl font-black text-[#0A1931] tracking-tight">{attendanceData?.sessionTitle || "Yoklama Raporu"}</h2>
                                    <p className="text-sm font-medium text-[#A0AEC0]">Oturum Katılım Detayları</p>
                                </div>
                                <button onClick={() => setAttendanceModalOpen(false)} className="p-2 text-[#A0AEC0] hover:text-[#0A1931] hover:bg-[#E2E8F0]/50 rounded-xl transition-all"><X size={20} /></button>
                            </div>
                            <div className="flex-1 overflow-auto p-6">
                                {attendanceLoading ? (
                                    <div className="flex flex-col items-center justify-center py-20"><Loader2 size={32} className="animate-spin text-[#A0AEC0] mb-4" /><p className="text-sm font-medium text-[#A0AEC0]">Veriler yükleniyor...</p></div>
                                ) : attendanceData ? (
                                    <div className="space-y-6">
                                        <div className="grid grid-cols-3 gap-4">
                                            <div className="p-4 rounded-2xl bg-[#E2E8F0]/20 border border-[#E2E8F0]/60 text-center">
                                                <p className="text-2xl font-black text-[#0A1931]">{attendanceData.totalAssigned}</p>
                                                <p className="text-[10px] font-bold uppercase tracking-widest text-[#A0AEC0] mt-1">Kayıtlı</p>
                                            </div>
                                            <div className="p-4 rounded-2xl bg-[#E2E8F0]/20 border border-[#E2E8F0]/60 text-center">
                                                <p className="text-2xl font-black text-emerald-600">{attendanceData.totalPresent}</p>
                                                <p className="text-[10px] font-bold uppercase tracking-widest text-[#A0AEC0] mt-1">Katılan</p>
                                            </div>
                                            <div className="p-4 rounded-2xl bg-[#E2E8F0]/20 border border-[#E2E8F0]/60 text-center">
                                                <p className={`text-2xl font-black ${attendanceData.attendanceRate >= 60 ? 'text-emerald-600' : 'text-amber-600'}`}>%{attendanceData.attendanceRate}</p>
                                                <p className="text-[10px] font-bold uppercase tracking-widest text-[#A0AEC0] mt-1">Oran</p>
                                            </div>
                                        </div>
                                        <div className="border border-[#E2E8F0]/60 rounded-2xl overflow-hidden">
                                            <table className="w-full text-left">
                                                <thead className="bg-[#E2E8F0]/20 border-b border-[#E2E8F0]">
                                                    <tr>
                                                        <th className="px-5 py-3 text-xs font-semibold text-[#A9A9A9]">Öğrenci</th>
                                                        <th className="px-5 py-3 text-xs font-semibold text-[#A9A9A9]">Katılım Z.</th>
                                                        <th className="px-5 py-3 text-xs font-semibold text-[#A9A9A9]">Çıkış Z.</th>
                                                        <th className="px-5 py-3 text-xs font-semibold text-[#A9A9A9] text-center">Süre</th>
                                                        <th className="px-5 py-3 text-xs font-semibold text-[#A9A9A9] text-center">Durum</th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    {attendanceData.attendees.map((a: any) => (
                                                        <tr key={a.userId} className="border-b border-[#E2E8F0]/60 last:border-0 hover:bg-[#E2E8F0]/10">
                                                            <td className="px-5 py-3 text-sm font-bold text-[#0A1931]">{a.userFullName}</td>
                                                            <td className="px-5 py-3 text-xs font-medium text-[#A0AEC0]">{new Date(a.joinedAt).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}</td>
                                                            <td className="px-5 py-3 text-xs font-medium text-[#A0AEC0]">{a.leftAt ? new Date(a.leftAt).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" }) : "—"}</td>
                                                            <td className="px-5 py-3 text-xs font-bold text-[#1B3B6F] text-center">{a.durationMinutes ? `${a.durationMinutes} dk` : "—"}</td>
                                                            <td className="px-5 py-3 text-center">
                                                                {a.isPresent ? <span className="inline-block px-2 py-1 bg-emerald-50 text-emerald-600 text-[10px] font-bold rounded-lg uppercase">Katıldı</span> : <span className="inline-block px-2 py-1 bg-red-50 text-red-600 text-[10px] font-bold rounded-lg uppercase">Yok</span>}
                                                            </td>
                                                        </tr>
                                                    ))}
                                                    {attendanceData.attendees.length === 0 && (
                                                        <tr><td colSpan={5} className="px-5 py-8 text-center text-sm text-[#A0AEC0]">Kaydedilmiş katılım bulunamadı.</td></tr>
                                                    )}
                                                </tbody>
                                            </table>
                                        </div>
                                    </div>
                                ) : null}
                            </div>
                        </div>
                    </div>
                )}
            </div>
        );
    }

    // ════════════════════════════════════════════════════════════════════════════
    // LIST VIEW
    // ════════════════════════════════════════════════════════════════════════════
    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold text-[#0A1931] tracking-tight">Dersler</h1>
                    <p className="text-sm font-semibold uppercase tracking-widest text-[#A9A9A9] mt-1 opacity-60">Müfredat ve İçerik Yönetimi</p>
                </div>
                <button onClick={() => { setEditCourse(null); setShowWizard(true); }}
                    className="px-6 py-3 text-sm font-bold bg-[#0A1931] text-white rounded-xl hover:bg-[#1B3B6F] transition-all flex items-center gap-2 shadow-lg shadow-black/10">
                    <Plus size={18} /> Yeni Ders
                </button>
            </div>

            {/* Stats */}
            <div className="grid grid-cols-2 gap-4">
                {[
                    { l: "Toplam Ders", v: stats.total, icon: BookOpen },
                    { l: "Yayında", v: stats.published, icon: Play },
                ].map(s => (
                    <div key={s.l} className="bg-white rounded-2xl border border-[#E2E8F0]/60 p-4 flex items-center gap-4 group hover:border-[#A0AEC0] transition-all">
                        <div className="w-10 h-10 rounded-xl bg-[#E2E8F0]/30 flex items-center justify-center">
                            <s.icon size={16} className="text-[#A0AEC0]" />
                        </div>
                        <div>
                            <p className="text-xl font-bold text-[#0A1931] tracking-tight">{s.v}</p>
                            <p className="text-[10px] font-bold uppercase tracking-widest text-[#A0AEC0]">{s.l}</p>
                        </div>
                    </div>
                ))}
            </div>

            {/* Toolbar: Search + Filters + Sort + View Toggle */}
            <div className="bg-white rounded-2xl border border-[#E2E8F0]/60 p-4 shadow-sm">
                <div className="flex items-center gap-3 overflow-x-auto hide-scrollbar pb-1 snap-x snap-mandatory">
                    {/* Search */}
                    <div className="w-[160px] sm:flex-1 shrink-0 snap-start relative">
                        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                        <input type="text" placeholder="Ders ara..." value={search}
                            onChange={e => { setSearch(e.target.value); setCoursePage(0); }}
                            className="w-full pl-9 pr-3 py-2.5 text-xs font-bold bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0] transition-all" />
                    </div>
                    {/* Status Filter */}
                    <div className="shrink-0 snap-start">
                        <CustomSelect 
                            value={statusFilter}
                            onChange={(val) => { setStatusFilter(val as string); setCoursePage(0); }}
                            options={[
                                { label: "Tüm Durumlar", value: "all" },
                                { label: "Yayında", value: "published" },
                                { label: "Taslak", value: "draft" }
                            ]}
                        />
                    </div>
                    {/* Sort */}
                    <div className="shrink-0 snap-start">
                        <CustomSelect 
                            value={sortBy}
                            onChange={(val) => setSortBy(val as any)}
                            options={[
                                { label: "Tarihe Göre", value: "date" },
                                { label: "İsme Göre", value: "name" },
                                { label: "Oturum Sayısı", value: "sessions" },
                                { label: "Duruma Göre", value: "status" }
                            ]}
                        />
                    </div>
                    {/* View Toggle */}
                    <div className="shrink-0 snap-start flex rounded-xl border border-[#E2E8F0] overflow-hidden">
                        <button onClick={() => setViewMode("grid")}
                            className={`p-2.5 transition-all ${viewMode === "grid" ? "bg-[#0A1931] text-white" : "bg-white text-[#A0AEC0] hover:bg-[#E2E8F0]/30"}`}>
                            <LayoutGrid size={14} />
                        </button>
                        <button onClick={() => setViewMode("list")}
                            className={`p-2.5 transition-all ${viewMode === "list" ? "bg-[#0A1931] text-white" : "bg-white text-[#A0AEC0] hover:bg-[#E2E8F0]/30"}`}>
                            <ListIcon size={14} />
                        </button>
                    </div>
                </div>
                {/* Result count */}
                <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest mt-3">{filtered.length} ders bulundu</p>
            </div>

            {/* Course List */}
            {loading ? (
                <div className="flex items-center justify-center py-20"><Loader2 size={32} className="animate-spin text-[#A0AEC0]" /></div>
            ) : filtered.length === 0 ? (
                <div className="bg-white rounded-2xl border border-[#E2E8F0]/60 flex flex-col items-center justify-center py-16 text-[#A0AEC0]">
                    <BookOpen size={40} className="opacity-30 mb-3" /><p className="text-sm font-medium">Ders bulunamadı</p>
                </div>
            ) : viewMode === "grid" ? (
                /* ── GRID VIEW ─────────────────────────────────── */
                <>
                    <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
                        {pagedCourses.map(co => {
                            const TI = typeIcons[co.type] ?? Monitor;
                            return (
                                <div key={co.id} onClick={() => openDetail(co)}
                                    className="bg-white rounded-2xl sm:rounded-[1.25rem] border border-[#E2E8F0]/80 overflow-hidden hover:shadow-[0_8px_30px_rgb(0,0,0,0.06)] hover:border-[#A0AEC0]/50 transition-all duration-300 hover:-translate-y-1 cursor-pointer group flex flex-row sm:flex-col h-[110px] sm:h-auto items-stretch">
                                    <div className="w-[110px] sm:w-full aspect-square sm:aspect-video relative overflow-hidden shrink-0 border-r sm:border-r-0 border-[#E2E8F0]/80">
                                        {co.thumbnailUrl ? (
                                            <img src={co.thumbnailUrl} alt={co.title} className="absolute inset-0 w-full h-full object-cover transition-transform duration-700 group-hover:scale-105" />
                                        ) : (
                                            <div className="absolute inset-0 bg-gradient-to-br from-[#1B3B6F] via-violet-600 to-blue-700 transition-transform duration-700 group-hover:scale-105" />
                                        )}
                                        <div className="absolute inset-0 bg-gradient-to-t from-[#0A1931]/90 via-black/20 to-transparent" />
                                        
                                        <div className="absolute top-2 left-2 sm:top-3 sm:left-3 flex items-center gap-1">
                                            {co.isPublished
                                                ? <span className="text-[8px] sm:text-[9px] font-bold uppercase tracking-widest px-2 py-0.5 sm:px-2.5 sm:py-1 rounded-md sm:rounded-lg bg-emerald-500/90 backdrop-blur-sm text-white shadow-sm">Yayında</span>
                                                : <span className="text-[8px] sm:text-[9px] font-bold uppercase tracking-widest px-2 py-0.5 sm:px-2.5 sm:py-1 rounded-md sm:rounded-lg bg-amber-500/90 backdrop-blur-sm text-white shadow-sm">Taslak</span>
                                            }
                                        </div>
                                        
                                        <div className="absolute bottom-2 left-2 right-2 flex items-center justify-between text-white hidden sm:flex">
                                            <h3 className="text-base font-black leading-tight truncate flex-1 drop-shadow-md">{co.title}</h3>
                                            <div className="flex items-center gap-2 text-[10px] font-bold drop-shadow-md shrink-0 ml-2 opacity-90">
                                                <span className="flex items-center gap-1 bg-black/40 px-2.5 py-1 rounded-lg backdrop-blur-md"><Radio size={12} /> {co.sessionCount}</span>
                                            </div>
                                        </div>

                                        <div className="absolute bottom-2 right-2 sm:hidden text-white">
                                            <span className="flex items-center gap-1 bg-black/60 px-1.5 py-0.5 text-[9px] rounded-md backdrop-blur-md font-bold"><Radio size={10} /> {co.sessionCount}</span>
                                        </div>
                                    </div>
                                    
                                    <div className="p-3 sm:p-5 flex-1 flex flex-col min-w-0 justify-between">
                                        <div>
                                            <h3 className="text-sm sm:hidden font-black text-[#0A1931] leading-tight truncate mb-1">{co.title}</h3>
                                            <p className="text-[10px] sm:text-xs text-[#64748B] line-clamp-2 sm:mb-4 leading-relaxed font-medium">{co.description || "Bu ders için açıklama eklenmemiş."}</p>
                                        </div>
                                        
                                        <div className="flex items-center justify-between pt-2 sm:pt-4 border-t border-[#E2E8F0] mt-auto">
                                            <div className="flex items-center gap-1 sm:gap-1.5">
                                                <Tooltip content="Düzenle">
                                                    <button onClick={e => { e.stopPropagation(); openDetail(co, "settings"); }}
                                                        className="p-1.5 sm:p-2 text-[#A0AEC0] hover:text-[#1B3B6F] hover:bg-[#E2E8F0]/50 rounded-lg sm:rounded-xl transition-all">
                                                        <Settings size={14} className="sm:w-4 sm:h-4" />
                                                    </button>
                                                </Tooltip>
                                                <Tooltip content="Dokümanlar">
                                                    <button onClick={e => { e.stopPropagation(); openDetail(co, "docs"); }}
                                                        className="p-1.5 sm:p-2 text-[#A0AEC0] hover:text-[#1B3B6F] hover:bg-[#E2E8F0]/50 rounded-lg sm:rounded-xl transition-all">
                                                        <FolderOpen size={14} className="sm:w-4 sm:h-4" />
                                                    </button>
                                                </Tooltip>
                                                <Tooltip content="Kayıtlar">
                                                    <button onClick={e => { e.stopPropagation(); openDetail(co, "recordings"); }}
                                                        className="p-1.5 sm:p-2 text-[#A0AEC0] hover:text-[#1B3B6F] hover:bg-[#E2E8F0]/50 rounded-lg sm:rounded-xl transition-all">
                                                        <Video size={14} className="sm:w-4 sm:h-4" />
                                                    </button>
                                                </Tooltip>
                                            </div>
                                            <Tooltip content="Sil">
                                                <button onClick={e => { e.stopPropagation(); setDeleteTarget(co.id); }}
                                                    className="p-1.5 sm:p-2 text-[#A0AEC0] hover:text-red-500 hover:bg-red-50 rounded-lg sm:rounded-xl transition-all">
                                                    <Trash2 size={14} className="sm:w-4 sm:h-4" />
                                                </button>
                                            </Tooltip>
                                        </div>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </>
            ) : (
                /* ── LIST VIEW ─────────────────────────────────── */
                /* ── LIST VIEW ─────────────────────────────────── */
                <ResponsiveList 
                    data={pagedCourses}
                    keyExtractor={co => co.id}
                    desktopColumns={[
                        <button key="name" onClick={() => handleSort("name")} className="flex items-center gap-1.5 hover:text-[#1B3B6F] transition-colors w-full text-left uppercase">DERS ADI <ArrowUpDown size={12} className={sortBy === "name" ? "text-[#1B3B6F]" : "opacity-30"} /></button>,
                        <button key="sessions" onClick={() => handleSort("sessions")} className="flex items-center justify-center gap-1.5 hover:text-[#1B3B6F] transition-colors w-full text-center uppercase">OTURUM <ArrowUpDown size={12} className={sortBy === "sessions" ? "text-[#1B3B6F]" : "opacity-30"} /></button>,
                        <button key="status" onClick={() => handleSort("status")} className="flex items-center justify-center gap-1.5 hover:text-[#1B3B6F] transition-colors w-full text-center uppercase">DURUM <ArrowUpDown size={12} className={sortBy === "status" ? "text-[#1B3B6F]" : "opacity-30"} /></button>,
                        <button key="date" onClick={() => handleSort("date")} className="flex items-center gap-1.5 hover:text-[#1B3B6F] transition-colors w-full text-left uppercase">OLUŞTURMA TARİHİ <ArrowUpDown size={12} className={sortBy === "date" ? "text-[#1B3B6F]" : "opacity-30"} /></button>,
                        <span key="actions" className="flex justify-end w-full uppercase">İŞLEM</span>
                    ]}
                    renderDesktopRow={co => (
                        <tr key={co.id} onClick={() => openDetail(co)}
                            className="border-b border-[#E2E8F0]/60 last:border-0 hover:bg-[#E2E8F0]/10 cursor-pointer transition-colors group">
                            <td className="px-5 py-3">
                                <p className="text-sm font-bold text-[#0A1931] truncate max-w-xs">{co.title}</p>
                                <p className="text-[10px] text-[#A0AEC0] truncate max-w-xs mt-0.5">{co.description || "—"}</p>
                            </td>
                            <td className="px-5 py-3 text-center">
                                <span className="text-sm font-bold text-[#0A1931]">{co.sessionCount}</span>
                            </td>
                            <td className="px-5 py-3 text-center">
                                {co.isPublished
                                    ? <span className="inline-block px-2 py-1 bg-emerald-50 text-emerald-600 text-[10px] font-bold rounded-lg uppercase">Yayında</span>
                                    : <span className="inline-block px-2 py-1 bg-amber-50 text-amber-600 text-[10px] font-bold rounded-lg uppercase">Taslak</span>
                                }
                            </td>
                            <td className="px-5 py-3 text-xs font-medium text-[#A0AEC0]">{co.createdAt}</td>
                            <td className="px-5 py-3 text-right">
                                <div className="flex items-center justify-end gap-1.5 opacity-60 group-hover:opacity-100 transition-opacity">
                                    <button onClick={e => { e.stopPropagation(); openDetail(co, "settings"); }}
                                        className="px-2 py-1.5 text-[10px] font-bold bg-[#1B3B6F]/10 text-[#1B3B6F] rounded-lg hover:bg-[#1B3B6F] hover:text-white transition-colors flex items-center gap-1.5">
                                        <Settings size={12} /> Düzenle
                                    </button>
                                    <button onClick={e => { e.stopPropagation(); openDetail(co, "docs"); }}
                                        className="px-2 py-1.5 text-[10px] font-bold bg-[#1B3B6F]/10 text-[#1B3B6F] rounded-lg hover:bg-[#1B3B6F] hover:text-white transition-colors flex items-center gap-1.5">
                                        <FolderOpen size={12} /> Dokümanlar
                                    </button>
                                    <button onClick={e => { e.stopPropagation(); openDetail(co, "recordings"); }}
                                        className="px-2 py-1.5 text-[10px] font-bold bg-[#1B3B6F]/10 text-[#1B3B6F] rounded-lg hover:bg-[#1B3B6F] hover:text-white transition-colors flex items-center gap-1.5">
                                        <Video size={12} /> Kayıtlar
                                    </button>
                                    <button onClick={e => { e.stopPropagation(); setDeleteTarget(co.id); }}
                                        className="px-2 py-1.5 text-[10px] font-bold bg-red-50 text-red-600 rounded-lg hover:bg-red-500 hover:text-white transition-colors flex items-center gap-1.5">
                                        <Trash2 size={12} /> Sil
                                    </button>
                                </div>
                            </td>
                        </tr>
                    )}
                    renderMobileCard={co => (
                        <div key={co.id} onClick={() => openDetail(co)} className="bg-white rounded-2xl border border-[#E2E8F0]/60 p-4 shadow-sm flex flex-col gap-3">
                            <div className="flex items-center justify-between">
                                <div>
                                    <p className="text-sm font-bold text-[#0A1931]">{co.title}</p>
                                    <p className="text-[10px] text-[#A0AEC0]">{co.sessionCount} Oturum</p>
                                </div>
                                {co.isPublished
                                    ? <span className="inline-block px-2 py-1 bg-emerald-50 text-emerald-600 text-[10px] font-bold rounded-lg uppercase">Yayında</span>
                                    : <span className="inline-block px-2 py-1 bg-amber-50 text-amber-600 text-[10px] font-bold rounded-lg uppercase">Taslak</span>
                                }
                            </div>
                            <div className="flex items-center gap-2 pt-3 border-t border-[#E2E8F0]/60">
                                <button onClick={e => { e.stopPropagation(); openDetail(co, "settings"); }} className="flex-1 py-2 bg-[#F0F4F8] text-[#1B3B6F] rounded-lg text-[10px] font-bold flex items-center justify-center gap-1"><Settings size={12}/> Ayarlar</button>
                                <button onClick={e => { e.stopPropagation(); openDetail(co, "docs"); }} className="flex-1 py-2 bg-[#F0F4F8] text-[#1B3B6F] rounded-lg text-[10px] font-bold flex items-center justify-center gap-1"><FolderOpen size={12}/> Doküman</button>
                                <button onClick={e => { e.stopPropagation(); openDetail(co, "recordings"); }} className="flex-1 py-2 bg-[#F0F4F8] text-[#1B3B6F] rounded-lg text-[10px] font-bold flex items-center justify-center gap-1"><Video size={12}/> Kayıtlar</button>
                            </div>
                        </div>
                    )}
                />
            )}

            {/* Pagination */}
            {!loading && (totalCoursePages > 1 || filtered.length > 0) && (
                <div className="flex flex-wrap items-center justify-center gap-4 mt-6 mb-2">
                    {totalCoursePages > 1 && (
                        <div className="flex items-center gap-2">
                            <button onClick={() => setCoursePage(p => Math.max(0, p - 1))} disabled={coursePage === 0}
                                className="p-2 rounded-xl border border-[#E2E8F0] hover:bg-[#E2E8F0]/30 transition-all disabled:opacity-30 disabled:cursor-not-allowed">
                                <ChevronLeft size={14} />
                            </button>
                            {Array.from({ length: totalCoursePages }, (_, i) => {
                                // Show max 7 page buttons with ellipsis
                                if (totalCoursePages <= 7 || i === 0 || i === totalCoursePages - 1 || Math.abs(i - coursePage) <= 1) {
                                    return (
                                        <button key={i} onClick={() => setCoursePage(i)}
                                            className={`w-8 h-8 rounded-xl text-xs font-bold transition-all ${coursePage === i ? "bg-[#1B3B6F] text-white shadow-lg" : "border border-[#E2E8F0] text-[#A0AEC0] hover:bg-[#E2E8F0]/30"}`}>
                                            {i + 1}
                                        </button>
                                    );
                                }
                                if (i === 1 && coursePage > 3) return <span key={i} className="text-[#A0AEC0] text-xs">…</span>;
                                if (i === totalCoursePages - 2 && coursePage < totalCoursePages - 4) return <span key={i} className="text-[#A0AEC0] text-xs">…</span>;
                                return null;
                            })}
                            <button onClick={() => setCoursePage(p => Math.min(totalCoursePages - 1, p + 1))} disabled={coursePage >= totalCoursePages - 1}
                                className="p-2 rounded-xl border border-[#E2E8F0] hover:bg-[#E2E8F0]/30 transition-all disabled:opacity-30 disabled:cursor-not-allowed">
                                <ChevronRight size={14} />
                            </button>
                        </div>
                    )}
                    
                    <div className="flex items-center gap-3 lg:border-l lg:border-[#E2E8F0] lg:pl-4">
                        <span className="text-[10px] font-bold text-[#A0AEC0] whitespace-nowrap">
                            {coursePage * coursesPerPage + 1}-{Math.min((coursePage + 1) * coursesPerPage, filtered.length)} / {filtered.length}
                        </span>
                        <div className="w-36">
                            <CustomSelect 
                                value={coursesPerPage}
                                onChange={(val) => {
                                    setCoursesPerPage(Number(val));
                                    setCoursePage(0);
                                }}
                                options={[
                                    { label: "15 Göster", value: 15 },
                                    { label: "30 Göster", value: 30 },
                                    { label: "60 Göster", value: 60 },
                                    { label: "Tümünü Göster", value: 999999 }
                                ]}
                            />
                        </div>
                    </div>
                </div>
            )}

            {showWizard && <CourseWizard onClose={() => { setShowWizard(false); setEditCourse(null); }} onSave={handleWizardSave} isSaving={isSavingWizard} />}
            <ConfirmDialog open={deleteTarget !== null} onClose={() => setDeleteTarget(null)} onConfirm={() => deleteTarget && handleDelete(deleteTarget)} title="Dersi Sil" message="Bu ders ve tüm içerikler kalıcı olarak silinecek." />
            
        </div>
    );
}

// ════════════════════════════════════════════════════════════════════════════════
// SETTINGS TAB (Zenginleştirilmiş)
// ════════════════════════════════════════════════════════════════════════════════
function SettingsTab({ course, onSave, onDelete }: { course: MappedCourse; onSave: (id: string, data: any) => void; onDelete: () => void }) {
    const { token, currentTenantId: tenantId } = useAuth();
    const { success, error: toastError } = useToast();
    const [uploading, setUploading] = useState(false);
    const [f, sF] = useState({ 
        title: course.title, description: course.description, courseType: course.type, 
        thumbnailUrl: course.thumbnailUrl || "", order: course.order, isPublished: course.isPublished,
        instructorId: course.instructorId || "" 
    });
    const [instructors, setInstructors] = useState<UserDto[]>([]);

    useEffect(() => {
        if (!token || !tenantId) return;
        userApi.list(token, tenantId, { pageSize: 100, role: "Instructor" })
            .then(res => setInstructors(res.items))
            .catch(() => {});
    }, [token, tenantId]);

    const handleUploadCover = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file || !token || !tenantId) return;
        setUploading(true);
        try {
            const res = await courseApi.uploadCover(token, tenantId, course.id, file);
            success("Kapak fotoğrafı güncellendi! Yeni görsel sisteme kaydedildi.");
            sF(p => ({ ...p, thumbnailUrl: res.url }));
        } catch {
            toastError("Hata", "Kapak fotoğrafı yüklenemedi.");
        } finally {
            setUploading(false);
            e.target.value = "";
        }
    };

    return (
        <div className="max-w-2xl space-y-8">
            {/* Temel Bilgiler */}
            <div>
                <h3 className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest mb-6 flex items-center gap-2"><BookOpen size={14} /> Temel Bilgiler</h3>
                <div className="space-y-4">
                    <div>
                        <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Ders Adı</label>
                        <input type="text" value={f.title} onChange={e => sF(p => ({ ...p, title: e.target.value }))}
                            className="w-full px-4 py-3 text-sm font-bold bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:border-[#A0AEC0] transition-all" />
                    </div>
                    <div>
                        <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Açıklama</label>
                        <textarea value={f.description} onChange={e => sF(p => ({ ...p, description: e.target.value }))}
                            className="w-full px-4 py-3 text-sm font-bold bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:border-[#A0AEC0] resize-none h-24 transition-all" />
                    </div>
                    {/* Eğitim Modeli */}
                    <div>
                        <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Eğitim Modeli</label>
                        <div className="grid grid-cols-2 gap-3">
                            {["Online", "Offline"].map(t => (
                                <button key={t} onClick={() => sF(p => ({ ...p, courseType: t }))}
                                    className={`py-2 text-xs font-bold rounded-xl border transition-all ${f.courseType === t ? "bg-[#0A1931] text-white border-[#0A1931]" : "bg-white text-[#A0AEC0] border-[#E2E8F0] hover:bg-[#E2E8F0]/30"}`}>
                                    {t}
                                </button>
                            ))}
                        </div>
                    </div>
                    <div className="grid grid-cols-1 gap-4">
                        <div>
                            <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5 flex items-center gap-2">
                                <Users size={12} /> Eğitmen Ata
                            </label>
                            <select value={f.instructorId} onChange={e => sF(p => ({ ...p, instructorId: e.target.value }))}
                                className="w-full px-4 py-3 text-sm font-bold bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:border-[#A0AEC0] transition-all appearance-none cursor-pointer">
                                <option value="">Eğitmen Seçiniz</option>
                                {instructors.map(i => (
                                    <option key={i.id} value={i.id}>{i.firstName} {i.lastName}</option>
                                ))}
                            </select>
                        </div>
                    </div>
                    <div>
                        <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Kapak Fotoğrafı</label>
                        <div className="flex items-center gap-4">
                            {f.thumbnailUrl ? (
                                <img src={f.thumbnailUrl} alt="Cover" className="w-16 h-16 rounded-xl object-cover border border-[#E2E8F0]" />
                            ) : (
                                <div className="w-16 h-16 rounded-xl bg-[#E2E8F0]/30 flex items-center justify-center border border-[#E2E8F0]"><ImageIcon size={24} className="text-[#A0AEC0]" /></div>
                            )}
                            <div className="flex-1">
                                <label className={`inline-flex items-center gap-2 px-4 py-2.5 text-xs font-bold bg-white border border-[#E2E8F0] rounded-xl hover:bg-[#E2E8F0]/20 transition-all cursor-pointer ${uploading ? 'opacity-50 pointer-events-none' : ''}`}>
                                    {uploading ? <Loader2 size={14} className="animate-spin" /> : <Upload size={14} />} 
                                    {uploading ? "Yükleniyor..." : "Yeni Görsel Yükle"}
                                    <input type="file" accept="image/*" className="hidden" onChange={handleUploadCover} />
                                </label>
                                <p className="text-[10px] text-[#A0AEC0] mt-2">Önerilen boyut: 1280x720px (Maks 2MB)</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            {/* Yayın Ayarları */}
            <div className="pt-8 border-t border-[#E2E8F0]/60">
                <h3 className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest mb-6 flex items-center gap-2"><Eye size={14} /> Yayın Ayarları</h3>
                <div className="p-6 rounded-2xl border border-[#E2E8F0]">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-bold text-[#0A1931]">Yayın Durumu</p>
                            <p className="text-xs text-[#A0AEC0] mt-0.5">Yayındaki dersler öğrencilere görünür</p>
                        </div>
                        <button onClick={() => sF(p => ({ ...p, isPublished: !p.isPublished }))}
                            className={`w-14 h-8 rounded-full transition-all relative ${f.isPublished ? "bg-emerald-500" : "bg-[#E2E8F0]"}`}>
                            <div className={`w-6 h-6 bg-white rounded-full shadow-md absolute top-1 transition-all ${f.isPublished ? "left-7" : "left-1"}`} />
                        </button>
                    </div>
                </div>
            </div>

            {/* Kaydet */}
            <button onClick={() => onSave(course.id, { ...f })} className="px-8 py-3 text-sm font-bold bg-[#0A1931] text-white rounded-xl active:scale-95 transition-all shadow-xl shadow-black/10">Değişiklikleri Kaydet</button>

            {/* Kritik Bölge */}
            <div className="pt-8 border-t border-[#E2E8F0]/60">
                <h3 className="text-[10px] font-bold text-red-400 uppercase tracking-widest mb-4">Kritik Bölge</h3>
                <p className="text-xs text-[#A0AEC0] mb-4">Bu ders ve tüm ilişkili oturumlar, dokümanlar ve kayıtlar kalıcı olarak silinecektir. Bu işlem geri alınamaz.</p>
                <button onClick={onDelete} className="px-6 py-3 text-sm font-bold text-red-600 bg-red-50 border border-red-100 rounded-xl hover:bg-red-100 transition-all flex items-center gap-2"><Trash2 size={14} /> Dersi Tamamen Sil</button>
            </div>
        </div>
    );
}

// ════════════════════════════════════════════════════════════════════════════════
// DOCS TAB
// ════════════════════════════════════════════════════════════════════════════════
function DocsTab({ courseId }: { courseId: string }) {
    const { token, currentTenantId: tenantId } = useAuth();
    const { success, error: toastError } = useToast();
    const [materials, setMaterials] = useState<CourseMaterialDto[]>([]);
    const [uploading, setUploading] = useState(false);
    const [loading, setLoading] = useState(true);

    const fetchMaterials = useCallback(async () => {
        if (!token || !tenantId) return;
        try {
            const data = await courseApi.listMaterials(token, tenantId, courseId);
            setMaterials(data);
        } catch { /* ignore */ }
        finally { setLoading(false); }
    }, [token, tenantId, courseId]);

    useEffect(() => { fetchMaterials(); }, [fetchMaterials]);

    const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
        if (!token || !tenantId || !e.target.files?.length) return;
        setUploading(true);
        try {
            for (const file of Array.from(e.target.files)) {
                const m = await courseApi.uploadMaterial(token, tenantId, courseId, file);
                setMaterials(prev => [m, ...prev]);
            }
            success("Dosya yüklendi! 📄");
        } catch { toastError("Hata", "Dosya yüklenemedi."); }
        finally { setUploading(false); e.target.value = ""; }
    };

    const handleDelete = async (materialId: string) => {
        if (!token || !tenantId) return;
        try {
            await courseApi.deleteMaterial(token, tenantId, courseId, materialId);
            setMaterials(prev => prev.filter(m => m.id !== materialId));
            success("Dosya silindi.");
        } catch { toastError("Hata", "Dosya silinemedi."); }
    };

    const formatSize = (bytes: number) => bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(0)} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

    const getFileUrl = (path: string) => {
        if (!path) return "#";
        if (path.startsWith("http") || path.startsWith("blob:") || path.startsWith("data:")) return path;
        const base = process.env.NEXT_PUBLIC_API_URL?.replace("/api/v1", "") || "http://localhost:5292";
        let cleanPath = path.startsWith("/") ? path : `/${path}`;
        if (cleanPath.startsWith('/uploads')) {
            cleanPath = `/api/v1${cleanPath}`;
        }
        return `${base}${cleanPath}`;
    };

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <h3 className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest">Dokümanlar ({materials.length})</h3>
                <label className={`px-4 py-2 text-xs font-bold bg-[#0A1931] text-white rounded-xl hover:bg-[#1B3B6F] transition-all flex items-center gap-2 cursor-pointer ${uploading ? "opacity-60 pointer-events-none" : ""}`}>
                    {uploading ? <Loader2 size={14} className="animate-spin" /> : <Plus size={14} />} Dosya Yükle
                    <input type="file" className="hidden" accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.zip,.rar,.jpg,.png,.txt" multiple onChange={handleUpload} />
                </label>
            </div>

            {/* Upload zone */}
            <label className={`flex flex-col items-center justify-center p-10 rounded-[2rem] border-2 border-dashed cursor-pointer transition-all ${uploading ? "border-[#A0AEC0] bg-[#E2E8F0]/30/50" : "border-[#E2E8F0] hover:border-[#A0AEC0] hover:bg-[#E2E8F0]/20"}`}>
                {uploading
                    ? <Loader2 size={32} className="animate-spin text-[#A0AEC0] mb-3" />
                    : <FolderOpen size={32} className="text-[#A0AEC0] mb-3" />}
                <p className="text-sm font-bold text-[#A9A9A9]">{uploading ? "Yükleniyor..." : "PDF, Word, Excel, PowerPoint veya görsel dosyaları buraya sürükleyin"}</p>
                <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest mt-2">Maks. 50 MB / dosya</p>
                <input type="file" className="hidden" accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.zip,.rar,.jpg,.png,.txt" multiple onChange={handleUpload} />
            </label>

            {/* File list */}
            {loading ? (
                <div className="flex justify-center py-8"><Loader2 size={24} className="animate-spin text-[#A0AEC0]" /></div>
            ) : materials.length === 0 ? (
                <div className="text-center py-12 text-[#A0AEC0]">
                    <FileText size={40} className="mx-auto opacity-30 mb-3" />
                    <p className="text-sm font-medium">Henüz doküman yok</p>
                    <p className="text-xs text-[#A0AEC0] mt-1">Yukarıdan dosya yükleyerek başlayın</p>
                </div>
            ) : (
                <div className="grid grid-cols-1 gap-3">
                    {materials.map(m => (
                        <div key={m.id} className="flex items-center gap-4 p-5 rounded-3xl border border-[#E2E8F0]/60 hover:bg-[#E2E8F0]/20 transition-all group">
                            <div className={`w-12 h-12 rounded-2xl flex items-center justify-center ${m.contentType.includes("pdf") ? "bg-red-50" : m.contentType.includes("word") || m.contentType.includes("doc") ? "bg-blue-50" : m.contentType.includes("sheet") || m.contentType.includes("xls") ? "bg-emerald-50" : "bg-[#E2E8F0]/20"
                                }`}>
                                <FileText size={20} className={`${m.contentType.includes("pdf") ? "text-red-400" : m.contentType.includes("word") || m.contentType.includes("doc") ? "text-blue-400" : m.contentType.includes("sheet") || m.contentType.includes("xls") ? "text-emerald-400" : "text-[#A0AEC0]"
                                    }`} />
                            </div>
                            <div className="flex-1 min-w-0">
                                <p className="text-sm font-bold text-[#0A1931] truncate">{m.title}</p>
                                <p className="text-xs text-[#A0AEC0] mt-0.5">{m.fileName} • {formatSize(m.fileSize)} • {new Date(m.createdAt).toLocaleDateString("tr-TR")}</p>
                            </div>
                            <div className="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                                <a href={getFileUrl(m.filePath)} target="_blank" rel="noopener" className="p-2.5 rounded-xl text-[#A0AEC0] hover:text-[#1B3B6F] hover:bg-[#E2E8F0]/30 transition-all"><Eye size={14} /></a>
                                <button onClick={() => handleDelete(m.id)} className="p-2.5 rounded-xl text-[#A0AEC0] hover:text-red-600 hover:bg-red-50 transition-all"><Trash2 size={14} /></button>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

// ════════════════════════════════════════════════════════════════════════════════
// 3-STEP WIZARD
// ════════════════════════════════════════════════════════════════════════════════
function CourseWizard({ onClose, onSave, isSaving }: {
    onClose: () => void;
    onSave: (data: { title: string; description: string; courseType: string; isPublished: boolean; coverImage?: File; startDate?: string; instructorId?: string }) => void;
    isSaving?: boolean;
}) {
    const { token, currentTenantId: tenantId } = useAuth();
    const [instructors, setInstructors] = useState<UserDto[]>([]);
    const [step, setStep] = useState(1);
    const [f, sF] = useState({
        title: "", description: "", courseType: "Online", isPublished: false, instructorId: "",
        coverImage: null as File | null, coverPreview: "",
        startDate: "", addFirstLesson: false,
    });
    const u = (k: string, v: any) => sF(p => ({ ...p, [k]: v }));
    const canNext = step === 1 ? (f.title.trim().length > 0) : true;

    useEffect(() => {
        if (!token || !tenantId) return;
        userApi.list(token, tenantId, { pageSize: 100, role: "Instructor" })
            .then(res => setInstructors(res.items))
            .catch(() => {});
    }, [token, tenantId]);

    const handleCoverUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file) return;
        sF(p => ({ ...p, coverImage: file, coverPreview: URL.createObjectURL(file) }));
    };

    const steps = [
        { n: 1, label: "Temel Bilgiler", icon: BookOpen },
        { n: 2, label: "Yayın Ayarları", icon: Settings },
        { n: 3, label: "İlk Ders", icon: Cal },
    ];

    const handleSubmit = () => {
        if (isSaving) return;
        const data: any = { title: f.title, description: f.description, courseType: f.courseType, isPublished: f.isPublished, instructorId: f.instructorId || undefined };
        if (f.coverImage) data.coverImage = f.coverImage;
        if (f.addFirstLesson && f.startDate) data.startDate = f.startDate;
        onSave(data);
    };

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4" onClick={onClose}>
            <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" />
            <div className="relative w-full max-w-lg max-h-[90vh] bg-white rounded-3xl shadow-2xl animate-fade-in overflow-hidden flex flex-col" onClick={e => e.stopPropagation()}>

                {/* Header */}
                <div className="flex items-center justify-between px-8 py-5 border-b border-[#E2E8F0]/60 shrink-0">
                    <div>
                        <h2 className="text-lg font-bold text-[#0A1931] tracking-tight">Yeni Ders Oluştur</h2>
                        <p className="text-[10px] font-bold uppercase tracking-widest text-[#A0AEC0] mt-0.5">Adım {step} / 3</p>
                    </div>
                    <button onClick={onClose} className="w-9 h-9 rounded-xl hover:bg-[#E2E8F0]/40 text-[#A0AEC0] hover:text-[#0A1931] transition-all flex items-center justify-center"><X size={20} /></button>
                </div>

                {/* Progress bar */}
                <div className="px-8 pt-6 flex items-center gap-2">
                    {steps.map((s, i) => (
                        <div key={s.n} className="flex items-center flex-1">
                            <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold transition-all ${step >= s.n ? "bg-[#0A1931] text-white" : "bg-[#E2E8F0]/40 text-[#A0AEC0]"}`}>
                                {step > s.n ? <CheckCircle2 size={16} /> : s.n}
                            </div>
                            <span className={`ml-2 text-[10px] font-bold uppercase tracking-widest hidden sm:block ${step >= s.n ? "text-[#0A1931]" : "text-[#A0AEC0]"}`}>{s.label}</span>
                            {i < steps.length - 1 && <div className={`flex-1 h-0.5 mx-3 rounded-full transition-colors ${step > s.n ? "bg-[#0A1931]" : "bg-[#E2E8F0]/40"}`} />}
                        </div>
                    ))}
                </div>

                {/* Step Content */}
                <div className="flex-1 overflow-y-auto p-8 space-y-5">
                    {step === 1 && (
                        <>
                            <div>
                                <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Ders Başlığı <span className="text-red-500">*</span></label>
                                <input type="text" value={f.title} onChange={e => u("title", e.target.value)} autoFocus
                                    className="w-full px-4 py-3 text-sm font-bold bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-4 focus:ring-[#0A1931]/5 focus:border-[#A0AEC0] transition-all" placeholder="Örn: TYT Matematik" />
                            </div>
                            <div>
                                <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Açıklama</label>
                                <textarea value={f.description} onChange={e => u("description", e.target.value)}
                                    className="w-full px-4 py-3 text-sm font-bold bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-4 focus:ring-[#0A1931]/5 focus:border-[#A0AEC0] resize-none h-20 transition-all" placeholder="Ders hakkında kısa bilgi..." />
                            </div>
                            {/* Eğitim Modeli */}
                            <div>
                                <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Eğitim Modeli</label>
                                <div className="grid grid-cols-2 gap-3">
                                    {["Online", "Offline"].map(t => (
                                        <button key={t} onClick={() => u("courseType", t)}
                                            className={`py-2 text-xs font-bold rounded-xl border transition-all ${f.courseType === t ? "bg-[#0A1931] text-white border-[#0A1931]" : "bg-white text-[#A0AEC0] border-[#E2E8F0] hover:bg-[#E2E8F0]/30"}`}>
                                            {t}
                                        </button>
                                    ))}
                                </div>
                            </div>
                            {/* Eğitmen Ata */}
                            <div>
                                <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5 flex items-center gap-2">
                                    <Users size={12} /> Eğitmen Ata
                                </label>
                                <select value={f.instructorId} onChange={e => u("instructorId", e.target.value)}
                                    className="w-full px-4 py-3 text-sm font-bold bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:border-[#A0AEC0] transition-all appearance-none cursor-pointer">
                                    <option value="">Eğitmen Seçiniz</option>
                                    {instructors.map(i => (
                                        <option key={i.id} value={i.id}>{i.firstName} {i.lastName}</option>
                                    ))}
                                </select>
                            </div>
                            {/* Kapak Fotoğrafı */}
                            <div>
                                <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Kapak Fotoğrafı</label>
                                {f.coverPreview ? (
                                    <div className="relative rounded-2xl overflow-hidden border border-[#E2E8F0] group">
                                        <img src={f.coverPreview} alt="Kapak" className="w-full h-32 object-cover" />
                                        <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
                                            <label className="px-4 py-2 bg-white/90 text-xs font-bold text-[#0A1931] rounded-xl cursor-pointer hover:bg-white transition-colors">
                                                Değiştir
                                                <input type="file" className="hidden" accept="image/*" onChange={handleCoverUpload} />
                                            </label>
                                        </div>
                                    </div>
                                ) : (
                                    <label className="flex flex-col items-center justify-center h-28 rounded-2xl border-2 border-dashed border-[#E2E8F0] hover:border-[#A0AEC0] hover:bg-[#E2E8F0]/10 cursor-pointer transition-all">
                                        <Video size={24} className="text-[#A0AEC0] mb-1.5" />
                                        <span className="text-xs font-bold text-[#A0AEC0]">Fotoğraf yükle</span>
                                        <span className="text-[9px] text-[#A0AEC0] mt-0.5">JPG, PNG — Maks 5MB</span>
                                        <input type="file" className="hidden" accept="image/*" onChange={handleCoverUpload} />
                                    </label>
                                )}
                            </div>
                        </>
                    )}

                    {step === 2 && (
                        <>
                            <div className="p-6 rounded-2xl border border-[#E2E8F0]">
                                <div className="flex items-center justify-between">
                                    <div>
                                        <p className="text-sm font-bold text-[#0A1931]">Yayın Durumu</p>
                                        <p className="text-xs text-[#A0AEC0] mt-0.5">Yayına alınan dersler öğrencilere görünür</p>
                                    </div>
                                    <button onClick={() => u("isPublished", !f.isPublished)}
                                        className={`w-14 h-8 rounded-full transition-all relative ${f.isPublished ? "bg-emerald-500" : "bg-[#E2E8F0]"}`}>
                                        <div className={`w-6 h-6 bg-white rounded-full shadow-md absolute top-1 transition-all ${f.isPublished ? "left-7" : "left-1"}`} />
                                    </button>
                                </div>
                            </div>
                            <div className="p-6 rounded-2xl bg-[#E2E8F0]/20 border border-[#E2E8F0]/60">
                                <p className="text-xs font-bold text-[#A9A9A9] mb-2">💡 İpucu</p>
                                <p className="text-xs text-[#A0AEC0] leading-relaxed">Dersi taslak olarak oluşturup, oturumları ve içerikleri ekledikten sonra yayına alabilirsiniz.</p>
                            </div>
                        </>
                    )}

                    {step === 3 && (
                        <>
                            <div className="p-6 rounded-2xl border border-[#E2E8F0]">
                                <div className="flex items-center justify-between mb-4">
                                    <div>
                                        <p className="text-sm font-bold text-[#0A1931]">İlk Dersi Planla</p>
                                        <p className="text-xs text-[#A0AEC0] mt-0.5">Dersin başlangıç tarihini belirleyin</p>
                                    </div>
                                    <button onClick={() => u("addFirstLesson", !f.addFirstLesson)}
                                        className={`w-14 h-8 rounded-full transition-all relative ${f.addFirstLesson ? "bg-[#1B3B6F]" : "bg-[#E2E8F0]"}`}>
                                        <div className={`w-6 h-6 bg-white rounded-full shadow-md absolute top-1 transition-all ${f.addFirstLesson ? "left-7" : "left-1"}`} />
                                    </button>
                                </div>
                                {f.addFirstLesson && (
                                    <div className="space-y-4 pt-4 border-t border-[#E2E8F0]/60 animate-fade-in">
                                        <div>
                                            <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Ders Başlama Tarihi</label>
                                            <input type="date" value={f.startDate} onChange={e => u("startDate", e.target.value)}
                                                className="w-full px-4 py-3 text-sm font-bold bg-white border border-[#E2E8F0] rounded-xl focus:outline-none focus:border-[#A0AEC0] transition-all" />
                                        </div>
                                        <div className="p-4 rounded-xl bg-blue-50/50 border border-blue-100">
                                            <p className="text-xs text-blue-700 leading-relaxed">📅 Bu tarih dersin resmi başlangıç tarihidir. Oturum planlaması daha sonra yapılabilir.</p>
                                        </div>
                                    </div>
                                )}
                            </div>
                        </>
                    )}
                </div>

                {/* Footer */}
                <div className="flex items-center justify-between px-8 py-5 border-t border-[#E2E8F0]/60 bg-[#E2E8F0]/20/50 shrink-0">
                    <button onClick={step > 1 ? () => setStep(step - 1) : onClose}
                        className="flex items-center gap-2 px-6 py-3 text-sm font-bold text-[#A9A9A9] hover:text-[#0A1931] transition-colors">
                        <ChevronLeft size={16} /> {step > 1 ? "Geri" : "İptal"}
                    </button>
                    {step < 3 ? (
                        <button onClick={() => setStep(step + 1)} disabled={!canNext || isSaving}
                            className="flex items-center gap-2 px-8 py-3 text-sm font-bold text-white bg-[#0A1931] rounded-xl hover:bg-[#1B3B6F] shadow-xl shadow-black/10 transition-all active:scale-[0.98] disabled:opacity-50">
                            İleri <ChevronRight size={16} />
                        </button>
                    ) : (
                        <button onClick={handleSubmit} disabled={isSaving}
                            className="flex items-center gap-2 px-8 py-3 text-sm font-bold text-white bg-[#1B3B6F] rounded-xl hover:bg-[#0A1931] shadow-xl shadow-[#0A1931]/20 transition-all active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed">
                            {isSaving ? <Loader2 size={16} className="animate-spin" /> : <CheckCircle2 size={16} />} 
                            {isSaving ? "Oluşturuluyor..." : "Oluştur"}
                        </button>
                    )}
                </div>
            </div>
        </div>
    );
}

// ════════════════════════════════════════════════════════════════════════════════
// LIVE START MODAL
// ════════════════════════════════════════════════════════════════════════════════
function LiveStartModal({ 
    modal, 
    topic, 
    setTopic, 
    onClose, 
    onStart, 
    loading 
}: { 
    modal: { courseId: string; courseName: string }; 
    topic: string; 
    setTopic: (t: string) => void; 
    onClose: () => void; 
    onStart: (id: string, topic: string, videoUrl: string) => void; 
    loading: boolean; 
}) {
    const [type, setType] = useState<"bbb" | "youtube">("bbb");
    const [videoUrl, setVideoUrl] = useState("");
    
    const isStartDisabled = !topic.trim() || loading || (type === "youtube" && !videoUrl.trim());

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
            <div className="bg-white rounded-[2rem] shadow-2xl w-full max-w-md overflow-hidden animate-fade-in-up">
                <div className="flex items-center gap-4 p-6 border-b border-[#E2E8F0]/60 bg-[#E2E8F0]/15">
                    <div className="w-12 h-12 rounded-2xl bg-indigo-100 flex items-center justify-center text-indigo-600"><Radio size={24} /></div>
                    <div className="flex-1">
                        <h2 className="text-xl font-black text-[#0A1931] tracking-tight">Canlı Ders Başlat</h2>
                        <p className="text-xs font-medium text-[#A0AEC0] mt-0.5">{modal.courseName}</p>
                    </div>
                    <button onClick={onClose} className="p-2 text-[#A0AEC0] hover:text-[#0A1931] hover:bg-[#E2E8F0]/50 rounded-xl transition-all"><X size={20} /></button>
                </div>
                <div className="p-6 space-y-4">
                    <div>
                        <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Ders Konusu <span className="text-red-500">*</span></label>
                        <input type="text" value={topic} onChange={e => setTopic(e.target.value)} autoFocus placeholder="Örn: Limit ve Süreklilik Soru Çözümü"
                            className="w-full px-4 py-3 text-sm font-bold bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-4 focus:ring-[#0A1931]/5 focus:border-[#A0AEC0] transition-all" />
                        <p className="text-[10px] text-[#A0AEC0] mt-2">Dersin konusu aynı zamanda ders kaydının da ismi olacaktır.</p>
                    </div>

                    <div>
                        <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Ders Tipi</label>
                        <div className="grid grid-cols-2 gap-3 mt-1">
                            <button
                                type="button"
                                onClick={() => setType("bbb")}
                                className={`py-3 px-4 text-xs font-bold rounded-xl border transition-all ${type === "bbb" ? "border-indigo-600 bg-indigo-50 text-indigo-600" : "border-[#E2E8F0] hover:bg-gray-50 text-[#1B3B6F]"}`}
                            >
                                BigBlueButton
                            </button>
                            <button
                                type="button"
                                onClick={() => setType("youtube")}
                                className={`py-3 px-4 text-xs font-bold rounded-xl border transition-all ${type === "youtube" ? "border-indigo-600 bg-indigo-50 text-indigo-600" : "border-[#E2E8F0] hover:bg-gray-50 text-[#1B3B6F]"}`}
                            >
                                YouTube
                            </button>
                        </div>
                    </div>

                    {type === "youtube" && (
                        <div className="animate-in fade-in slide-in-from-top-2 duration-200">
                            <label className="block text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5">Canlı Yayın Linki <span className="text-red-500">*</span></label>
                            <input type="text" value={videoUrl} onChange={e => setVideoUrl(e.target.value)} placeholder="Örn: https://youtube.com/live/... veya Zoom vb."
                                className="w-full px-4 py-3 text-sm font-bold bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-4 focus:ring-[#0A1931]/5 focus:border-[#A0AEC0] transition-all" />
                            <p className="text-[10px] text-[#A0AEC0] mt-2">Öğrencilerin harici platformlar (YouTube, Zoom vb.) üzerinden katılması için link eklemelisiniz.</p>
                        </div>
                    )}
                </div>
                <div className="p-6 pt-0 flex gap-3">
                    <button onClick={onClose} className="flex-1 py-3 text-sm font-bold text-[#A9A9A9] hover:bg-[#E2E8F0]/40 rounded-xl transition-all">İptal</button>
                    <button onClick={() => onStart(modal.courseId, topic, type === "youtube" ? videoUrl : "")} disabled={isStartDisabled}
                        className="flex-1 py-3 text-sm font-bold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl shadow-lg shadow-indigo-600/20 disabled:opacity-50 transition-all flex items-center justify-center gap-2">
                        {loading ? <Loader2 size={16} className="animate-spin" /> : <Play size={16} fill="currentColor" />} Canlı Dersi Başlat
                    </button>
                </div>
            </div>
        </div>
    );
}

