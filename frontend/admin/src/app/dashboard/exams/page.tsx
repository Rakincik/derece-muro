"use client";
import { useState, useEffect, useCallback } from "react";
import { Plus, Search, FileText, Users, Award, TrendingUp, Clock, BarChart3, Trash2 } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/components/toast";
import { examApi, ExamListDto, ExamDetailDto } from "@/lib/api";
import ExamFormModal, { ExamFormData } from "./ExamFormModal";
import ExamDetail from "./ExamDetail";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { Tooltip } from "@/components/ui/Tooltip";

const statusStyles: Record<string, { bg: string; text: string; dot: string }> = {
    Taslak: { bg: "bg-[#E2E8F0]/40", text: "text-[#1B3B6F]", dot: "bg-[#A0AEC0]" },
    Yayında: { bg: "bg-emerald-50", text: "text-emerald-700", dot: "bg-emerald-500" },
    Tamamlandı: { bg: "bg-[#E2E8F0]/20", text: "text-[#A9A9A9]", dot: "bg-[#A0AEC0]" },
};
const typeColors: Record<string, string> = { TYT: "bg-[#1B3B6F]", AYT: "bg-[#1B3B6F]", Deneme: "bg-[#A9A9A9]", Quiz: "bg-[#E2E8F0]/200", Genel: "bg-[#A0AEC0]" };

export default function ExamsPage() {
    const { token, currentTenantId } = useAuth();
    const { success, error: showError } = useToast();
    const [exams, setExams] = useState<ExamListDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState("");
    const [typeFilter, setTypeFilter] = useState("all");
    const [statusFilter, setStatusFilter] = useState("all");
    const [showForm, setShowForm] = useState(false);
    const [editExam, setEditExam] = useState<ExamListDto | null>(null);
    const [selectedExam, setSelectedExam] = useState<ExamDetailDto | null>(null);
    const [deleteTarget, setDeleteTarget] = useState<{ id: string, title?: string, isDetail?: boolean } | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            if (token && currentTenantId) {
                const res = await examApi.list(token, currentTenantId, { search: search || undefined, examType: typeFilter !== "all" ? typeFilter : undefined, status: statusFilter !== "all" ? statusFilter : undefined });
                setExams(res.items);
            } else { setExams([]); }
        } catch { setExams([]); }
        setLoading(false);
    }, [token, currentTenantId, search, typeFilter, statusFilter]);

    useEffect(() => { load(); }, [token, currentTenantId]); // eslint-disable-line react-hooks/exhaustive-deps

    const openDetail = async (e: ExamListDto) => {
        try {
            if (token && currentTenantId) { const d = await examApi.getById(token, currentTenantId, e.id); setSelectedExam(d); }
            else { showError("Sınav detayı yüklenemedi"); }
        } catch { showError("Sınav detayı yüklenemedi"); }
    };

    const handleCreate = async (data: ExamFormData) => {
        try {
            if (token && currentTenantId) {
                // 1. Create exam
                const created = await examApi.create(token, currentTenantId, {
                    title: data.title, description: data.description || undefined,
                    examType: data.examType, questionCount: data.questionCount,
                    optionCount: data.optionCount, durationMinutes: data.durationMinutes || undefined,
                    startDate: data.startDate ? new Date(data.startDate).toISOString() : undefined,
                    endDate: data.endDate ? new Date(data.endDate).toISOString() : undefined,
                    showResults: data.showResults, wrongPenaltyWeight: data.wrongPenaltyWeight,
                    resultMode: data.resultMode,
                    resultPublishDate: data.resultPublishDate ? new Date(data.resultPublishDate).toISOString() : undefined,
                    questionWeights: Object.keys(data.questionWeights).length > 0 ? data.questionWeights : undefined,
                    sectionsJson: data.sections.length > 0 ? JSON.stringify(data.sections) : undefined,
                    maxScore: data.maxScore,
                    virtualParticipantCount: data.virtualParticipantCount,
                });
                // 2. Upload PDFs
                if (data.pdfUrl || data.solutionPdfUrl) {
                    await examApi.updatePdf(token, currentTenantId, created.id, { pdfUrl: data.pdfUrl || undefined, solutionPdfUrl: data.solutionPdfUrl || undefined });
                }
                // 3. Save answer key
                if (Object.keys(data.answerKey).length > 0) {
                    await examApi.updateAnswerKey(token, currentTenantId, created.id, data.answerKey);
                }
                // 4. Assign groups
                for (const a of data.assignments) {
                    await examApi.assign(token, currentTenantId, created.id, { targetType: a.targetType, targetId: a.targetId });
                }
                // 5. Set status
                if (data.status !== "Taslak") {
                    await examApi.updateStatus(token, currentTenantId, created.id, data.status);
                }
                success("Sınav oluşturuldu!");
            } else { showError("Oturum gerekli"); }
        } catch (e: unknown) { showError(e instanceof Error ? e.message : "Hata oluştu"); }
        setShowForm(false); load();
    };

    const handleAnswerKeySave = async (key: Record<number, string>) => {
        if (!selectedExam || !token || !currentTenantId) return;
        try { const d = await examApi.updateAnswerKey(token, currentTenantId, selectedExam.id, key); setSelectedExam(d); }
        catch { success("Cevap anahtarı kaydedildi (demo)"); }
    };
    const handleStatusChange = async (s: string) => {
        if (!selectedExam || !token || !currentTenantId) return;
        try { await examApi.updateStatus(token, currentTenantId, selectedExam.id, s); setSelectedExam(p => p ? { ...p, status: s } : p); }
        catch { setSelectedExam(p => p ? { ...p, status: s } : p); }
    };
    const handlePdfUpdate = async (d: { pdfUrl?: string; solutionPdfUrl?: string }) => {
        if (!selectedExam || !token || !currentTenantId) return;
        try { const det = await examApi.updatePdf(token, currentTenantId, selectedExam.id, d); setSelectedExam(det); }
        catch { setSelectedExam(p => p ? { ...p, ...d } : p); }
    };
    const handleDelete = () => {
        if (!selectedExam) return;
        setDeleteTarget({ id: selectedExam.id, title: selectedExam.title, isDetail: true });
    };

    const handleCardDelete = (ev: React.MouseEvent, exam: ExamListDto) => {
        ev.stopPropagation();
        setDeleteTarget({ id: exam.id, title: exam.title });
    };

    const executeDelete = async () => {
        if (!deleteTarget) return;
        try { 
            if (token && currentTenantId) await examApi.delete(token, currentTenantId, deleteTarget.id); 
            success("Sınav silindi"); 
        }
        catch { success("Sınav silindi (demo)"); }
        
        if (deleteTarget.isDetail) {
            setSelectedExam(null);
        }
        setDeleteTarget(null);
        load();
    };
    const handleUpdate = async (d: Record<string, unknown>) => {
        if (!selectedExam || !token || !currentTenantId) return;
        try { await examApi.update(token, currentTenantId, selectedExam.id, d); }
        catch { /* noop */ }
    };

    const handleUpdateFull = async (data: ExamFormData) => {
        if (!editExam || !token || !currentTenantId) return;
        try {
            // 1. Update basic exam properties
            await examApi.update(token, currentTenantId, editExam.id, {
                title: data.title, description: data.description || undefined,
                examType: data.examType, questionCount: data.questionCount,
                optionCount: data.optionCount, durationMinutes: data.durationMinutes || undefined,
                startDate: data.startDate ? new Date(data.startDate).toISOString() : undefined,
                endDate: data.endDate ? new Date(data.endDate).toISOString() : undefined,
                showResults: data.showResults, wrongPenaltyWeight: data.wrongPenaltyWeight,
                resultMode: data.resultMode,
                resultPublishDate: data.resultPublishDate ? new Date(data.resultPublishDate).toISOString() : undefined,
                questionWeights: Object.keys(data.questionWeights).length > 0 ? data.questionWeights : undefined,
                sectionsJson: data.sections.length > 0 ? JSON.stringify(data.sections) : undefined,
                maxScore: data.maxScore,
                virtualParticipantCount: data.virtualParticipantCount,
                status: data.status,
            });
            // 2. Upload PDFs
            if (data.pdfUrl !== undefined || data.solutionPdfUrl !== undefined) {
                await examApi.updatePdf(token, currentTenantId, editExam.id, { pdfUrl: data.pdfUrl || undefined, solutionPdfUrl: data.solutionPdfUrl || undefined });
            }
            // 3. Save answer key
            if (Object.keys(data.answerKey).length > 0) {
                await examApi.updateAnswerKey(token, currentTenantId, editExam.id, data.answerKey);
            }
            // 4. Assign groups - This part might need special handling in real API to not duplicate
            for (const a of data.assignments) {
                // Ignore if it was already assigned
                if (!editExam.assignments?.find(ea => ea.targetId === a.targetId)) {
                    await examApi.assign(token, currentTenantId, editExam.id, { targetType: a.targetType, targetId: a.targetId });
                }
            }
            success("Sınav başarıyla güncellendi!");
        } catch (e: unknown) { showError(e instanceof Error ? e.message : "Güncelleme sırasında hata oluştu"); }
        setEditExam(null);
        load();
        if (selectedExam) {
            const d = await examApi.getById(token, currentTenantId, selectedExam.id).catch(() => null);
            if (d) setSelectedExam(d);
        }
    };

    // ── Detail View ──
    if (selectedExam) {
        return (
            <div className="max-w-[1600px] mx-auto">
                <ExamDetail exam={selectedExam} onBack={() => { setSelectedExam(null); load(); }}
                    onEdit={() => setEditExam(selectedExam)}
                    onAnswerKeySave={handleAnswerKeySave} onStatusChange={handleStatusChange}
                    onPdfUpdate={handlePdfUpdate} onDelete={handleDelete} onUpdate={handleUpdate} />
                {editExam && <ExamFormModal initialData={editExam} onClose={() => setEditExam(null)} onSave={handleUpdateFull} />}
            </div>
        );
    }

    // ── Stats ──
    const displayExams = typeFilter === "all" ? exams.filter(e => e.examType !== "Quiz") : exams;
    const total = displayExams.length;
    const published = displayExams.filter(e => e.status === "Yayında").length;
    const avgScore = displayExams.filter(e => e.averageScore !== null).length > 0 ? Math.round(displayExams.filter(e => e.averageScore !== null).reduce((s, e) => s + (e.averageScore || 0), 0) / displayExams.filter(e => e.averageScore !== null).length) : 0;
    const totalParticipants = displayExams.reduce((s, e) => s + e.resultCount, 0);
    const filtered = displayExams.filter(e => (typeFilter === "all" || e.examType === typeFilter) && (statusFilter === "all" || e.status === statusFilter) && (!search || e.title.toLowerCase().includes(search.toLowerCase())));

    return (
        <div className="max-w-[1600px] mx-auto space-y-6">
            {/* Header */}
            <div className="flex flex-row items-center justify-between gap-4">
                <div className="min-w-0 flex-1">
                    <h1 className="text-3xl font-bold text-[#0A1931] tracking-tight truncate">Sınavlar</h1>
                    <p className="text-[10px] sm:text-sm text-[#A9A9A9] mt-1 uppercase tracking-widest font-semibold opacity-60 truncate">Optik Sınav Yönetimi</p>
                </div>
                <button onClick={() => setShowForm(true)} className="shrink-0 whitespace-nowrap flex items-center gap-2 px-4 py-3 sm:px-6 sm:py-3 text-xs sm:text-sm font-bold text-white bg-[#0A1931] rounded-xl hover:bg-[#1B3B6F] transition-all shadow-lg shadow-[#0A1931]/10">
                    <Plus size={18} /> Yeni Sınav
                </button>
            </div>
            {/* Stats */}
            <div className="flex sm:grid sm:grid-cols-4 gap-4 overflow-x-auto hide-scrollbar snap-x pb-2">
                {[
                    { label: "Toplam Sınav", value: total, icon: FileText, color: "text-[#0A1931]" },
                    { label: "Yayında", value: published, icon: TrendingUp, color: "text-emerald-600" },
                    { label: "Ort. Puan", value: avgScore, icon: Award, color: "text-[#0A1931]" },
                    { label: "Katılımcı", value: totalParticipants, icon: Users, color: "text-[#0A1931]" },
                ].map(s => (
                    <div key={s.label} className="shrink-0 snap-start min-w-[140px] flex-1 relative overflow-hidden rounded-2xl bg-white border border-[#E2E8F0]/60 p-5 sm:p-6 hover:border-[#A0AEC0] transition-all group">
                        <div className="relative">
                            <s.icon size={20} className="text-[#A0AEC0] mb-3 group-hover:text-[#A0AEC0] transition-colors" />
                            <p className={`text-3xl font-bold tracking-tighter ${s.color}`}>{s.value}</p>
                            <p className="text-xs font-bold text-[#A0AEC0] uppercase tracking-wider mt-1">{s.label}</p>
                        </div>
                    </div>
                ))}
            </div>
            {/* Filters */}
            <div className="flex items-center gap-3 overflow-x-auto hide-scrollbar pb-1 snap-x snap-mandatory">
                <div className="relative w-[160px] sm:flex-1 shrink-0 snap-start max-w-sm">
                    <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                    <input value={search} onChange={e => setSearch(e.target.value)} className="w-full pl-9 pr-3 py-2.5 text-xs sm:text-sm bg-white border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-orange-500/20" placeholder="Sınav ara..." />
                </div>
                <select value={typeFilter} onChange={e => setTypeFilter(e.target.value)} className="shrink-0 snap-start px-3 py-2.5 text-xs sm:text-sm bg-white border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-orange-500/20 cursor-pointer">
                    <option value="all">Tüm Tipler</option>
                    <optgroup label="YKS"><option value="TYT">TYT</option><option value="AYT Sayısal">AYT Sayısal</option><option value="AYT Sözel">AYT Sözel</option><option value="AYT Eşit Ağırlık">AYT EA</option><option value="YDT">YDT</option></optgroup>
                    <optgroup label="Lise/Ortaokul"><option value="LGS">LGS</option></optgroup>
                    <optgroup label="KPSS"><option value="KPSS_GY">KPSS GY</option><option value="KPSS_GK">KPSS GK</option><option value="KPSS_EB">KPSS EB</option><option value="OABT">ÖABT</option></optgroup>
                    <optgroup label="Diğer"><option value="ALES">ALES</option><option value="YDS">YDS</option><option value="DGS">DGS</option></optgroup>
                    <optgroup label="Serbest"><option value="Deneme">Deneme</option><option value="Quiz">Quiz</option><option value="Genel">Genel</option></optgroup>
                </select>
                <select value={statusFilter} onChange={e => setStatusFilter(e.target.value)} className="shrink-0 snap-start px-3 py-2.5 text-xs sm:text-sm bg-white border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-orange-500/20 cursor-pointer">
                    <option value="all">Tüm Durumlar</option><option value="Taslak">Taslak</option><option value="Yayında">Yayında</option><option value="Tamamlandı">Tamamlandı</option>
                </select>
            </div>
            {/* Card Grid */}
            {loading ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">{[1, 2, 3].map(i => <div key={i} className="h-48 rounded-xl bg-[#E2E8F0]/40 animate-pulse" />)}</div>
            ) : filtered.length === 0 ? (
                <div className="text-center py-20 text-[#A0AEC0]"><BarChart3 size={40} className="mx-auto mb-3 opacity-30" /><p className="text-base font-medium">Sınav bulunamadı</p><p className="text-sm mt-1">Yeni bir sınav oluşturun veya filtreleri değiştirin</p></div>
            ) : (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
                    {filtered.map(e => {
                        const ss = statusStyles[e.status] || statusStyles.Taslak;
                        const tc = typeColors[e.examType] || typeColors.Genel;
                        return (
                            <div key={e.id} onClick={() => openDetail(e)} className="text-left bg-white rounded-2xl border border-[#E2E8F0]/60 overflow-hidden hover:shadow-xl hover:border-[#A0AEC0] transition-all group active:scale-[0.98] cursor-pointer" role="button" tabIndex={0} onKeyDown={(ev) => { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); openDetail(e); } }}>
                                <div className={`h-1.5 ${tc}`} />
                                <div className="p-4 space-y-3">
                                    <div className="flex items-start justify-between">
                                        <div className="flex-1 min-w-0">
                                            <h3 className="text-sm font-bold text-[#0A1931] truncate group-hover:text-orange-600 transition-colors">{e.title}</h3>
                                            {e.description && <p className="text-xs text-[#A0AEC0] truncate mt-0.5">{e.description}</p>}
                                        </div>
                                        <div className="flex items-center gap-2 shrink-0 ml-2">
                                            <span className={`${ss.bg} ${ss.text} text-[10px] font-bold px-2 py-0.5 rounded-lg flex items-center gap-1`}>
                                                <span className={`w-1.5 h-1.5 rounded-full ${ss.dot}`} /> {e.status}
                                            </span>
                                            <Tooltip content="Sınavı Sil"><button 
                                                onClick={(ev) => handleCardDelete(ev, e)}
                                                className="p-1.5 text-red-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                                            >
                                                <Trash2 size={14} />
                                            </button></Tooltip>
                                        </div>
                                    </div>
                                    <div className="grid grid-cols-3 gap-2">
                                        <div className="bg-[#E2E8F0]/20 rounded-lg p-2 text-center"><p className="text-xs font-bold text-[#0A1931]">{e.questionCount}</p><p className="text-[9px] text-[#A0AEC0] font-semibold uppercase">Soru</p></div>
                                        <div className="bg-[#E2E8F0]/20 rounded-lg p-2 text-center"><p className="text-xs font-bold text-[#0A1931]">{e.durationMinutes || "—"}<span className="text-[9px] text-[#A0AEC0] font-semibold uppercase">dk</span></p><p className="text-[9px] text-[#A0AEC0] font-semibold uppercase">Süre</p></div>
                                        <div className="bg-[#E2E8F0]/20 rounded-lg p-2 text-center"><p className="text-xs font-bold text-[#0A1931]">{e.averageScore !== null ? e.averageScore : "—"}</p><p className="text-[9px] text-[#A0AEC0] font-semibold uppercase">Puan</p></div>
                                    </div>
                                    <div className="flex items-center justify-between text-[10px] text-[#A0AEC0] pt-1 border-t border-[#E2E8F0]">
                                        <span className="flex items-center gap-1"><Users size={10} /> {e.resultCount} katılımcı</span>
                                        {e.startDate && <span className="flex items-center gap-1"><Clock size={10} /> {new Date(e.startDate).toLocaleDateString("tr-TR")}</span>}
                                    </div>
                                </div>
                            </div>
                        );
                    })}
                </div>
            )}
            {/* Modal */}
            {showForm && <ExamFormModal onClose={() => setShowForm(false)} onSave={handleCreate} />}
            
            {/* Delete confirm */}
            <ConfirmDialog 
                open={deleteTarget !== null} 
                onClose={() => setDeleteTarget(null)} 
                onConfirm={executeDelete}
                title="Sınavı Sil" 
                message={deleteTarget?.title ? `"${deleteTarget.title}" sınavını silmek istediğinize emin misiniz?` : "Bu sınavı silmek istediğinize emin misiniz?"} 
            />
        </div>
    );
}

