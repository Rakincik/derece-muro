import { ArrowLeft, Target, Shield, Users, Phone, MessageCircle, Mail, KeyRound, ToggleRight, ToggleLeft, Trash2, CalendarIcon, Clock, Copy, Briefcase, GraduationCap, MoreHorizontal, Edit3, Building, ShieldCheck } from "lucide-react";
import { UserDirectCoursesTab } from "./UserDirectCoursesTab";
import { RoleSelect } from "./RoleSelect";
import { useState, useRef, useEffect } from "react";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { useToast } from "@/components/toast";

export interface User {
    id: string; firstName: string; lastName: string; email: string; phone: string;
    role: string;
    studentType: string | null; isActive: boolean;
    createdAt: string; lastLoginAt: string | null; groupNames: string[];
    password?: string;
    tcNo?: string;
}

const rc: Record<string, { bg: string; text: string; avatar: string; hero: string }> = {
    Admin: { bg: "bg-blue-50 border border-blue-200", text: "text-blue-700", avatar: "bg-blue-600", hero: "bg-blue-600" },
    SuperAdmin: { bg: "bg-rose-50 border border-rose-200", text: "text-rose-700", avatar: "bg-rose-600", hero: "bg-rose-600" },
    Instructor: { bg: "bg-purple-50 border border-purple-200", text: "text-purple-700", avatar: "bg-purple-600", hero: "bg-purple-600" },
    Student: { bg: "bg-emerald-50 border border-emerald-200", text: "text-emerald-700", avatar: "bg-emerald-500", hero: "bg-emerald-500" },
    Accountant: { bg: "bg-amber-50 border border-amber-200", text: "text-amber-700", avatar: "bg-amber-500", hero: "bg-amber-500" },
    Assistant: { bg: "bg-indigo-50 border border-indigo-200", text: "text-indigo-700", avatar: "bg-indigo-500", hero: "bg-indigo-500" },
    Eğitmen: { bg: "bg-purple-50 border border-purple-200", text: "text-purple-700", avatar: "bg-purple-600", hero: "bg-purple-600" },
    Öğrenci: { bg: "bg-emerald-50 border border-emerald-200", text: "text-emerald-700", avatar: "bg-emerald-500", hero: "bg-emerald-500" },
    Muhasebe: { bg: "bg-amber-50 border border-amber-200", text: "text-amber-700", avatar: "bg-amber-500", hero: "bg-amber-500" },
    Asistan: { bg: "bg-indigo-50 border border-indigo-200", text: "text-indigo-700", avatar: "bg-indigo-500", hero: "bg-indigo-500" },
};

const roleIcons: Record<string, typeof Shield> = { Admin: Shield, SuperAdmin: Shield, Instructor: Briefcase, Student: GraduationCap, Accountant: Briefcase, Assistant: Briefcase, Eğitmen: Briefcase, Öğrenci: GraduationCap, Muhasebe: Briefcase, Asistan: Briefcase };
const roleLabel: Record<string, string> = { Student: "Öğrenci", Instructor: "Eğitmen", Admin: "Admin", Accountant: "Muhasebe", Assistant: "Asistan", SuperAdmin: "Süper Admin", Öğrenci: "Öğrenci", Eğitmen: "Eğitmen", Muhasebe: "Muhasebe", Asistan: "Asistan" };

interface UserDetailProps {
    user: User;
    onBack: () => void;
    onToggleActive: (id: string) => void;
    onChangeRole: (id: string, role: string) => void;
    onDelete: (id: string) => void;
    onQuickReset: (user: User) => void;
    onEdit?: (user: User) => void;
    canEdit?: boolean;
}

export function UserDetail({ user: u, onBack, onToggleActive, onChangeRole, onDelete, onQuickReset, onEdit, canEdit = true }: UserDetailProps) {
    const { success } = useToast();
    const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
    const [confirmRoleChange, setConfirmRoleChange] = useState<string | null>(null);

    const c = rc[u.role] || rc['Student']; 
    const RI = roleIcons[u.role] || Shield;
    const phoneClean = u.phone?.replace(/\s/g, '').replace(/^\+?0?/, '+90') || '';
    const lastLoginText = u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString("tr-TR", { day: "numeric", month: "long", year: "numeric", hour: "2-digit", minute: "2-digit" }) : "Henüz giriş yok";
    const ini = (u: User) => `${(u.firstName?.[0] || "")}${(u.lastName?.[0] || "")}`.toUpperCase();

    const handleRoleChangeRequest = (newRole: string) => {
        if (newRole !== u.role) {
            setConfirmRoleChange(newRole);
        }
    };

    const confirmRole = async () => {
        if (confirmRoleChange) {
            await onChangeRole(u.id, confirmRoleChange);
            setConfirmRoleChange(null);
        }
    };

    return (
        <div className="space-y-5 animate-fade-in max-w-5xl mx-auto">
            {/* Back Button */}
            <button onClick={onBack} className="flex items-center gap-2 text-sm text-[#A9A9A9] hover:text-[#0A1931] transition-colors group">
                <ArrowLeft size={16} className="group-hover:-translate-x-0.5 transition-transform" /> Kullanıcı Listesine Dön
            </button>

            {/* Hero Header */}
            <div className="relative rounded-[1.5rem] md:rounded-[2rem] bg-white p-6 md:p-8 flex flex-col md:flex-row items-center md:items-start gap-5 md:gap-8 shadow-[0_8px_30px_rgb(0,0,0,0.04)] border border-[#E2E8F0] mb-6">
                {/* Avatar */}
                <div className={`w-20 h-20 md:w-28 md:h-28 rounded-2xl md:rounded-3xl ${c.hero} flex items-center justify-center text-white text-3xl md:text-4xl font-extrabold shadow-lg md:shadow-xl shadow-${c.hero.split('-')[1]}-500/30 relative overflow-hidden shrink-0`}>
                    <div className="absolute inset-0 bg-gradient-to-br from-white/30 to-transparent pointer-events-none" />
                    <span className="drop-shadow-md uppercase relative z-10">{ini(u)}</span>
                </div>

                <div className="flex-1 text-center md:text-left">
                    <h1 className="text-2xl md:text-3xl font-extrabold text-[#0A1931] tracking-tight capitalize mb-3">
                        {u.firstName.toLowerCase()} {u.lastName.toLowerCase()}
                    </h1>
                    
                    <div className="flex items-center justify-center md:justify-start gap-2 flex-wrap">
                        <span className="inline-flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-widest px-3 py-1.5 rounded-xl bg-[#F0F4F8] text-[#1B3B6F]">
                            <RI size={14} className="opacity-70" /> {roleLabel[u.role] || u.role}
                        </span>
                        
                        <span className={`inline-flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-widest px-3 py-1.5 rounded-xl ${u.isActive ? "bg-emerald-50 text-emerald-600" : "bg-[#F0F4F8] text-[#A0AEC0]"}`}>
                            <span className={`w-1.5 h-1.5 rounded-full ${u.isActive ? "bg-emerald-500" : "bg-[#A0AEC0]"}`} /> {u.isActive ? "AKTİF" : "PASİF"}
                        </span>
                        
                        {u.studentType === "Demo" && (
                            <span className="inline-flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-widest px-3 py-1.5 rounded-xl bg-amber-50 text-amber-600">
                                <Target size={14} className="opacity-70"/> DEMO
                            </span>
                        )}
                        {u.studentType === "Passive" && (
                            <span className="inline-flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-widest px-3 py-1.5 rounded-xl bg-[#F0F4F8] text-[#A0AEC0]">
                                <Target size={14} className="opacity-70"/> PASİF
                            </span>
                        )}
                        
                        {u.groupNames.slice(0, 3).map(g => (
                            <span key={g} className="text-[11px] font-bold px-3 py-1.5 rounded-xl bg-white border border-[#E2E8F0] text-[#475569] shadow-sm">
                                {g}
                            </span>
                        ))}
                        
                        {u.groupNames.length > 3 && (
                            <span title={u.groupNames.slice(3).join(', ')} className="text-[11px] font-bold px-3 py-1.5 rounded-xl bg-[#F0F4F8] text-[#64748B] cursor-help">
                                +{u.groupNames.length - 3} Diğer
                            </span>
                        )}
                    </div>
                </div>

                <div className="flex flex-col items-center justify-center px-8 py-5 rounded-[1.5rem] bg-[#F8FAFC] border border-[#E2E8F0] shadow-sm shrink-0 w-full md:w-auto">
                    <p className="text-3xl font-black text-[#0A1931]">{u.groupNames.length}</p>
                    <p className="text-[10px] font-extrabold text-[#64748B] mt-1 uppercase tracking-widest">Grup</p>
                </div>
            </div>

            {/* Hızlı Aksiyon Barı (Modernized) */}
            <div className="bg-white/90 backdrop-blur-xl rounded-2xl border border-[#E2E8F0] p-2.5 flex flex-wrap items-center justify-between gap-3 shadow-sm">
                <div className="flex flex-wrap items-center gap-2">
                    {u.phone && (
                        <a href={`tel:${u.phone.replace(/\s/g, '')}`} className="flex items-center gap-2 px-4 py-2 rounded-xl bg-[#0A1931] text-white text-xs font-bold hover:bg-[#1B3B6F] transition-all active:scale-[0.97] shadow-md shadow-[#0A1931]/20">
                            <Phone size={14} /> Ara
                        </a>
                    )}
                    {u.phone && (
                        <a href={`https://wa.me/${phoneClean}`} target="_blank" rel="noopener noreferrer" className="flex items-center gap-2 px-4 py-2 rounded-xl bg-[#25D366] text-white text-xs font-bold hover:bg-[#1DA851] transition-all active:scale-[0.97] shadow-md shadow-[#25D366]/20">
                            <MessageCircle size={14} /> WhatsApp
                        </a>
                    )}
                    {u.email.includes("@") && (
                        <a href={`mailto:${u.email}`} className="flex items-center gap-2 px-4 py-2 rounded-xl text-[#1B3B6F] text-xs font-bold hover:bg-[#E2E8F0]/50 transition-all active:scale-[0.97] border border-transparent hover:border-[#E2E8F0]">
                            <Mail size={14} /> E-posta
                        </a>
                    )}
                </div>

                <div className="flex flex-wrap items-center gap-2">
                    {canEdit ? (
                        <>
                            {onEdit && (
                                <button onClick={() => onEdit(u)} className="flex items-center gap-2 px-4 py-2 rounded-xl text-amber-600 text-xs font-bold whitespace-nowrap hover:bg-amber-50 transition-all active:scale-[0.97] bg-white border border-[#E2E8F0]/80 shadow-sm">
                                    <Edit3 size={14} /> Düzenle
                                </button>
                            )}
                            <RoleSelect value={u.role} onChange={handleRoleChangeRequest} />
                            <div className="h-8 w-px bg-[#E2E8F0] mx-1 hidden md:block"></div>
                            <button onClick={() => onQuickReset(u)} className="flex items-center gap-2 px-4 py-2 rounded-xl text-[#1B3B6F] text-xs font-bold whitespace-nowrap hover:bg-[#F0F4F8] transition-all active:scale-[0.97] bg-white border border-[#E2E8F0]/80 shadow-sm">
                                <KeyRound size={14} /> Şifre Sıfırla
                            </button>
                            <button onClick={() => onToggleActive(u.id)} className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold whitespace-nowrap transition-all active:scale-[0.97] bg-white border border-[#E2E8F0]/80 shadow-sm ${u.isActive ? "text-orange-600 hover:bg-orange-50" : "text-emerald-600 hover:bg-emerald-50"}`}>
                                {u.isActive ? <><ToggleRight size={14} /> Pasife Al</> : <><ToggleLeft size={14} /> Aktif Et</>}
                            </button>
                            <button onClick={() => setDeleteTarget(u.id)} className="flex items-center gap-2 px-4 py-2 rounded-xl text-red-600 text-xs font-bold whitespace-nowrap hover:bg-red-50 transition-all active:scale-[0.97] bg-white border border-[#E2E8F0]/80 shadow-sm">
                                <Trash2 size={14} /> Sil
                            </button>
                        </>
                    ) : (
                        <div className="px-4 py-2 rounded-xl bg-[#F8FAFC] text-[#64748B] text-xs font-bold border border-[#E2E8F0] flex items-center gap-2">
                            <Shield size={14} /> Yetki Yok
                        </div>
                    )}
                </div>
            </div>

            {/* İletişim Bilgileri (2-Column Grid) */}
            <div className="bg-white rounded-3xl border border-[#E2E8F0]/60 p-6 shadow-sm">
                <h3 className="text-[11px] font-extrabold text-[#A0AEC0] uppercase tracking-widest mb-5">İletişim & Güvenlik Bilgileri</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {[
                        { icon: Building, label: "Rol", value: roleLabel[u.role] || u.role, bg: "bg-purple-50 text-purple-600" },
                        { icon: ShieldCheck, label: "Kullanıcı Adı", value: u.username || "—", bg: "bg-blue-50 text-blue-600" },
                        { icon: Mail, label: "E-posta", value: u.email || "—", bg: "bg-blue-50 text-blue-600" },
                        { icon: Phone, label: "Telefon", value: u.phone || "—", bg: "bg-emerald-50 text-emerald-600" },
                        { icon: KeyRound, label: "Şifre", value: u.password || "******** (Gizli)", bg: "bg-amber-50 text-amber-600", action: () => onQuickReset(u), actionIcon: <KeyRound size={16} />, actionTooltip: "Şifre Sıfırla" },
                        { icon: CalendarIcon, label: "Kayıt Tarihi", value: new Date(u.createdAt).toLocaleDateString("tr-TR", { day: "numeric", month: "long", year: "numeric" }), bg: "bg-purple-50 text-purple-600" },
                        { icon: Clock, label: "Son Giriş", value: lastLoginText, bg: "bg-[#F0F4F8] text-[#A0AEC0]", colSpan: 2 },
                    ].map((r, idx) => (
                        <div key={idx} className={`flex items-center gap-4 group/item p-3.5 border border-[#E2E8F0]/40 rounded-2xl bg-white hover:border-[#E2E8F0] hover:shadow-sm transition-all ${r.colSpan === 2 ? "md:col-span-2" : ""}`}>
                            <div className={`w-10 h-10 rounded-xl ${r.bg} flex items-center justify-center shrink-0`}>
                                <r.icon size={18} />
                            </div>
                            <div className="flex-1 min-w-0">
                                <p className="text-[10px] text-[#A0AEC0] uppercase tracking-widest font-bold mb-0.5">{r.label}</p>
                                <p className="text-[15px] font-bold text-[#0A1931] truncate">{r.value}</p>
                            </div>
                            {r.action && canEdit && (
                                <button onClick={r.action} title={r.actionTooltip} className="p-2.5 rounded-xl bg-white shadow-sm border border-[#E2E8F0] hover:bg-[#F0F4F8] text-[#1B3B6F] transition-all">
                                    {r.actionIcon}
                                </button>
                            )}
                        </div>
                    ))}
                </div>
            </div>

            {/* Dahil Olduğu Gruplar */}
            {u.groupNames.length > 0 && (
                <div className="bg-white rounded-3xl border border-[#E2E8F0]/60 p-6 shadow-sm">
                    <h3 className="text-[11px] font-extrabold text-[#A0AEC0] uppercase tracking-widest mb-5 flex items-center gap-2">
                        <Users size={16} className="text-[#A0AEC0]" /> Dahil Olduğu Gruplar
                        <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-[#E2E8F0]/50 text-[#1B3B6F] ml-1">{u.groupNames.length}</span>
                    </h3>
                    <div className="flex flex-wrap gap-2.5">
                        {u.groupNames.map((g, i) => {
                            const colors = [
                                "bg-blue-50 text-blue-700 ring-blue-200",
                                "bg-violet-50 text-violet-700 ring-violet-200",
                                "bg-emerald-50 text-emerald-700 ring-emerald-200",
                                "bg-amber-50 text-amber-700 ring-amber-200",
                                "bg-rose-50 text-rose-700 ring-rose-200",
                            ];
                            return (
                                <span key={g} className={`inline-flex items-center gap-2 text-xs font-bold px-4 py-2 rounded-xl ring-1 ring-inset ${colors[i % colors.length]}`}>
                                    <span className="w-1.5 h-1.5 rounded-full bg-current opacity-60" />
                                    {g}
                                </span>
                            );
                        })}
                    </div>
                </div>
            )}

            {/* Özel Atanmış Dersler */}
            <div className="bg-white rounded-3xl border border-[#E2E8F0]/60 p-6 shadow-sm">
                <UserDirectCoursesTab userId={u.id} />
            </div>

            {/* Modals */}
            <ConfirmDialog
                open={deleteTarget === u.id}
                onClose={() => setDeleteTarget(null)}
                onConfirm={async () => {
                    await onDelete(u.id);
                }}
                title="Kullanıcıyı Sil"
                message={`"${u.firstName} ${u.lastName}" kullanıcısını silmek istediğinize emin misiniz?\n\nBu işlem geri alınamaz!`}
            />

            <ConfirmDialog
                open={!!confirmRoleChange}
                onClose={() => setConfirmRoleChange(null)}
                onConfirm={confirmRole}
                title="Yetki Değişikliği Onayı"
                message={`Kullanıcının yetkisini "${roleLabel[confirmRoleChange || ""]}" olarak değiştirmek istediğinize emin misiniz?`}
            />
        </div>
    );
}
