"use client";
import { useState, useMemo, useRef, useEffect } from "react";
import { useSearchParams } from "next/navigation";
import {
    Users, UserPlus, UserCheck, Search, Download, Upload, Trash2, Edit3,
    ChevronLeft, ChevronRight, X, Check, Shield, ArrowUpDown, ArrowUp, ArrowDown,
    GraduationCap, Briefcase, Mail, Phone, Calendar as CalendarIcon,
    BookOpen, ClipboardList, Activity, ToggleLeft, ToggleRight,
    KeyRound, Clock, TrendingUp, Award, BarChart3, ChevronUp, ChevronDown, Lock, RefreshCw, Copy,
    ArrowLeft, Flame, Target, CreditCard, Eye, EyeOff, Zap, MessageCircle, AlertTriangle, ExternalLink
} from "lucide-react";
import { API_URL } from "@/lib/api/core";
import { KpiGrid } from "@/components/ui/KpiGrid";
import { Tooltip } from "@/components/ui/Tooltip";
import { useToast } from "@/components/toast";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { useAuth } from "@/contexts/AuthContext";
import { userApi, groupsApi, type UserDto, type PagedUsersResult, type CreateUserRequest } from "@/lib/api";
import { UserDirectCoursesTab } from "./UserDirectCoursesTab";
import { UserDetail, type User } from "./UserDetail";

const _default = { bg: "bg-[#E2E8F0]/20 border border-[#E2E8F0]", text: "text-[#1B3B6F]", avatar: "bg-[#A0AEC0]", hero: "bg-[#A0AEC0]" };
const rc: Record<string, { bg: string; text: string; avatar: string; hero: string }> = {
    Admin: { bg: "bg-blue-50 border border-blue-200", text: "text-blue-700", avatar: "bg-blue-600", hero: "bg-blue-600" },
    SuperAdmin: { bg: "bg-rose-50 border border-rose-200", text: "text-rose-700", avatar: "bg-rose-600", hero: "bg-rose-600" },
    Instructor: { bg: "bg-purple-50 border border-purple-200", text: "text-purple-700", avatar: "bg-purple-600", hero: "bg-purple-600" },
    Student: { bg: "bg-emerald-50 border border-emerald-200", text: "text-emerald-700", avatar: "bg-emerald-500", hero: "bg-emerald-500" },
    Accountant: { bg: "bg-amber-50 border border-amber-200", text: "text-amber-700", avatar: "bg-amber-500", hero: "bg-amber-500" },
    Assistant: { bg: "bg-indigo-50 border border-indigo-200", text: "text-indigo-700", avatar: "bg-indigo-500", hero: "bg-indigo-500" },
    // Türkçe eski roller (geriye uyum)
    Eğitmen: { bg: "bg-purple-50 border border-purple-200", text: "text-purple-700", avatar: "bg-purple-600", hero: "bg-purple-600" },
    Öğrenci: { bg: "bg-emerald-50 border border-emerald-200", text: "text-emerald-700", avatar: "bg-emerald-500", hero: "bg-emerald-500" },
    Muhasebe: { bg: "bg-amber-50 border border-amber-200", text: "text-amber-700", avatar: "bg-amber-500", hero: "bg-amber-500" },
    Asistan: { bg: "bg-indigo-50 border border-indigo-200", text: "text-indigo-700", avatar: "bg-indigo-500", hero: "bg-indigo-500" },
};
const roleIcons: Record<string, typeof Shield> = { Admin: Shield, SuperAdmin: Shield, Instructor: Briefcase, Student: GraduationCap, Accountant: Briefcase, Assistant: Briefcase, Eğitmen: Briefcase, Öğrenci: GraduationCap, Muhasebe: Briefcase, Asistan: Briefcase };
const roleLabel: Record<string, string> = { Student: "Öğrenci", Instructor: "Eğitmen", Admin: "Admin", Accountant: "Muhasebe", Assistant: "Asistan", SuperAdmin: "Süper Admin", Öğrenci: "Öğrenci", Eğitmen: "Eğitmen", Muhasebe: "Muhasebe", Asistan: "Asistan" };
const roleRank: Record<string, number> = {
    SuperAdmin: 100,
    Admin: 90,
    Instructor: 80,
    Eğitmen: 80,
    Assistant: 70,
    Asistan: 70,
    Accountant: 60,
    Muhasebe: 60,
    Student: 50,
    Öğrenci: 50
};
// groupOptions artık API'den dinamik olarak çekiliyor
const PER_PAGE_OPTIONS = [10, 50, 100];
type SortField = "name" | "role" | "group" | "status" | "phone" | "createdAt" | "lastLogin"; type SortDir = "asc" | "desc";

const normalizeTurkish = (str: string) => {
    return str.toLocaleLowerCase('tr')
        .replace(/ğ/g, 'g').replace(/ü/g, 'u').replace(/ş/g, 's')
        .replace(/ı/g, 'i').replace(/ö/g, 'o').replace(/ç/g, 'c');
};

const generateDefaultPassword = (firstName: string = "", lastName: string = "", phone: string = "") => {
    const fn = normalizeTurkish(firstName).trim().split(/\s+/)[0] || "user";
    const ln = normalizeTurkish(lastName).trim();
    const lChar = ln.length > 0 ? ln.charAt(0) : "x";
    const pStr = (phone || "").replace(/\D/g, "");
    const pLast2 = pStr.length >= 2 ? pStr.slice(-2) : "00";
    return `${fn}.${pLast2}.${lChar}`;
};

function formatPhoneForDisplay(phone?: string): string {
    if (!phone) return "";
    let digits = phone.replace(/\D/g, "");
    while (digits.startsWith("0")) {
        digits = digits.substring(1);
    }
    if (digits.length === 12 && digits.startsWith("90")) {
        digits = digits.substring(2);
    }
    return digits.substring(0, 10);
}

function mapApiUser(u: UserDto): User {
    return { ...u, phone: formatPhoneForDisplay(u.phone ?? undefined), groupNames: u.groupNames || [], tcNo: (u as any).tcNo || "" };
}

function FilterDropdown({ value, onChange, options, icon: Icon, widthClass = "w-full lg:w-44", searchable = false }: { value: string; onChange: (v: string) => void; options: { value: string; label: string; icon?: any }[]; icon?: any; widthClass?: string; searchable?: boolean }) {
    const [open, setOpen] = useState(false);
    const [searchQuery, setSearchQuery] = useState("");
    const ref = useRef<HTMLDivElement>(null);
    useEffect(() => {
        const handler = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); };
        document.addEventListener("mousedown", handler);
        return () => document.removeEventListener("mousedown", handler);
    }, []);
    const sel = options.find(o => o.value === value) || options[0];
    const filteredOptions = searchable && searchQuery.trim() 
        ? options.filter(o => o.label.toLowerCase().includes(searchQuery.toLowerCase()))
        : options;

    return (
        <div ref={ref} className={`relative shrink-0 ${widthClass}`}>
            <button type="button" onClick={() => setOpen(!open)} className={`group w-full flex items-center justify-between px-4 py-2.5 text-sm bg-white border rounded-2xl transition-all duration-200 shadow-sm ${open ? 'border-slate-300 ring-4 ring-slate-100/60 shadow-sm' : 'border-slate-200 hover:border-slate-300 hover:bg-slate-50/50 hover:shadow-md hover:shadow-slate-200/60'}`}>
                <div className="flex items-center gap-2.5 min-w-0">
                    {Icon && <Icon size={16} className={`shrink-0 transition-colors ${open ? 'text-[#0A1931]' : 'text-slate-400 group-hover:text-slate-500'}`} />}
                    <span className="text-[#0A1931] font-semibold truncate" title={sel.label}>{sel.label}</span>
                </div>
                <ChevronDown size={16} className={`text-slate-400 transition-transform duration-200 shrink-0 ml-2 group-hover:text-slate-500 ${open ? 'rotate-180' : ''}`} />
            </button>
            {open && (
                <div className="absolute z-50 left-0 right-0 top-full mt-2 bg-white/95 backdrop-blur-md rounded-2xl border border-slate-200/60 shadow-[0_12px_38px_rgba(15,23,42,0.12)] p-1.5 flex flex-col origin-top transition-all duration-200 animate-fade-in">
                    {searchable && (
                        <div className="px-2 pb-2 mb-1.5 border-b border-slate-100">
                            <div className="relative">
                                <Search size={14} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-400" />
                                <input 
                                    type="text" 
                                    placeholder="Ara..." 
                                    value={searchQuery}
                                    onChange={e => setSearchQuery(e.target.value)}
                                    className="w-full pl-8 pr-3 py-1.5 text-xs bg-[#F8FAFC] border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-slate-100 focus:border-slate-300 transition-all"
                                    onClick={e => e.stopPropagation()}
                                    autoFocus
                                />
                            </div>
                        </div>
                    )}
                    <div className={`${searchable ? 'max-h-60 overflow-y-auto custom-scrollbar' : ''} flex flex-col gap-0.5`}>
                        {filteredOptions.length > 0 ? filteredOptions.map((o, idx) => {
                            const OptionIcon = o.icon;
                            const isSelected = value === o.value;
                            return (
                                <div key={o.value} className="flex flex-col gap-0.5">
                                    <button 
                                        type="button" 
                                        onClick={() => { onChange(o.value); setOpen(false); setSearchQuery(""); }} 
                                        className={`group w-full flex items-center justify-between px-3.5 py-2.5 text-left rounded-xl transition-all duration-150 ${
                                            isSelected 
                                                ? "bg-slate-50 text-[#0A1931]" 
                                                : "bg-transparent text-slate-600 hover:bg-slate-50 hover:text-slate-900 active:bg-slate-100"
                                        }`}
                                    >
                                        <div className="flex items-center gap-3 min-w-0">
                                            {OptionIcon && <OptionIcon size={16} className={`shrink-0 transition-colors ${isSelected ? 'text-[#0A1931]' : 'text-slate-400 group-hover:text-slate-500'}`} />}
                                            <span className={`text-sm truncate ${isSelected ? 'font-semibold' : 'font-medium'}`} title={o.label}>{o.label}</span>
                                        </div>
                                        {isSelected && <Check size={16} strokeWidth={2.5} className="text-blue-600 shrink-0 ml-2" />}
                                    </button>
                                    {idx === 0 && filteredOptions.length > 1 && (
                                        <div className="my-1 border-t border-slate-100/80" />
                                    )}
                                </div>
                            );
                        }) : (
                            <div className="px-4 py-3 text-xs text-center text-slate-400 font-medium">Bulunamadı</div>
                        )}
                    </div>
                </div>
            )}
        </div>
    );
}

export default function UsersPage() {
    const { success, error: toastError } = useToast();
    const { token, currentTenantId: tenantId, user: currentUser } = useAuth();
    const [users, setUsers] = useState<User[]>([]);
    const [loading, setLoading] = useState(true);
    const [groupOptions, setGroupOptions] = useState<string[]>([]);
    const isAssistant = currentUser?.role === "Assistant" || currentUser?.tenants?.find((t: any) => t.tenantId === tenantId)?.role === "Assistant";

    const canEditUser = (u: User) => {
        if (isAssistant && ["Admin", "SuperAdmin", "Accountant"].includes(u.role)) {
            return false;
        }
        return true;
    };

    // ── Fetch users from API ──
    const fetchUsers = async () => {
        if (!token || !tenantId) return;
        try {
            setLoading(true);
            const result = await userApi.list(token, tenantId, { pageSize: 10000 });
            setUsers(result.items.map(mapApiUser));
        } catch (e) { console.error('Failed to fetch users:', e); }
        finally { setLoading(false); }
    };

    useEffect(() => { fetchUsers(); }, [token, tenantId]);
    useEffect(() => {
        if (!token || !tenantId) return;
        groupsApi.list(token, tenantId, { pageSize: 1000 })
            .then(res => setGroupOptions(res.items.map((g: any) => g.name)))
            .catch(() => {});
    }, [token, tenantId]);
    const searchParams = useSearchParams();
    const [search, setSearch] = useState(""); const [roleFilter, setRoleFilter] = useState("all"); const [statusFilter, setStatusFilter] = useState("all"); const [groupFilter, setGroupFilter] = useState("all");
    const [page, setPage] = useState(1); const [perPage, setPerPage] = useState(10); const [selected, setSelected] = useState<Set<string>>(new Set());
    const [showAddModal, setShowAddModal] = useState(false); const [editUser, setEditUser] = useState<User | null>(null);
    const [detailUser, setDetailUser] = useState<User | null>(null);
    const [deleteTarget, setDeleteTarget] = useState<string | null>(null); const [bulkDeleteOpen, setBulkDeleteOpen] = useState(false);
    const [sortField, setSortField] = useState<SortField>("name"); const [sortDir, setSortDir] = useState<SortDir>("asc");

    useEffect(() => {
        const urlUserId = searchParams?.get("userId");
        if (!urlUserId || !token || !tenantId) return;

        const targetUser = users.find(u => u.id === urlUserId);
        if (targetUser) {
            setDetailUser(targetUser);
            return;
        }

        userApi.get(token, tenantId, urlUserId)
            .then(res => {
                setDetailUser(mapApiUser(res));
            })
            .catch(err => {
                console.error("Kullanıcı ID ile çekilemedi:", err);
            });
    }, [searchParams, users, token, tenantId]);

    const handleCloseDetail = () => {
        setDetailUser(null);
        if (typeof window !== "undefined") {
            const url = new URL(window.location.href);
            url.searchParams.delete("userId");
            window.history.pushState({}, "", url.toString());
        }
    };

    const filtered = useMemo(() => {
        let r = users.filter(u => {
            const ms = !search || `${u.firstName} ${u.lastName}`.toLocaleLowerCase('tr-TR').includes(search.toLocaleLowerCase('tr-TR')) || u.email.toLocaleLowerCase('tr-TR').includes(search.toLocaleLowerCase('tr-TR')) || (u.phone && u.phone.includes(search));
            const mr = roleFilter === "all" || u.role === roleFilter || roleLabel[u.role] === roleLabel[roleFilter];
            const md = statusFilter === "all" || (statusFilter === "active" && u.isActive) || (statusFilter === "inactive" && !u.isActive) || (statusFilter === "demo" && u.studentType === "Demo");
            const mg = groupFilter === "all" || (u.groupNames && u.groupNames.includes(groupFilter));
            return ms && mr && md && mg;
        });
        r.sort((a, b) => { 
            let c = 0; 
            switch (sortField) { 
                case "name": 
                    c = `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`, "tr-TR"); 
                    break; 
                case "role": 
                    const rankA = roleRank[a.role] ?? 0;
                    const rankB = roleRank[b.role] ?? 0;
                    c = rankA - rankB;
                    if (c === 0) {
                        c = `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`, "tr-TR");
                    }
                    break; 
                case "group": 
                    c = a.groupNames.length - b.groupNames.length; 
                    if (c === 0) c = (a.groupNames[0] || "zzz").localeCompare(b.groupNames[0] || "zzz", "tr-TR"); 
                    break; 
                case "status": 
                    c = Number(b.isActive) - Number(a.isActive); 
                    break; 
                case "phone": 
                    c = (a.phone || "").localeCompare(b.phone || "", "tr-TR"); 
                    break; 
                case "createdAt": 
                    c = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime(); 
                    break; 
                case "lastLogin": 
                    c = new Date(a.lastLoginAt || 0).getTime() - new Date(b.lastLoginAt || 0).getTime(); 
                    break; 
                default: 
                    c = 0; 
            } 
            return sortDir === "desc" ? -c : c; 
        });
        return r;
    }, [users, search, roleFilter, statusFilter, groupFilter, sortField, sortDir]);

    const totalPages = Math.ceil(filtered.length / perPage);
    const paginated = filtered.slice((page - 1) * perPage, page * perPage);
    const stats = { total: users.length, active: users.filter(u => (u.role === "Student" || u.role === "Öğrenci") && u.studentType !== "Demo" && u.isActive).length, students: users.filter(u => u.role === "Student" || u.role === "Öğrenci").length, demo: users.filter(u => u.studentType === "Demo").length };

    const toggleSort = (f: SortField) => { if (sortField === f) setSortDir(d => d === "asc" ? "desc" : "asc"); else { setSortField(f); setSortDir("asc"); } };
    const SI = ({ f }: { f: SortField }) => sortField !== f ? <ArrowUpDown size={12} className="text-[#A0AEC0] ml-1" /> : sortDir === "asc" ? <ArrowUp size={12} className="text-[#0A1931] ml-1" /> : <ArrowDown size={12} className="text-[#0A1931] ml-1" />;
    const toggleSel = (id: string) => { const n = new Set(selected); n.has(id) ? n.delete(id) : n.add(id); setSelected(n); };
    const selAll = () => { selected.size === paginated.length ? setSelected(new Set()) : setSelected(new Set(paginated.map(u => u.id))); };
    const del = async (id: string) => {
        if (!token || !tenantId) return;
        try { await userApi.delete(token, tenantId, id); setUsers(p => p.filter(u => u.id !== id)); setDeleteTarget(null); success("Kullanıcı Silindi"); }
        catch (e: any) { toastError("Hata", e.message || "Silme başarısız"); setDeleteTarget(null); }
    };
    const bulkDel = async () => {
        if (!token || !tenantId) return;
        try { await userApi.bulkDelete(token, tenantId, Array.from(selected)); setUsers(p => p.filter(u => !selected.has(u.id))); success(`${selected.size} Kullanıcı Silindi`); }
        catch (e: any) { toastError("Hata", e.message || "Toplu silme başarısız"); }
        finally { setSelected(new Set()); setBulkDeleteOpen(false); }
    };
    const ini = (u: User) => `${(u.firstName?.[0] || "")}${(u.lastName?.[0] || "")}`.toUpperCase();
    const toggleActive = async (id: string) => {
        if (!token || !tenantId) return;
        const u = users.find(x => x.id === id); if (!u) return;
        try { await userApi.update(token, tenantId, id, { isActive: !u.isActive }); setUsers(p => p.map(x => x.id === id ? { ...x, isActive: !x.isActive } : x)); success(u.isActive ? "Hesap Pasife Alındı" : "Hesap Aktif Edildi"); if (detailUser?.id === id) setDetailUser(p => p ? { ...p, isActive: !p.isActive } : null); }
        catch (e: any) { toastError("Hata", e.message || "Güncelleme başarısız"); }
    };
    const changeRole = async (id: string, role: string) => {
        if (!token || !tenantId) return;
        try { await userApi.update(token, tenantId, id, { role }); setUsers(p => p.map(u => u.id === id ? { ...u, role } : u)); success("Rol Güncellendi"); if (detailUser?.id === id) setDetailUser(p => p ? { ...p, role } : null); }
        catch (e: any) { toastError("Hata", e.message || "Rol güncellenemedi"); }
    };
    const saveUser = async (d: Partial<User> & { password?: string }) => {
        if (!token || !tenantId) return;
        try {
            // Backend expects "Active" or "Demo", UI uses "Aktif" or "Demo"
            const apiStudentType = d.studentType === "Aktif" ? "Active" : d.studentType === "Pasif" ? "Passive" : d.studentType;

            if (editUser) {
                const updatePayload: any = { 
                    firstName: d.firstName, 
                    lastName: d.lastName, 
                    email: d.email, 
                    role: d.role, 
                    phone: d.phone,
                    studentType: apiStudentType,
                    tcNo: d.tcNo
                };
                if (d.password) {
                    updatePayload.password = d.password;
                }
                await userApi.update(token, tenantId, editUser.id, updatePayload);
                setUsers(p => p.map(u => u.id === editUser.id ? { ...u, ...d, studentType: apiStudentType as any } : u)); 
                success("Güncellendi"); 
                setEditUser(null);
                setShowAddModal(false);
            } else {
                let targetPassword = d.password;
                if (!targetPassword && (d.role === "Student" || d.role === "Öğrenci")) {
                    targetPassword = generateDefaultPassword(d.firstName, d.lastName, d.phone);
                } else if (!targetPassword) {
                    targetPassword = "123456";
                }

                const created = await userApi.create(token, tenantId, { 
                    firstName: d.firstName || "", 
                    lastName: d.lastName || "", 
                    email: d.email || "", 
                    password: targetPassword, 
                    role: d.role || "Student", 
                    studentType: apiStudentType || undefined, 
                    phone: d.phone,
                    tcNo: d.tcNo
                } as any);
                setUsers(p => [mapApiUser(created), ...p]); 
                success("Kullanıcı Eklendi"); 
                setShowAddModal(false);
            }
        } catch (e: any) { toastError("Hata", e.message || "İşlem başarısız"); }
    };

    const exportExcel = async () => {
        if (token && tenantId) {
            try {
                // Sadece seçili olanları veya filtredeki herkesi indir
                const idsToExport = selected.size > 0 
                    ? Array.from(selected) 
                    : filtered.map(u => u.id);

                await userApi.exportExcel(token, tenantId, idsToExport);
                success('İndirildi', 'Kullanıcılar Excel olarak indirildi.');
            } catch {
                toastError('Hata', 'İndirme sırasında bir hata oluştu.');
            }
        }
    };

    const downloadTemplate = async () => {
        if (token && tenantId) {
            try {
                await userApi.exportTemplate(token, tenantId);
                success('Şablon İndirildi', 'Öğrenci yükleme şablonu başarıyla indirildi.');
            } catch {
                toastError('Hata', 'Şablon indirme sırasında bir hata oluştu.');
            }
        }
    };

    // ── Bulk Upload ──
    const [bulkModalOpen, setBulkModalOpen] = useState(false);
    const [bulkFile, setBulkFile] = useState<File | null>(null);
    const [bulkLoading, setBulkLoading] = useState(false);
    const [bulkDragging, setBulkDragging] = useState(false);
    const [bulkResult, setBulkResult] = useState<{ ok: number; fail: number; details?: { firstName: string, lastName: string, email: string, status: string, reason: string }[] } | null>(null);

    // ── Quick Password Reset ──
    const [resetPwModalOpen, setResetPwModalOpen] = useState<{id: string, name: string} | null>(null);
    const [manualPw, setManualPw] = useState('');
    const [resetPwLoading, setResetPwLoading] = useState(false);
    const [showQuickPw, setShowQuickPw] = useState(false);

    const handleQuickReset = (u: any) => {
        setResetPwModalOpen({ id: u.id, name: `${u.firstName} ${u.lastName}` });
        setManualPw('');
        setShowQuickPw(false);
    };

    const handleSaveNewPassword = async () => {
        if (!token || !tenantId || !resetPwModalOpen || !manualPw) return;
        setResetPwLoading(true);
        try {
            await userApi.update(token, tenantId, resetPwModalOpen.id, { password: manualPw });
            success("Şifre başarıyla yenilendi");
            if (detailUser?.id === resetPwModalOpen.id) {
                setDetailUser(p => p ? { ...p, password: manualPw } : null);
            }
            setResetPwModalOpen(null);
        } catch {
            toastError("Hata", "Şifre sıfırlanamadı");
        } finally {
            setResetPwLoading(false);
        }
    };

    const handleBulkFile = (file: File) => {
        setBulkFile(file);
        setBulkResult(null);
    };

    const handleBulkUpload = async () => {
        if (!token || !tenantId || !bulkFile) return;
        setBulkLoading(true);
        try {
            const result = await userApi.importExcel(token, tenantId, bulkFile);
            setBulkResult({ ok: result.importedCount, fail: result.skippedCount || 0, details: result.details });
            success('Toplu Yükleme Tamamlandı', result.message);
        } catch (e: any) {
            setBulkResult({ ok: 0, fail: 1, details: [] });
        } finally { setBulkLoading(false); }
    };

    const passwordResetModalNode = resetPwModalOpen ? (
        <div className="fixed inset-0 z-[110] flex items-center justify-center p-4">
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setResetPwModalOpen(null)} />
            <div className="relative w-full max-w-sm bg-white rounded-3xl shadow-2xl p-7 text-center animate-fade-in">
                <div className="absolute top-5 right-5">
                    <button onClick={() => setResetPwModalOpen(null)} className="p-1.5 rounded-xl hover:bg-[#E2E8F0]/50 text-[#A0AEC0] transition-colors"><X size={18} /></button>
                </div>
                <div className="w-14 h-14 bg-blue-50 text-blue-600 rounded-2xl flex items-center justify-center mx-auto mb-5 ring-4 ring-blue-50/50 shadow-inner">
                    <KeyRound size={24} />
                </div>
                <h3 className="text-xl font-extrabold text-[#0A1931] mb-2 tracking-tight">Şifre Belirle</h3>
                <p className="text-xs text-[#64748B] mb-6">
                    <strong className="text-[#1B3B6F]">{resetPwModalOpen.name}</strong> adlı kullanıcı için yeni şifreyi aşağıya giriniz:
                </p>
                <div className="relative mb-8 text-left">
                    <Lock size={16} className="absolute left-4 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                    <input 
                        type={showQuickPw ? "text" : "password"} 
                        value={manualPw} 
                        onChange={e => setManualPw(e.target.value)}
                        placeholder="Yeni şifre..."
                        autoFocus
                        autoComplete="new-password"
                        className="w-full pl-11 pr-11 py-3.5 text-sm font-bold bg-[#F8FAFC] border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0] transition-all shadow-inner"
                    />
                    <button type="button" onClick={() => setShowQuickPw(!showQuickPw)} className="absolute right-4 top-1/2 -translate-y-1/2 text-[#A0AEC0] hover:text-[#0A1931] transition-colors">
                        {showQuickPw ? <EyeOff size={16} /> : <Eye size={16} />}
                    </button>
                </div>
                <div className="flex gap-3">
                    <button onClick={() => setResetPwModalOpen(null)} className="flex-1 py-3.5 bg-white border border-[#E2E8F0] text-[#475569] text-xs font-bold rounded-xl hover:bg-[#F8FAFC] transition-colors shadow-sm">
                        İptal
                    </button>
                    <button onClick={handleSaveNewPassword} disabled={!manualPw || resetPwLoading} className="flex-1 py-3.5 bg-[#0A1931] text-white text-xs font-bold rounded-xl hover:bg-[#1B3B6F] transition-all disabled:opacity-50 active:scale-[0.98] shadow-lg shadow-black/10">
                        {resetPwLoading ? 'Kaydediliyor...' : 'Kaydet'}
                    </button>
                </div>
            </div>
        </div>
    ) : null;

    // ── DETAIL VIEW ──
    if (detailUser) {
        return (
            <>
                <UserDetail 
                    user={detailUser}
                    onBack={handleCloseDetail}
                    onToggleActive={canEditUser(detailUser) ? toggleActive : undefined}
                    onChangeRole={canEditUser(detailUser) ? changeRole : undefined}
                    onDelete={canEditUser(detailUser) ? async (id) => { await del(id); handleCloseDetail(); } : undefined}
                    onQuickReset={canEditUser(detailUser) ? handleQuickReset : undefined}
                    onEdit={canEditUser(detailUser) ? (u) => { setEditUser(u); setShowAddModal(true); } : undefined}
                    canEdit={canEditUser(detailUser)}
                />
                {passwordResetModalNode}
                {showAddModal && <UserFormModal user={editUser} onClose={() => { setShowAddModal(false); setEditUser(null); }} onSave={saveUser} />}
            </>
        );
    }


    // ── LIST VIEW ──
    return (
        <div className="space-y-6 pb-24 lg:pb-0">
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-2xl sm:text-3xl font-bold text-[#0A1931] tracking-tight flex items-center gap-3">Kullanıcılar</h1>
                    <p className="text-xs sm:text-sm font-semibold uppercase tracking-widest text-[#A9A9A9] mt-1 opacity-60">Kullanıcı ve Rol Yönetimi</p>
                </div>
                <div className="flex flex-row items-center gap-2 sm:gap-3 overflow-x-auto pb-2 snap-x snap-mandatory hide-scrollbar">
                    <button className="shrink-0 snap-start px-4 py-2.5 text-xs sm:text-sm font-bold bg-white text-[#1B3B6F] border border-[#E2E8F0] rounded-xl hover:bg-[#E2E8F0]/20 transition-all flex items-center justify-center gap-2 shadow-sm" onClick={downloadTemplate}><Download size={16} className="shrink-0" /> Şablon</button>
                    <Tooltip content="Excel olarak indir">
                        <button onClick={exportExcel} className="shrink-0 snap-start px-4 py-2.5 text-xs sm:text-sm font-bold bg-white text-emerald-600 border border-emerald-200 rounded-xl hover:bg-emerald-50 transition-all flex items-center justify-center gap-2 shadow-sm">
                            <Download size={16} className="shrink-0" /> İndir
                        </button>
                    </Tooltip>
                    <button onClick={() => { setBulkModalOpen(true); setBulkFile(null); setBulkResult(null); }} className="shrink-0 snap-start px-4 py-2.5 text-xs sm:text-sm font-bold bg-white text-[#1B3B6F] border border-[#E2E8F0] rounded-xl hover:bg-[#E2E8F0]/20 transition-all flex items-center justify-center gap-2 shadow-sm"><Upload size={16} className="shrink-0" /> Toplu Ekle</button>
                    <button onClick={() => { setEditUser(null); setShowAddModal(true); }} className="shrink-0 snap-start px-6 py-2.5 text-sm sm:text-base font-bold bg-[#0A1931] text-white rounded-xl hover:bg-[#1B3B6F] transition-all flex items-center justify-center gap-2 shadow-xl shadow-[#0A1931]/20"><UserPlus size={18} className="shrink-0" /> Yeni Kullanıcı</button>
                </div>
            </div>
            <KpiGrid 
                items={[
                    { label: "Toplam Kullanıcı", value: stats.total, icon: Users, colorClass: "text-indigo-600", bgClass: "bg-indigo-50" },
                    { label: "Aktif Öğrenci", value: stats.active, icon: UserCheck, colorClass: "text-emerald-600", bgClass: "bg-emerald-50" },
                    { label: "Öğrenci", value: stats.students, icon: GraduationCap, colorClass: "text-blue-600", bgClass: "bg-blue-50" },
                    { label: "Demo", value: stats.demo, icon: CalendarIcon, colorClass: "text-amber-600", bgClass: "bg-amber-50" }
                ]}
                className="flex xl:grid xl:grid-cols-4 gap-4 overflow-x-auto pb-2 snap-x snap-mandatory hide-scrollbar"
            />
            <div className="bg-white rounded-2xl border border-[#E2E8F0]/60 p-4 flex flex-col lg:flex-row lg:items-center gap-3 sm:gap-4">
                <div className="w-full lg:flex-1 relative">
                    <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                    <input type="text" placeholder="İsim, kullanıcı adı veya telefon ile ara..." value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} className="w-full pl-9 pr-4 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0] transition-all" />
                </div>
                <div className="flex flex-col sm:flex-row flex-wrap gap-3 w-full lg:w-auto pb-1 sm:pb-0">
                    <FilterDropdown 
                        value={groupFilter} 
                        onChange={v => { setGroupFilter(v); setPage(1); }}
                        icon={Users}
                        searchable={true}
                        widthClass="w-full lg:w-64"
                        options={[
                            { value: "all", label: "Tüm Gruplar", icon: Users },
                            ...groupOptions.map(g => ({ value: g, label: g, icon: BookOpen }))
                        ]}
                    />
                    <FilterDropdown 
                        value={roleFilter} 
                        onChange={v => { setRoleFilter(v); setPage(1); }}
                        icon={Shield}
                        options={[
                            { value: "all", label: "Tüm Roller", icon: Shield },
                            { value: "Admin", label: "Admin", icon: Shield },
                            { value: "Eğitmen", label: "Eğitmen", icon: Briefcase },
                            { value: "Öğrenci", label: "Öğrenci", icon: GraduationCap },
                            { value: "Muhasebe", label: "Muhasebe", icon: CreditCard },
                            { value: "Asistan", label: "Asistan", icon: UserCheck }
                        ]}
                    />
                    <FilterDropdown 
                        value={statusFilter} 
                        onChange={v => { setStatusFilter(v); setPage(1); }}
                        icon={Activity}
                        options={[
                            { value: "all", label: "Tüm Durum", icon: Activity },
                            { value: "active", label: "Aktif", icon: ToggleRight },
                            { value: "inactive", label: "Pasif", icon: ToggleLeft },
                            { value: "demo", label: "Demo", icon: CalendarIcon }
                        ]}
                    />
                </div>
                {selected.size > 0 && <button onClick={() => setBulkDeleteOpen(true)} className="w-full lg:w-auto px-4 py-2.5 text-sm font-medium bg-red-50 text-red-600 rounded-xl hover:bg-red-100 flex items-center justify-center gap-2"><Trash2 size={16} /> {selected.size} Seçiliyi Sil</button>}
            </div>
            <div className="bg-white rounded-xl border border-[#E2E8F0]/60 overflow-hidden">
                {/* ── DESKTOP TABLE ── */}
                <div className="hidden lg:block overflow-x-auto min-h-[220px]">
                    <table className="w-full min-w-[900px]"><thead><tr className="border-b border-[#A0AEC0]/40 bg-[#E2E8F0]/50">
                        <th className="w-10 px-4 py-4"><input type="checkbox" checked={selected.size === paginated.length && paginated.length > 0} onChange={selAll} className="w-4 h-4 rounded border-[#A0AEC0] text-[#1B3B6F]" /></th>
                        <th className="text-left px-4 py-4"><button onClick={() => toggleSort("name")} className="flex items-center text-sm font-extrabold text-[#0A1931] uppercase tracking-widest hover:text-[#1B3B6F]">Kullanıcı<SI f="name" /></button></th>
                        <th className="text-left px-4 py-4"><button onClick={() => toggleSort("role")} className="flex items-center text-sm font-extrabold text-[#0A1931] uppercase tracking-widest hover:text-[#1B3B6F]">Rol<SI f="role" /></button></th>
                        <th className="text-left px-4 py-4"><button onClick={() => toggleSort("group")} className="flex items-center text-sm font-extrabold text-[#0A1931] uppercase tracking-widest hover:text-[#1B3B6F]">Grup<SI f="group" /></button></th>
                        <th className="text-left px-4 py-4"><button onClick={() => toggleSort("status")} className="flex items-center text-sm font-extrabold text-[#0A1931] uppercase tracking-widest hover:text-[#1B3B6F]">Durum<SI f="status" /></button></th>
                        <th className="text-left px-4 py-4"><button onClick={() => toggleSort("phone")} className="flex items-center text-sm font-extrabold text-[#0A1931] uppercase tracking-widest hover:text-[#1B3B6F]">Telefon<SI f="phone" /></button></th>
                        <th className="text-left px-4 py-4"><button onClick={() => toggleSort("createdAt")} className="flex items-center text-sm font-extrabold text-[#0A1931] uppercase tracking-widest hover:text-[#1B3B6F]">Kayıt Tarihi<SI f="createdAt" /></button></th>
                        <th className="text-left px-4 py-4"><button onClick={() => toggleSort("lastLogin")} className="flex items-center text-sm font-extrabold text-[#0A1931] uppercase tracking-widest hover:text-[#1B3B6F]">Son Giriş<SI f="lastLogin" /></button></th>
                        <th className="w-36 text-center text-sm font-extrabold text-[#0A1931] uppercase tracking-widest px-4 py-4">Hızlı İşlemler</th>
                    </tr></thead><tbody>
                            {paginated.map((u, idx) => {
                                const c = rc[u.role] || _default;
                                const rowBg = selected.has(u.id) ? "bg-[#E2E8F0]/20" : idx % 2 === 0 ? "bg-white" : "bg-[#F0F4F8]";
                                return (
                                    <tr key={u.id} onClick={() => setDetailUser(u)} className={`border-b border-[#E2E8F0]/40 hover:bg-[#DCE5EF] transition-colors cursor-pointer ${rowBg}`}>
                                        <td className="px-4 py-3" onClick={e => e.stopPropagation()}><input type="checkbox" checked={selected.has(u.id)} onChange={() => toggleSel(u.id)} className="w-4 h-4 rounded border-[#A0AEC0] text-[#0A1931]" /></td>
                                        <td className="px-4 py-3"><div className="flex items-center gap-3"><div className={`w-10 h-10 rounded-xl ${c.avatar} flex items-center justify-center text-white text-[10px] font-bold shadow-sm border border-white/10`}>{ini(u)}</div><div><p className="text-sm font-bold text-[#0A1931] tracking-tight">{u.firstName} {u.lastName}</p><p className="text-[11px] text-[#A0AEC0] font-medium">{u.username || u.email}</p></div></div></td>
                                        <td className="px-4 py-3"><span className={`inline-flex items-center gap-1 text-[10px] font-bold uppercase tracking-widest px-2.5 py-1 rounded-lg ${c.bg} ${c.text}`}>{roleLabel[u.role] || u.role}</span></td>
                                        <td className="px-4 py-3" onClick={e => e.stopPropagation()}>{u.groupNames.length > 0 ? <div className="relative group/grp inline-flex"><span className="inline-flex items-center gap-1.5 text-[11px] font-bold px-3 py-1 rounded-lg bg-[#1B3B6F]/10 text-[#1B3B6F] border border-[#1B3B6F]/20 cursor-default"><Users size={12} className="text-[#A0AEC0]" />{u.groupNames.length}</span><div className="absolute left-0 top-full mt-1 z-50 hidden group-hover/grp:block"><div className="bg-[#0A1931] text-white rounded-xl shadow-2xl p-3 min-w-[160px] border border-[#1B3B6F]/30"><p className="text-[10px] font-bold uppercase tracking-wider text-[#A0AEC0] mb-2">Gruplar</p><div className="space-y-1">{u.groupNames.map(g => <div key={g} className="text-[11px] font-medium px-2 py-1 rounded-lg bg-white/10">{g}</div>)}</div></div></div></div> : <span className="text-[11px] text-[#A0AEC0]">—</span>}</td>
                                        <td className="px-4 py-3"><span className={`inline-flex items-center gap-1.5 text-[11px] font-bold ${u.isActive ? "text-emerald-600" : "text-[#A0AEC0]"}`}><span className={`w-1.5 h-1.5 rounded-full ${u.isActive ? "bg-emerald-500" : "bg-[#A0AEC0]"}`} />{u.isActive ? "AKTİF" : "PASİF"}{u.studentType === "Demo" && <span className="text-[10px] px-1.5 py-0.5 rounded-lg bg-amber-50 text-amber-600 font-bold ml-2">DEMO</span>}{u.studentType === "Passive" && <span className="text-[10px] px-1.5 py-0.5 rounded-lg bg-[#F0F4F8] text-[#A0AEC0] font-bold ml-2">PASİF</span>}</span></td>
                                        <td className="px-4 py-3" onClick={e => e.stopPropagation()}>{u.phone ? <div className="flex items-center gap-2"><a href={`tel:${u.phone.replace(/\s/g, '')}`} className="text-[11px] font-medium text-[#1B3B6F] hover:text-[#0A1931] flex items-center gap-1.5 transition-colors"><Phone size={12} className="text-[#A0AEC0]" />{u.phone}</a><Tooltip content="WhatsApp"><a href={`https://wa.me/${u.phone.replace(/\s/g, '').replace(/^\+?0?/, '+90')}`} target="_blank" rel="noopener noreferrer" className="p-1.5 rounded-lg bg-[#25D366]/10 hover:bg-[#25D366]/20 transition-all hover:scale-110 flex"><svg viewBox="0 0 24 24" width="14" height="14" fill="#25D366"><path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413z" /></svg></a></Tooltip></div> : <span className="text-[11px] text-[#A0AEC0]">—</span>}</td>
                                        <td className="px-4 py-3"><span className="text-[11px] font-medium text-[#1B3B6F] flex items-center gap-1.5"><CalendarIcon size={12} className="text-[#A0AEC0]" />{new Date(u.createdAt).toLocaleDateString("tr-TR")}</span></td>
                                        <td className="px-4 py-3"><span className="text-[11px] font-medium text-[#A0AEC0] flex items-center gap-1.5"><Clock size={12} />{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString("tr-TR") : "—"}</span></td>
                                        <td className="px-4 py-3" onClick={e => e.stopPropagation()}>
                                            <div className="flex items-center justify-center gap-2">
                                                <Tooltip content="Detay"><button onClick={() => setDetailUser(u)} className="p-2 rounded-lg hover:bg-[#E2E8F0]/30 text-[#A0AEC0] hover:text-[#1B3B6F]"><Eye size={18} /></button></Tooltip>
                                                {canEditUser(u) ? (
                                                    <>
                                                        <Tooltip content="Düzenle"><button onClick={() => { setEditUser(u); setShowAddModal(true); }} className="p-2 rounded-lg hover:bg-amber-50 text-[#A0AEC0] hover:text-amber-600"><Edit3 size={18} /></button></Tooltip>
                                                        <Tooltip content={u.isActive ? "Pasife Al" : "Aktif Et"}><button onClick={() => toggleActive(u.id)} className={`p-2 rounded-lg ${u.isActive ? "hover:bg-orange-50 text-[#A0AEC0] hover:text-orange-500" : "hover:bg-emerald-50 text-[#A0AEC0] hover:text-emerald-500"}`}>{u.isActive ? <ToggleRight size={18} /> : <ToggleLeft size={18} />}</button></Tooltip>
                                                        <Tooltip content="Sil"><button onClick={() => setDeleteTarget(u.id)} className="p-2 rounded-lg hover:bg-red-50 text-[#A0AEC0] hover:text-red-600"><Trash2 size={18} /></button></Tooltip>
                                                    </>
                                                ) : (
                                                    <div className="text-[10px] text-[#A0AEC0] font-bold uppercase tracking-widest px-2 opacity-50"><Shield size={14} /></div>
                                                )}
                                            </div>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody></table>
                </div>

                {/* ── PREMIUM MOBILE CARDS VIEW ── */}
                <div className="lg:hidden flex flex-col gap-4">
                    <div className="px-4 py-3 bg-white rounded-xl border border-[#E2E8F0]/60 shadow-sm flex items-center justify-between">
                        <label className="flex items-center gap-3 cursor-pointer">
                            <input type="checkbox" checked={selected.size === paginated.length && paginated.length > 0} onChange={selAll} className="w-4 h-4 rounded border-[#A0AEC0] text-[#1B3B6F] focus:ring-[#1B3B6F]" />
                            <span className="text-[11px] font-extrabold text-[#0A1931] uppercase tracking-widest">Tümünü Seç ({paginated.length})</span>
                        </label>
                    </div>
                    {paginated.map((u) => {
                        const c = rc[u.role] || _default;
                        const isSelected = selected.has(u.id);
                        return (
                            <div key={u.id} className={`relative p-4 rounded-xl border transition-all duration-300 shadow-sm ${isSelected ? "bg-blue-50/50 border-blue-300 ring-1 ring-blue-500/20" : "bg-white border-[#E2E8F0]/60 hover:shadow-md"}`}>
                                <div className="absolute top-4 right-4 z-10">
                                    <input type="checkbox" checked={isSelected} onChange={() => toggleSel(u.id)} className="w-4 h-4 rounded border-[#A0AEC0] text-[#0A1931] focus:ring-[#0A1931]" />
                                </div>
                                <div className="flex flex-col gap-3" onClick={() => setDetailUser(u)}>
                                    <div className="flex items-start justify-between">
                                        <div className="flex items-center gap-3 pr-8">
                                            <div className={`w-12 h-12 rounded-full ${c.avatar} flex items-center justify-center text-white text-sm font-bold shadow-sm shrink-0 border border-white`}>
                                                {ini(u)}
                                            </div>
                                            <div className="flex-1 min-w-0">
                                                <p className="text-sm font-bold text-[#0A1931] tracking-tight truncate">{u.firstName} {u.lastName}</p>
                                                <p className="text-[11px] text-[#A0AEC0] font-medium truncate mt-0.5">{u.username || u.email}</p>
                                                <div className="flex items-center gap-2 mt-1 text-[10px]">
                                                    <span className={`font-bold uppercase tracking-widest ${c.text}`}>{roleLabel[u.role] || u.role}</span>
                                                    <span className="text-[#E2E8F0]">•</span>
                                                    <span className={`font-bold ${u.isActive ? "text-emerald-600" : "text-[#A0AEC0]"}`}>{u.isActive ? "AKTİF" : "PASİF"}</span>
                                                    {u.studentType === "Demo" && (
                                                        <>
                                                            <span className="text-[#E2E8F0]">•</span>
                                                            <span className="font-bold text-amber-600 uppercase tracking-widest">DEMO</span>
                                                        </>
                                                    )}
                                                    {u.studentType === "Passive" && (
                                                        <>
                                                            <span className="text-[#E2E8F0]">•</span>
                                                            <span className="font-bold text-[#A0AEC0] uppercase tracking-widest">PASİF</span>
                                                        </>
                                                    )}
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                    <div className="flex items-center justify-between mt-1 pt-3 border-t border-[#E2E8F0]/60" onClick={e => e.stopPropagation()}>
                                        <button onClick={() => setDetailUser(u)} className="text-[11px] font-bold text-[#1B3B6F] hover:text-[#0A1931] transition-colors flex items-center gap-1">
                                            <Eye size={14} /> Profili İncele
                                        </button>
                                        <div className="flex items-center gap-1">
                                            {u.phone && (
                                                <a href={`https://wa.me/${u.phone.replace(/\s/g, '').replace(/^\+?0?/, '+90')}`} target="_blank" rel="noopener noreferrer" className="w-8 h-8 rounded-lg bg-[#25D366]/10 text-[#25D366] flex items-center justify-center hover:bg-[#25D366]/20 transition-colors mr-1">
                                                    <MessageCircle size={14} />
                                                </a>
                                            )}
                                            {canEditUser(u) && (
                                                <>
                                                    <button onClick={() => { setEditUser(u); setShowAddModal(true); }} className="w-8 h-8 rounded-lg bg-[#E2E8F0]/30 text-[#A0AEC0] flex items-center justify-center hover:bg-amber-50 hover:text-amber-600 transition-colors"><Edit3 size={14} /></button>
                                                    <button onClick={() => toggleActive(u.id)} className={`w-8 h-8 rounded-lg bg-[#E2E8F0]/30 text-[#A0AEC0] flex items-center justify-center transition-colors ${u.isActive ? "hover:bg-orange-50 hover:text-orange-500" : "hover:bg-emerald-50 hover:text-emerald-500"}`}>{u.isActive ? <ToggleRight size={14} /> : <ToggleLeft size={14} />}</button>
                                                    <button onClick={() => setDeleteTarget(u.id)} className="w-8 h-8 rounded-lg bg-[#E2E8F0]/30 text-[#A0AEC0] flex items-center justify-center hover:bg-red-50 hover:text-red-600 transition-colors"><Trash2 size={14} /></button>
                                                </>
                                            )}
                                        </div>
                                    </div>
                                </div>
                            </div>
                        );
                    })}
                </div>
                <div className="flex items-center justify-between px-6 py-4 border-t border-[#E2E8F0]/60 bg-[#E2E8F0]/15">
                    <div className="flex items-center gap-4">
                        <p className="text-[11px] font-bold text-[#A0AEC0] uppercase tracking-widest">{filtered.length} Kayıt · {filtered.length > 0 ? `${(page - 1) * perPage + 1}–${Math.min(page * perPage, filtered.length)}` : '0'}</p>
                        <div className="flex items-center gap-1.5">
                            <span className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-wider">Göster:</span>
                            {PER_PAGE_OPTIONS.map(opt => (
                                <button key={opt} onClick={() => { setPerPage(opt); setPage(1); }} className={`px-2.5 py-1 rounded-lg text-[11px] font-bold transition-all ${perPage === opt ? 'bg-[#0A1931] text-white shadow-md' : 'bg-white text-[#A9A9A9] border border-[#E2E8F0] hover:border-[#A0AEC0] hover:text-[#1B3B6F]'}`}>{opt}</button>
                            ))}
                        </div>
                    </div>
                    {totalPages > 1 && <div className="flex items-center gap-1.5"><button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} className="p-2 rounded-xl hover:bg-white text-[#A0AEC0] disabled:opacity-30 border border-transparent hover:border-[#E2E8F0] transition-all"><ChevronLeft size={16} /></button>{Array.from({ length: Math.min(totalPages, 7) }, (_, i) => { if (totalPages <= 7) return i + 1; if (page <= 4) return i + 1; if (page >= totalPages - 3) return totalPages - 6 + i; return page - 3 + i; }).map(p => <button key={p} onClick={() => setPage(p)} className={`w-9 h-9 rounded-xl text-xs font-bold transition-all ${p === page ? "bg-[#0A1931] text-white shadow-lg shadow-black/10" : "hover:bg-white text-[#A9A9A9] border border-transparent hover:border-[#E2E8F0]"}`}>{p}</button>)}<button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages} className="p-2 rounded-xl hover:bg-white text-[#A0AEC0] disabled:opacity-30 border border-transparent hover:border-[#E2E8F0] transition-all"><ChevronRight size={16} /></button></div>}
                </div>
                {paginated.length === 0 && <div className="flex flex-col items-center justify-center py-16 text-[#A0AEC0]"><Users size={40} className="mb-3 opacity-30" /><p className="text-sm font-medium">Kullanıcı bulunamadı</p></div>}
            </div>
            {showAddModal && <UserFormModal user={editUser} onClose={() => { setShowAddModal(false); setEditUser(null); }} onSave={saveUser} />}
            <ConfirmDialog open={deleteTarget !== null && !detailUser} onClose={() => setDeleteTarget(null)} onConfirm={() => deleteTarget && del(deleteTarget)} title="Kullanıcıyı Sil" message="Bu kullanıcıyı silmek istediğinize emin misiniz? Bu işlem geri alınamaz!" />
            <ConfirmDialog open={bulkDeleteOpen} onClose={() => setBulkDeleteOpen(false)} onConfirm={bulkDel} title={`${selected.size} Kullanıcı Sil`} message={`Seçili ${selected.size} kullanıcıyı silmek istediğinize emin misiniz? Bu işlem geri alınamaz!`} />
            
            {/* Quick Password Reset Modal */}
            {passwordResetModalNode}

            {bulkModalOpen && (
                <div className="fixed inset-0 z-[100] flex items-center justify-center p-4" onClick={() => setBulkModalOpen(false)}>
                    <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
                    <div className="relative w-full max-w-lg bg-white rounded-2xl shadow-xl animate-fade-in" onClick={e => e.stopPropagation()}>
                        <div className="flex items-center justify-between px-6 py-4 border-b border-[#E2E8F0]/60">
                            <h2 className="text-lg font-bold text-[#0A1931] flex items-center gap-2"><Upload size={18} /> Toplu Kullanıcı Yükleme</h2>
                            <button onClick={() => setBulkModalOpen(false)} className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/40 text-[#A0AEC0]"><X size={18} /></button>
                        </div>
                        <div className="p-6 space-y-4">
                            <div 
                                onDragOver={(e) => { e.preventDefault(); setBulkDragging(true); }}
                                onDragLeave={(e) => { e.preventDefault(); setBulkDragging(false); }}
                                onDrop={(e) => {
                                    e.preventDefault();
                                    setBulkDragging(false);
                                    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                                        const f = e.dataTransfer.files[0];
                                        if (f.name.endsWith('.xlsx')) setBulkFile(f);
                                        else alert("Lütfen geçerli bir .xlsx dosyası yükleyin.");
                                    }
                                }}
                                className={`border-2 rounded-2xl p-10 text-center transition-colors ${bulkFile ? 'border-emerald-500 bg-emerald-50/30 border-solid' : (bulkDragging ? 'border-blue-500 bg-blue-50/50 border-solid' : 'border-dashed border-[#E2E8F0] hover:border-[#1B3B6F]/40 hover:bg-[#F8FAFC]')}`}
                            >
                                <input type="file" accept=".xlsx" className="hidden" id="bulk-file"
                                    onChange={e => { if (e.target.files?.[0]) handleBulkFile(e.target.files[0]); }} />
                                <label htmlFor="bulk-file" className="cursor-pointer block">
                                    {bulkFile ? (
                                        <div className="flex flex-col items-center justify-center">
                                            <div className="w-16 h-16 bg-emerald-100 text-emerald-600 rounded-full flex items-center justify-center mb-4 ring-4 ring-emerald-50 shadow-sm animate-fade-in">
                                                <Check size={32} strokeWidth={3} />
                                            </div>
                                            <p className="text-lg font-extrabold text-emerald-700 mb-1">Dosya Hazır!</p>
                                            <p className="text-sm font-semibold mb-2 truncate max-w-[300px] mx-auto text-[#0A1931]">
                                                {bulkFile.name}
                                            </p>
                                            <p className="text-xs text-emerald-600 font-medium mb-4">{(bulkFile.size / 1024).toFixed(1)} KB</p>
                                            <div className="inline-flex px-4 py-1.5 rounded-full bg-white border border-emerald-200 text-xs text-emerald-700 font-bold hover:bg-emerald-50 transition-colors">
                                                Değiştir
                                            </div>
                                        </div>
                                    ) : (
                                        <>
                                            <div className="w-14 h-14 bg-[#F8FAFC] rounded-2xl flex items-center justify-center mx-auto mb-4 border border-[#E2E8F0]/60 shadow-sm">
                                                <Upload size={24} className="text-[#A0AEC0]" />
                                            </div>
                                            <p className="text-base font-bold mb-2 text-[#0A1931]">
                                                Excel dosyası (.xlsx) seçin
                                            </p>
                                            <p className="text-xs text-[#64748B] mb-5">Sütunlar: Ad, Soyad, TC, Telefon, Rol</p>
                                            <div className="bg-[#F8FAFC] p-4 rounded-xl text-left border border-[#E2E8F0]/60 space-y-3">
                                                <div className="flex items-start gap-3">
                                                    <div className="mt-0.5 w-5 h-5 rounded-full bg-emerald-100 flex items-center justify-center shrink-0"><Check size={12} strokeWidth={3} className="text-emerald-500" /></div> 
                                                    <p className="text-xs text-[#64748B] font-medium leading-relaxed">Şifreler otomatik olarak <strong className="text-[#0A1931]">TC + Numaranın Son 2 Hanesi</strong> olarak atanır.</p>
                                                </div>
                                                <div className="flex items-start gap-3">
                                                    <div className="mt-0.5 w-5 h-5 rounded-full bg-rose-100 flex items-center justify-center shrink-0"><AlertTriangle size={12} strokeWidth={3} className="text-rose-500" /></div> 
                                                    <p className="text-xs text-[#64748B] font-medium leading-relaxed">Telefon numaralarını başında <strong className="text-rose-600">0 olmadan</strong> giriniz!</p>
                                                </div>
                                            </div>
                                        </>
                                    )}
                                </label>
                            </div>

                            {bulkResult && (
                                <div className={`rounded-xl p-4 ${bulkResult.fail === 0 ? 'bg-emerald-50 border border-emerald-200' : 'bg-amber-50 border border-amber-200'}`}>
                                    <p className="text-sm font-bold">{bulkResult.ok > 0 ? `${bulkResult.ok} kullanıcı başarıyla eklendi.` : 'Yükleme başarısız.'}</p>
                                </div>
                            )}
                        </div>
                        <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-[#E2E8F0]/60 bg-[#E2E8F0]/15 rounded-b-2xl">
                            <button onClick={() => setBulkModalOpen(false)} className="px-5 py-2.5 text-sm font-bold text-[#A9A9A9] hover:text-[#0A1931]">İptal</button>
                            <button onClick={handleBulkUpload} disabled={!bulkFile || bulkLoading}
                                className="px-6 py-3 text-sm font-bold text-white bg-[#0A1931] rounded-xl hover:bg-[#1B3B6F] shadow-lg disabled:opacity-40 transition-all">
                                {bulkLoading ? 'Yükleniyor...' : `Yüklemeyi Başlat`}
                            </button>
                        </div>
                    </div>
                </div>
            )}
            {bulkResult && <ImportResultsModal result={bulkResult} onClose={() => { setBulkResult(null); fetchUsers(); setBulkModalOpen(false); setBulkFile(null); }} />}
        </div>
    );

    function ImportResultsModal({ result, onClose }: { result: { ok: number; fail: number; details?: { firstName: string, lastName: string, email: string, status: string, reason: string }[] }; onClose: () => void }) {
        return (
            <div className="fixed inset-0 z-[120] flex items-center justify-center p-4">
                <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
                <div className="relative w-full max-w-4xl max-h-[85vh] flex flex-col bg-white rounded-3xl shadow-2xl overflow-hidden animate-fade-in">
                    <div className="flex items-center justify-between p-6 border-b border-[#E2E8F0]/60 bg-[#F8FAFC]">
                        <div>
                            <h2 className="text-xl font-bold text-[#0A1931]">Yükleme Sonuçları</h2>
                            <p className="text-sm text-[#64748B] mt-1">{result.ok} Başarılı, {result.fail} Başarısız/Atlanan</p>
                        </div>
                        <button onClick={onClose} className="p-2 bg-white border border-[#E2E8F0] rounded-xl hover:bg-[#E2E8F0]/50 text-[#A0AEC0] transition-colors"><X size={20} /></button>
                    </div>
                    <div className="flex-1 overflow-y-auto p-6">
                        <table className="w-full text-left text-sm whitespace-nowrap">
                            <thead>
                                <tr className="text-[#64748B] font-medium border-b border-[#E2E8F0]">
                                    <th className="pb-3 pr-4 font-bold">Ad Soyad</th>
                                    <th className="pb-3 px-4 font-bold">E-posta / Kullanıcı Adı</th>
                                    <th className="pb-3 px-4 font-bold">Durum</th>
                                    <th className="pb-3 pl-4 font-bold w-full">Açıklama</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-[#E2E8F0]/60">
                                {result.details?.map((d, i) => (
                                    <tr key={i} className="hover:bg-[#F8FAFC]/50">
                                        <td className="py-3 pr-4 font-medium text-[#0A1931]">{d.firstName} {d.lastName}</td>
                                        <td className="py-3 px-4 text-[#64748B]">{d.email}</td>
                                        <td className="py-3 px-4">
                                            <span className={`inline-flex items-center px-2 py-1 rounded-md text-[11px] font-bold ${d.status === 'Başarılı' ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700'}`}>
                                                {d.status}
                                            </span>
                                        </td>
                                        <td className="py-3 pl-4 text-xs text-[#64748B] whitespace-normal">{d.reason}</td>
                                    </tr>
                                ))}
                                {(!result.details || result.details.length === 0) && (
                                    <tr>
                                        <td colSpan={4} className="py-8 text-center text-[#64748B]">Detay bilgisi bulunamadı.</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                    <div className="p-4 border-t border-[#E2E8F0]/60 bg-[#F8FAFC] flex justify-end">
                        <button onClick={onClose} className="px-6 py-2.5 bg-[#0A1931] text-white text-sm font-bold rounded-xl hover:bg-[#1B3B6F] shadow-lg transition-all">
                            Kapat
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    function GroupDropdown({ groups, selected, onToggle }: { groups: string[]; selected: string[]; onToggle: (g: string) => void }) {
        const [open, setOpen] = useState(false);
        const [search, setSearch] = useState("");
        const ref = useRef<HTMLDivElement>(null);
        useEffect(() => {
            const handler = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); };
            document.addEventListener("mousedown", handler);
            return () => document.removeEventListener("mousedown", handler);
        }, []);

        const filtered = groups.filter(g => g.toLowerCase().includes(search.toLowerCase()));

        return (
            <div ref={ref} className="relative">
                <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Gruplar</label>
                <button type="button" onClick={() => setOpen(!open)}
                    className={`w-full flex items-center justify-between px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border rounded-xl transition-all ${open ? 'border-[#A0AEC0] ring-2 ring-[#0A1931]/10' : 'border-[#E2E8F0]'}`}>
                    <span className={selected.length > 0 ? "text-[#0A1931] font-medium" : "text-[#A9A9A9]"}>
                        {selected.length > 0 ? `${selected.length} grup seçili` : "Grup seçin"}
                    </span>
                    <ChevronDown size={16} className={`text-[#A0AEC0] transition-transform ${open ? 'rotate-180' : ''}`} />
                </button>
                {open && (
                    <div className="absolute z-[60] bottom-full mb-2 left-0 right-0 bg-white rounded-xl border border-[#E2E8F0] shadow-2xl shadow-black/10 flex flex-col animate-fade-in-up">
                        <div className="p-2 border-b border-[#E2E8F0]">
                            <div className="relative">
                                <Search size={14} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                                <input 
                                    type="text" 
                                    value={search} 
                                    onChange={e => setSearch(e.target.value)} 
                                    placeholder="Grup ara..." 
                                    className="w-full pl-8 pr-3 py-2 text-xs bg-[#F8FAFC] border border-[#E2E8F0] rounded-lg focus:outline-none focus:ring-1 focus:ring-[#0A1931]/20"
                                    autoFocus
                                />
                            </div>
                        </div>
                        <div className="max-h-48 overflow-y-auto p-1 custom-scrollbar">
                            {filtered.length > 0 ? filtered.map(g => (
                                <button key={g} type="button" onClick={() => onToggle(g)}
                                    className="w-full flex items-center gap-3 px-3 py-2 text-left hover:bg-[#F8FAFC] rounded-lg transition-colors">
                                    <div className={`w-4.5 h-4.5 rounded-md border-2 flex items-center justify-center transition-all shrink-0 ${selected.includes(g) ? 'bg-[#0A1931] border-[#0A1931]' : 'border-[#A0AEC0]'}`}
                                        style={{ width: 18, height: 18 }}>
                                        {selected.includes(g) && <Check size={12} className="text-white" />}
                                    </div>
                                    <span className={`text-sm ${selected.includes(g) ? 'font-bold text-[#0A1931]' : 'text-[#1B3B6F]'}`}>{g}</span>
                                </button>
                            )) : (
                                <p className="text-xs text-center text-[#A0AEC0] py-3">Sonuç bulunamadı</p>
                            )}
                        </div>
                    </div>
                )}
                {selected.length > 0 && (
                    <div className="flex flex-wrap gap-1.5 mt-2">
                        {selected.map(g => (
                            <span key={g} className="inline-flex items-center gap-1 text-[10px] font-bold px-2 py-1 rounded-lg bg-[#0A1931] text-white">
                                {g}
                                <button type="button" onClick={() => onToggle(g)} className="hover:bg-white/20 rounded p-0.5 transition-colors"><X size={10} /></button>
                            </span>
                        ))}
                    </div>
                )}
            </div>
        );
    }

    function UserFormModal({ user, onClose, onSave }: { user: User | null; onClose: () => void; onSave: (d: Partial<User> & { password?: string }) => void }) {
        const [f, sF] = useState({ firstName: user?.firstName || "", lastName: user?.lastName || "", email: user?.email || "", phone: user?.phone || "", role: user?.role || "Student", groupNames: user?.groupNames || [] as string[], studentType: user?.studentType || "Aktif", tcNo: user?.tcNo || "" });
        const u = (k: string, v: string) => sF(p => ({ ...p, [k]: v }));
        const toggleGroup = (g: string) => sF(p => ({ ...p, groupNames: p.groupNames.includes(g) ? p.groupNames.filter(x => x !== g) : [...p.groupNames, g] }));

        const [roleOpen, setRoleOpen] = useState(false);
        const roleRef = useRef<HTMLDivElement>(null);
        useEffect(() => {
            const handler = (e: MouseEvent) => { if (roleRef.current && !roleRef.current.contains(e.target as Node)) setRoleOpen(false); };
            document.addEventListener("mousedown", handler);
            return () => document.removeEventListener("mousedown", handler);
        }, []);
        const FORM_ROLES = [
            { value: "Student", label: "Öğrenci", icon: GraduationCap, color: "text-emerald-600", bg: "bg-emerald-50" },
            { value: "Instructor", label: "Eğitmen", icon: Briefcase, color: "text-purple-600", bg: "bg-purple-50" },
            { value: "Admin", label: "Admin", icon: Shield, color: "text-blue-600", bg: "bg-blue-50" },
            { value: "Accountant", label: "Muhasebe", icon: Briefcase, color: "text-amber-600", bg: "bg-amber-50" },
            { value: "Assistant", label: "Asistan", icon: Briefcase, color: "text-indigo-600", bg: "bg-indigo-50" }
        ];
        const availableRoles = FORM_ROLES.filter(r => !(isAssistant && (r.value === "Admin" || r.value === "Accountant")));
        const selRole = FORM_ROLES.find(r => r.value === f.role) || FORM_ROLES[0];

        const [manualPw, setManualPw] = useState('');
        const [showPw, setShowPw] = useState(false);
        const currentPassword = manualPw || undefined;

        const cleanPhone = (val: string) => {
            let digits = val.replace(/\D/g, "");
            if (digits.startsWith("0")) {
                digits = digits.substring(1);
            }
            return digits.substring(0, 10);
        };

        const ToEnglishUsernameSlug = (first: string, last: string) => {
            const combined = `${first}${last}`.trim().toLowerCase();
            const trMap: Record<string, string> = { 'ç':'c', 'ğ':'g', 'ı':'i', 'ö':'o', 'ş':'s', 'ü':'u' };
            return combined.split('').map(c => trMap[c] || c).join('').replace(/[^a-z0-9]/g, '');
        };

        const handleSave = () => {
            if ((f.role === "Student" || f.role === "Öğrenci") && (!f.tcNo || f.tcNo.length !== 11)) {
                alert("Öğrenci eklemek için 11 haneli TC Kimlik Numarası zorunludur.");
                return;
            }
            if (!f.firstName || !f.lastName) {
                alert("Ad ve Soyad zorunludur.");
                return;
            }
            if (!f.email) {
                alert("Kullanıcı Adı veya E-posta alanı zorunludur.");
                return;
            }
            if (manualPw && manualPw.length < 6) {
                alert("Şifreniz en az 6 haneli olmalıdır.");
                return;
            }
            onSave({ ...f, role: f.role as User["role"], groupNames: f.groupNames, studentType: (f.role === "Student" || f.role === "Öğrenci") ? f.studentType as User["studentType"] : null, ...(currentPassword && { password: currentPassword }) });
        };

        return (
            <div className="fixed inset-0 z-[100] flex items-center justify-center p-4" onClick={onClose}><div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
                <div className="relative w-full max-w-lg max-h-[90vh] bg-white rounded-2xl shadow-xl animate-fade-in flex flex-col" onClick={e => e.stopPropagation()}>
                    <div className="flex items-center justify-between px-6 py-4 border-b border-[#E2E8F0]/60 shrink-0"><h2 className="text-lg font-bold text-[#0A1931]">{user ? "Kullanıcı Düzenle" : "Yeni Kullanıcı"}</h2><button onClick={onClose} className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/40 text-[#A0AEC0]"><X size={18} /></button></div>
                    <div className="flex-1 overflow-y-auto p-6 space-y-4">
                        <div className="grid grid-cols-2 gap-4">
                            <div><label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Ad</label><input type="text" value={f.firstName} onChange={e => u("firstName", e.target.value)} className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0]" placeholder="Ad" /></div>
                            <div><label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Soyad</label><input type="text" value={f.lastName} onChange={e => u("lastName", e.target.value)} className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0]" placeholder="Soyad" /></div>
                        </div>
                        
                        {(f.role === "Student" || f.role === "Öğrenci") ? (
                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Oluşturulacak Kullanıcı Adı</label>
                                    <div className="w-full px-3.5 py-2.5 text-sm bg-emerald-50 border border-emerald-200 rounded-xl text-emerald-800 font-bold flex items-center gap-2">
                                        <Zap size={14} className="text-emerald-500 animate-pulse shrink-0" />
                                        {f.phone || "(Telefon girin)"}
                                    </div>
                                </div>
                                <div>
                                    <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">E-posta</label>
                                    <div className="relative">
                                        <Mail size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                                        <input type="email" value={f.email} onChange={e => u("email", e.target.value)} className="w-full pl-9 pr-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0]" placeholder="Örn: kullanici@mail.com" />
                                    </div>
                                </div>
                            </div>
                        ) : (
                            <div><label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Kullanıcı Adı / E-posta</label><div className="relative"><Mail size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" /><input type="text" value={f.email} onChange={e => u("email", e.target.value)} className="w-full pl-9 pr-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0]" placeholder="kullaniciadi veya email@sirket.com" /></div></div>
                        )}

                        <div className="grid grid-cols-2 gap-4">
                            <div><label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">TC Kimlik No {(f.role === "Student" || f.role === "Öğrenci") && "*"}</label><input type="text" value={f.tcNo} onChange={e => u("tcNo", e.target.value.replace(/\D/g, "").substring(0, 11))} className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0]" placeholder="11 haneli TC No" /></div>
                            <div><label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Telefon</label><div className="relative"><Phone size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" /><input type="tel" value={f.phone} onChange={e => u("phone", cleanPhone(e.target.value))} maxLength={10} className="w-full pl-9 pr-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0]" placeholder="532 000 0000" /></div></div>
                        </div>

                        {/* Şifre Alanı */}
                        <div className="pt-2 border-t border-[#E2E8F0]/60 space-y-2">
                            <label className="block text-xs font-medium text-[#1B3B6F]">{user ? "Şifre Sıfırla (Opsiyonel)" : "Şifre"}</label>
                            <div className="relative">
                                <Lock size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                                <input type={showPw ? "text" : "password"} value={manualPw} onChange={e => setManualPw(e.target.value)}
                                    autoComplete="new-password"
                                    className="w-full pl-9 pr-11 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0]"
                                    placeholder="Manuel şifre belirlemek için girin..." />
                                <button type="button" onClick={() => setShowPw(!showPw)} className="absolute right-3 top-1/2 -translate-y-1/2 text-[#A0AEC0] hover:text-[#0A1931] transition-colors">
                                    {showPw ? <EyeOff size={14} /> : <Eye size={14} />}
                                </button>
                            </div>
                            <p className="text-[10px] text-[#A0AEC0] mt-1 flex items-center gap-1">
                                <AlertTriangle size={12} className="text-amber-500" />
                                Şifreniz en az 6 haneli olmalıdır.
                            </p>
                            {!user && (f.role === "Student" || f.role === "Öğrenci") && !manualPw && (
                                <div className="text-xs bg-blue-50 border border-blue-200 rounded-xl p-3 text-blue-800 font-semibold flex items-center justify-between">
                                    <span>Varsayılan Öğrenci Şifresi:</span>
                                    <span className="font-mono bg-white px-2 py-0.5 rounded border border-blue-300 select-all tracking-wider text-sm font-bold">
                                        {f.email || "(E-posta girin)"}
                                    </span>
                                </div>
                            )}
                        </div>

                        <div className="grid grid-cols-2 gap-4">
                            <div className="relative" ref={roleRef}>
                                <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Rol</label>
                                <button type="button" onClick={() => setRoleOpen(!roleOpen)}
                                    className={`w-full flex items-center justify-between px-3 py-2 text-sm bg-white border rounded-xl transition-all shadow-sm ${roleOpen ? 'border-[#A0AEC0] ring-2 ring-[#0A1931]/10' : 'border-[#E2E8F0]'}`}>
                                    <div className="flex items-center gap-2">
                                        <div className={`p-1.5 rounded-lg ${selRole.bg} ${selRole.color}`}><selRole.icon size={14} /></div>
                                        <span className="text-[#0A1931] font-semibold">{selRole.label}</span>
                                    </div>
                                    <ChevronDown size={16} className={`text-[#A0AEC0] transition-transform ${roleOpen ? 'rotate-180' : ''}`} />
                                </button>
                                {roleOpen && (
                                    <div className="absolute z-50 left-0 right-0 top-full mt-1.5 bg-white rounded-xl border border-[#E2E8F0] shadow-xl shadow-black/10 py-1 max-h-48 overflow-y-auto animate-fade-in-up">
                                        {availableRoles.map(r => (
                                            <button key={r.value} type="button" onClick={() => { sF(p => ({ ...p, role: r.value, groupNames: (r.value === "Student" || r.value === "Öğrenci") ? p.groupNames : [] })); setRoleOpen(false); }}
                                                className="w-full flex items-center justify-between px-3 py-2 text-left hover:bg-[#F8FAFC] transition-colors">
                                                <div className="flex items-center gap-2.5">
                                                    <div className={`p-1.5 rounded-lg ${r.bg} ${r.color}`}><r.icon size={14} /></div>
                                                    <span className={`text-xs ${f.role === r.value ? 'font-bold text-[#0A1931]' : 'text-[#475569] font-medium'}`}>{r.label}</span>
                                                </div>
                                                {f.role === r.value && <Check size={14} className="text-blue-600" />}
                                            </button>
                                        ))}
                                    </div>
                                )}
                            </div>
                            {(f.role === "Student" || f.role === "Öğrenci") ? (
                                <GroupDropdown groups={groupOptions} selected={f.groupNames} onToggle={toggleGroup} />
                            ) : (
                                <div>
                                    <label className="block text-xs font-medium text-[#A0AEC0] mb-1.5">Gruplar</label>
                                    <div className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/40 border border-[#E2E8F0] rounded-xl text-[#A0AEC0] cursor-not-allowed">
                                        Bu rol gruba atanamaz
                                    </div>
                                </div>
                            )}
                        </div>
                        {(f.role === "Student" || f.role === "Öğrenci") && <div><label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Öğrenci Tipi</label><div className="flex gap-3">{((["Aktif", "Demo", "Pasif"] as const)).map(t => <button key={t} type="button" onClick={() => u("studentType", t)} className={`flex-1 py-2.5 text-sm font-medium rounded-xl border transition-all ${f.studentType === t || (t === "Aktif" && f.studentType === "Active") || (t === "Pasif" && f.studentType === "Passive") ? "bg-[#E2E8F0]/30 border-[#A0AEC0] text-[#0A1931]" : "bg-[#E2E8F0]/20 border-[#E2E8F0] text-[#A9A9A9] hover:bg-[#E2E8F0]/40"}`}>{t}</button>)}</div></div>}
                    </div>
                    <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-[#E2E8F0]/60 bg-[#E2E8F0]/15 rounded-b-2xl shrink-0">
                        <button onClick={onClose} className="px-5 py-2.5 text-sm font-bold text-[#A9A9A9] hover:text-[#0A1931] transition-colors">İptal</button>
                        <button onClick={handleSave} className="px-6 py-3 text-sm font-bold text-white bg-[#0A1931] rounded-xl hover:bg-[#1B3B6F] shadow-lg shadow-black/10 transition-all active:scale-[0.98]">{user ? "Güncelle" : "Oluştur"}</button>
                    </div>
                </div>
            </div>
        );
    }
}
