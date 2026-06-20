"use client";

import { useState, useEffect, useMemo } from "react";
import {
    PiMagnifyingGlassDuotone as Search,
    PiUsersDuotone as Users,
    PiChartBarDuotone as BarChart3,
    PiTrendUpDuotone as TrendingUp,
    PiCaretDownBold as ChevronDown,
    PiCaretUpBold as ChevronUp,
    PiDownloadDuotone as Download,
    PiXBold as X,
    PiMedalDuotone as Award,
    PiTargetDuotone as Target,
    PiBookOpenTextDuotone as BookOpen,
    PiClockDuotone as Clock,
    PiEyeDuotone as Eye,
    PiArrowsClockwiseDuotone as RefreshCw,
    PiFileTextDuotone as FileText
} from "react-icons/pi";
import { useAuth } from "@/contexts/AuthContext";
import { examApi, type ExamListDto, type ExamResultSummaryDto, type ExamResultDto } from "@/lib/api";
import { Tooltip } from "@/components/ui/Tooltip";

function scoreColor(score: number) {
    if (score >= 80) return "text-emerald-600";
    if (score >= 60) return "text-blue-600";
    if (score >= 40) return "text-amber-600";
    return "text-red-600";
}
function scoreBg(score: number) {
    if (score >= 80) return "bg-emerald-500";
    if (score >= 60) return "bg-blue-500";
    if (score >= 40) return "bg-amber-500";
    return "bg-red-500";
}
function scoreBadge(score: number) {
    if (score >= 80) return { label: "Başarılı", cls: "bg-emerald-50 text-emerald-700 border-emerald-200" };
    if (score >= 60) return { label: "Orta", cls: "bg-blue-50 text-blue-700 border-blue-200" };
    if (score >= 40) return { label: "Geçer", cls: "bg-amber-50 text-amber-700 border-amber-200" };
    return { label: "Başarısız", cls: "bg-red-50 text-red-600 border-red-200" };
}

// Score distribution histogram
function ScoreDistribution({ results, avgScore }: { results: ExamResultDto[]; avgScore: number }) {
    const ranges = ["0-20", "20-40", "40-60", "60-80", "80-100"];
    const buckets = ranges.map((range, i) => {
        const lo = i * 20, hi = (i + 1) * 20;
        return { range, count: results.filter(r => r.score >= lo && (i === 4 ? r.score <= hi : r.score < hi)).length };
    });
    const maxCount = Math.max(...buckets.map(b => b.count), 1);
    const colors = ["#ef4444", "#f59e0b", "#eab308", "#3b82f6", "#10b981"];

    return (
        <div className="space-y-1.5">
            {buckets.map((b, i) => (
                <div key={b.range} className="flex items-center gap-2">
                    <span className="text-[10px] text-[#A0AEC0] font-bold w-10 text-right">{b.range}</span>
                    <div className="flex-1 h-5 bg-[#E2E8F0]/20 rounded-lg overflow-hidden relative">
                        <div className="h-full rounded-lg transition-all duration-700"
                            style={{ width: `${(b.count / maxCount) * 100}%`, background: colors[i] }} />
                    </div>
                    <span className="text-[10px] font-bold text-[#0A1931] w-5">{b.count}</span>
                </div>
            ))}
        </div>
    );
}

// CSV Export
function exportCSV(examTitle: string, results: ExamResultDto[]) {
    const header = "Sıra,Öğrenci,Doğru,Yanlış,Boş,Net,Puan\n";
    const rows = results.map((r, i) =>
        `${i + 1},"${r.userFullName}",${r.correctCount},${r.wrongCount},${r.emptyCount},${r.net.toFixed(2)},${r.score.toFixed(1)}`
    ).join("\n");
    const blob = new Blob(["\uFEFF" + header + rows], { type: "text/csv;charset=utf-8;" });
    const a = document.createElement("a"); a.href = URL.createObjectURL(blob);
    a.download = `sinav_${examTitle.replace(/\s+/g, "_")}_${new Date().toISOString().slice(0, 10)}.csv`; a.click();
}

// Student Paper Modal
function StudentPaperModal({ result, answerKey, onClose }: { result: ExamResultDto; answerKey: Record<number, string>; onClose: () => void }) {
    return (
        <div className="fixed inset-0 z-[110] flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm" onClick={onClose}>
            <div className="bg-white rounded-2xl shadow-xl w-full max-w-2xl max-h-[90vh] overflow-hidden flex flex-col" onClick={e => e.stopPropagation()}>
                <div className="px-6 py-4 border-b border-[#E2E8F0] flex items-center justify-between bg-gray-50">
                    <div>
                        <h3 className="font-bold text-[#0A1931]">{result.userFullName} - Sınav Kağıdı</h3>
                        <p className="text-xs text-[#A0AEC0] mt-0.5">Puan: {result.score.toFixed(1)} · Net: {result.net.toFixed(2)}</p>
                    </div>
                    <button onClick={onClose} className="p-2 rounded-xl bg-white text-[#A0AEC0] hover:text-[#0A1931] shadow-sm"><X size={16} /></button>
                </div>
                <div className="p-6 overflow-y-auto">
                    <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
                        {Object.entries(answerKey).map(([qStr, correctAns]) => {
                            const qNum = parseInt(qStr);
                            const studentAns = result.answers?.[qNum];
                            const isCorrect = studentAns === correctAns;
                            const isEmpty = !studentAns;
                            return (
                                <div key={qNum} className={`p-3 rounded-xl border flex flex-col items-center justify-center gap-1 ${isCorrect ? 'bg-emerald-50 border-emerald-100' : isEmpty ? 'bg-gray-50 border-gray-200' : 'bg-red-50 border-red-100'}`}>
                                    <span className="text-[10px] font-bold text-[#A0AEC0]">Soru {qNum}</span>
                                    <div className="flex items-center gap-2 text-sm font-bold">
                                        <span className={isCorrect ? 'text-emerald-600' : isEmpty ? 'text-gray-400' : 'text-red-500'}>{studentAns || "Boş"}</span>
                                        {!isCorrect && <span className="text-emerald-600 text-xs opacity-60">({correctAns})</span>}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            </div>
        </div>
    );
}

// Question Analysis Tab
function QuestionAnalysis({ exam, summary }: { exam: ExamListDto; summary: ExamResultSummaryDto }) {
    if (!summary.answerKey) {
        return <div className="p-12 text-center text-[#A0AEC0]"><FileText size={40} className="mx-auto mb-3 opacity-20" /><p className="text-sm">Cevap anahtarı tanımlanmadığı için soru analizi yapılamıyor.</p></div>;
    }

    const questionCount = exam.questionCount;
    // Calculate stats per question
    const stats = Array.from({ length: questionCount }, (_, i) => {
        const qNum = i + 1;
        const correctAns = summary.answerKey![qNum];
        let correct = 0, wrong = 0, empty = 0;
        const distribution: Record<string, number> = {};
        
        summary.results.forEach(r => {
            const ans = r.answers?.[qNum];
            if (!ans) empty++;
            else {
                distribution[ans] = (distribution[ans] || 0) + 1;
                if (ans === correctAns) correct++;
                else wrong++;
            }
        });

        return { qNum, correctAns, correct, wrong, empty, distribution };
    });

    return (
        <div className="p-6 space-y-4">
            <h3 className="text-sm font-bold text-[#0A1931]">Soru Analizi ({questionCount} Soru)</h3>
            <div className="rounded-xl border border-[#E2E8F0] overflow-x-auto">
                <table className="w-full text-xs text-left min-w-[500px]">
                    <thead className="bg-[#E2E8F0]/20 border-b border-[#E2E8F0] text-[10px] text-[#A0AEC0] uppercase tracking-wider">
                        <tr>
                            <th className="px-4 py-3 font-bold">Soru</th>
                            <th className="px-4 py-3 font-bold">Doğru C.</th>
                            <th className="px-4 py-3 font-bold text-emerald-600">Doğru</th>
                            <th className="px-4 py-3 font-bold text-red-500">Yanlış</th>
                            <th className="px-4 py-3 font-bold text-[#A0AEC0]">Boş</th>
                            <th className="px-4 py-3 font-bold text-[#1B3B6F]">Başarı Oranı</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-[#E2E8F0]/40">
                        {stats.map(s => {
                            const successRate = summary.results.length > 0 ? Math.round((s.correct / summary.results.length) * 100) : 0;
                            return (
                                <tr key={s.qNum} className="hover:bg-[#E2E8F0]/10">
                                    <td className="px-4 py-3 font-bold text-[#0A1931]">Soru {s.qNum}</td>
                                    <td className="px-4 py-3 font-bold">{s.correctAns || "—"}</td>
                                    <td className="px-4 py-3 text-emerald-600 font-medium">{s.correct}</td>
                                    <td className="px-4 py-3 text-red-500 font-medium">{s.wrong}</td>
                                    <td className="px-4 py-3 text-[#A0AEC0] font-medium">{s.empty}</td>
                                    <td className="px-4 py-3">
                                        <div className="flex items-center gap-2">
                                            <div className="flex-1 h-1.5 bg-[#E2E8F0] rounded-full overflow-hidden">
                                                <div className={`h-full rounded-full ${successRate >= 50 ? "bg-emerald-500" : "bg-red-500"}`} style={{ width: `${successRate}%` }} />
                                            </div>
                                            <span className={`font-bold w-8 text-right ${successRate >= 50 ? "text-emerald-600" : "text-red-500"}`}>{successRate}%</span>
                                        </div>
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>
        </div>
    );
}

// ─── Exam Detail Panel ─────────────────────────────────────────────────────
function ExamResultPanel({ exam, onClose }: { exam: ExamListDto; onClose: () => void }) {
    const { token, currentTenantId: tenantId } = useAuth();
    const [summary, setSummary] = useState<ExamResultSummaryDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [sortBy, setSortBy] = useState<"score" | "net">("score");
    const [sortAsc, setSortAsc] = useState(false);
    const [activeTab, setActiveTab] = useState<"results" | "analysis">("results");
    const [selectedStudent, setSelectedStudent] = useState<ExamResultDto | null>(null);

    useEffect(() => {
        if (!token || !tenantId) return;
        examApi.getResults(token, tenantId, exam.id)
            .then(setSummary).catch(console.error).finally(() => setLoading(false));
    }, [token, tenantId, exam.id]);

    const sorted = summary ? [...summary.results].sort((a, b) =>
        sortAsc ? a[sortBy] - b[sortBy] : b[sortBy] - a[sortBy]) : [];

    const toggleSort = (col: "score" | "net") => {
        if (sortBy === col) setSortAsc(!sortAsc);
        else { setSortBy(col); setSortAsc(false); }
    };

    return (
        <div className="fixed inset-0 z-[100] flex" onClick={onClose}>
            <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" />
            <div className="ml-auto relative w-full max-w-[620px] bg-white shadow-2xl border-l border-[#E2E8F0] overflow-y-auto h-full" onClick={e => e.stopPropagation()}>
                {/* Header */}
                <div className="sticky top-0 bg-white z-10 px-6 py-4 border-b border-[#E2E8F0]">
                    <div className="flex items-center justify-between">
                        <div className="min-w-0">
                            <h2 className="font-bold text-[#0A1931] truncate text-lg">{exam.title}</h2>
                            <p className="text-xs text-[#A0AEC0] mt-0.5">{exam.examType} · {exam.questionCount} Soru · {exam.durationMinutes ? `${exam.durationMinutes}dk` : "Süresiz"}</p>
                        </div>
                        <div className="flex items-center gap-2 ml-4">
                            {summary && summary.results.length > 0 && (
                                <button onClick={() => exportCSV(exam.title, sorted)} className="p-2 rounded-xl bg-[#E2E8F0]/20 text-[#1B3B6F] hover:bg-[#E2E8F0]/40">
                                    <Download size={14} />
                                </button>
                            )}
                            <button onClick={onClose} className="p-2 rounded-xl bg-[#E2E8F0]/20 text-[#A0AEC0] hover:text-[#0A1931]"><X size={16} /></button>
                        </div>
                    </div>
                    {/* Tab bar */}
                    {!loading && summary && summary.totalParticipants > 0 && (
                        <div className="flex mt-3 bg-[#E2E8F0]/30 rounded-xl p-0.5">
                            <button onClick={() => setActiveTab("results")} className={`flex-1 py-1.5 rounded-lg text-xs font-bold transition-all ${activeTab === "results" ? "bg-white text-[#0A1931] shadow-sm" : "text-[#A0AEC0]"}`}>Öğrenci Sonuçları ({summary.results.length})</button>
                            <button onClick={() => setActiveTab("analysis")} className={`flex-1 py-1.5 rounded-lg text-xs font-bold transition-all ${activeTab === "analysis" ? "bg-white text-[#0A1931] shadow-sm" : "text-[#A0AEC0]"}`}>Soru Analizi</button>
                        </div>
                    )}
                </div>

                {loading ? (
                    <div className="p-6 space-y-3">{[...Array(4)].map((_, i) => <div key={i} className="h-20 rounded-xl bg-[#E2E8F0]/40 animate-pulse" />)}</div>
                ) : !summary || summary.totalParticipants === 0 ? (
                    <div className="p-12 text-center"><BarChart3 size={40} className="text-[#A9A9A9] mx-auto mb-3" /><p className="text-[#A0AEC0] text-sm">Henüz sonuç yok</p></div>
                ) : activeTab === "analysis" ? (
                    <QuestionAnalysis exam={exam} summary={summary} />
                ) : (
                    <>
                        {/* KPIs */}
                        <div className="bg-[#E2E8F0]/10 p-5 border-b border-[#E2E8F0]">
                            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-4">
                                {[
                                    { label: "Katılımcı", value: String(summary.totalParticipants), icon: Users, color: "text-[#1B3B6F]", bg: "bg-[#1B3B6F]/5" },
                                    { label: "Ortalama", value: summary.averageScore.toFixed(1), icon: Target, color: scoreColor(summary.averageScore), bg: summary.averageScore >= 60 ? "bg-emerald-50" : "bg-amber-50" },
                                    { label: "En Yüksek", value: summary.highestScore.toFixed(1), icon: Award, color: "text-emerald-600", bg: "bg-emerald-50" },
                                    { label: "En Düşük", value: summary.lowestScore.toFixed(1), icon: TrendingUp, color: "text-red-500", bg: "bg-red-50" },
                                ].map(s => (
                                    <div key={s.label} className={`${s.bg} rounded-xl border border-[#E2E8F0]/40 p-3 text-center`}>
                                        <s.icon size={14} className={`${s.color} mx-auto mb-1`} />
                                        <p className={`text-lg font-bold ${s.color}`}>{s.value}</p>
                                        <p className="text-[9px] text-[#A0AEC0] font-medium">{s.label}</p>
                                    </div>
                                ))}
                            </div>

                            {/* Score Distribution */}
                            <h4 className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-2">Puan Dağılımı</h4>
                            <ScoreDistribution results={summary.results} avgScore={summary.averageScore} />

                            {/* Score Range Bar */}
                            <div className="mt-4">
                                <div className="relative h-3 bg-[#E2E8F0]/40 rounded-full overflow-hidden">
                                    <div className="absolute h-full rounded-full opacity-60 bg-gradient-to-r from-red-400 via-amber-400 to-emerald-400"
                                        style={{ left: `${summary.lowestScore}%`, width: `${summary.highestScore - summary.lowestScore}%` }} />
                                    <div className="absolute top-1/2 -translate-y-1/2 w-3 h-3 bg-white border-2 border-[#1B3B6F] rounded-full shadow-lg"
                                        style={{ left: `${summary.averageScore}%`, transform: "translateX(-50%) translateY(-50%)" }} />
                                </div>
                                <div className="flex justify-between mt-1">
                                    <span className="text-[9px] text-red-500 font-bold">{summary.lowestScore.toFixed(0)}</span>
                                    <span className="text-[9px] text-[#1B3B6F] font-bold">{summary.averageScore.toFixed(0)} ort.</span>
                                    <span className="text-[9px] text-emerald-600 font-bold">{summary.highestScore.toFixed(0)}</span>
                                </div>
                            </div>
                        </div>

                        {/* Results Table */}
                        <div className="p-6">
                            <h3 className="text-sm font-bold text-[#0A1931] mb-3">Öğrenci Sonuçları ({summary.results.length})</h3>
                            <div className="rounded-xl border border-[#E2E8F0] overflow-x-auto">
                                <table className="w-full text-xs min-w-[500px]">
                                    <thead className="bg-[#E2E8F0]/15 border-b border-[#E2E8F0]">
                                        <tr>
                                            <th className="text-left px-3 py-2.5 text-[10px] font-bold text-[#A9A9A9] uppercase">#</th>
                                            <th className="text-left px-3 py-2.5 text-[10px] font-bold text-[#A9A9A9] uppercase">Öğrenci</th>
                                            <th className="text-center px-2 py-2.5 text-[10px] font-bold text-emerald-600 uppercase">D</th>
                                            <th className="text-center px-2 py-2.5 text-[10px] font-bold text-red-500 uppercase">Y</th>
                                            <th className="text-center px-2 py-2.5 text-[10px] font-bold text-[#A9A9A9] uppercase">B</th>
                                            <th className="text-center px-2 py-2.5 cursor-pointer" onClick={() => toggleSort("net")}>
                                                <span className="flex items-center gap-0.5 justify-center text-[10px] font-bold text-[#1B3B6F] uppercase">
                                                    Net {sortBy === "net" ? (sortAsc ? <ChevronUp size={9} /> : <ChevronDown size={9} />) : null}
                                                </span>
                                            </th>
                                            <th className="text-center px-2 py-2.5 cursor-pointer" onClick={() => toggleSort("score")}>
                                                <span className="flex items-center gap-0.5 justify-center text-[10px] font-bold text-[#1B3B6F] uppercase">
                                                    Puan {sortBy === "score" ? (sortAsc ? <ChevronUp size={9} /> : <ChevronDown size={9} />) : null}
                                                </span>
                                            </th>
                                            <th className="text-center px-2 py-2.5 text-[10px] font-bold text-[#A9A9A9] uppercase">Durum</th>
                                            <th className="text-center px-2 py-2.5 text-[10px] font-bold text-[#A9A9A9] uppercase">Kağıt</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {sorted.map((r, i) => {
                                            const badge = scoreBadge(r.score);
                                            return (
                                                <tr key={r.id} className={`border-b border-[#E2E8F0]/40 hover:bg-[#E2E8F0]/10 transition-colors ${i < 3 ? "bg-emerald-50/20" : ""}`}>
                                                    <td className="px-3 py-2.5">
                                                        {i === 0 ? <span className="text-amber-500">🥇</span> : i === 1 ? <span className="text-gray-400">🥈</span> : i === 2 ? <span className="text-amber-700">🥉</span> : <span className="text-[#A0AEC0]">{i + 1}</span>}
                                                    </td>
                                                    <td className="px-3 py-2.5 font-medium text-[#0A1931]">{r.userFullName}</td>
                                                    <td className="px-2 py-2.5 text-center text-emerald-600 font-bold">{r.correctCount}</td>
                                                    <td className="px-2 py-2.5 text-center text-red-500 font-bold">{r.wrongCount}</td>
                                                    <td className="px-2 py-2.5 text-center text-[#A0AEC0]">{r.emptyCount}</td>
                                                    <td className="px-2 py-2.5 text-center text-[#1B3B6F] font-bold">{r.net.toFixed(2)}</td>
                                                    <td className="px-2 py-2.5 text-center">
                                                        <span className={`font-bold ${scoreColor(r.score)}`}>{r.score.toFixed(1)}</span>
                                                    </td>
                                                    <td className="px-2 py-2.5 text-center">
                                                        <span className={`text-[8px] font-bold px-1.5 py-0.5 rounded-full border ${badge.cls}`}>{badge.label}</span>
                                                    </td>
                                                    <td className="px-2 py-2.5 text-center">
                                                        <Tooltip content="Sınav Kağıdı"><button onClick={() => setSelectedStudent(r)} className="p-1.5 rounded-lg bg-[#E2E8F0]/30 hover:bg-[#1B3B6F] hover:text-white transition-colors text-[#1B3B6F]"><FileText size={12} /></button></Tooltip>
                                                    </td>
                                                </tr>
                                            );
                                        })}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </>
                )}
            </div>
            {selectedStudent && summary?.answerKey && (
                <StudentPaperModal result={selectedStudent} answerKey={summary.answerKey} onClose={() => setSelectedStudent(null)} />
            )}
        </div>
    );
}

// ─── MAIN PAGE ────────────────────────────────────────────────────────────────
export default function ExamResultsPage() {
    const { token, currentTenantId: tenantId } = useAuth();
    const [exams, setExams] = useState<ExamListDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState("");
    const [typeFilter, setTypeFilter] = useState("");
    const [selected, setSelected] = useState<ExamListDto | null>(null);

    useEffect(() => {
        if (!token || !tenantId) return;
        examApi.list(token, tenantId, { pageSize: 100 }).then(r => setExams(r.items)).catch(console.error).finally(() => setLoading(false));
    }, [token, tenantId]);

    const withResults = exams.filter(e => e.resultCount > 0);
    const examTypes = [...new Set(withResults.map(e => e.examType))];

    const filtered = withResults.filter(e => {
        const ms = !search || e.title.toLowerCase().includes(search.toLowerCase());
        const mt = !typeFilter || e.examType === typeFilter;
        return ms && mt;
    });

    const overallAvg = withResults.length > 0
        ? withResults.reduce((s, e) => s + (e.averageScore ?? 0), 0) / withResults.length : 0;
    const totalParticipants = withResults.reduce((s, e) => s + e.resultCount, 0);

    return (
        <div className="space-y-5">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-[#0A1931] flex items-center gap-2">
                        <BarChart3 size={22} className="text-[#1B3B6F]" /> Sınav Sonuçları
                    </h1>
                    <p className="text-sm text-[#A9A9A9] mt-0.5">Performans analizi ve sonuç özeti</p>
                </div>
            </div>

            {/* KPI Cards */}
            <div className="flex lg:grid lg:grid-cols-4 gap-3 overflow-x-auto hide-scrollbar pb-2 snap-x">
                {[
                    { label: "Sonuçlu Sınav", value: String(withResults.length), icon: BookOpen, color: "text-[#1B3B6F]", bg: "bg-[#1B3B6F]/5" },
                    { label: "Toplam Katılımcı", value: String(totalParticipants), icon: Users, color: "text-violet-600", bg: "bg-violet-50" },
                    { label: "Genel Ortalama", value: overallAvg > 0 ? overallAvg.toFixed(1) : "—", icon: Target, color: scoreColor(overallAvg), bg: overallAvg >= 60 ? "bg-emerald-50" : "bg-amber-50" },
                    { label: "Sınav Tipleri", value: String(examTypes.length), icon: Award, color: "text-blue-600", bg: "bg-blue-50" },
                ].map(s => (
                    <div key={s.label} className={`min-w-[150px] lg:min-w-0 shrink-0 snap-start ${s.bg} rounded-xl border border-[#E2E8F0]/60 p-4 flex items-center gap-3`}>
                        <s.icon size={16} className={s.color} />
                        <div>
                            <p className={`text-xl font-bold ${s.color}`}>{s.value}</p>
                            <p className="text-[10px] text-[#A0AEC0] font-medium">{s.label}</p>
                        </div>
                    </div>
                ))}
            </div>

            {/* Search + Filter */}
            <div className="bg-white rounded-xl border border-[#E2E8F0]/60 p-3 flex flex-col sm:flex-row items-center gap-3">
                <div className="w-full sm:flex-1 relative">
                    <Search size={13} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                    <input type="text" placeholder="Sınav ara..." value={search} onChange={e => setSearch(e.target.value)}
                        className="w-full pl-8 pr-3 py-2 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10" />
                </div>
                <div className="w-full sm:w-auto flex items-center gap-3">
                    {examTypes.length > 1 && (
                        <select value={typeFilter} onChange={e => setTypeFilter(e.target.value)}
                            className="flex-1 sm:flex-none px-3 py-2 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none text-[#1B3B6F]">
                            <option value="">Tüm Tipler</option>
                            {examTypes.map(t => <option key={t} value={t}>{t}</option>)}
                        </select>
                    )}
                    <span className="text-xs text-[#A0AEC0] font-bold shrink-0">{filtered.length} sınav</span>
                </div>
            </div>

            {/* Exam Cards */}
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
                {loading ? [...Array(6)].map((_, i) => <div key={i} className="h-36 rounded-xl bg-[#E2E8F0]/40 animate-pulse" />) :
                    filtered.length === 0 ? (
                        <div className="col-span-3 py-16 text-center text-[#A0AEC0]">
                            <BarChart3 size={32} className="mx-auto mb-2 opacity-20" />
                            <p className="text-sm font-bold">Sonuçlu sınav bulunamadı</p>
                        </div>
                    ) : filtered.map(exam => {
                        const avg = exam.averageScore ?? 0;
                        const badge = scoreBadge(avg);
                        return (
                            <div key={exam.id} onClick={() => setSelected(exam)}
                                className="bg-white rounded-xl border border-[#E2E8F0]/60 p-5 hover:shadow-lg transition-all cursor-pointer group relative overflow-hidden">
                                <div className={`absolute top-0 right-0 w-24 h-24 rounded-full -mr-8 -mt-8 opacity-[0.06] ${scoreBg(avg)}`} />
                                <div className="flex items-start justify-between mb-3">
                                    <div className="min-w-0 flex-1">
                                        <h3 className="text-sm font-bold text-[#0A1931] truncate">{exam.title}</h3>
                                        <p className="text-[10px] text-[#A0AEC0] mt-0.5">
                                            {exam.examType} · {exam.questionCount} Soru
                                            {exam.durationMinutes && ` · ${exam.durationMinutes}dk`}
                                        </p>
                                    </div>
                                    <span className={`text-[9px] font-bold px-2 py-0.5 rounded-full border ml-2 flex-shrink-0 ${badge.cls}`}>{badge.label}</span>
                                </div>

                                <div className="flex items-end justify-between mb-3">
                                    <div>
                                        <p className={`text-2xl font-bold ${scoreColor(avg)}`}>{avg > 0 ? avg.toFixed(1) : "—"}</p>
                                        <p className="text-[9px] text-[#A0AEC0]">Ortalama Puan</p>
                                    </div>
                                    <div className="flex items-center gap-3">
                                        <div className="text-right">
                                            <p className="text-sm font-bold text-[#0A1931]">{exam.resultCount}</p>
                                            <p className="text-[9px] text-[#A0AEC0]">Katılımcı</p>
                                        </div>
                                    </div>
                                </div>

                                <div className="h-1.5 bg-[#E2E8F0]/40 rounded-full overflow-hidden">
                                    <div className={`h-full rounded-full transition-all duration-500 ${scoreBg(avg)}`}
                                        style={{ width: `${Math.min(100, avg)}%` }} />
                                </div>

                                <div className="mt-2 flex items-center justify-between">
                                    <span className="text-[9px] text-[#A0AEC0]">
                                        {new Date(exam.createdAt).toLocaleDateString("tr-TR", { day: "numeric", month: "short", year: "numeric" })}
                                    </span>
                                    <span className="text-[9px] font-bold text-[#1B3B6F] opacity-0 group-hover:opacity-100 transition-opacity flex items-center gap-0.5">
                                        <Eye size={9} /> Detay →
                                    </span>
                                </div>
                            </div>
                        );
                    })}
            </div>

            {selected && <ExamResultPanel exam={selected} onClose={() => setSelected(null)} />}
        </div>
    );
}
