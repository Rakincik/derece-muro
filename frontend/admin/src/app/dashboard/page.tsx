"use client";

import { useState, useEffect } from "react";
import {
    Users, BookOpen, FolderTree, FileText, Calendar, Video,
    Headphones, TrendingUp, TrendingDown, ArrowUpRight,
    Clock, Bell, CheckCircle, AlertCircle, Activity,
    BarChart3, Eye, Play, MessageSquare, Award, Loader2, Plus
} from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import CoursesPage from "./courses/page";
import { KpiGrid } from "@/components/ui/KpiGrid";
import { useAuth } from "@/contexts/AuthContext";
import {
    analyticsApi,
    analyticsAdminApi,
    calendarApi,
    supportApi,
    type AdminDashboardDto,
    type DashboardStatsDto,
    type CalendarEventDto,
    type TicketDto,
} from "@/lib/api";

// ── Default empty state (used until API responds) ──
const FALLBACK_STATS: DashboardStatsDto = {
    totalUsers: 0, activeStudents: 0, demoStudents: 0,
    totalCourses: 0, publishedCourses: 0, totalExams: 0,
    totalAssignments: 0, totalGroups: 0, pendingTickets: 0,
};

const activityColors = {
    exam: { bg: "bg-[#E2E8F0]", text: "text-[#0A1931]", icon: FileText },
    assignment: { bg: "bg-[#E2E8F0]/50", text: "text-[#1B3B6F]", icon: CheckCircle },
    course: { bg: "bg-[#0A1931]", text: "text-white", icon: BookOpen },
    support: { bg: "bg-red-50", text: "text-red-600", icon: AlertCircle },
    video: { bg: "bg-[#A0AEC0]/20", text: "text-[#0A1931]", icon: Play },
    question: { bg: "bg-[#E2E8F0]/50", text: "text-[#1B3B6F]", icon: MessageSquare },
};

const quickLinks = [
    { label: "Yeni Kullanıcı", href: "/dashboard/users", icon: Users, color: "bg-[#0A1931] text-white hover:bg-[#1B3B6F]" },
    { label: "Ders Ekle", href: "/dashboard/courses", icon: BookOpen, color: "bg-white text-[#0A1931] border border-[#E2E8F0] hover:bg-[#E2E8F0]/30" },
    { label: "Sınav Oluştur", href: "/dashboard/exams", icon: FileText, color: "bg-white text-[#0A1931] border border-[#E2E8F0] hover:bg-[#E2E8F0]/30" },
    { label: "Bildirim Gönder", href: "/dashboard/notifications", icon: Bell, color: "bg-white text-[#0A1931] border border-[#E2E8F0] hover:bg-[#E2E8F0]/30" },
    { label: "Takvim", href: "/dashboard/calendar", icon: Calendar, color: "bg-white text-[#0A1931] border border-[#E2E8F0] hover:bg-[#E2E8F0]/30" },
    { label: "Performans", href: "/dashboard/analytics", icon: BarChart3, color: "bg-white text-[#0A1931] border border-[#E2E8F0] hover:bg-[#E2E8F0]/30" },
];

const EVENT_TYPE_COLORS: Record<string, string> = {
    "Sınav": "#0A1931",
    "Canlı Ders": "#1B3B6F",
    "Ödev": "#A9A9A9",
    "Etkinlik": "#A0AEC0",
};

// --- Helper: Beautiful Empty State ---
const EmptyState = ({ icon: Icon, title, message, actionLabel, onAction }: { icon: any, title: string, message: string, actionLabel?: string, onAction?: () => void }) => (
    <div className="flex flex-col items-center justify-center h-full min-h-[200px] text-center p-6 bg-gradient-to-b from-transparent to-[#F8FAFC]/80 rounded-2xl border-2 border-dashed border-[#E2E8F0] group hover:border-[#1B3B6F]/20 transition-colors">
        <div className="w-14 h-14 rounded-2xl bg-white shadow-sm border border-[#E2E8F0] flex items-center justify-center mb-4 group-hover:scale-110 group-hover:rotate-3 transition-transform duration-300">
            <Icon className="text-[#A0AEC0] group-hover:text-[#1B3B6F] transition-colors duration-300" size={26} />
        </div>
        <h3 className="text-sm font-bold text-[#0A1931] mb-1.5">{title}</h3>
        <p className="text-xs text-[#64748B] mb-5 max-w-[220px] leading-relaxed">{message}</p>
        {actionLabel && onAction && (
            <button onClick={onAction} className="px-5 py-2.5 bg-white text-xs font-bold text-[#1B3B6F] border border-[#E2E8F0] rounded-xl shadow-sm hover:shadow-md hover:border-[#1B3B6F]/30 hover:bg-[#F8FAFC] transition-all flex items-center gap-2">
                <Plus size={14} /> {actionLabel}
            </button>
        )}
    </div>
);

export default function DashboardPage() {
    const { user, token, currentTenantId: tenantId } = useAuth();
    const router = useRouter();
    const isInstructor = user?.role === "Instructor" || user?.tenants?.find((t: any) => t.tenantId === tenantId)?.role === "Instructor";
    const isAccountant = user?.role === "Accountant" || user?.tenants?.find((t: any) => t.tenantId === tenantId)?.role === "Accountant";

    const [loading, setLoading] = useState(true);
    const [stats, setStats] = useState<DashboardStatsDto>(FALLBACK_STATS);
    const [dashboard, setDashboard] = useState<AdminDashboardDto | null>(null);
    const [upcomingEvents, setUpcomingEvents] = useState<CalendarEventDto[]>([]);
    const [pendingTickets, setPendingTickets] = useState<TicketDto[]>([]);

    useEffect(() => {
        if (!token || !tenantId) return;
        if (isAccountant) {
            router.replace("/dashboard/accounting");
            return;
        }
        if (isInstructor) {
            setLoading(false);
            return;
        }
        setLoading(true);

        const now = new Date();

        Promise.allSettled([
            analyticsAdminApi.stats(token, tenantId),
            analyticsApi.dashboard(token, tenantId),
            calendarApi.list(token, tenantId, { year: now.getFullYear(), month: now.getMonth() + 1 }),
            supportApi.list(token, tenantId, { status: "Açık", pageSize: 5 }),
        ]).then(([statsRes, dashRes, calRes, ticketRes]) => {
            if (statsRes.status === "fulfilled") setStats(statsRes.value);
            if (dashRes.status === "fulfilled") setDashboard(dashRes.value);
            if (calRes.status === "fulfilled") {
                const events = Array.isArray(calRes.value) ? calRes.value : [];
                // Only future events, sorted
                const future = events
                    .filter(e => new Date(e.startDate) >= now)
                    .sort((a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime())
                    .slice(0, 4);
                setUpcomingEvents(future);
            }
            if (ticketRes.status === "fulfilled") {
                const items = ticketRes.value?.items ?? [];
                setPendingTickets(items.slice(0, 5));
            }
        }).finally(() => setLoading(false));
    }, [token, tenantId]);

    // Derived stats for cards
    const statCards = [
        { label: "Toplam Öğrenci", value: stats.activeStudents.toLocaleString("tr-TR"), sub: `${stats.demoStudents} demo`, icon: Users, color: "bg-[#0A1931]", shadow: "shadow-[#0A1931]/10" },
        { label: "Aktif Dersler", value: String(stats.publishedCourses), sub: `${stats.totalCourses} toplam`, icon: BookOpen, color: "bg-[#1B3B6F]", shadow: "shadow-[#1B3B6F]/10" },
        { label: "Sınavlar", value: String(stats.totalExams), sub: `${stats.totalAssignments} ödev`, icon: FileText, color: "bg-[#1B3B6F]", shadow: "shadow-[#1B3B6F]/10" },
        { label: "Destek Talepleri", value: String(stats.pendingTickets), sub: `${stats.totalGroups} grup`, icon: MessageSquare, color: "bg-[#A9A9A9]", shadow: "shadow-[#A9A9A9]/10" },
    ];

    // Weekly activity chart data from dashboard
    const weeklyData = dashboard?.weeklyActivity?.slice(-7) ?? [];
    const maxViews = Math.max(...weeklyData.map(d => d.videoMinutes || 1), 1);

    // Top content from dashboard
    const topCourses = dashboard?.topCourses?.slice(0, 5) ?? [];
    const topStudents = dashboard?.topStudents?.slice(0, 5) ?? [];

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-[60vh]">
                <div className="flex flex-col items-center gap-3">
                    <Loader2 className="animate-spin text-[#1B3B6F]" size={36} />
                    <p className="text-sm text-[#A9A9A9]">Dashboard yükleniyor...</p>
                </div>
            </div>
        );
    }

    if (isInstructor) {
        return <CoursesPage />;
    }

    return (
        <div className="space-y-8">
            {/* ── Header ── */}
            <div>
                <h1 className="text-2xl font-bold text-[#0A1931]">
                    Hoş geldiniz
                </h1>
            </div>

            {/* ── Stat Cards ── */}
            <KpiGrid 
                items={statCards.map(s => ({
                    label: s.label,
                    value: s.value,
                    subValue: s.sub,
                    icon: s.icon,
                    bgClass: s.color.replace('bg-', 'bg-').replace(']', ']/10'),
                    colorClass: s.color.replace('bg-', 'text-'),
                    iconColorClass: s.color.replace('bg-', 'text-')
                }))}
                className="flex xl:grid xl:grid-cols-4 gap-4 overflow-x-auto pb-2 snap-x snap-mandatory hide-scrollbar"
            />

            {/* ── Quick Links ── */}
            <div className="bg-white rounded-2xl border border-[#E2E8F0] p-5">
                <h2 className="text-sm font-semibold text-[#0A1931] mb-3">Hızlı Erişim</h2>
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-6 gap-3">
                    {quickLinks.map((l) => (
                        <Link key={l.label} href={l.href}
                            className={`flex flex-col items-center justify-center text-center gap-2 p-3.5 rounded-xl transition-all duration-200 ${l.color}`}>
                            <l.icon size={22} className="shrink-0" />
                            <span className="text-xs font-medium leading-tight">{l.label}</span>
                        </Link>
                    ))}
                </div>
            </div>

            {/* ── Main Grid: Activity Chart + Upcoming Events ── */}
            <div className="grid grid-cols-1 xl:grid-cols-5 gap-6">
                {/* Weekly Activity Chart */}
                <div className="xl:col-span-3 bg-white rounded-2xl border border-[#E2E8F0] p-6">
                    <div className="flex items-center justify-between mb-6">
                        <div>
                            <h2 className="text-base font-semibold text-[#0A1931]">Haftalık Aktivite</h2>
                            <p className="text-xs text-[#A0AEC0] mt-0.5">Oturumlar & video izlenme (dk)</p>
                        </div>
                        <div className="flex items-center gap-4 text-xs font-bold uppercase tracking-wider">
                            <span className="flex items-center gap-1.5">
                                <span className="w-2.5 h-2.5 rounded-full bg-[#1B3B6F]" />
                                Video (dk)
                            </span>
                            <span className="flex items-center gap-1.5">
                                <span className="w-2.5 h-2.5 rounded-full bg-teal-500" />
                                Oturum
                            </span>
                        </div>
                    </div>
                    {weeklyData.length === 0 ? (
                        <div className="h-48 pt-2">
                            <EmptyState 
                                icon={Activity} 
                                title="Aktivite Bulunamadı" 
                                message="Bu hafta henüz platformda video izlenmemiş veya oturum açılmamış." 
                            />
                        </div>
                    ) : (
                        <div className="flex items-end gap-1 sm:gap-3 h-48">
                            {weeklyData.map((d) => (
                                <div key={d.day} className="flex-1 flex flex-col items-center gap-1">
                                    <div className="w-full flex flex-col gap-1 items-center" style={{ height: '160px' }}>
                                        <div className="w-full flex gap-0.5 sm:gap-1 items-end h-full">
                                            <div className="flex-1 rounded-sm sm:rounded-lg bg-[#1B3B6F] transition-all duration-500 hover:opacity-80"
                                                style={{ height: `${(d.videoMinutes / maxViews) * 100}%` }}
                                                title={`${d.videoMinutes} dk izlenme`} />
                                            <div className="flex-1 rounded-sm sm:rounded-lg bg-teal-500 transition-all duration-500 hover:opacity-80"
                                                style={{ height: `${(d.sessions / maxViews) * 100}%` }}
                                                title={`${d.sessions} oturum`} />
                                        </div>
                                    </div>
                                    <span className="text-[10px] sm:text-xs font-medium text-[#A9A9A9] truncate w-full text-center">{d.day}</span>
                                </div>
                            ))}
                        </div>
                    )}
                </div>

                {/* Upcoming Events (from Calendar API) */}
                <div className="xl:col-span-2 bg-white rounded-2xl border border-[#E2E8F0] p-6 flex flex-col">
                    <div className="flex items-center justify-between mb-5 shrink-0">
                        <h2 className="text-base font-semibold text-[#0A1931]">Yaklaşan Etkinlikler</h2>
                        <Link href="/dashboard/calendar" className="text-xs text-[#0A1931] hover:text-[#1B3B6F] font-bold uppercase tracking-widest flex items-center gap-1">
                            Tümü <ArrowUpRight size={12} />
                        </Link>
                    </div>
                    {upcomingEvents.length === 0 ? (
                        <div className="flex-1 py-2">
                            <EmptyState 
                                icon={Calendar} 
                                title="Planlanmış Etkinlik Yok" 
                                message="Gelecek günlerde planlanmış herhangi bir sanal sınıf veya sınav bulunmuyor." 
                                actionLabel="Takvime Git" 
                                onAction={() => router.push("/dashboard/calendar")} 
                            />
                        </div>
                    ) : (
                        <div className="flex-1 space-y-3">
                            {upcomingEvents.map((e) => (
                                <div key={e.id}
                                    className="flex items-center gap-3 p-3 rounded-xl bg-[#E2E8F0]/20 hover:bg-[#E2E8F0]/40 transition-colors">
                                    <div className="w-1 h-10 rounded-full" style={{ background: e.color || EVENT_TYPE_COLORS[e.eventType] || "#A0AEC0" }} />
                                    <div className="flex-1 min-w-0">
                                        <p className="text-sm font-medium text-[#0A1931] truncate">{e.title}</p>
                                        <p className="text-xs text-[#A0AEC0] mt-0.5">
                                            {new Date(e.startDate).toLocaleDateString("tr-TR", { day: "numeric", month: "short" })}
                                            {", "}
                                            {new Date(e.startDate).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}
                                        </p>
                                    </div>
                                    <span className="text-[10px] font-semibold px-2 py-1 rounded-lg bg-[#E2E8F0]/50 text-[#1B3B6F]">
                                        {e.eventType}
                                    </span>
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            </div>

            {/* ── Bottom Grid: Top Courses + Top Students ── */}
            <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
                {/* Top Courses */}
                <div className="bg-white rounded-2xl border border-[#E2E8F0] p-6 flex flex-col">
                    <div className="flex items-center justify-between mb-5 shrink-0">
                        <h2 className="text-base font-semibold text-[#0A1931]">En Popüler Dersler</h2>
                        <Award size={18} className="text-[#A0AEC0]" />
                    </div>
                    {topCourses.length === 0 ? (
                        <div className="flex-1 py-2">
                            <EmptyState 
                                icon={Award} 
                                title="Kurs Verisi Yok" 
                                message="Öğrenci kaydı bulunan veya yayında olan bir kursunuz henüz yok." 
                                actionLabel="Kurs Ekle" 
                                onAction={() => router.push("/dashboard/courses")} 
                            />
                        </div>
                    ) : (
                        <div className="flex-1 space-y-3">
                            {topCourses.map((c, i) => (
                                <div key={c.courseId}
                                    className="flex items-center gap-4 p-3 rounded-xl hover:bg-[#E2E8F0]/20 transition-colors">
                                    <span className="w-7 h-7 rounded-lg bg-[#E2E8F0]/50 text-[#1B3B6F] text-xs font-bold flex items-center justify-center">
                                        {i + 1}
                                    </span>
                                    <div className="flex-1 min-w-0">
                                        <p className="text-sm font-medium text-[#0A1931] truncate">{c.title}</p>
                                        <div className="flex items-center gap-3 mt-1">
                                            <span className="text-xs text-[#A0AEC0] flex items-center gap-1">
                                                <Users size={10} /> {c.studentCount} öğrenci
                                            </span>
                                        </div>
                                    </div>
                                    <div className="w-20">
                                        <div className="flex items-center justify-between mb-1">
                                            <span className="text-xs font-semibold text-[#1B3B6F]">{Math.round(c.avgAttendance)}%</span>
                                        </div>
                                        <div className="w-full h-1.5 bg-[#E2E8F0] rounded-full overflow-hidden">
                                            <div className="h-full rounded-full bg-[#1B3B6F] transition-all"
                                                style={{ width: `${Math.min(c.avgAttendance, 100)}%` }} />
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </div>

                {/* Top Students / Pending Tickets */}
                <div className="bg-white rounded-2xl border border-[#E2E8F0] p-6 flex flex-col">
                    <div className="flex items-center justify-between mb-5 shrink-0">
                        <h2 className="text-base font-semibold text-[#0A1931]">
                            {topStudents.length > 0 ? "En Başarılı Öğrenciler" : "Bekleyen Destek Talepleri"}
                        </h2>
                        {topStudents.length > 0 ? (
                            <TrendingUp size={18} className="text-[#A0AEC0]" />
                        ) : (
                            <Link href="/dashboard/support" className="text-xs text-[#1B3B6F] hover:text-[#0A1931] font-bold uppercase tracking-widest flex items-center gap-1">
                                Tümü <ArrowUpRight size={12} />
                            </Link>
                        )}
                    </div>
                    {topStudents.length > 0 ? (
                        <div className="flex-1 space-y-3">
                            {topStudents.map((s, i) => (
                                <div key={s.userId}
                                    className="flex items-center gap-3 p-3 rounded-xl hover:bg-[#E2E8F0]/20 transition-colors">
                                    <span className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold ${i === 0 ? "bg-amber-100 text-amber-700" : i === 1 ? "bg-gray-100 text-gray-600" : i === 2 ? "bg-orange-100 text-orange-700" : "bg-[#E2E8F0]/50 text-[#1B3B6F]"}`}>
                                        {i + 1}
                                    </span>
                                    <div className="flex-1 min-w-0">
                                        <p className="text-sm font-medium text-[#0A1931] truncate">{s.fullName}</p>
                                    </div>
                                    <span className="text-sm font-bold text-[#1B3B6F]">{Math.round(s.score)} puan</span>
                                </div>
                            ))}
                        </div>
                    ) : pendingTickets.length === 0 ? (
                        <div className="flex-1 py-2">
                            <EmptyState 
                                icon={MessageSquare} 
                                title="Harika Haber!" 
                                message="Şu an bekleyen hiçbir destek talebi bulunmuyor. Her şey yolunda." 
                                actionLabel="Taleplere Göz At" 
                                onAction={() => router.push("/dashboard/support")} 
                            />
                        </div>
                    ) : (
                        <div className="flex-1 space-y-1">
                            {pendingTickets.map((t) => (
                                <Link key={t.id} href="/dashboard/support"
                                    className="flex items-center gap-3 p-3 rounded-xl hover:bg-[#E2E8F0]/20 transition-colors">
                                    <div className={`w-9 h-9 rounded-xl ${t.priority === "Acil" ? "bg-red-50 text-red-600" : t.priority === "Yüksek" ? "bg-amber-50 text-amber-600" : "bg-[#E2E8F0]/50 text-[#1B3B6F]"} flex items-center justify-center shrink-0`}>
                                        <AlertCircle size={16} />
                                    </div>
                                    <div className="flex-1 min-w-0">
                                        <p className="text-sm text-[#1B3B6F]">
                                            <span className="font-semibold text-[#0A1931]">{t.studentName}</span>{" "}
                                            — {t.subject}
                                        </p>
                                    </div>
                                    <span className="text-xs text-[#A0AEC0] shrink-0 flex items-center gap-1">
                                        <Clock size={10} /> {new Date(t.createdAt).toLocaleDateString("tr-TR", { day: "numeric", month: "short" })}
                                    </span>
                                </Link>
                            ))}
                        </div>
                    )}
                </div>
            </div>

            {/* ── Dashboard KPIs Bar ── */}
            <div className="bg-[#0A1931] rounded-3xl p-6 lg:p-8 border border-[#1B3B6F]/20 shadow-2xl shadow-[#0A1931]/30 flex flex-col xl:flex-row xl:items-center justify-between gap-6 xl:gap-0">
                <div className="flex items-center gap-5 justify-center xl:justify-start">
                    <div className="w-3.5 h-3.5 rounded-full bg-emerald-500 shadow-[0_0_12px_rgba(16,185,129,0.5)] animate-pulse" />
                    <div>
                        <p className="text-sm font-bold text-white uppercase tracking-widest opacity-80">Sistem Özeti</p>
                        <p className="text-[10px] text-[#A9A9A9] font-bold uppercase tracking-widest mt-1">Gerçek zamanlı veriler</p>
                    </div>
                </div>
                <div className="flex md:grid md:grid-cols-4 xl:flex items-center gap-8 xl:gap-12 overflow-x-auto pb-2 snap-x snap-mandatory hide-scrollbar">
                    <div className="text-center shrink-0 min-w-[100px] snap-center">
                        <p className="text-3xl font-bold text-white tracking-tighter">{stats.activeStudents}</p>
                        <p className="text-[10px] text-[#A9A9A9] font-bold uppercase tracking-widest mt-1">Aktif Öğrenci</p>
                    </div>
                    <div className="hidden md:block xl:block w-px h-10 bg-[#1B3B6F]/30" />
                    <div className="text-center shrink-0 min-w-[100px] snap-center">
                        <p className="text-3xl font-bold text-white tracking-tighter">{stats.publishedCourses}</p>
                        <p className="text-[10px] text-[#A9A9A9] font-bold uppercase tracking-widest mt-1">Yayında Ders</p>
                    </div>
                    <div className="hidden md:block xl:block w-px h-10 bg-[#1B3B6F]/30" />
                    <div className="text-center shrink-0 min-w-[100px] snap-center">
                        <p className="text-3xl font-bold text-white tracking-tighter">{dashboard?.totalVideosWatched ?? 0}</p>
                        <p className="text-[10px] text-[#A9A9A9] font-bold uppercase tracking-widest mt-1">Video İzlenme</p>
                    </div>
                    <div className="hidden md:block xl:block w-px h-10 bg-[#1B3B6F]/30" />
                    <div className="text-center shrink-0 min-w-[100px] snap-center">
                        <p className="text-3xl font-bold text-white tracking-tighter">{stats.totalExams}</p>
                        <p className="text-[10px] text-[#A9A9A9] font-bold uppercase tracking-widest mt-1">Sınav</p>
                    </div>
                </div>
            </div>
        </div>
    );
}
