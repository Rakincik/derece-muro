"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import {
    Users, Calendar, TrendingUp, AlertTriangle, Download, RefreshCw,
    ChevronDown, ChevronUp, CheckCircle2, XCircle, Clock, BarChart3, Shield,
    Check, X
} from "lucide-react";
import { analyticsAdminApi, api, type CourseAttendanceDto } from "@/lib/api";
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

function formatSessionDate(dateStr?: string) {
    if (!dateStr) return "Tarih Yok";
    return new Date(dateStr).toLocaleDateString("tr-TR", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
}

function CustomCourseSelect({ courses, value, onChange, disabled }: { courses: CourseSummary[], value: string, onChange: (val: string) => void, disabled?: boolean }) {
    const [isOpen, setIsOpen] = useState(false);
    const [search, setSearch] = useState("");
    const selected = courses.find(c => c.id === value);

    const filteredCourses = useMemo(() => {
        if (!search.trim()) return courses;
        const s = search.toLocaleLowerCase('tr');
        return courses.filter(c => c.title.toLocaleLowerCase('tr').includes(s));
    }, [courses, search]);

    useEffect(() => {
        const handleClick = () => setIsOpen(false);
        if (isOpen) {
            document.addEventListener('click', handleClick);
            return () => document.removeEventListener('click', handleClick);
        }
    }, [isOpen]);

    return (
        <div className={`relative w-full sm:flex-1 ${disabled ? 'opacity-50 pointer-events-none' : ''}`}>
            <div 
                onClick={(e) => { e.stopPropagation(); setIsOpen(!isOpen); setSearch(""); }}
                className="px-4 py-2.5 bg-white border border-[#E2E8F0] rounded-xl flex items-center justify-between cursor-pointer hover:border-blue-400 hover:shadow-sm transition-all select-none"
            >
                <span className="text-sm text-[#0A1931] font-medium truncate pr-4">
                    {selected ? selected.title : "Ders Seçiniz..."}
                </span>
                <ChevronDown size={16} className={`text-[#A0AEC0] transition-transform duration-200 ${isOpen ? 'rotate-180' : ''}`} />
            </div>
            
            {isOpen && (
                <div 
                    onClick={(e) => e.stopPropagation()}
                    className="absolute z-50 top-full left-0 right-0 mt-2 bg-white border border-[#E2E8F0] rounded-xl shadow-xl overflow-hidden animate-in fade-in slide-in-from-top-2 flex flex-col"
                >
                    <div className="p-2 border-b border-[#E2E8F0] bg-[#f8fafc]">
                        <div className="relative">
                            <input 
                                type="text"
                                autoFocus
                                placeholder="Ders ara..."
                                value={search}
                                onChange={(e) => setSearch(e.target.value)}
                                className="w-full pl-9 pr-3 py-2 text-sm bg-white border border-[#E2E8F0] rounded-lg focus:outline-none focus:border-blue-400 focus:ring-2 focus:ring-blue-100 transition-all text-[#0A1931]"
                            />
                            <div className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]">
                                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
                            </div>
                        </div>
                    </div>
                    <div className="max-h-[300px] overflow-y-auto py-1 custom-scrollbar">
                        {filteredCourses.map(c => (
                            <div 
                                key={c.id} 
                                onClick={() => { onChange(c.id); setIsOpen(false); }}
                                className={`px-4 py-2.5 text-sm cursor-pointer transition-colors flex items-center justify-between
                                    ${c.id === value ? 'bg-blue-50/50 text-blue-700 font-bold' : 'text-[#0A1931] hover:bg-[#f8fafc]'}`}
                            >
                                <span className="truncate">{c.title}</span>
                                {c.id === value && <Check size={16} className="text-blue-600 shrink-0 ml-3" />}
                            </div>
                        ))}
                        {filteredCourses.length === 0 && (
                            <div className="px-4 py-6 text-sm text-[#A0AEC0] text-center flex flex-col items-center gap-2">
                                <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="opacity-20"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
                                <span>Aramanıza uygun ders bulunamadı.</span>
                            </div>
                        )}
                    </div>
                </div>
            )}
        </div>
    );
}

export default function CourseAttendancePage() {
    const { token, currentTenantId: tenantId } = useAuth();
    const [courses, setCourses] = useState<CourseSummary[]>([]);
    const [selectedCourseId, setSelectedCourseId] = useState("");
    const [report, setReport] = useState<CourseAttendanceDto | null>(null);
    const [loadingCourses, setLoadingCourses] = useState(true);
    const [loadingReport, setLoadingReport] = useState(false);
    const [expandedStudent, setExpandedStudent] = useState<string | null>(null);
    const [currentPage, setCurrentPage] = useState(1);
    const [pageSize, setPageSize] = useState(12);

    useEffect(() => {
        setCurrentPage(1);
    }, [selectedCourseId]);

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
        setExpandedStudent(null);
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

    const paginatedStudents = useMemo(() => {
        const start = (currentPage - 1) * pageSize;
        return students.slice(start, start + pageSize);
    }, [students, currentPage, pageSize]);

    const totalPages = Math.ceil(students.length / pageSize) || 1;
    const pageNumbers = useMemo(() => {
        const pages: number[] = [];
        const maxVisible = 5;
        let start = Math.max(1, currentPage - Math.floor(maxVisible / 2));
        let end = Math.min(totalPages, start + maxVisible - 1);
        
        if (end - start + 1 < maxVisible) {
            start = Math.max(1, end - maxVisible + 1);
        }
        
        for (let i = start; i <= end; i++) {
            pages.push(i);
        }
        return pages;
    }, [currentPage, totalPages]);

    const riskStudents = students.filter(s => s.rate < 50);

    // CSV export
    const exportCSV = useCallback(() => {
        if (!report || students.length === 0) return;
        const headers = ["Öğrenci", ...report.sessions.map(s => s.sessionTitle), "Devam Oranı"];
        const rows = students.map(s => [
            s.name,
            ...report.sessions.map(sess => s.sessions.get(sess.sessionId) ? "Evet" : "Hayır"),
            `${s.rate.toFixed(0)}%`
        ]);
        const csv = [headers, ...rows].map(r => r.join(",")).join("\n");
        const blob = new Blob(["\uFEFF" + csv], { type: "text/csv;charset=utf-8;" });
        const a = document.createElement("a"); a.href = URL.createObjectURL(blob);
        a.download = `devam_raporu_${report.courseTitle}.csv`; a.click();
    }, [report, students]);

    const toggleStudent = (id: string) => {
        setExpandedStudent(prev => prev === id ? null : id);
    };

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
            <div className="bg-[#f8fafc] rounded-xl border border-[#E2E8F0]/60 p-4 flex flex-col sm:flex-row sm:items-center gap-3">
                <div className="flex items-center gap-2 text-[#1B3B6F] shrink-0">
                    <div className="w-8 h-8 rounded-lg bg-white border border-[#E2E8F0] flex items-center justify-center shadow-sm">
                        <Users size={16} className="text-blue-500" />
                    </div>
                    <label className="text-sm font-semibold whitespace-nowrap">Analiz Edilecek Ders:</label>
                </div>
                
                <CustomCourseSelect 
                    courses={courses} 
                    value={selectedCourseId} 
                    onChange={setSelectedCourseId} 
                    disabled={loadingCourses} 
                />
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
                        {/* Accordion List */}
                        <div className="col-span-1 lg:col-span-7 bg-white rounded-xl border border-[#E2E8F0]/60 overflow-hidden">
                            <div className="px-5 py-4 border-b border-[#E2E8F0]/60 flex items-center justify-between bg-[#f8fafc]">
                                <h3 className="text-sm font-semibold text-[#0A1931] flex items-center gap-2">
                                    <Users size={16} className="text-[#1B3B6F]" />
                                    Öğrenci Devam Detayları
                                </h3>
                                <span className="text-xs font-medium text-[#A0AEC0] bg-[#E2E8F0]/40 px-2 py-1 rounded-md">
                                    {students.length} Öğrenci
                                </span>
                            </div>
                            
                            {students.length === 0 ? (
                                <div className="py-16 text-center text-[#A0AEC0]">
                                    <Users size={32} className="mx-auto opacity-20 mb-2" />
                                    <p className="text-sm">Devam verisi bulunamadı</p>
                                </div>
                            ) : (
                                <>
                                    <div className="divide-y divide-[#E2E8F0]/40">
                                        {paginatedStudents.map((st) => {
                                            const isExpanded = expandedStudent === st.id;
                                            const isRisk = st.rate < 50;
                                            
                                            return (
                                                <div key={st.id} className="transition-colors hover:bg-[#f8fafc]/50">
                                                    {/* Accordion Header */}
                                                    <div 
                                                        className={`cursor-pointer px-5 py-4 flex flex-col sm:flex-row sm:items-center justify-between gap-4 ${isExpanded ? "bg-blue-50/30" : ""}`}
                                                        onClick={() => toggleStudent(st.id)}
                                                    >
                                                        <div className="flex items-center gap-3">
                                                            <div className={`w-8 h-8 rounded-full flex items-center justify-center font-bold text-xs ${isRisk ? "bg-red-100 text-red-600" : "bg-blue-100 text-blue-600"}`}>
                                                                {st.name.charAt(0).toUpperCase()}
                                                            </div>
                                                            <div>
                                                                <div className="flex items-center gap-2">
                                                                    <span className={`text-sm font-bold ${isRisk ? "text-red-600" : "text-[#0A1931]"}`}>
                                                                        {st.name}
                                                                    </span>
                                                                    {isRisk && <AlertTriangle size={14} className="text-red-500" />}
                                                                </div>
                                                                <div className="text-xs text-[#A0AEC0] flex items-center gap-2 mt-0.5">
                                                                    <span className="flex items-center gap-1">
                                                                        <CheckCircle2 size={12} className="text-emerald-500" /> {st.present} Katılım
                                                                    </span>
                                                                    <span>•</span>
                                                                    <span className="flex items-center gap-1">
                                                                        <XCircle size={12} className="text-red-500" /> {st.total - st.present} Devamsız
                                                                    </span>
                                                                </div>
                                                            </div>
                                                        </div>

                                                        <div className="flex items-center gap-4 sm:gap-6 justify-between sm:justify-end">
                                                            <div className="flex flex-col items-end w-32">
                                                                <div className="flex justify-between w-full mb-1">
                                                                    <span className="text-[10px] font-medium text-[#A0AEC0]">Devam Oranı</span>
                                                                    <span className={`text-xs font-bold ${rateColor(st.rate)}`}>{st.rate.toFixed(0)}%</span>
                                                                </div>
                                                                <div className="w-full h-1.5 bg-[#E2E8F0]/40 rounded-full overflow-hidden">
                                                                    <div 
                                                                        className={`h-full rounded-full transition-all duration-500 ${rateBg(st.rate)}`}
                                                                        style={{ width: `${st.rate}%` }}
                                                                    />
                                                                </div>
                                                            </div>
                                                            <button className="text-[#A0AEC0] hover:text-[#0A1931] p-1">
                                                                {isExpanded ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
                                                            </button>
                                                        </div>
                                                    </div>

                                                    {/* Accordion Details */}
                                                    {isExpanded && (
                                                        <div className="px-5 py-4 bg-[#f8fafc] border-t border-[#E2E8F0]/40">
                                                            <h4 className="text-xs font-semibold text-[#1B3B6F] mb-3 flex items-center gap-1.5">
                                                                <Calendar size={12} /> Oturum Katılım Detayları
                                                            </h4>
                                                            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-2 xl:grid-cols-3 gap-2">
                                                                {report.sessions.map((sess) => {
                                                                    const v = st.sessions.get(sess.sessionId);
                                                                    const attended = v === true;
                                                                    
                                                                    return (
                                                                        <div 
                                                                            key={sess.sessionId} 
                                                                            className={`flex items-start gap-2 p-2.5 rounded-lg border ${attended ? "bg-emerald-50/50 border-emerald-100" : "bg-red-50/50 border-red-100"}`}
                                                                        >
                                                                            <div className={`mt-0.5 shrink-0 ${attended ? "text-emerald-500" : "text-red-500"}`}>
                                                                                {attended ? <CheckCircle2 size={14} /> : <XCircle size={14} />}
                                                                            </div>
                                                                            <div className="min-w-0">
                                                                                <p className={`text-xs font-medium truncate ${attended ? "text-emerald-800" : "text-red-800"}`} title={sess.sessionTitle}>
                                                                                    {sess.sessionTitle}
                                                                                </p>
                                                                                <p className={`text-[10px] mt-0.5 ${attended ? "text-emerald-600/70" : "text-red-600/70"}`}>
                                                                                    {formatSessionDate(sess.scheduledStart)}
                                                                                </p>
                                                                            </div>
                                                                        </div>
                                                                    );
                                                                })}
                                                            </div>
                                                        </div>
                                                    )}
                                                </div>
                                            );
                                        })}
                                    </div>

                                    {/* Pagination Footer */}
                                    <div className="px-5 py-4 border-t border-[#E2E8F0]/60 flex flex-col sm:flex-row items-center justify-between gap-4 bg-[#f8fafc]/50">
                                        <div className="flex items-center gap-2 text-xs font-semibold text-[#1B3B6F]">
                                            <span className="text-[#A0AEC0]">Sayfa Başına Gösterim:</span>
                                            <select 
                                                value={pageSize} 
                                                onChange={(e) => {
                                                    setPageSize(Number(e.target.value));
                                                    setCurrentPage(1);
                                                }}
                                                className="bg-white border border-[#E2E8F0] rounded-lg px-2 py-1 focus:outline-none focus:border-blue-400 text-[#0A1931] cursor-pointer"
                                            >
                                                <option value={12}>12</option>
                                                <option value={24}>24</option>
                                                <option value={36}>36</option>
                                                <option value={48}>48</option>
                                            </select>
                                        </div>
                                        
                                        <div className="text-xs text-[#A0AEC0] font-medium">
                                            {students.length > 0 ? (
                                                <span>{(currentPage - 1) * pageSize + 1} - {Math.min(currentPage * pageSize, students.length)} / {students.length} Öğrenci</span>
                                            ) : (
                                                <span>0 - 0 / 0 Öğrenci</span>
                                            )}
                                        </div>
                                        
                                        <div className="flex items-center gap-1">
                                            <button
                                                disabled={currentPage === 1}
                                                onClick={() => setCurrentPage(prev => Math.max(prev - 1, 1))}
                                                className="px-2.5 py-1.5 text-xs font-bold rounded-lg border border-[#E2E8F0] bg-white text-[#1B3B6F] hover:bg-slate-50 active:scale-95 disabled:opacity-50 disabled:pointer-events-none transition-all"
                                            >
                                                Önceki
                                            </button>
                                            
                                            {pageNumbers.map(page => (
                                                <button
                                                    key={page}
                                                    onClick={() => setCurrentPage(page)}
                                                    className={`w-7 h-7 flex items-center justify-center text-xs font-bold rounded-lg transition-all active:scale-95 ${
                                                        currentPage === page
                                                            ? "bg-[#1B3B6F] text-white shadow-sm"
                                                            : "border border-[#E2E8F0] bg-white text-[#1B3B6F] hover:bg-slate-50"
                                                    }`}
                                                >
                                                    {page}
                                                </button>
                                            ))}
                                            
                                            <button
                                                disabled={currentPage === totalPages}
                                                onClick={() => setCurrentPage(prev => Math.min(prev + 1, totalPages))}
                                                className="px-2.5 py-1.5 text-xs font-bold rounded-lg border border-[#E2E8F0] bg-white text-[#1B3B6F] hover:bg-slate-50 active:scale-95 disabled:opacity-50 disabled:pointer-events-none transition-all"
                                            >
                                                Sonraki
                                            </button>
                                        </div>
                                    </div>
                                </>
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
                                    <div className="space-y-2 max-h-[300px] overflow-y-auto pr-1">
                                        {riskStudents.map(s => (
                                            <div key={s.id} className="flex items-center justify-between p-2.5 bg-white rounded-lg border border-red-200 shadow-sm">
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

                            {/* Session Trend List */}
                            <div className="bg-white rounded-xl border border-[#E2E8F0]/60 p-4">
                                <h3 className="text-sm font-semibold text-[#0A1931] mb-3 flex items-center gap-1.5">
                                    <BarChart3 size={14} className="text-blue-500" /> Oturum Katılım Oranları
                                </h3>
                                <div className="space-y-3 max-h-[400px] overflow-y-auto pr-2">
                                    {report.sessions.map(s => (
                                        <div key={s.sessionId}>
                                            <div className="flex items-center justify-between mb-1">
                                                <span 
                                                    className="text-xs text-[#0A1931] font-medium truncate pr-2 flex-1" 
                                                    title={s.sessionTitle}
                                                >
                                                    {s.sessionTitle}
                                                </span>
                                                <span className={`text-xs font-bold shrink-0 ${rateColor(s.attendanceRate)}`}>
                                                    {s.attendanceRate.toFixed(0)}%
                                                </span>
                                            </div>
                                            <div className="flex items-center gap-2">
                                                <div className="flex-1 h-1.5 bg-[#E2E8F0]/40 rounded-full overflow-hidden">
                                                    <div className={`h-full rounded-full transition-all duration-700 ${rateBg(s.attendanceRate)}`}
                                                        style={{ width: `${s.attendanceRate}%` }} />
                                                </div>
                                                <span className="text-[10px] text-[#A0AEC0] shrink-0 w-8 text-right">
                                                    {s.presentCount}/{s.totalEnrolled}
                                                </span>
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
