"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import {
    BarChart3, Users, BookOpen, FileText,
    Clock, Activity, RefreshCw, Monitor, Wifi, Globe,
    ArrowUpRight, Search, User
} from "lucide-react";
import Link from "next/link";
import { PieChart, Pie, Cell, ResponsiveContainer, Tooltip as RechartsTooltip } from "recharts";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/components/toast";
import {
    analyticsAdminApi,
    type DashboardStatsDto,
    type DeviceSessionDto,
} from "@/lib/api";

const COLORS = ["#1B3B6F", "#10b981", "#f59e0b", "#ec4899", "#8b5cf6", "#6366f1", "#14b8a6", "#f97316"];
const DAY_LABELS = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];

function formatLoginTime(dateStr: string) {
    const d = new Date(dateStr);
    const today = new Date();
    const isToday = d.getDate() === today.getDate() && d.getMonth() === today.getMonth() && d.getFullYear() === today.getFullYear();
    
    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);
    const isYesterday = d.getDate() === yesterday.getDate() && d.getMonth() === yesterday.getMonth() && d.getFullYear() === yesterday.getFullYear();
    
    const time = d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
    if (isToday) return `Bugün ${time}`;
    if (isYesterday) return `Dün ${time}`;
    return `${d.toLocaleDateString('tr-TR', { day: 'numeric', month: 'short' })} ${time}`;
}

function formatDuration(loginAt: string, logoutAt: string | null) {
    const end = logoutAt ? new Date(logoutAt).getTime() : Date.now();
    const mins = Math.floor((end - new Date(loginAt).getTime()) / 60000);
    if (mins < 60) return `${mins} dk`;
    return `${Math.floor(mins / 60)}sa ${mins % 60}dk`;
}

function CountUp({ target, duration = 800 }: { target: number; duration?: number }) {
    const [val, setVal] = useState(0);
    useEffect(() => {
        if (target === 0) { setVal(0); return; }
        const start = Date.now();
        const frame = () => {
            const elapsed = Date.now() - start;
            const progress = Math.min(elapsed / duration, 1);
            const ease = 1 - Math.pow(1 - progress, 3);
            setVal(Math.round(target * ease));
            if (progress < 1) requestAnimationFrame(frame);
        };
        requestAnimationFrame(frame);
    }, [target, duration]);
    return <>{val.toLocaleString("tr-TR")}</>;
}

function MiniSparkline({ data, color = "#6366f1" }: { data: number[]; color?: string }) {
    if (data.length < 2) return null;
    const max = Math.max(...data, 1);
    const w = 80, h = 28;
    const points = data.map((v, i) => `${(i / (data.length - 1)) * w},${h - (v / max) * h}`).join(" ");
    return (
        <svg width={w} height={h} className="opacity-60">
            <polyline points={points} fill="none" stroke={color} strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
    );
}

export default function AnalyticsPage() {
    const { token, currentTenantId: tenantId } = useAuth();
    const { error: toastError } = useToast();

    const [stats, setStats] = useState<DashboardStatsDto | null>(null);
    const [sessions, setSessions] = useState<DeviceSessionDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState("");

    const load = useCallback(async () => {
        if (!token || !tenantId) return;
        setLoading(true);
        try {
            const [s, sess] = await Promise.all([
                analyticsAdminApi.stats(token, tenantId),
                analyticsAdminApi.recentSessions(token, tenantId, 7).catch(() => []),
            ]);
            setStats(s);
            setSessions(sess);
        } catch {
            toastError("Hata", "Analitik verileri yüklenemedi.");
        } finally {
            setLoading(false);
        }
    }, [token, tenantId]); // eslint-disable-line react-hooks/exhaustive-deps

    useEffect(() => { load(); }, [token, tenantId]); // eslint-disable-line react-hooks/exhaustive-deps

    // Build heatmap from sessions data
    const heatmapData = useMemo(() => {
        const grid: number[][] = Array.from({ length: 7 }, () => Array(24).fill(0));
        sessions.forEach(s => {
            const d = new Date(s.loginAt);
            const dayIdx = d.getDay() === 0 ? 6 : d.getDay() - 1;
            grid[dayIdx][d.getHours()] += 1;
        });
        return grid;
    }, [sessions]);
    const heatmapMax = Math.max(...heatmapData.flat(), 1);

    // Filter sessions
    const filteredSessions = useMemo(() => {
        if (!searchTerm) return sessions;
        const lower = searchTerm.toLowerCase();
        return sessions.filter(s => 
            s.userFullName.toLowerCase().includes(lower) || 
            (s.ipAddress && s.ipAddress.toLowerCase().includes(lower))
        );
    }, [sessions, searchTerm]);

    const activeSessions = filteredSessions.filter(s => s.isActive);
    const recentSessions = filteredSessions
        .sort((a, b) => new Date(b.loginAt).getTime() - new Date(a.loginAt).getTime())
        .slice(0, 15);

    // Peak activity analysis
    const hourCounts = useMemo(() => {
        const counts = Array(24).fill(0);
        sessions.forEach(s => { counts[new Date(s.loginAt).getHours()]++; });
        return counts;
    }, [sessions]);
    const peakHour = hourCounts.indexOf(Math.max(...hourCounts));

    // Device distribution donut chart
    const deviceData = useMemo(() => {
        const map = new Map<string, number>();
        sessions.forEach(s => {
            let info = s.deviceInfo || "Bilinmiyor";
            if (info.includes("Win")) info = "Windows";
            else if (info.includes("Mac")) info = "Mac";
            else if (info.includes("iOS") || info.includes("iPhone") || info.includes("iPad")) info = "iOS";
            else if (info.includes("Android")) info = "Android";
            map.set(info, (map.get(info) || 0) + 1);
        });
        return Array.from(map.entries())
            .map(([name, value], i) => ({ name, value, color: COLORS[i % COLORS.length] }))
            .sort((a, b) => b.value - a.value);
    }, [sessions]);

    const kpis = stats ? [
        { label: "Toplam Kullanıcı", value: stats.totalUsers, icon: Users, color: "text-[#1B3B6F]", bg: "bg-[#1B3B6F]/5", sparkData: [3, 5, 4, 7, 8, 6, stats.totalUsers > 0 ? 10 : 0] },
        { label: "Aktif Öğrenci", value: stats.activeStudents, icon: Activity, color: "text-emerald-600", bg: "bg-emerald-50", sparkData: [2, 4, 3, 5, 6, 7, 8] },
        { label: "Demo Öğrenci", value: stats.demoStudents, icon: Users, color: "text-amber-600", bg: "bg-amber-50", sparkData: [1, 2, 1, 3, 2, 1, 2] },
        { label: "Yayındaki Ders", value: stats.publishedCourses, icon: BookOpen, color: "text-blue-600", bg: "bg-blue-50", sparkData: [5, 6, 7, 8, 9, 10, stats.publishedCourses] },
        { label: "Canlı Oturum", value: activeSessions.length, icon: Monitor, color: "text-violet-600", bg: "bg-violet-50", sparkData: [] },
        { label: "Bekleyen Ticket", value: stats.pendingTickets, icon: FileText, color: "text-rose-600", bg: "bg-rose-50", sparkData: [] },
    ] : [];

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-[#0A1931] flex items-center gap-2">
                        <BarChart3 size={24} className="text-amber-500" /> Sistem Analitiği
                    </h1>
                    <p className="text-sm text-[#A9A9A9] mt-1">Girişler, aktiviteler ve canlı oturumlar</p>
                </div>
                <div className="flex items-center gap-2">
                    {activeSessions.length > 0 && (
                        <div className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-50 border border-emerald-200 rounded-lg animate-pulse">
                            <div className="w-2 h-2 rounded-full bg-emerald-500" />
                            <span className="text-xs font-bold text-emerald-700">{activeSessions.length} çevrimiçi</span>
                        </div>
                    )}
                    <button onClick={load} className="p-2 rounded-lg bg-white border border-[#E2E8F0] hover:bg-[#E2E8F0]/20 text-[#A9A9A9]">
                        <RefreshCw size={14} className={loading ? "animate-spin" : ""} />
                    </button>
                </div>
            </div>

            {/* KPI Cards */}
            {loading ? (
                <div className="flex lg:grid lg:grid-cols-6 gap-3 overflow-x-auto hide-scrollbar pb-2 snap-x">
                    {[...Array(6)].map((_, i) => <div key={i} className="min-w-[140px] lg:min-w-0 shrink-0 snap-start h-24 bg-[#E2E8F0]/40 rounded-xl animate-pulse" />)}
                </div>
            ) : (
                <div className="flex lg:grid lg:grid-cols-6 gap-3 overflow-x-auto hide-scrollbar pb-2 snap-x">
                    {kpis.map(s => (
                        <div key={s.label} className={`min-w-[140px] lg:min-w-0 shrink-0 snap-start rounded-xl border border-[#E2E8F0]/60 p-4 ${s.bg} transition-all hover:shadow-md`}>
                            <div className="flex items-center justify-between mb-2">
                                <s.icon size={16} className={s.color} />
                                {s.sparkData.length > 0 ? <MiniSparkline data={s.sparkData} color={s.color.includes("emerald") ? "#10b981" : s.color.includes("amber") ? "#f59e0b" : s.color.includes("blue") ? "#3b82f6" : "#1B3B6F"} /> : <ArrowUpRight size={12} className="text-[#A0AEC0]" />}
                            </div>
                            <p className={`text-2xl font-bold ${s.color}`}><CountUp target={s.value} /></p>
                            <p className="text-[10px] text-[#A0AEC0] mt-0.5 font-medium">{s.label}</p>
                        </div>
                    ))}
                </div>
            )}

            {/* Charts & Sessions Grid */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
                {/* Left Column: Heatmap + Donut */}
                <div className="col-span-1 lg:col-span-1 flex flex-col gap-5">
                    {/* Activity Heatmap */}
                    <div className="bg-white rounded-xl border border-[#E2E8F0]/60 p-5 overflow-x-auto">
                        <h3 className="text-sm font-semibold text-[#0A1931] mb-1">🕐 Giriş Saatleri Haritası</h3>
                        <p className="text-[10px] text-[#A0AEC0] mb-3">Hangi gün/saatte giriş yapılıyor</p>
                        <div className="space-y-1">
                            {DAY_LABELS.map((day, dIdx) => (
                                <div key={day} className="flex items-center gap-1">
                                    <span className="text-[9px] text-[#A0AEC0] w-6 text-right font-medium">{day}</span>
                                    <div className="flex gap-[2px] flex-1">
                                        {Array.from({ length: 24 }).map((_, h) => {
                                            const val = heatmapData[dIdx][h];
                                            const intensity = val / heatmapMax;
                                            return (
                                                <div key={h} className="flex-1 h-4 rounded-[2px] transition-all hover:scale-110 cursor-default"
                                                    style={{ background: val > 0 ? `rgba(16, 185, 129, ${Math.max(0.15, intensity)})` : "rgba(226,232,240,0.3)" }}
                                                    title={`${day} ${String(h).padStart(2, "0")}:00 — ${val} giriş`} />
                                            );
                                        })}
                                    </div>
                                </div>
                            ))}
                        </div>
                        <div className="flex items-center justify-between mt-3 pt-2 border-t border-[#E2E8F0]/40">
                            <p className="text-[10px] text-[#A0AEC0]">⚡ En yoğun: <b className="text-[#0A1931]">{String(peakHour).padStart(2, "0")}:00</b></p>
                            <div className="flex items-center gap-1">
                                <span className="text-[9px] text-[#A0AEC0]">Az</span>
                                {[0.1, 0.3, 0.5, 0.7, 1].map(v => (
                                    <div key={v} className="w-3 h-3 rounded-sm" style={{ background: `rgba(16, 185, 129, ${v})` }} />
                                ))}
                                <span className="text-[9px] text-[#A0AEC0]">Çok</span>
                            </div>
                        </div>
                    </div>

                    {/* Device Donut Chart */}
                    <div className="bg-white rounded-xl border border-[#E2E8F0]/60 p-5">
                        <h3 className="text-sm font-semibold text-[#0A1931] mb-1">💻 Cihaz Dağılımı</h3>
                        <p className="text-[10px] text-[#A0AEC0] mb-4">Oturumların yapıldığı işletim sistemleri</p>
                        <div className="h-40 w-full">
                            <ResponsiveContainer width="100%" height="100%">
                                <PieChart>
                                    <Pie
                                        data={deviceData}
                                        cx="50%"
                                        cy="50%"
                                        innerRadius={45}
                                        outerRadius={65}
                                        paddingAngle={2}
                                        dataKey="value"
                                        stroke="none"
                                    >
                                        {deviceData.map((entry, index) => (
                                            <Cell key={`cell-${index}`} fill={entry.color} />
                                        ))}
                                    </Pie>
                                    <RechartsTooltip 
                                        contentStyle={{ borderRadius: '8px', border: '1px solid #E2E8F0', fontSize: '11px', padding: '4px 8px' }}
                                        itemStyle={{ color: '#0A1931', fontWeight: 600 }}
                                    />
                                </PieChart>
                            </ResponsiveContainer>
                        </div>
                        <div className="flex flex-wrap items-center justify-center gap-3 mt-2">
                            {deviceData.slice(0, 4).map(d => (
                                <div key={d.name} className="flex items-center gap-1.5">
                                    <div className="w-2 h-2 rounded-full" style={{ background: d.color }} />
                                    <span className="text-[10px] text-[#0A1931] font-medium">{d.name} <span className="text-[#A0AEC0]">({d.value})</span></span>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>

                {/* Right Column: Sessions Area */}
                <div className="col-span-1 lg:col-span-2 bg-white rounded-xl border border-[#E2E8F0]/60 flex flex-col overflow-hidden max-h-[600px]">
                    {/* Header with Search */}
                    <div className="flex flex-col sm:flex-row sm:items-center justify-between border-b border-[#E2E8F0] p-4 gap-3 bg-[#E2E8F0]/5">
                        <div className="text-xs font-bold flex items-center gap-1.5 text-[#0A1931]">
                            <Globe size={13} /> Giriş/Oturum Kayıtları ({filteredSessions.length})
                        </div>
                        <div className="relative">
                            <Search size={12} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                            <input 
                                type="text"
                                placeholder="İsim veya IP ara..."
                                value={searchTerm}
                                onChange={e => setSearchTerm(e.target.value)}
                                className="pl-7 pr-3 py-1.5 text-xs bg-white border border-[#E2E8F0] rounded-lg focus:outline-none focus:border-[#1B3B6F] focus:ring-1 focus:ring-[#1B3B6F] transition-all w-full sm:w-56 placeholder:text-[#A0AEC0] text-[#0A1931]"
                            />
                        </div>
                    </div>

                    <div className="p-5 flex-1 overflow-y-auto">
                        {loading ? (
                            <div className="space-y-3">{[...Array(6)].map((_, i) => <div key={i} className="h-12 bg-[#E2E8F0]/30 rounded-lg animate-pulse" />)}</div>
                        ) : recentSessions.length === 0 ? (
                            <div className="flex flex-col items-center justify-center py-12 text-[#A0AEC0]">
                                <Globe size={32} className="opacity-20 mb-2" /><p className="text-sm">Eşleşen oturum bulunamadı</p>
                            </div>
                        ) : (
                            <div className="space-y-2">
                                {recentSessions.map(s => (
                                    <div key={s.id} className={`group relative flex items-center gap-3 p-3 rounded-xl border transition-all hover:shadow-sm ${s.isActive ? "bg-emerald-50/50 border-emerald-200/50" : "border-[#E2E8F0]/40 hover:border-[#E2E8F0]"}`}>
                                        <div className={`w-2 h-2 rounded-full flex-shrink-0 ${s.isActive ? "bg-emerald-500 animate-pulse" : "bg-[#E2E8F0]"}`} />
                                        <div className="flex-1 min-w-0">
                                            <div className="flex items-center gap-2">
                                                <span className="text-xs font-bold text-[#0A1931] truncate">{s.userFullName}</span>
                                                {s.isActive && <span className="text-[8px] font-bold bg-emerald-100 text-emerald-700 px-1.5 py-0.5 rounded-full">ÇEVRİMİÇİ</span>}
                                            </div>
                                            <div className="flex items-center gap-3 mt-0.5">
                                                <span className="text-[10px] text-[#A0AEC0] flex items-center gap-0.5"><Monitor size={9} />{s.deviceInfo || "Bilinmiyor"}</span>
                                                {s.ipAddress && <span className="text-[10px] text-[#A0AEC0] flex items-center gap-0.5"><Wifi size={9} />{s.ipAddress}</span>}
                                            </div>
                                        </div>
                                        <div className="text-right flex-shrink-0 transition-opacity group-hover:opacity-0 group-hover:invisible">
                                            <p className="text-[10px] font-medium text-[#0A1931] mb-0.5">{formatLoginTime(s.loginAt)}</p>
                                            <p className="text-[10px] text-[#A0AEC0] flex items-center gap-0.5 justify-end"><Clock size={9} />{formatDuration(s.loginAt, s.logoutAt)}</p>
                                        </div>
                                        <div className="absolute right-3 top-1/2 -translate-y-1/2 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all">
                                            <Link href={`/dashboard/users?userId=${s.userId}`} className="flex items-center gap-1.5 bg-[#1B3B6F] text-white px-3 py-1.5 rounded-lg text-[10px] font-bold hover:bg-[#0A1931] hover:scale-105 transition-all shadow-md">
                                                <User size={12} /> Profile Git
                                            </Link>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                </div>
            </div>

            {/* Platform Summary Row */}
            {stats && (
                <div className="flex lg:grid lg:grid-cols-5 gap-3 overflow-x-auto hide-scrollbar pb-2 snap-x">
                    {[
                        { label: "Toplam Ders", value: stats.totalCourses, sub: `${stats.publishedCourses} yayında`, icon: BookOpen, color: "text-[#1B3B6F]", bg: "bg-[#1B3B6F]/5" },
                        { label: "Toplam Sınav", value: stats.totalExams, sub: "oluşturuldu", icon: FileText, color: "text-orange-600", bg: "bg-orange-50" },
                        { label: "Toplam Ödev", value: stats.totalAssignments, sub: "atandı", icon: Clock, color: "text-blue-600", bg: "bg-blue-50" },
                        { label: "Toplam Grup", value: stats.totalGroups, sub: "oluşturuldu", icon: Users, color: "text-violet-600", bg: "bg-violet-50" },
                        { label: "Toplam Oturum", value: sessions.length, sub: `${activeSessions.length} aktif`, icon: Globe, color: "text-emerald-600", bg: "bg-emerald-50" },
                    ].map(row => (
                        <div key={row.label} className={`min-w-[200px] lg:min-w-0 shrink-0 snap-start ${row.bg} rounded-xl border border-[#E2E8F0]/60 p-4 flex items-center gap-3`}>
                            <div className={`w-10 h-10 rounded-lg ${row.bg} flex items-center justify-center`}>
                                <row.icon size={18} className={row.color} />
                            </div>
                            <div>
                                <p className={`text-lg font-bold ${row.color}`}><CountUp target={row.value} /></p>
                                <p className="text-[10px] text-[#A0AEC0]">{row.label} · {row.sub}</p>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}
