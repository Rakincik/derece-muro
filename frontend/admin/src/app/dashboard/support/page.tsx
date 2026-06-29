"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { useRouter } from "next/navigation";
import {
    MessageSquare, Search, CheckCircle, Clock, X,
    Send, RefreshCw, Tag, AlertCircle, Trash2
} from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/components/toast";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { supportApi, type TicketDto, type TicketReplyDto } from "@/lib/api";
import { CustomSelect } from "@/components/ui/CustomSelect";

const STATUS_MAP: Record<string, { label: string; bg: string; text: string; icon: React.ElementType }> = {
    "Açık": { label: "Açık", bg: "bg-amber-50", text: "text-amber-700", icon: Clock },
    "Yanıtlandı": { label: "Yanıtlandı", bg: "bg-blue-50", text: "text-blue-700", icon: MessageSquare },
    "Çözüldü": { label: "Çözüldü", bg: "bg-emerald-50", text: "text-emerald-700", icon: CheckCircle },
    "Open": { label: "Açık", bg: "bg-amber-50", text: "text-amber-700", icon: Clock },
    "Answered": { label: "Yanıtlandı", bg: "bg-blue-50", text: "text-blue-700", icon: MessageSquare },
    "Closed": { label: "Çözüldü", bg: "bg-emerald-50", text: "text-emerald-700", icon: CheckCircle },
    "InProgress": { label: "Yanıtlandı", bg: "bg-blue-50", text: "text-blue-700", icon: MessageSquare },
    "Resolved": { label: "Çözüldü", bg: "bg-emerald-50", text: "text-emerald-700", icon: CheckCircle },
};
function getStatus(s: string) { return STATUS_MAP[s] ?? { label: s, bg: "bg-[#E2E8F0]/20", text: "text-[#A9A9A9]", icon: AlertCircle }; }

const PRIORITY_MAP: Record<string, { label: string; dot: string }> = {
    high: { label: "Yüksek", dot: "bg-red-500" },
    normal: { label: "Normal", dot: "bg-amber-500" },
    low: { label: "Düşük", dot: "bg-[#A0AEC0]" },
    urgent: { label: "Acil", dot: "bg-red-700" },
};

const STATUS_OPTIONS = [
    { label: "Açık", value: "Open", icon: Clock },
    { label: "Yanıtlandı", value: "InProgress", icon: MessageSquare },
    { label: "Çözüldü", value: "Closed", icon: CheckCircle }
];

const normalizeStatus = (status: string) => {
    if (status === "Açık" || status === "Open") return "Open";
    if (status === "Yanıtlandı" || status === "Answered" || status === "InProgress") return "InProgress";
    if (status === "Çözüldü" || status === "Closed" || status === "Resolved") return "Closed";
    return "Open";
};

export default function SupportPage() {
    const router = useRouter();
    const { token, currentTenantId: tenantId } = useAuth();
    const { success, error: toastError } = useToast();

    const [tickets, setTickets] = useState<TicketDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [selected, setSelected] = useState<TicketDto | null>(null);
    const [loadingDetail, setLoadingDetail] = useState(false);
    const [replyText, setReplyText] = useState("");
    const [replying, setReplying] = useState(false);
    const [search, setSearch] = useState("");
    const [statusFilter, setStatusFilter] = useState("all");
    const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
    const chatRef = useRef<HTMLDivElement>(null);

    const handleSelectTicket = async (ticket: TicketDto) => {
        if (!token || !tenantId) return;
        setSelected(ticket);
        setLoadingDetail(true);
        try {
            const detail = await supportApi.get(token, tenantId, ticket.id);
            setSelected(detail);
        } catch {
            toastError("Hata", "Talep detayları yüklenemedi.");
        } finally {
            setLoadingDetail(false);
        }
    };

    const load = useCallback(async () => {
        if (!token || !tenantId) return;
        setLoading(true);
        try {
            const data = await supportApi.list(token, tenantId, { pageSize: 100 });
            setTickets(data.items ?? []);
        } catch {
            toastError("Hata", "Destek talepleri yüklenemedi.");
        } finally {
            setLoading(false);
        }
    }, [token, tenantId]); // eslint-disable-line react-hooks/exhaustive-deps

    useEffect(() => { load(); }, [token, tenantId]); // eslint-disable-line react-hooks/exhaustive-deps

    // Scroll chat to bottom when messages change
    useEffect(() => {
        if (chatRef.current) chatRef.current.scrollTop = chatRef.current.scrollHeight;
    }, [selected?.messages]);

    const filtered = tickets.filter(t => {
        const matchSearch = !search || t.subject.toLowerCase().includes(search.toLowerCase()) || (t.userFullName && t.userFullName.toLowerCase().includes(search.toLowerCase()));
        const matchStatus = statusFilter === "all" || 
            (statusFilter === "Open" && (t.status === "Open" || t.status === "Açık")) ||
            (statusFilter === "InProgress" && (t.status === "Answered" || t.status === "Yanıtlandı" || t.status === "InProgress")) ||
            (statusFilter === "Closed" && (t.status === "Closed" || t.status === "Çözüldü" || t.status === "Resolved"));
        return matchSearch && matchStatus;
    }).sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());

    const open = tickets.filter(t => t.status === "Açık" || t.status === "Open").length;
    const answered = tickets.filter(t => t.status === "Yanıtlandı" || t.status === "Answered" || t.status === "InProgress").length;
    const closed = tickets.filter(t => t.status === "Çözüldü" || t.status === "Closed" || t.status === "Resolved").length;

    const handleReply = async () => {
        if (!token || !tenantId || !selected || !replyText.trim()) return;
        setReplying(true);
        try {
            const reply = await supportApi.reply(token, tenantId, selected.id, replyText.trim());
            const updatedTicket: TicketDto = {
                ...selected,
                messages: [...(selected.messages ?? []), reply],
                status: "Yanıtlandı",
                createdAt: reply.createdAt, // Update the sorting date locally to pop it to the top
            };
            setSelected(updatedTicket);
            setTickets(prev => prev.map(t => t.id === updatedTicket.id ? updatedTicket : t));
            setReplyText("");
            success("Yanıt gönderildi");
        } catch { toastError("Hata", "Yanıt gönderilemedi."); }
        finally { setReplying(false); }
    };

    const handleStatusChange = async (ticketId: string, newStatus: string) => {
        if (!token || !tenantId) return;
        try {
            await supportApi.updateStatus(token, tenantId, ticketId, newStatus);
            setTickets(prev => prev.map(t => t.id === ticketId ? { ...t, status: newStatus } : t));
            if (selected?.id === ticketId) {
                setSelected(prev => prev ? { ...prev, status: newStatus } : null);
            }
            success("Durum güncellendi");
        } catch { toastError("Hata", "Durum güncellenemedi."); }
    };

    const handleDelete = async () => {
        if (!token || !tenantId || !deleteTarget) return;
        try {
            await supportApi.delete(token, tenantId, deleteTarget);
            setTickets(prev => prev.filter(t => t.id !== deleteTarget));
            if (selected?.id === deleteTarget) setSelected(null);
            setDeleteTarget(null);
            success("Talep silindi");
        } catch { toastError("Hata", "Silinemedi."); }
    };

    return (
        <div className="space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-[#0A1931] flex items-center gap-2">
                        <MessageSquare size={24} className="text-[#A0AEC0]" /> Teknik Destek Talepleri
                    </h1>
                    <p className="text-sm text-[#A9A9A9] mt-1">Öğrenci sorularını yönetin</p>
                </div>
                <div className="flex flex-wrap items-center gap-3">
                    <div className="flex items-center gap-2 bg-[#25D366]/10 px-3 py-1.5 rounded-xl border border-[#25D366]/20">
                        <div className="flex flex-col">
                            <span className="text-[10px] font-bold text-[#25D366] uppercase">Acil Durum WhatsApp</span>
                            <div className="flex items-center gap-3 mt-0.5">
                                <a href="https://wa.me/905453724201" target="_blank" rel="noreferrer" className="text-xs font-bold text-[#0A1931] hover:text-[#25D366] transition-colors">
                                    Rüstem Akıncık (0545 372 4201)
                                </a>
                                <span className="text-[#A0AEC0]/40">|</span>
                                <a href="https://wa.me/905536445851" target="_blank" rel="noreferrer" className="text-xs font-bold text-[#0A1931] hover:text-[#25D366] transition-colors">
                                    Volkan Çetin (0553 644 5851)
                                </a>
                            </div>
                        </div>
                    </div>
                    <button onClick={load} className="self-start sm:self-auto p-2.5 rounded-xl bg-white border border-[#E2E8F0] hover:bg-[#E2E8F0]/20 text-[#A9A9A9]" title="Yenile">
                        <RefreshCw size={15} />
                    </button>
                </div>
            </div>

            {/* KPIs */}
            <div className="flex lg:grid lg:grid-cols-3 gap-3 overflow-x-auto hide-scrollbar pb-2 snap-x">
                {[
                    { label: "Açık", value: open, color: "text-amber-600", bg: "bg-amber-50" },
                    { label: "Yanıtlandı", value: answered, color: "text-blue-600", bg: "bg-blue-50" },
                    { label: "Çözüldü", value: closed, color: "text-emerald-600", bg: "bg-emerald-50" },
                ].map(s => (
                    <div key={s.label} className={`min-w-[120px] lg:min-w-0 shrink-0 snap-start ${s.bg} rounded-xl p-2.5 flex items-center justify-center gap-2 border border-white/50 shadow-sm`}>
                        <p className={`text-xl font-black ${s.color}`}>{s.value}</p>
                        <p className="text-[11px] font-bold text-[#64748B] uppercase tracking-wider">{s.label}</p>
                    </div>
                ))}
            </div>

            <div className="grid grid-cols-1 md:grid-cols-5 gap-4" style={{ height: "calc(100vh - 280px)", minHeight: "500px" }}>
                {/* Left: Ticket list */}
                <div className={`md:col-span-2 bg-white rounded-2xl border border-[#E2E8F0]/60 flex-col overflow-hidden ${selected ? 'hidden md:flex' : 'flex'}`}>
                    <div className="p-3 border-b border-[#E2E8F0]/60 space-y-2">
                        <div className="relative">
                            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                            <input type="text" placeholder="Konu veya öğrenci ara..." value={search}
                                onChange={e => setSearch(e.target.value)}
                                className="w-full pl-8 pr-3 py-2 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-purple-500/20" />
                        </div>
                        <div className="flex gap-1">
                            {["all", "Open", "InProgress", "Closed"].map(s => (
                                <button key={s}
                                    onClick={() => setStatusFilter(s)}
                                    className={`flex-1 text-[10px] font-semibold py-1 rounded-lg transition-all ${statusFilter === s ? "bg-purple-600 text-white" : "bg-[#E2E8F0]/40 text-[#A9A9A9] hover:bg-[#E2E8F0]"}`}>
                                    {s === "all" ? "Tümü" : s === "Open" ? "Açık" : s === "InProgress" ? "Yanıtlandı" : "Çözüldü"}
                                </button>
                            ))}
                        </div>
                    </div>
                    <div className="flex-1 overflow-y-auto divide-y divide-[#E2E8F0]">
                        {loading ? (
                            [...Array(5)].map((_, i) => <div key={i} className="m-3 h-16 bg-[#E2E8F0]/40 rounded-xl animate-pulse" />)
                        ) : filtered.length === 0 ? (
                            <div className="flex flex-col items-center justify-center h-full py-12 text-[#A0AEC0]">
                                <MessageSquare size={32} className="opacity-25 mb-2" />
                                <p className="text-sm">Talep bulunamadı</p>
                            </div>
                        ) : filtered.map(t => {
                            const st = getStatus(t.status);
                            const prio = PRIORITY_MAP[t.priority] ?? PRIORITY_MAP.normal;
                            return (
                                <button key={t.id} onClick={() => handleSelectTicket(t)}
                                    className={`w-full text-left p-3 hover:bg-[#E2E8F0]/20 transition-colors ${selected?.id === t.id ? "bg-purple-50 border-l-2 border-purple-500" : ""}`}>
                                    <div className="flex items-start justify-between gap-2 mb-1">
                                        <p className="text-sm font-semibold text-[#0A1931] truncate">{t.subject}</p>
                                        <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded shrink-0 ${st.bg} ${st.text}`}>{st.label}</span>
                                    </div>
                                    <p className="text-xs text-[#A9A9A9] truncate">{t.userFullName}</p>
                                    <div className="flex items-center gap-2 mt-1">
                                        <span className={`w-1.5 h-1.5 rounded-full ${prio.dot}`} />
                                        <span className="text-[10px] text-[#A0AEC0]">{prio.label}</span>
                                        <span className="text-[10px] text-[#A0AEC0] ml-auto">{new Date(t.createdAt).toLocaleDateString("tr-TR")}</span>
                                    </div>
                                </button>
                            );
                        })}
                    </div>
                </div>

                {/* Right: Detail Panel */}
                <div className={`md:col-span-3 bg-white rounded-2xl border border-[#E2E8F0]/60 flex-col overflow-hidden ${!selected ? 'hidden md:flex' : 'flex'}`}>
                    {!selected ? (
                        <div className="flex-1 flex flex-col items-center justify-center text-[#A0AEC0]">
                            <MessageSquare size={48} className="opacity-20 mb-3" />
                            <p className="text-sm">Bir destek talebi seçin</p>
                        </div>
                    ) : (
                        <>
                            {/* Header */}
                            <div className="px-4 md:px-5 py-4 border-b border-[#E2E8F0]/60 flex items-start justify-between gap-2">
                                <div className="flex items-start gap-2 min-w-0">
                                    <button onClick={() => setSelected(null)} className="md:hidden p-1.5 -ml-1 mt-0.5 rounded-lg bg-[#E2E8F0]/40 text-[#1B3B6F] hover:bg-[#E2E8F0] shrink-0">
                                        <X size={16} />
                                    </button>
                                    <div className="min-w-0">
                                        <h2 className="text-base font-bold text-[#0A1931] truncate">{selected.subject}</h2>
                                        <div className="flex items-center gap-2 mt-1 flex-wrap">
                                            <span 
                                                onClick={() => router.push(`/dashboard/users?userId=${selected.userId}`)}
                                                className="text-xs font-semibold text-purple-600 hover:text-purple-800 hover:underline cursor-pointer transition-colors"
                                                title="Öğrencinin Profiline Git"
                                            >
                                                {selected.userFullName}
                                            </span>
                                            <span className="text-[#A0AEC0]">·</span>
                                            <span className="text-[11px] text-[#A0AEC0] flex items-center gap-1"><Tag size={10} />{selected.category}</span>
                                            <span className="text-[#A0AEC0]">·</span>
                                            <span className="text-[11px] text-[#A0AEC0]">{new Date(selected.createdAt).toLocaleDateString("tr-TR")}</span>
                                        </div>
                                    </div>
                                </div>
                                <div className="flex items-center gap-1.5 ml-auto shrink-0">
                                    <CustomSelect
                                        value={normalizeStatus(selected.status)}
                                        onChange={val => handleStatusChange(selected.id, String(val))}
                                        options={STATUS_OPTIONS}
                                        className="w-[125px]"
                                    />
                                    <button onClick={() => setDeleteTarget(selected.id)}
                                        className="p-1.5 rounded-lg hover:bg-red-50 text-[#A0AEC0] hover:text-red-500">
                                        <Trash2 size={14} />
                                    </button>
                                    <button onClick={() => setSelected(null)} className="hidden md:block p-1.5 rounded-lg hover:bg-[#E2E8F0]/40 text-[#A0AEC0]">
                                        <X size={14} />
                                    </button>
                                </div>
                            </div>

                            {/* Chat messages */}
                            <div ref={chatRef} className="flex-1 overflow-y-auto p-4 space-y-3">
                                {loadingDetail ? (
                                    <div className="flex flex-col justify-center items-center h-full text-sm text-[#A0AEC0] gap-2">
                                        <RefreshCw size={18} className="animate-spin text-purple-600" />
                                        <span>Yükleniyor...</span>
                                    </div>
                                ) : (
                                    <>
                                        {/* Original message */}
                                        <div className="flex gap-3">
                                            <div 
                                                onClick={() => router.push(`/dashboard/users?userId=${selected.userId}`)}
                                                className="w-8 h-8 rounded-full bg-gradient-to-br from-[#1B3B6F] to-blue-800 flex items-center justify-center shrink-0 text-xs font-bold text-white hover:opacity-90 cursor-pointer transition-all active:scale-95"
                                                title="Profilini Gör"
                                            >
                                                {selected.userFullName?.[0] ?? "?"}
                                            </div>
                                            <div className="flex-1">
                                                <p 
                                                    onClick={() => router.push(`/dashboard/users?userId=${selected.userId}`)}
                                                    className="text-[11px] font-semibold text-[#5A6A7A] hover:text-[#1B3B6F] cursor-pointer transition-colors mb-1 inline-block"
                                                    title="Profilini Gör"
                                                >
                                                    {selected.userFullName}
                                                </p>
                                                <div className="bg-[#F8FAFC] border border-[#E2E8F0]/60 rounded-xl rounded-tl-none p-3 max-w-[85%]">
                                                    <p className="text-sm text-[#0A1931]">{selected.body}</p>
                                                </div>
                                                <p className="text-[10px] text-[#A0AEC0] mt-1 ml-1">{new Date(selected.createdAt).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}</p>
                                            </div>
                                        </div>
                                        {/* Replies */}
                                        {(selected.messages ?? []).map((r: TicketReplyDto) => {
                                            const isAdminReply = !!r.isAdmin;
                                            return (
                                            <div key={r.id} className={`flex gap-3 ${isAdminReply ? "flex-row-reverse" : ""}`}>
                                                <div 
                                                    onClick={() => !isAdminReply && router.push(`/dashboard/users?userId=${selected.userId}`)}
                                                    className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 text-xs font-bold ${
                                                        isAdminReply 
                                                            ? "bg-emerald-500 text-white" 
                                                            : "bg-gradient-to-br from-[#1B3B6F] to-blue-800 text-white hover:opacity-90 cursor-pointer transition-all active:scale-95"
                                                    }`}
                                                    title={isAdminReply ? undefined : "Profilini Gör"}
                                                >
                                                    {r.senderName?.[0] ?? "?"}
                                                </div>
                                                <div className={`flex-1 ${isAdminReply ? "flex flex-col items-end" : ""}`}>
                                                    <p 
                                                        onClick={() => !isAdminReply && router.push(`/dashboard/users?userId=${selected.userId}`)}
                                                        className={`text-[11px] font-semibold text-[#5A6A7A] mb-1 mx-1 ${
                                                            isAdminReply 
                                                                ? "" 
                                                                : "hover:text-[#1B3B6F] cursor-pointer transition-colors"
                                                        }`}
                                                        title={isAdminReply ? undefined : "Profilini Gör"}
                                                    >
                                                        {isAdminReply ? "Destek Ekibi" : r.senderName}
                                                    </p>
                                                    <div className={`rounded-xl p-3 max-w-[85%] ${isAdminReply ? "bg-emerald-50 border border-emerald-100 rounded-tr-none text-emerald-900" : "bg-[#F8FAFC] border border-[#E2E8F0]/60 rounded-tl-none text-[#0A1931]"}`}>
                                                        <p className="text-sm">{r.body}</p>
                                                    </div>
                                                    <p className="text-[10px] text-[#A0AEC0] mt-1 mx-1">{new Date(r.createdAt).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}</p>
                                                </div>
                                            </div>
                                            );
                                        })}
                                    </>
                                )}
                            </div>

                            {/* Reply box */}
                            <div className="px-4 pb-4 pt-2 border-t border-[#E2E8F0]/60">
                                <div className="flex items-end gap-2">
                                    <textarea
                                        value={replyText}
                                        onChange={e => setReplyText(e.target.value)}
                                        onKeyDown={e => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); handleReply(); } }}
                                        placeholder="Yanıtınızı yazın... (Enter göndermek için)"
                                        className="flex-1 px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-purple-500/20 resize-none max-h-32"
                                        rows={2}
                                    />
                                    <button
                                        onClick={handleReply}
                                        disabled={replying || !replyText.trim()}
                                        className="p-3 bg-purple-600 hover:bg-purple-700 disabled:opacity-40 text-white rounded-xl transition-colors shrink-0"
                                    >
                                        <Send size={16} />
                                    </button>
                                </div>
                            </div>
                        </>
                    )}
                </div>
            </div>

            <ConfirmDialog
                open={deleteTarget !== null}
                onClose={() => setDeleteTarget(null)}
                onConfirm={handleDelete}
                title="Talebi Sil"
                message="Bu destek talebi kalıcı olarak silinecek."
            />
        </div>
    );
}
