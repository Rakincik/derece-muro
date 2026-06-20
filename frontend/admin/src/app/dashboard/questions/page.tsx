"use client";

import { useState, useEffect, useMemo } from "react";
import {
    MessageSquare, Search, Send, X, User, Clock,
    CheckCircle, AlertCircle, Filter, ChevronDown, Image, Trash2
} from "lucide-react";
import { useToast } from "@/components/toast";
import { useAuth } from "@/contexts/AuthContext";
import { questionApi, type QuestionDto } from "@/lib/api";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { Tooltip } from "@/components/ui/Tooltip";

interface Question {
    id: string;
    studentName: string;
    title: string;
    content: string;
    course: string;
    status: string;
    createdAt: string;
    answer: string | null;
    answeredAt: string | null;
    imageUrl: string | null;
    audioUrl: string | null;
}

const getFileUrl = (path: string | null) => {
    if (!path) return "";
    if (path.startsWith("http") || path.startsWith("blob:") || path.startsWith("data:")) return path;
    let hostname = "localhost";
    if (typeof window !== "undefined") {
        hostname = window.location.hostname;
    }
    const base = process.env.NEXT_PUBLIC_API_URL?.replace("localhost", hostname).replace("/api/v1", "") || `http://${hostname}:5292`;
    let cleanPath = path.startsWith("/") ? path : `/${path}`;
    if (cleanPath.startsWith('/uploads')) {
        cleanPath = `/api/v1${cleanPath}`;
    }
    return `${base}${cleanPath}`;
};

function mapQuestion(q: QuestionDto): Question {
    const s = q.status?.toLowerCase() || "";
    return {
        id: q.id,
        studentName: q.userFullName,
        title: q.subject,
        content: q.body,
        course: q.courseTitle || "Genel",
        status: s === "answered" ? "Yanıtlandı" : s === "closed" ? "Kapandı" : "Bekliyor",
        createdAt: new Date(q.createdAt).toLocaleString("tr-TR"),
        answer: q.answer,
        answeredAt: q.answeredAt ? new Date(q.answeredAt).toLocaleString("tr-TR") : null,
        imageUrl: q.imageUrl,
        audioUrl: q.audioUrl,
    };
}

const statusStyles: Record<string, { bg: string; text: string; dot: string }> = {
    Bekliyor: { bg: "bg-amber-50", text: "text-amber-700", dot: "bg-amber-500" },
    Yanıtlandı: { bg: "bg-emerald-50", text: "text-emerald-700", dot: "bg-emerald-500" },
    Kapandı: { bg: "bg-[#E2E8F0]/40", text: "text-[#A9A9A9]", dot: "bg-[#A0AEC0]" },
};

export default function QuestionsPage() {
    const { success, error } = useToast();
    const { token, currentTenantId: tenantId } = useAuth();
    const [questions, setQuestions] = useState<Question[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState("");
    const [statusFilter, setStatusFilter] = useState("all");
    const [selectedId, setSelectedId] = useState<string | null>(null);
    const [replyText, setReplyText] = useState("");
    const [confirmConfig, setConfirmConfig] = useState<{open: boolean, title: string, message: string, onConfirm: () => void}>({ open: false, title: "", message: "", onConfirm: () => {} });

    // ── Fetch questions from API ──
    const fetchQuestions = async () => {
        if (!token || !tenantId) return;
        try {
            setLoading(true);
            const result = await questionApi.list(token, tenantId, { pageSize: 100 });
            setQuestions(result.items.map(mapQuestion));
            if (result.items.length > 0 && !selectedId) {
                if (typeof window !== 'undefined' && window.innerWidth >= 768) {
                    setSelectedId(result.items[0].id);
                }
            }
        } catch (e) { console.error('Failed to fetch questions:', e); }
        finally { setLoading(false); }
    };

    useEffect(() => { fetchQuestions(); }, [token, tenantId]);

    const filtered = useMemo(() => {
        return questions.filter(q => {
            const matchSearch = search === "" || q.title.toLowerCase().includes(search.toLowerCase()) || q.studentName.toLowerCase().includes(search.toLowerCase());
            const matchStatus = statusFilter === "all" || q.status === statusFilter;
            return matchSearch && matchStatus;
        });
    }, [questions, search, statusFilter]);

    const selected = questions.find(q => q.id === selectedId) || null;
    const waitingCount = questions.filter(q => q.status === "Bekliyor").length;

    const handleReply = async () => {
        if (!selected || !replyText.trim() || !token || !tenantId) return;
        try {
            const updated = await questionApi.answer(token, tenantId, selected.id, replyText);
            setQuestions(prev => prev.map(q => q.id === selected.id ? mapQuestion(updated) : q));
            setReplyText("");
            success("Yanıt Gönderildi");
        } catch (e: any) {
            success("Hata", e.message || "Yanıt gönderilemedi");
        }
    };

    const handleDeleteQuestion = async (e: React.MouseEvent) => {
        e.stopPropagation();
        if (!selected || !token || !tenantId) return;
        setConfirmConfig({
            open: true,
            title: "Soruyu Sil",
            message: "Bu soruyu tamamen silmek istediğinize emin misiniz? Bu işlem geri alınamaz.",
            onConfirm: async () => {
                try {
                    await questionApi.deleteQuestion(token, tenantId, selected.id);
                    setQuestions(prev => prev.filter(q => q.id !== selected.id));
                    setSelectedId(null);
                    success("Soru başarıyla silindi");
                } catch (e: any) {
                    error("Hata", e.message || "Soru silinemedi");
                }
            }
        });
    };

    const handleDeleteAnswer = async (e: React.MouseEvent) => {
        e.stopPropagation();
        if (!selected || !token || !tenantId) return;
        setConfirmConfig({
            open: true,
            title: "Cevabı Sil",
            message: "Bu cevabı silmek istediğinize emin misiniz? Soru tekrar 'Bekliyor' statüsüne dönecektir.",
            onConfirm: async () => {
                try {
                    const updated = await questionApi.deleteAnswer(token, tenantId, selected.id);
                    setQuestions(prev => prev.map(q => q.id === selected.id ? mapQuestion(updated) : q));
                    success("Cevap başarıyla silindi");
                } catch (e: any) {
                    error("Hata", e.message || "Cevap silinemedi");
                }
            }
        });
    };

    return (
        <>
        <ConfirmDialog
            open={confirmConfig.open}
            onClose={() => setConfirmConfig(prev => ({ ...prev, open: false }))}
            onConfirm={confirmConfig.onConfirm}
            title={confirmConfig.title}
            message={confirmConfig.message}
            confirmText="Evet, Sil"
            cancelText="İptal"
            variant="danger"
        />
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-[#0A1931] flex items-center gap-2"><MessageSquare size={24} className="text-pink-500" /> Soru-Cevap</h1>
                    <p className="text-sm text-[#A9A9A9] mt-1">Öğrenci sorularını yanıtlayın</p>
                </div>
                {waitingCount > 0 && (
                    <div className="px-3 py-1.5 bg-amber-50 text-amber-700 rounded-lg text-xs font-semibold flex items-center gap-1.5">
                        <AlertCircle size={14} /> {waitingCount} yanıt bekleyen soru
                    </div>
                )}
            </div>

            <div className="grid grid-cols-1 md:grid-cols-5 gap-4 md:gap-6" style={{ height: 'calc(100vh - 240px)', minHeight: '500px' }}>
                {/* List */}
                <div className={`md:col-span-2 bg-white rounded-2xl border border-[#E2E8F0]/60 flex-col overflow-hidden ${selectedId ? 'hidden md:flex' : 'flex'}`}>
                    <div className="p-4 border-b border-[#E2E8F0]/60 space-y-2">
                        <div className="relative">
                            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                            <input type="text" placeholder="Soru ara..." value={search} onChange={e => setSearch(e.target.value)}
                                className="w-full pl-8 pr-3 py-2 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-lg focus:outline-none focus:ring-2 focus:ring-pink-500/20" />
                        </div>
                        <select value={statusFilter} onChange={e => setStatusFilter(e.target.value)}
                            className="w-full px-3 py-1.5 text-xs bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-lg focus:outline-none focus:ring-2 focus:ring-pink-500/20">
                            <option value="all">Tüm Durum</option>
                            <option value="Bekliyor">Bekliyor</option>
                            <option value="Yanıtlandı">Yanıtlandı</option>
                            <option value="Kapandı">Kapandı</option>
                        </select>
                    </div>
                    <div className="flex-1 overflow-y-auto">
                        {loading ? (
                            <div className="p-4 space-y-3">{[1, 2, 3].map(i => <div key={i} className="h-16 rounded-lg bg-[#E2E8F0]/30 animate-pulse" />)}</div>
                        ) : filtered.length === 0 ? (
                            <div className="flex flex-col items-center justify-center h-full text-[#A0AEC0]">
                                <MessageSquare size={32} className="opacity-20 mb-2" />
                                <p className="text-sm">Soru bulunamadı</p>
                            </div>
                        ) : filtered.map(q => {
                            const ss = statusStyles[q.status] || statusStyles.Bekliyor;
                            return (
                                <button key={q.id} onClick={() => setSelectedId(q.id)}
                                    className={`w-full text-left px-4 py-3.5 border-b border-[#E2E8F0]/60 hover:bg-[#E2E8F0]/20 transition-colors ${selectedId === q.id ? "bg-pink-50/50 border-l-2 border-l-pink-500" : ""}`}>
                                    <div className="flex items-center gap-2 mb-1">
                                        <span className="text-xs font-bold text-[#0A1931] truncate flex-1">{q.title}</span>
                                        <span className={`w-2 h-2 rounded-full shrink-0 ${ss.dot}`} />
                                    </div>
                                    <p className="text-xs text-[#A0AEC0] truncate">{q.studentName} · {q.course}</p>
                                    <p className="text-[10px] text-[#A0AEC0] mt-1">{q.createdAt}</p>
                                </button>
                            );
                        })}
                    </div>
                </div>

                {/* Detail */}
                <div className={`md:col-span-3 bg-white rounded-2xl border border-[#E2E8F0]/60 flex-col overflow-hidden ${!selectedId ? 'hidden md:flex' : 'flex'}`}>
                    {selected ? (
                        <>
                            <div className="px-4 md:px-6 py-4 border-b border-[#E2E8F0]/60 flex items-start justify-between gap-2">
                                <div className="flex items-start gap-3 min-w-0">
                                    <button onClick={() => setSelectedId(null)} className="md:hidden p-1.5 -ml-1 mt-0.5 rounded-lg bg-[#E2E8F0]/40 text-[#1B3B6F] hover:bg-[#E2E8F0] shrink-0">
                                        <X size={16} />
                                    </button>
                                    <div className="min-w-0">
                                        <div className="flex flex-wrap items-center gap-2 mb-1">
                                            <h2 className="text-base font-bold text-[#0A1931]">{selected.title}</h2>
                                            <span className={`text-[10px] font-semibold px-2 py-0.5 rounded-lg shrink-0 ${(statusStyles[selected.status] || statusStyles.Bekliyor).bg} ${(statusStyles[selected.status] || statusStyles.Bekliyor).text}`}>{selected.status}</span>
                                        </div>
                                        <p className="text-xs text-[#A0AEC0] truncate">{selected.studentName} · {selected.course} · {selected.createdAt}</p>
                                    </div>
                                </div>
                                <Tooltip content="Soruyu Komple Sil"><button onClick={handleDeleteQuestion}
                                    className="p-2 text-[#A0AEC0] hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors shrink-0">
                                    <Trash2 size={16} />
                                </button></Tooltip>
                            </div>

                            <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
                                {/* Student question */}
                                <div className="flex gap-3">
                                    <div className="w-8 h-8 rounded-full bg-gradient-to-br from-pink-500 to-rose-500 flex items-center justify-center text-white text-xs font-bold shrink-0">
                                        {selected.studentName.split(" ").map(n => n[0]).join("")}
                                    </div>
                                    <div className="flex-1 bg-[#E2E8F0]/20 rounded-xl p-4">
                                        <p className="text-xs font-semibold text-[#0A1931] mb-1">{selected.studentName}</p>
                                        <p className="text-sm text-[#1B3B6F] whitespace-pre-wrap">{selected.content}</p>
                                        
                                        {selected.imageUrl && (
                                            <div className="mt-3">
                                                <img src={getFileUrl(selected.imageUrl)} alt="Soru Görseli" className="max-w-full max-h-64 rounded-xl border border-[#E2E8F0]/60 shadow-sm object-contain" />
                                            </div>
                                        )}
                                        {selected.audioUrl && (
                                            <div className="mt-3 w-full max-w-[300px]">
                                                <audio src={getFileUrl(selected.audioUrl)} controls className="h-10 w-full rounded-lg" />
                                            </div>
                                        )}
                                    </div>
                                </div>

                                {/* Answer */}
                                {selected.answer && (
                                    <div className="flex gap-3 justify-end">
                                        <div className="flex-1 max-w-[85%] group">
                                            <div className="bg-[#E2E8F0]/30 rounded-xl p-4">
                                                <div className="flex items-center justify-between gap-2 mb-1">
                                                    <p className="text-xs font-semibold text-[#0A1931]">Eğitmen Yanıtı</p>
                                                    <Tooltip content="Cevabı Sil"><button onClick={handleDeleteAnswer}
                                                        className="text-[#A0AEC0] hover:text-red-500 transition-colors opacity-0 group-hover:opacity-100">
                                                        <Trash2 size={14} />
                                                    </button></Tooltip>
                                                </div>
                                                <p className="text-sm text-[#1B3B6F]">{selected.answer}</p>
                                                {selected.answeredAt && <p className="text-[10px] text-[#A0AEC0] mt-2">{selected.answeredAt}</p>}
                                            </div>
                                        </div>
                                        <div className="w-8 h-8 rounded-full bg-gradient-to-br from-[#1B3B6F] to-[#1B3B6F] flex items-center justify-center text-white text-xs font-bold shrink-0">
                                            <CheckCircle size={14} />
                                        </div>
                                    </div>
                                )}
                            </div>

                            {/* Reply */}
                            {selected.status !== "Kapandı" && !selected.answer && (
                                <div className="px-6 py-4 border-t border-[#E2E8F0]/60 bg-[#E2E8F0]/15">
                                    <div className="flex items-end gap-3">
                                        <textarea value={replyText} onChange={e => setReplyText(e.target.value)}
                                            className="flex-1 px-3 py-2.5 text-sm bg-white border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-pink-500/20 resize-none h-16"
                                            placeholder="Yanıtınızı yazın..." />
                                        <button onClick={handleReply} disabled={!replyText.trim()}
                                            className="p-3 rounded-xl bg-pink-600 text-white hover:bg-pink-700 transition-colors disabled:opacity-30 shadow-sm shadow-pink-500/25">
                                            <Send size={16} />
                                        </button>
                                    </div>
                                </div>
                            )}
                        </>
                    ) : (
                        <div className="flex flex-col items-center justify-center h-full text-[#A0AEC0]">
                            <MessageSquare size={40} className="opacity-20 mb-3" />
                            <p className="text-sm">Bir soru seçin</p>
                        </div>
                    )}
                </div>
            </div>
        </div>
        </>
    );
}

