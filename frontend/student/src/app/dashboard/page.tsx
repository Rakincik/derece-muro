"use client";

import { useState } from "react";
import useSWR from "swr";
import { useAuth } from "@/contexts/AuthContext";
import { analyticsApi, courseApi, getFileUrl, type StudentDashboardDto, type UpcomingSessionDto, type CourseDto } from "@/lib/api";
import Link from "next/link";
import { openUrl } from "@/lib/openUrl";
import { useToast } from "@/components/ToastProvider";
import Image from "next/image";
import {
    Play, Clock, Flame, BookOpen, Calendar, CheckCircle2, Video,
    ArrowRight, Zap, Trophy, Target, Star
} from "lucide-react";
import { KpiGrid } from "@/components/ui/KpiGrid";



export default function StudentDashboardPage() {
    const { user, token, currentTenantId: tenantId } = useAuth();
    
    const { data: summary, isLoading, error } = useSWR(
        token && tenantId ? `dashboard-summary:${tenantId}` : null,
        () => analyticsApi.dashboardSummary(token!, tenantId!),
        { refreshInterval: 300000 } // Refresh everything every 5 minutes
    );

    const stats = summary?.stats;
    const sessions = summary?.upcomingSessions || [];
    const courses = summary?.courses || [];
    
    const liveSessions = sessions?.filter?.(s => s.status === "Live") || [];
    const upcomingSessions = sessions?.filter?.(s => s.status !== "Live")?.slice(0, 5) || [];
    const loading = isLoading;

    const [joiningId, setJoiningId] = useState<string | null>(null);
    const { showToast } = useToast();

    const handleJoinLive = async (session: UpcomingSessionDto) => {
        if (!token || !tenantId) return;
        setJoiningId(session.id);
        try {
            const res = await courseApi.joinSession(token, tenantId, session.courseId, session.id);
            await openUrl(res.joinUrl);
        } catch { showToast("Derse katılım sağlanamadı.", "error"); }
        finally { setJoiningId(null); }
    };

    const hours = stats ? Math.floor((stats.totalWatchedMinutes || 0) / 60) : 0;
    const mins = stats ? (stats.totalWatchedMinutes || 0) % 60 : 0;
    const now = new Date();
    const greeting = now.getHours() < 12 ? "Günaydın" : now.getHours() < 18 ? "İyi günler" : "İyi akşamlar";
    const streak = stats?.consecutiveDays || 0;

    // Weekly activity comes from API now, ensure it falls back to empty array if null
    const weeklyData = stats?.weeklyActivity || [];

    return (
        <div className="w-full max-w-[1600px] mx-auto px-4 lg:px-8 space-y-6">
            {/* ── Hero Welcome ── */}
            <div className="hero-continue pt-16 sm:pt-8 p-6 sm:p-8 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-6 sm:gap-0 animate-fade-in relative z-10">
                <div className="relative z-10">
                    <p className="text-white/50 text-sm font-medium mb-1">{greeting}</p>
                    <h1 className="text-3xl font-bold text-white mb-2">{user?.firstName} {user?.lastName}</h1>
                    <p className="text-white/40 text-sm">
                        {new Date().toLocaleDateString("tr-TR", { weekday: "long", day: "numeric", month: "long", year: "numeric" })}
                    </p>
                    {streak > 0 && (
                        <div className="flex items-center gap-2 mt-4 px-4 py-2 bg-white/10 rounded-xl w-fit backdrop-blur-sm border border-white/10">
                            <Flame size={18} className="text-orange-400" />
                            <span className="text-white font-bold text-sm">{streak} Gün</span>
                            <span className="text-white/50 text-xs">kesintisiz çalışma 🔥</span>
                        </div>
                    )}
                </div>
                <div className="hidden md:flex items-center gap-6 relative z-10">
                    <div className="text-center">
                        <div className="w-14 h-14 mx-auto rounded-2xl bg-white/10 flex items-center justify-center mb-2 backdrop-blur-sm border border-white/10">
                            <Clock size={22} className="text-white/70" />
                        </div>
                        <p className="text-white font-bold text-lg">{hours}s {mins}dk</p>
                        <p className="text-white/60 text-xs font-medium">çalışma süresi</p>
                    </div>
                    <div className="text-center">
                        <div className="w-14 h-14 mx-auto rounded-2xl bg-white/10 flex items-center justify-center mb-2 backdrop-blur-sm border border-white/10">
                            <Video size={22} className="text-white/70" />
                        </div>
                        <p className="text-white font-bold text-lg">{stats?.completedVideos ?? 0}</p>
                        <p className="text-white/60 text-xs font-medium">tamamlanan video</p>
                    </div>
                    <div className="text-center">
                        <div className="w-14 h-14 mx-auto rounded-2xl bg-white/10 flex items-center justify-center mb-2 backdrop-blur-sm border border-white/10">
                            <Target size={22} className="text-white/70" />
                        </div>
                        <p className="text-white font-bold text-lg">%{stats?.attendanceRate ?? 0}</p>
                        <p className="text-white/60 text-xs font-medium">katılım oranı</p>
                    </div>
                </div>
            </div>

            {/* 🔴 Canlı Ders Banner */}
            {liveSessions?.length > 0 && (
                <div className="space-y-4 animate-fade-in animate-fade-in-delay-1">
                    {liveSessions.map(s => (
                        <div key={s.id} className="relative overflow-hidden rounded-[2rem] bg-gradient-to-r from-red-600 via-red-500 to-orange-500 shadow-2xl shadow-red-500/40 p-6 sm:p-8">
                            {/* Animated Background Elements */}
                            <div className="absolute top-0 right-0 -mt-10 -mr-10 w-64 h-64 bg-white opacity-10 rounded-full blur-3xl animate-pulse" />
                            <div className="absolute bottom-0 left-0 -mb-10 -ml-10 w-48 h-48 bg-black opacity-10 rounded-full blur-2xl animate-pulse" />
                            
                            <div className="relative z-10 flex flex-col md:flex-row items-start md:items-center justify-between gap-6">
                                <div className="flex items-center gap-5 sm:gap-6">
                                    <div className="w-16 h-16 sm:w-20 sm:h-20 bg-white/20 rounded-2xl sm:rounded-3xl flex items-center justify-center shrink-0 backdrop-blur-md border border-white/30 shadow-inner">
                                        <div className="relative flex items-center justify-center">
                                            <span className="absolute w-8 h-8 rounded-full bg-red-400 animate-ping opacity-75" />
                                            <span className="relative w-6 h-6 rounded-full bg-white shadow-[0_0_20px_rgba(255,255,255,1)]" />
                                        </div>
                                    </div>
                                    <div>
                                        <div className="flex flex-wrap items-center gap-2 mb-2">
                                            <span className="px-3 py-1 rounded-lg bg-white/20 text-white text-[11px] font-black uppercase tracking-widest backdrop-blur-md border border-white/20">Şu An Canlı</span>
                                            <span className="text-white/90 text-sm font-semibold">{s.courseTitle}</span>
                                        </div>
                                        <h2 className="text-2xl sm:text-3xl lg:text-4xl font-extrabold text-white drop-shadow-lg tracking-tight">
                                            {s.title}
                                        </h2>
                                        <p className="text-white/80 text-sm mt-2 font-medium">Öğretmen seni bekliyor, hemen katıl!</p>
                                    </div>
                                </div>
                                
                                <button 
                                    onClick={() => handleJoinLive(s)} 
                                    disabled={joiningId === s.id}
                                    className="w-full md:w-auto px-8 py-4 sm:py-5 bg-white hover:bg-gray-50 text-red-600 font-extrabold text-lg sm:text-xl rounded-2xl sm:rounded-3xl transition-all hover:scale-105 hover:-translate-y-1 active:scale-95 shadow-[0_10px_40px_-10px_rgba(0,0,0,0.5)] flex items-center justify-center gap-3 shrink-0 group"
                                >
                                    {joiningId === s.id ? (
                                        <span className="animate-pulse">Bağlanıyor...</span>
                                    ) : (
                                        <>
                                            <span className="relative flex h-5 w-5 sm:h-6 sm:w-6">
                                                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75"></span>
                                                <span className="relative inline-flex rounded-full h-5 w-5 sm:h-6 sm:w-6 bg-red-600">
                                                    <Play size={14} className="text-white absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 ml-0.5" fill="currentColor" />
                                                </span>
                                            </span>
                                            <span>Canlı Derse Katıl</span>
                                        </>
                                    )}
                                </button>
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {/* ── Stats Grid (Mobile) ── */}
            <div className="md:hidden animate-fade-in animate-fade-in-delay-1">
                {loading ? (
                    <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
                        {Array.from({ length: 4 }).map((_, i) => (
                            <div key={i} className="glass-card p-4"><div className="w-9 h-9 rounded-xl shimmer mb-3" /><div className="h-3 shimmer rounded w-16 mb-2" /><div className="h-6 shimmer rounded w-12" /></div>
                        ))}
                    </div>
                ) : (
                    <KpiGrid 
                        items={[
                            { label: "çalışma süresi", value: `${hours}s ${mins}dk`, icon: Clock, colorClass: "text-[#0A1931]", bgClass: "bg-[#1B3B6F]/10", iconColorClass: "text-[#1B3B6F]" },
                            { label: "tamamlanan video", value: stats?.completedVideos ?? 0, icon: CheckCircle2, colorClass: "text-[#0A1931]", bgClass: "bg-emerald-100", iconColorClass: "text-emerald-600" },
                            { label: "katılım oranı", value: `%${stats?.attendanceRate ?? 0}`, icon: Target, colorClass: "text-[#0A1931]", bgClass: "bg-blue-100", iconColorClass: "text-blue-600" },
                            { label: "aktif seri 🔥", value: `${streak} Gün`, icon: Flame, colorClass: "text-[#0A1931]", bgClass: "bg-orange-100", iconColorClass: "text-orange-500" }
                        ]}
                        className="grid grid-cols-2 gap-4"
                    />
                )}
            </div>

            <div className="grid grid-cols-12 gap-6">
                {/* ── Left Column ── */}
                <div className="col-span-12 lg:col-span-8 space-y-6">

                    {/* My Courses */}
                    <div className="animate-fade-in animate-fade-in-delay-2">
                        <div className="flex items-center justify-between mb-4">
                            <h2 className="text-[#0A1931] font-bold text-base flex items-center gap-2">
                                <BookOpen size={16} className="text-[#1B3B6F]" /> Derslerim
                            </h2>
                            <Link href="/dashboard/courses" className="text-xs text-[#1B3B6F] font-semibold hover:text-[#0A1931] flex items-center gap-1">
                                Tümü <ArrowRight size={12} />
                            </Link>
                        </div>
                        {courses?.length > 0 ? (
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                                {courses.slice(0, 4).map(c => (
                                    <Link key={c.id} href={`/dashboard/courses/${c.id}`}
                                        className="glass-card p-4 flex items-center gap-4 group hover:border-[#1B3B6F]/30">
                                        {c.thumbnailUrl ? (
                                            <div className="w-12 h-12 rounded-xl overflow-hidden shrink-0 relative">
                                                <Image src={getFileUrl(c.thumbnailUrl)} alt={c.title} fill className="object-cover" unoptimized />
                                            </div>
                                        ) : (
                                            <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-[#1B3B6F]/10 to-[#0A1931]/10 flex items-center justify-center shrink-0">
                                                <BookOpen size={18} className="text-[#1B3B6F]" />
                                            </div>
                                        )}
                                        <div className="flex-1 min-w-0">
                                            <p className="text-sm font-semibold text-[#0A1931] truncate group-hover:text-[#1B3B6F] transition-colors">{c.title}</p>
                                            <p className="text-[11px] font-medium text-[#4A5568] bg-[#F1F5F9] px-2 py-0.5 rounded inline-flex mt-1 border border-[#E2E8F0]">{c.sessionCount} video</p>
                                        </div>
                                        <ArrowRight size={14} className="text-[#A0AEC0] group-hover:text-[#1B3B6F] transition-colors shrink-0" />
                                    </Link>
                                ))}
                            </div>
                        ) : (
                            <div className="p-8 text-center bg-[#F8FAFC] rounded-2xl border border-[#E2E8F0]">
                                <p className="text-[#0A1931] font-semibold">Henüz hiç derse kayıtlı değilsin</p>
                                <p className="text-sm text-[#A9A9A9] mt-1">Sol menüden tüm derslere göz atabilirsin.</p>
                            </div>
                        )}
                    </div>

                    {/* Weekly Activity */}
                    <div className="glass-card p-5 animate-fade-in animate-fade-in-delay-3">
                        <h3 className="text-sm font-bold text-[#0A1931] mb-4 flex items-center gap-2">
                            <Zap size={14} className="text-amber-500" /> Bu Hafta Aktivitesi
                        </h3>
                        {weeklyData?.length > 0 ? (
                            <div className="flex items-end gap-2 justify-between">
                                {weeklyData.map((d, i) => {
                                    const maxMin = Math.max(...weeklyData.map(w => w?.minutes || 0), 1);
                                    const mins = d?.minutes || 0;
                                    const barH = mins > 0 ? Math.max(20, (mins / maxMin) * 80) : 8;
                                    return (
                                        <div key={i} className="flex-1 flex flex-col items-center gap-2">
                                            <div className="w-full max-w-[36px] rounded-lg transition-all group relative"
                                                style={{
                                                    height: `${barH}px`,
                                                    background: d.isToday
                                                        ? "linear-gradient(180deg, #1B3B6F, #0A1931)"
                                                        : d.minutes > 0 ? "#E2E8F0" : "#f1f5f9"
                                                }}>
                                                {d.minutes > 0 && (
                                                    <div className="absolute -top-7 left-1/2 -translate-x-1/2 bg-[#0A1931] text-white text-[9px] font-bold px-1.5 py-0.5 rounded-md whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity">
                                                        {d.minutes}dk
                                                    </div>
                                                )}
                                            </div>
                                            <span className={`text-[10px] font-bold ${d.isToday ? "text-[#1B3B6F]" : "text-[#A9A9A9]"}`}>{d.dayLabel}</span>
                                        </div>
                                    );
                                })}
                            </div>
                        ) : (
                            <div className="text-center py-4 text-[#A9A9A9] text-xs">Henüz aktivite yok</div>
                        )}
                    </div>
                </div >

                {/* ── Right Sidebar ── */}
                < div className="col-span-12 lg:col-span-4 space-y-5" >

                    {/* Quick Actions */}
                    < div className="glass-card p-5 animate-fade-in animate-fade-in-delay-1" >
                        <h3 className="text-sm font-bold text-[#0A1931] mb-3 flex items-center gap-2">
                            <Zap size={14} className="text-[#1B3B6F]" /> Hızlı Erişim
                        </h3>
                        <div className="grid grid-cols-2 gap-2">
                            {[
                                { href: "/dashboard/courses", icon: <BookOpen size={16} />, label: "Derslerim", color: "text-[#1B3B6F] bg-[#1B3B6F]/10" },
                                { href: "/dashboard/assignments", icon: <Target size={16} />, label: "Ödevler", color: "text-amber-600 bg-amber-50" },
                                { href: "/dashboard/exams", icon: <Trophy size={16} />, label: "Sınavlar", color: "text-violet-600 bg-violet-50" },
                                { href: "/dashboard/notes", icon: <Star size={16} />, label: "Notlarım", color: "text-pink-600 bg-pink-50" },
                                { href: "/dashboard/podcast", icon: <Zap size={16} />, label: "Podcast", color: "text-blue-600 bg-blue-50" },
                            ].map(a => (
                                <Link key={a.href} href={a.href}
                                    className="flex items-center gap-2.5 p-3 rounded-xl hover:bg-[#E2E8F0]/30 transition-all group">
                                    <div className={`w-8 h-8 rounded-lg ${a.color} flex items-center justify-center shrink-0`}>{a.icon}</div>
                                    <span className="text-xs font-semibold text-[#0A1931] group-hover:text-[#1B3B6F] transition-colors">{a.label}</span>
                                </Link>
                            ))}
                        </div>
                    </div >

                    {/* Upcoming Sessions */}
                    < div className="glass-card p-5 animate-fade-in animate-fade-in-delay-2" >
                        <div className="flex items-center justify-between mb-3">
                            <h3 className="text-sm font-bold text-[#0A1931] flex items-center gap-2">
                                <Calendar size={14} className="text-[#1B3B6F]" /> Yaklaşan Dersler
                            </h3>
                            <Link href="/dashboard/calendar" className="text-[10px] text-[#1B3B6F] font-bold hover:text-[#0A1931]">Takvim →</Link>
                        </div>
                        {
                            upcomingSessions?.length > 0 ? (
                                <div className="space-y-2">
                                    {upcomingSessions.map(s => (
                                        <div key={s.id} className="flex items-center gap-3 p-3 rounded-xl bg-[#E2E8F0]/10 hover:bg-[#E2E8F0]/20 transition-colors">
                                            <div className="w-10 h-10 rounded-lg bg-[#1B3B6F]/10 flex items-center justify-center shrink-0">
                                                <Calendar size={14} className="text-[#1B3B6F]" />
                                            </div>
                                            <div className="flex-1 min-w-0">
                                                <p className="text-xs font-semibold text-[#0A1931] truncate">{s.title}</p>
                                                <p className="text-[10px] text-[#A9A9A9] truncate">{s.courseTitle}</p>
                                                {s.scheduledStart && !isNaN(new Date(s.scheduledStart).getTime()) && (
                                                    <p className="text-[10px] text-[#1B3B6F] font-medium mt-0.5">
                                                        {new Date(s.scheduledStart).toLocaleDateString("tr-TR", { day: "numeric", month: "short" })} ·{" "}
                                                        {new Date(s.scheduledStart).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}
                                                    </p>
                                                )}
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            ) : (
                                <div className="text-center py-6 text-[#A9A9A9]">
                                    <Calendar size={24} className="mx-auto opacity-20 mb-2" />
                                    <p className="text-xs">Yaklaşan ders yok</p>
                                </div>
                            )
                        }
                    </div >


                </div >
            </div >
        </div >
    );
}
