"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import {
    Users, Calendar, TrendingUp, AlertTriangle, Download, RefreshCw,
    ChevronDown, CheckCircle2, XCircle, Clock, BarChart3, Shield
} from "lucide-react";
import { analyticsAdminApi, sessionApi, api, type CourseAttendanceDto } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import { Tooltip } from "@/components/ui/Tooltip";

interface CourseSummary { id: string; title: string; }
interface AttendeeRecord { userId: string; userFullName: string; isPresent: boolean; joinedAt: string; durationMinutes: number | null; }
interface SessionAttendees { sessionId: string; attendees: AttendeeRecord[]; }

function CountUp({ target, duration = 600 }: { target: number; duration?: number }) {
    const [val, setVal] = useState(0);
    useEffect(() => {
        if (target === 0) { setVal(0); return; }
        const start = Date.now();
        const frame = () => {
            const elapsed = Date.now() - start;
            const p = Math.min(elapsed / duration, 1);
            setVal(Math.round(target * (1 - Math.pow(1 - p, 3))));
            if (p < 1) requestAnimationFrame(frame);
        };
        requestAnimationFrame(frame);
    }, [target, duration]);
    return <>{val}</>;
}



function rateColor(rate: number) {
    if (rate === 0) return "text-[#A0AEC0]";
    if (rate >= 80) return "text-emerald-600";
    if (rate >= 60) return "text-amber-600";
    return "text-red-600";
}

function rateBg(rate: number) {
    if (rate === 0) return "bg-[#E2E8F0]";
    if (rate >= 80) return "bg-emerald-500";
    if (rate >= 60) return "bg-amber-500";
    return "bg-red-500";
}

export default function CourseAttendancePage() {
    const { token, currentTenantId: tenantId } = useAuth();
    const [courses, setCourses] = useState<CourseSummary[]>([]);
    const [selectedCourseId, setSelectedCourseId] = useState("");
    const [report, setReport] = useState<CourseAttendanceDto | null>(null);
    const [loadingCourses, setLoadingCourses] = useState(true);
    const [loadingReport, setLoadingReport] = useState(false);

    useEffect(() => {
        if (!token || !tenantId) return;
        api<{ items: CourseSummary[] }>("/courses?pageSize=200", { token, tenantId })
            .then(r => { setCourses(r.items); if (r.items.length > 0) setSelectedCourseId(r.items[0].id); })
            .catch(console.error)
            .finally(() => setLoadingCourses(false));
    }, [token, tenantId]);

    useEffect(() => {
        if (!token || !tenantId || !selectedCourseId) return;
        setLoadingReport(true);
        setReport(null);
        analyticsAdminApi.courseAttendance(token, tenantId, selectedCourseId)
            .then(setReport)
            .catch(console.error)
            .finally(() => setLoadingReport(false));
    }, [token, tenantId, selectedCourseId]);

    // Build student-level attendance data
    const studentMap = useMemo(() => {
        const map = new Map<string, { name: string; sessions: Map<string, boolean> }>();
        if (!report) return map;
        
        report.enrolledStudents.forEach(st => {
            map.set(st.userId, { name: st.fullName, sessions: new Map() });
        });
        
        report.sessions.forEach(s => {
            const presentIds = new Set(s.presentStudentIds || []);
            report.enrolledStudents.forEach(st => {
                if (map.has(st.userId)) {
                    map.get(st.userId)!.sessions.set(s.sessionId, presentIds.has(st.userId));
                }
            });
        });
        return map;
    }, [report]);

    const students = useMemo(() => {
        if (!report) return [];
        const total = report.sessions.length;
        return Array.from(studentMap.entries()).map(([id, data]) => {
            const present = Array.from(data.sessions.values()).filter(Boolean).length;
            const rate = total > 0 ? (present / total) * 100 : 0;
            return { id, name: data.name, present, total, rate, sessions: data.sessions };
        }).sort((a, b) => a.rate - b.rate);
    }, [studentMap, report]);

    const riskStudents = students.filter(s => s.rate < 50);

    // CSV export
    const exportCSV = useCallback(() => {
        if (!report || students.length === 0) return;
        const headers = ["Öğrenci", ...report.sessions.map(s => s.sessionTitle), "Devam Oranı"];
        const rows = students.map(s => [
            s.name,
            ...report.sessions.map(sess => s.sessions.get(sess.sessionId) ? "✓" : "✗"),
            `${s.rate.toFixed(0)}%`
        ]);
        const csv = [headers, ...rows].map(r => r.join(",")).join("\n");
        const blob = new Blob(["\uFEFF" + csv], { type: "text/csv;charset=utf-8;" });
        const a = document.createElement("a"); a.href = URL.createObjectURL(blob);
        a.download = `devam_raporu_${report.courseTitle}.csv`; a.click();
    }, [report, students]);

    return (
        <div className="space-y-5">
            {/* Header */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div>
                    <h1 className="text-2xl font-bold text-[#0A1931] flex items-center gap-2">
                        <Calendar size={22} className="text-blue-500" /> Devam Raporu
                    </h1>
                    <p className="text-sm text-[#A9A9A9] mt-0.5">Kurs bazlı öğrenci devam analizi</p>
                </div>
                <div className="flex items-center gap-2">
                    {students.length > 0 && (
                        <button onClick={exportCSV} className="px-3 py-2 text-xs font-bold bg-white border border-[#E2E8F0] rounded-xl text-[#1B3B6F] hover:bg-[#E2E8F0]/20 flex items-center gap-1.5">
                            <Download size={12} /> CSV İndir
                        </button>
                    )}
                </div>
            </div>

            {/* Course Picker */}
            <div className="bg-white rounded-xl border border-[#E2E8F0]/60 p-4 flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-4">
                <label className="text-sm font-medium text-[#1B3B6F] whitespace-nowrap">Ders Seç:</label>
                <select value={selectedCourseId} onChange={e => setSelectedCourseId(e.target.value)}
                    disabled={loadingCourses}
                    className="w-full sm:flex-1 px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 disabled:opacity-50 text-[#0A1931]">
                    {courses.map(c => <option key={c.id} value={c.id}>{c.title}</option>)}
                </select>
            </div>

            {loadingReport ? (
                <div className="space-y-3">{[...Array(4)].map((_, i) => <div key={i} className="h-20 rounded-xl bg-[#E2E8F0]/40 animate-pulse" />)}</div>
            ) : !report ? null : (
                <>
                    {/* KPI Cards */}
                    <div className="flex lg:grid lg:grid-cols-4 gap-4 overflow-x-auto hide-scrollbar pb-2 snap-x">
                        <div className="min-w-[160px] lg:min-w-0 shrink-0 snap-start bg-white rounded-xl border border-[#E2E8F0]/60 p-4">
                            <div className="flex items-center gap-2 mb-2"><Users size={14} className="text-[#1B3B6F]" /><span className="text-[10px] text-[#A0AEC0] font-medium">KAYITLI ÖĞRENCİ</span></div>
                            <p className="text-2xl font-bold text-[#0A1931]"><CountUp target={report.totalEnrolled} /></p>
                        </div>
                        <div className="min-w-[160px] lg:min-w-0 shrink-0 snap-start bg-white rounded-xl border border-[#E2E8F0]/60 p-4">
                            <div className="flex items-center gap-2 mb-2"><BarChart3 size={14} className="text-blue-500" /><span className="text-[10px] text-[#A0AEC0] font-medium">TOPLAM OTURUM</span></div>
                            <p className="text-2xl font-bold text-blue-600"><CountUp target={report.sessions.length} /></p>
                        </div>
                        <div className="min-w-[160px] lg:min-w-0 shrink-0 snap-start bg-white rounded-xl border border-[#E2E8F0]/60 p-4">
                            <div className="flex items-center gap-2 mb-2"><TrendingUp size={14} className={rateColor(report.avgAttendanceRate)} /><span className="text-[10px] text-[#A0AEC0] font-medium">GENEL DEVAM</span></div>
                            <p className={`text-2xl font-bold ${rateColor(report.avgAttendanceRate)}`}>{report.avgAttendanceRate.toFixed(0)}%</p>
                            <div className="mt-1.5 h-1.5 bg-[#E2E8F0]/40 rounded-full overflow-hidden">
                                <div className={`h-full rounded-full transition-all duration-700 ${rateBg(report.avgAttendanceRate)}`}
                                    style={{ width: `${report.avgAttendanceRate}%` }} />
                            </div>
                        </div>
                        <div className={`min-w-[160px] lg:min-w-0 shrink-0 snap-start rounded-xl border p-4 ${riskStudents.length > 0 ? "bg-red-50 border-red-200" : "bg-emerald-50 border-emerald-200"}`}>
                            <div className="flex items-center gap-2 mb-2">
                                <AlertTriangle size={14} className={riskStudents.length > 0 ? "text-red-500" : "text-emerald-500"} />
                                <span className="text-[10px] text-[#A0AEC0] font-medium">RİSK ALTINDA</span>
                            </div>
                            <p className={`text-2xl font-bold ${riskStudents.length > 0 ? "text-red-600" : "text-emerald-600"}`}><CountUp target={riskStudents.length} /></p>
                            <p className="text-[10px] text-[#A0AEC0]">%50 altı devam</p>
                        </div>
                    </div>

                    {/* Main Content */}
                    <div className="grid grid-cols-1 lg:grid-cols-10 gap-5">
                        {/* Heatmap */}
                        <div className="col-span-1 lg:col-span-7 bg-white rounded-xl border border-[#E2E8F0]/60 overflow-hidden">
                            <div className="px-5 py-3 border-b border-[#E2E8F0]/60 flex items-center justify-between">
                                <h3 className="text-sm font-semibold text-[#0A1931]">📊 Öğrenci × Oturum Matrisi</h3>
                            </div>
                            {students.length === 0 ? (
                                <div className="py-16 text-center text-[#A0AEC0]"><Users size={32} className="mx-auto opacity-20 mb-2" /><p className="text-sm">Devam verisi bulunamadı</p></div>
                            ) : (
                                <div className="overflow-x-auto hide-scrollbar">
                                    <table className="w-full text-xs min-w-[500px]">
                                        <thead>
                                            <tr className="bg-[#E2E8F0]/15">
                                                <th className="sticky left-0 bg-[#E2E8F0]/15 px-3 py-2.5 text-left font-semibold text-[#A9A9A9] min-w-[180px] z-10 shadow-[1px_0_0_#E2E8F0]">Öğrenci</th>
                                                {report.sessions.map(s => (
                                                    <th key={s.sessionId} className="px-1.5 py-2.5 text-center font-medium text-[#A9A9A9] min-w-[52px]" title={s.sessionTitle}>
                                                        <div className="truncate max-w-[52px]">{s.scheduledStart ? new Date(s.scheduledStart).toLocaleDateString("tr-TR", { day: "numeric", month: "short" }) : s.sessionTitle.slice(0, 6)}</div>
                                                    </th>
                                                ))}
                                                <th className="px-3 py-2.5 text-right font-semibold text-[#A9A9A9] min-w-[70px]">Oran</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {students.map((st, idx) => (
                                                <tr key={st.id} className={`border-t border-[#E2E8F0]/30 ${st.rate < 50 ? "bg-red-50/30" : idx % 2 === 0 ? "" : "bg-[#E2E8F0]/5"}`}>
                                                    <td className={`sticky left-0 px-3 py-2 font-medium text-[#0A1931] whitespace-nowrap z-10 shadow-[1px_0_0_#E2E8F0] ${st.rate < 50 ? "bg-[#fef2f2]" : idx % 2 === 0 ? "bg-white" : "bg-[#f8fafc]"}`}>
                                                        <div className="flex items-center gap-1.5">
                                                            {st.rate < 50 && <AlertTriangle size={10} className="text-red-500 flex-shrink-0" />}
                                                            <span className="truncate max-w-[150px]">{st.name}</span>
                                                        </div>
                                                    </td>
                                                    {report.sessions.map(sess => {
                                                        const v = st.sessions.get(sess.sessionId);
                                                        return (
                                                            <td key={sess.sessionId} className="px-1.5 py-2 text-center">
                                                                {v === undefined ? (
                                                                    <div className="w-6 h-6 mx-auto rounded bg-[#E2E8F0]/20" />
                                                                ) : v ? (
                                                                    <Tooltip content="Katıldı"><div className="w-6 h-6 mx-auto rounded bg-emerald-100 flex items-center justify-center">
                                                                        <CheckCircle2 size={12} className="text-emerald-600" />
                                                                    </div></Tooltip>
                                                                ) : (
                                                                    <Tooltip content="Katılmadı"><div className="w-6 h-6 mx-auto rounded bg-red-100 flex items-center justify-center">
                                                                        <XCircle size={12} className="text-red-500" />
                                                                    </div></Tooltip>
                                                                )}
                                                            </td>
                                                        );
                                                    })}
                                                    <td className="px-3 py-2 text-right">
                                                        <span className={`font-bold ${rateColor(st.rate)}`}>{st.rate.toFixed(0)}%</span>
                                                    </td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            )}
                        </div>

                        {/* Right Panel */}
                        <div className="col-span-1 lg:col-span-3 space-y-4">
                            {/* Risk Panel */}
                            <div className={`rounded-xl border p-4 ${riskStudents.length > 0 ? "bg-red-50/50 border-red-200" : "bg-emerald-50/50 border-emerald-200"}`}>
                                <h3 className="text-sm font-semibold text-[#0A1931] flex items-center gap-1.5 mb-3">
                                    <AlertTriangle size={14} className={riskStudents.length > 0 ? "text-red-500" : "text-emerald-500"} />
                                    {students.length === 0 ? "Risk Durumu" : riskStudents.length > 0 ? "Risk Altındaki Öğrenciler" : "Tüm Öğrenciler İyi Durumda"}
                                </h3>
                                {students.length === 0 ? (
                                    <p className="text-xs text-[#A0AEC0]">Henüz devam verisi oluşmadı.</p>
                                ) : riskStudents.length > 0 ? (
                                    <div className="space-y-2 max-h-[200px] overflow-y-auto">
                                        {riskStudents.map(s => (
                                            <div key={s.id} className="flex items-center justify-between p-2.5 bg-white rounded-lg border border-red-200">
                                                <div>
                                                    <p className="text-xs font-bold text-[#0A1931]">{s.name}</p>
                                                    <p className="text-[10px] text-red-500">{s.present}/{s.total} oturum</p>
                                                </div>
                                                <span className="text-sm font-bold text-red-600">{s.rate.toFixed(0)}%</span>
                                            </div>
                                        ))}
                                    </div>
                                ) : (
                                    <p className="text-xs text-emerald-600">Tüm öğrenciler %50 üzeri devam oranına sahip 🎉</p>
                                )}
                            </div>

                            {/* Session Trend */}
                            <div className="bg-white rounded-xl border border-[#E2E8F0]/60 p-4">
                                <h3 className="text-sm font-semibold text-[#0A1931] mb-3">📈 Oturum Bazlı Devam</h3>
                                <div className="space-y-2">
                                    {report.sessions.map(s => (
                                        <div key={s.sessionId}>
                                            <div className="flex items-center justify-between mb-1">
                                                <span className="text-[10px] text-[#0A1931] font-medium truncate max-w-[140px]">{s.sessionTitle}</span>
                                                <span className={`text-[10px] font-bold ${rateColor(s.attendanceRate)}`}>{s.attendanceRate.toFixed(0)}%</span>
                                            </div>
                                            <div className="h-1.5 bg-[#E2E8F0]/40 rounded-full overflow-hidden">
                                                <div className={`h-full rounded-full transition-all duration-700 ${rateBg(s.attendanceRate)}`}
                                                    style={{ width: `${s.attendanceRate}%` }} />
                                            </div>
                                        </div>
                                    ))}
                                    {report.sessions.length === 0 && <p className="text-xs text-[#A0AEC0]">Oturum yok</p>}
                                </div>
                            </div>


                        </div>
                    </div>
                </>
            )}
        </div>
    );
}
