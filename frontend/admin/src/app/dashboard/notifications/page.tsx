"use client";

import { useState, useEffect, useCallback } from "react";
import {
    Bell, Search, Send, X, Users, Megaphone,
    CheckCheck, Clock, AlertCircle, Info, Trash2, RefreshCw
} from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/components/toast";
import {
    notificationApi,
    type AdminSentNotificationDto,
    type GroupSummaryDto,
    type UserDto,
    courseApi
} from "@/lib/api";

// ─── Type badge helpers ─────────────────────────────────────────────────────

const TYPE_STYLES: Record<string, { bg: string; text: string; icon: React.ElementType }> = {
    Info: { bg: "bg-blue-50", text: "text-blue-700", icon: Info },
    Bilgi: { bg: "bg-blue-50", text: "text-blue-700", icon: Info },
    Uyarı: { bg: "bg-amber-50", text: "text-amber-700", icon: AlertCircle },
    Warning: { bg: "bg-amber-50", text: "text-amber-700", icon: AlertCircle },
    Acil: { bg: "bg-red-50", text: "text-red-700", icon: Megaphone },
    Urgent: { bg: "bg-red-50", text: "text-red-700", icon: Megaphone },
    Başarı: { bg: "bg-emerald-50", text: "text-emerald-700", icon: CheckCheck },
    Success: { bg: "bg-emerald-50", text: "text-emerald-700", icon: CheckCheck },
};
const defaultStyle = { bg: "bg-[#E2E8F0]/20", text: "text-[#1B3B6F]", icon: Bell };
function typeLookup(t: string | null) {
    if (!t) return defaultStyle;
    return TYPE_STYLES[t] ?? defaultStyle;
}

// ─── Send Modal ──────────────────────────────────────────────────────────────

interface SendModalProps {
    onClose: () => void;
    onSent: () => void;
    groups: GroupSummaryDto[];
    users: UserDto[];
    courses: { id: string, title: string }[];
}

function SendNotifModal({ onClose, onSent, groups, users, courses }: SendModalProps) {
    const { token, currentTenantId: tenantId } = useAuth();
    const { success, error: toastError } = useToast();
    const [title, setTitle] = useState("");
    const [body, setBody] = useState("");
    const [type, setType] = useState("Bilgi");
    const [target, setTarget] = useState<"all" | "group" | "course" | "single">("all");
    const [groupId, setGroupId] = useState("");
    const [courseId, setCourseId] = useState("");
    const [userId, setUserId] = useState("");
    const [scheduledAt, setScheduledAt] = useState("");
    const [sending, setSending] = useState(false);
    
    // For student search
    const [studentSearch, setStudentSearch] = useState("");
    const [studentSearchFocused, setStudentSearchFocused] = useState(false);

    const handleSend = async () => {
        if (!token || !tenantId) return;
        if (!title.trim() || !body.trim()) { toastError("Hata", "Başlık ve mesaj zorunludur."); return; }

        let userIds: string[] = [];
        let sendToAll = false;
        let targetGroupId: string | undefined = undefined;
        let targetCourseId: string | undefined = undefined;

        if (target === "all") {
            sendToAll = true;
        } else if (target === "group") {
            const g = groups.find(g => g.id === groupId);
            if (!g) { toastError("Hata", "Lütfen bir grup seçin."); return; }
            targetGroupId = g.id;
        } else if (target === "course") {
            const c = courses.find(c => c.id === courseId);
            if (!c) { toastError("Hata", "Lütfen bir ders seçin."); return; }
            targetCourseId = c.id;
        } else {
            if (!userId) { toastError("Hata", "Lütfen bir öğrenci seçin."); return; }
            userIds = [userId];
        }

        if (target === "single" && userIds.length === 0) { toastError("Hata", "Hedef kullanıcı bulunamadı."); return; }

        setSending(true);
        try {
            const count = await notificationApi.bulkSend(
                token, tenantId, userIds, title, body, type, 
                scheduledAt || undefined, targetGroupId, sendToAll, targetCourseId
            );
            const schedMsg = scheduledAt ? ` (${new Date(scheduledAt).toLocaleString("tr-TR")} için zamanlandı)` : "";
            success("Bildirim Gönderildi", `${count} kullanıcıya gönderildi${schedMsg}.`);
            onSent();
            onClose();
        } catch {
            toastError("Hata", "Bildirim gönderilemedi.");
        } finally {
            setSending(false);
        }
    };

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4" onClick={onClose}>
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
            <div className="relative w-full max-w-lg bg-white rounded-2xl shadow-xl" onClick={e => e.stopPropagation()}>

                {/* Header */}
                <div className="flex items-center justify-between px-6 py-4 border-b border-[#E2E8F0]/60">
                    <h2 className="text-lg font-bold text-[#0A1931] flex items-center gap-2"><Send size={18} />Bildirim Gönder</h2>
                    <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/40 text-[#A0AEC0]"><X size={18} /></button>
                </div>

                <div className="p-6 space-y-4">
                    {/* Title */}
                    <div>
                        <label className="block text-xs font-semibold text-[#1B3B6F] mb-1.5">Başlık</label>
                        <input value={title} onChange={e => setTitle(e.target.value)} placeholder="Bildirim başlığı"
                            className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20" />
                    </div>

                    {/* Body */}
                    <div>
                        <label className="block text-xs font-semibold text-[#1B3B6F] mb-1.5">Mesaj</label>
                        <textarea value={body} onChange={e => setBody(e.target.value)} placeholder="Bildirim içeriği..."
                            className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 resize-none h-24" />
                    </div>

                    {/* Type + Target */}
                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <label className="block text-xs font-semibold text-[#1B3B6F] mb-1.5">Tip</label>
                            <select value={type} onChange={e => setType(e.target.value)}
                                className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20">
                                <option value="Bilgi">ℹ️ Bilgi</option>
                                <option value="Uyarı">⚠️ Uyarı</option>
                                <option value="Acil">🔴 Acil</option>
                                <option value="Başarı">✅ Başarı</option>
                            </select>
                        </div>
                        <div>
                            <label className="block text-xs font-semibold text-[#1B3B6F] mb-1.5">Hedef</label>
                            <select value={target} onChange={e => setTarget(e.target.value as typeof target)}
                                className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20">
                                <option value="all">👥 Tüm Kullanıcılar</option>
                                <option value="group">🏫 Grup</option>
                                <option value="course">📚 Ders</option>
                                <option value="single">👤 Tek Öğrenci</option>
                            </select>
                        </div>
                    </div>

                    {/* Zamanlanmış Bildirim */}
                    <div>
                        <label className="block text-xs font-semibold text-[#1B3B6F] mb-1.5">🕒 Zamanla (İsteğe Bağlı)</label>
                        <input type="datetime-local" value={scheduledAt} onChange={e => setScheduledAt(e.target.value)}
                            min={new Date().toISOString().slice(0, 16)}
                            className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20" />
                        {scheduledAt && (
                            <p className="text-xs text-[#1B3B6F] mt-1 flex items-center gap-1">
                                <Clock size={11} /> {new Date(scheduledAt).toLocaleString("tr-TR")} tarihinde gönderilecek
                            </p>
                        )}
                    </div>

                    {/* Conditional group/user picker */}
                    {target === "group" && (
                        <div>
                            <label className="block text-xs font-semibold text-[#1B3B6F] mb-1.5">Grup</label>
                            <select value={groupId} onChange={e => setGroupId(e.target.value)}
                                className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20">
                                <option value="">— Grup seçin —</option>
                                {groups.map(g => (
                                    <option key={g.id} value={g.id}>{g.name} ({g.memberCount} üye)</option>
                                ))}
                            </select>
                        </div>
                    )}
                    {target === "course" && (
                        <div>
                            <label className="block text-xs font-semibold text-[#1B3B6F] mb-1.5">Ders</label>
                            <select value={courseId} onChange={e => setCourseId(e.target.value)}
                                className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20">
                                <option value="">— Ders seçin —</option>
                                {courses.map(c => (
                                    <option key={c.id} value={c.id}>{c.title}</option>
                                ))}
                            </select>
                        </div>
                    )}
                    {target === "single" && (
                        <div className="relative">
                            <label className="block text-xs font-semibold text-[#1B3B6F] mb-1.5">Öğrenci</label>
                            <div className="relative">
                                <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                                <input value={studentSearch} onChange={e => { setStudentSearch(e.target.value); setUserId(""); }}
                                    onFocus={() => setStudentSearchFocused(true)}
                                    onBlur={() => setTimeout(() => setStudentSearchFocused(false), 200)}
                                    placeholder="Listeden seçin veya aramak için yazın..."
                                    className="w-full pl-9 pr-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 text-[#0A1931] placeholder:text-[#A0AEC0]" />
                                {studentSearchFocused && (
                                    <div className="absolute z-50 left-0 right-0 top-full mt-2 bg-white rounded-xl shadow-xl border border-[#E2E8F0] max-h-60 overflow-y-auto">
                                        {users.filter(u => (u.role === "Student" || u.role === "student") && 
                                            (!studentSearch || `${u.firstName} ${u.lastName}`.toLowerCase().includes(studentSearch.toLowerCase())))
                                            .map(uItem => (
                                            <div key={uItem.id} onClick={() => { setUserId(uItem.id); setStudentSearch(`${uItem.firstName} ${uItem.lastName}`); setStudentSearchFocused(false); }}
                                                className={`px-4 py-3 cursor-pointer transition-colors border-b border-[#E2E8F0] last:border-0 ${userId === uItem.id ? "bg-emerald-50 hover:bg-emerald-100" : "hover:bg-[#F8FAFC]"}`}>
                                                <p className="text-sm font-bold text-[#0A1931]">{uItem.firstName} {uItem.lastName}</p>
                                                <p className="text-xs text-[#A0AEC0]">{uItem.email}</p>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>
                    )}
                </div>

                <div className="flex items-center justify-between px-6 py-4 border-t border-[#E2E8F0]/60 bg-[#E2E8F0]/15 rounded-b-2xl">
                    <p className="text-xs text-[#A0AEC0]">
                        {target === "all" ? `${users.length} kullanıcıya gönderilecek` :
                            target === "single" ? "1 kullanıcıya gönderilecek" : 
                            target === "course" ? "Derse kayıtlı gruplara gönderilecek" :
                            "Gruba gönderilecek"}
                    </p>
                    <div className="flex gap-3">
                        <button onClick={onClose} className="px-4 py-2.5 text-sm font-medium text-[#1B3B6F] bg-white border border-[#E2E8F0] rounded-xl hover:bg-[#E2E8F0]/20">İptal</button>
                        <button onClick={handleSend} disabled={sending}
                            className="px-5 py-2.5 text-sm font-medium text-white bg-[#1B3B6F] rounded-xl hover:bg-[#0A1931] shadow-sm disabled:opacity-60 flex items-center gap-2">
                            {sending ? <RefreshCw size={14} className="animate-spin" /> : <Send size={14} />}
                            Gönder
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}

// ─── Main Page ───────────────────────────────────────────────────────────────

export default function NotificationsPage() {
    const { token, currentTenantId: tenantId } = useAuth();
    const { success } = useToast();

    const [notifs, setNotifs] = useState<AdminSentNotificationDto[]>([]);
    const [groups, setGroups] = useState<GroupSummaryDto[]>([]);
    const [users, setUsers] = useState<UserDto[]>([]);
    const [courses, setCourses] = useState<{ id: string, title: string }[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState("");
    const [typeFilter, setTypeFilter] = useState("all");
    const [showForm, setShowForm] = useState(false);

    const load = useCallback(async () => {
        if (!token || !tenantId) return;
        setLoading(true);
        try {
            const [n, g, u, cRes] = await Promise.all([
                notificationApi.adminSent(token, tenantId).catch(() => [] as AdminSentNotificationDto[]),
                notificationApi.groups(token, tenantId).catch(() => [] as GroupSummaryDto[]),
                notificationApi.allUsers(token, tenantId).catch(() => [] as UserDto[]),
                courseApi.list(token, tenantId, { pageSize: 1000 }).catch(() => ({ items: [] })),
            ]);
            setNotifs(n);
            setGroups(g);
            setUsers(u);
            setCourses(Array.isArray(cRes) ? cRes : (cRes?.items || []).map((c: any) => ({ id: c.id, title: c.title })));
        } finally {
            setLoading(false);
        }
    }, [token, tenantId]);

    useEffect(() => { load(); }, [token, tenantId]); // eslint-disable-line react-hooks/exhaustive-deps

    const filtered = notifs.filter(n => {
        const matchSearch = search === "" || n.title.toLowerCase().includes(search.toLowerCase()) || n.body.toLowerCase().includes(search.toLowerCase());
        const matchType = typeFilter === "all" || n.type === typeFilter;
        return matchSearch && matchType;
    });

    const stats = [
        { label: "Toplam Gönderim", value: notifs.length, icon: Bell, color: "text-[#1B3B6F]" },
        { label: "Toplam Alıcı", value: notifs.reduce((s, n) => s + n.recipientCount, 0), icon: Users, color: "text-blue-600" },
        { label: "Uyarı", value: notifs.filter(n => n.type === "Uyarı" || n.type === "Warning").length, icon: AlertCircle, color: "text-amber-600" },
        { label: "Acil", value: notifs.filter(n => n.type === "Acil" || n.type === "Urgent").length, icon: Megaphone, color: "text-red-600" },
    ];

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold text-[#0A1931] tracking-tight flex items-center gap-3">
                        <Bell size={28} className="text-[#1B3B6F]" /> Bildirimler
                    </h1>
                    <p className="text-sm text-[#A9A9A9] mt-1">Öğrencilere toplu bildirim gönderin ve geçmişi takip edin</p>
                </div>
                <button onClick={() => setShowForm(true)}
                    className="w-full sm:w-auto px-4 py-2.5 text-sm font-semibold bg-[#1B3B6F] text-white rounded-xl hover:bg-[#0A1931] shadow-sm shadow-[#0A1931]/25 flex items-center justify-center gap-2 transition-colors">
                    <Send size={15} /> Bildirim Gönder
                </button>
            </div>

            {/* Stats */}
            <div className="flex lg:grid lg:grid-cols-4 gap-4 overflow-x-auto hide-scrollbar pb-2 snap-x">
                {stats.map(s => (
                    <div key={s.label} className="min-w-[160px] lg:min-w-0 shrink-0 snap-start bg-white rounded-xl border border-[#E2E8F0]/60 p-4 flex items-center gap-3">
                        <div className="w-10 h-10 rounded-xl bg-[#E2E8F0]/20 flex items-center justify-center">
                            <s.icon size={18} className="text-[#A0AEC0]" />
                        </div>
                        <div>
                            <p className={`text-xl font-bold ${s.color}`}>{s.value}</p>
                            <p className="text-xs text-[#A0AEC0]">{s.label}</p>
                        </div>
                    </div>
                ))}
            </div>

            {/* Filters */}
            <div className="bg-white rounded-xl border border-[#E2E8F0]/60 p-4 flex flex-col sm:flex-row items-stretch sm:items-center gap-3">
                <div className="flex-1 relative">
                    <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                    <input type="text" placeholder="Bildirim ara..." value={search} onChange={e => setSearch(e.target.value)}
                        className="w-full pl-9 pr-4 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10" />
                </div>
                <select value={typeFilter} onChange={e => setTypeFilter(e.target.value)}
                    className="px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10">
                    <option value="all">Tüm Tipler</option>
                    <option value="Bilgi">Bilgi</option>
                    <option value="Uyarı">Uyarı</option>
                    <option value="Acil">Acil</option>
                    <option value="Başarı">Başarı</option>
                </select>
                <button onClick={load} className="p-2.5 rounded-xl bg-[#E2E8F0]/20 border border-[#E2E8F0] hover:bg-[#E2E8F0]/40 text-[#A9A9A9] transition-colors">
                    <RefreshCw size={16} />
                </button>
            </div>

            {/* List */}
            {loading ? (
                <div className="space-y-3">{[...Array(5)].map((_, i) => <div key={i} className="h-20 rounded-xl bg-[#E2E8F0]/40 animate-pulse" />)}</div>
            ) : filtered.length === 0 ? (
                <div className="bg-white rounded-2xl border border-[#E2E8F0]/60 py-20 flex flex-col items-center text-[#A0AEC0]">
                    <Bell size={40} className="opacity-25 mb-3" />
                    <p className="text-sm font-medium">Henüz bildirim gönderilmedi</p>
                    <button onClick={() => setShowForm(true)} className="mt-4 px-4 py-2 text-sm font-semibold bg-[#1B3B6F] text-white rounded-xl hover:bg-[#0A1931] flex items-center gap-2">
                        <Send size={14} /> İlk bildirimi gönder
                    </button>
                </div>
            ) : (
                <div className="space-y-3">
                    {filtered.map(n => {
                        const ts = typeLookup(n.type);
                        const Icon = ts.icon;
                        return (
                            <div key={n.id} className="bg-white rounded-xl border border-[#E2E8F0]/60 p-5 hover:shadow-md transition-all">
                                <div className="flex items-start gap-4">
                                    <div className={`w-10 h-10 rounded-xl ${ts.bg} flex items-center justify-center shrink-0`}>
                                        <Icon size={18} className={ts.text} />
                                    </div>
                                    <div className="flex-1 min-w-0">
                                        <div className="flex flex-wrap items-center gap-2 mb-1">
                                            <h3 className="text-sm font-bold text-[#0A1931] truncate max-w-full">{n.title}</h3>
                                            {n.type && (
                                                <span className={`text-[10px] font-semibold px-2 py-0.5 rounded-lg shrink-0 ${ts.bg} ${ts.text}`}>{n.type}</span>
                                            )}
                                        </div>
                                        <p className="text-xs text-[#A9A9A9] mb-2 line-clamp-2">{n.body}</p>
                                        <div className="flex items-center gap-4 text-xs text-[#A0AEC0]">
                                            <span className="flex items-center gap-1">
                                                <Users size={10} /> {n.recipientCount} alıcı
                                            </span>
                                            <span className="flex items-center gap-1">
                                                <Clock size={10} /> {new Date(n.createdAt).toLocaleString("tr-TR", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                                            </span>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        );
                    })}
                </div>
            )}

            {showForm && <SendNotifModal onClose={() => setShowForm(false)} onSent={() => { load(); success("Bildirim gönderildi!"); }} groups={groups} users={users} courses={courses} />}
        </div>
    );
}
