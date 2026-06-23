"use client";

import { useState, useRef } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { api, type UserTenantDto } from "@/lib/api";
import { Camera, Lock, LogOut, User2, Shield, Building2, Calendar, Upload } from "lucide-react";

export default function ProfilePage() {
    const { user, token, currentTenantId, logout } = useAuth();
    const [pwForm, setPwForm] = useState({ current: "", next: "", confirm: "" });
    const [pwStatus, setPwStatus] = useState<{ ok?: boolean; msg: string } | null>(null);
    const [saving, setSaving] = useState(false);
    const [avatarUploading, setAvatarUploading] = useState(false);
    const [avatarUrl, setAvatarUrl] = useState<string | null>((user as { avatarUrl?: string } | null)?.avatarUrl ?? null);
    const fileRef = useRef<HTMLInputElement>(null);

    const initials = `${user?.firstName?.[0] ?? ""}${user?.lastName?.[0] ?? ""}`.toUpperCase();
    const activeTenant: UserTenantDto | undefined =
        user?.tenants?.find((t: UserTenantDto) => t.tenantId === currentTenantId) ?? user?.tenants?.[0];

    const handlePasswordChange = async () => {
        if (pwForm.next !== pwForm.confirm) {
            setPwStatus({ ok: false, msg: "Şifreler eşleşmiyor." });
            return;
        }
        if (pwForm.next.length < 6) {
            setPwStatus({ ok: false, msg: "Şifreniz en az 6 haneli olmalıdır." });
            return;
        }
        setSaving(true);
        try {
            await api("/users/me/password", {
                method: "PUT",
                token: token ?? undefined,
                tenantId: currentTenantId ?? undefined,
                body: JSON.stringify({ currentPassword: pwForm.current, newPassword: pwForm.next }),
            });
            setPwStatus({ ok: true, msg: "Şifre başarıyla güncellendi." });
            setPwForm({ current: "", next: "", confirm: "" });
        } catch (e: unknown) {
            setPwStatus({ ok: false, msg: e instanceof Error ? e.message : "Hata oluştu." });
        } finally {
            setSaving(false);
        }
    };

    const handleAvatarChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file) return;
        // Önizleme
        const preview = URL.createObjectURL(file);
        setAvatarUrl(preview);
        setAvatarUploading(true);
        try {
            const fd = new FormData();
            fd.append("avatar", file);
            const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/users/me/avatar`, {
                method: "POST",
                headers: { Authorization: `Bearer ${token}`, "X-Tenant-Id": currentTenantId ?? "" },
                body: fd,
            });
            if (!res.ok) throw new Error("Yükleme başarısız");
            const { url } = await res.json() as { url: string };
            setAvatarUrl(url);
        } catch {
            // Önizleme kalsın
        } finally {
            setAvatarUploading(false);
        }
    };

    return (
        <div className="max-w-2xl mx-auto space-y-6">
            <h1 className="text-2xl font-bold text-[#0A1931] flex items-center gap-2">
                <User2 size={22} /> Profilim
            </h1>

            {/* Avatar + Bilgiler */}
            <div className="glass-card p-6">
                <div className="flex flex-col sm:flex-row items-center sm:items-start text-center sm:text-left gap-5 mb-6">
                    {/* Avatar */}
                    <div className="relative group">
                        <div className="w-20 h-20 rounded-2xl bg-gradient-to-br from-[#1B3B6F] to-[#0A1931] flex items-center justify-center text-white text-2xl font-bold shadow-xl shadow-[#0A1931]/25 overflow-hidden">
                            {avatarUrl
                                ? <img src={avatarUrl} alt="Avatar" className="w-full h-full object-cover" />
                                : <span>{initials || "?"}</span>}
                        </div>
                        <button
                            onClick={() => fileRef.current?.click()}
                            disabled={avatarUploading}
                            className="absolute inset-0 rounded-2xl bg-black/50 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity cursor-pointer"
                        >
                            {avatarUploading
                                ? <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                                : <Camera size={20} className="text-white" />}
                        </button>
                        <input ref={fileRef} type="file" accept="image/*" className="hidden" onChange={handleAvatarChange} />
                    </div>
                    <div className="flex-1">
                        <h2 className="text-[#0A1931] text-xl font-bold">{user?.firstName} {user?.lastName}</h2>
                        <p className="text-[#A0AEC0] text-sm mt-0.5">{user?.email}</p>
                        <div className="flex items-center gap-2 mt-2 flex-wrap">
                            <span className="px-2 py-0.5 bg-[#1B3B6F]/15 text-[#A0AEC0] text-xs rounded-full border border-violet-500/30 flex items-center gap-1">
                                <Shield size={10} /> {user?.role === "Student" ? "Öğrenci" : user?.role}
                            </span>
                            {user?.studentType && (
                                <span className="px-2 py-0.5 bg-[#1B3B6F]/15 text-[#A0AEC0] text-xs rounded-full border border-indigo-500/30">
                                    {user.studentType === "Active" ? "Aktif" : user.studentType === "Demo" ? "Demo" : user.studentType}
                                </span>
                            )}
                        </div>
                        <button onClick={() => fileRef.current?.click()} className="mt-2 text-xs text-[#A0AEC0] hover:text-violet-300 flex items-center gap-1 transition-colors">
                            <Upload size={11} /> Fotoğraf değiştir
                        </button>
                    </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    {[
                        { icon: User2, label: "Ad Soyad", val: `${user?.firstName ?? ""} ${user?.lastName ?? ""}` },
                        { icon: Shield, label: "E-posta", val: user?.email ?? "—" },
                        activeTenant ? { icon: Building2, label: "Kurum", val: activeTenant.tenantName } : null,
                        {
                            icon: Calendar, label: "Üyelik Tarihi", val: user?.createdAt
                                ? new Date(user.createdAt).toLocaleDateString("tr-TR", { day: "numeric", month: "long", year: "numeric" })
                                : "—"
                        },
                    ].filter(Boolean).map((item) => item && (
                        <div key={item.label} className="p-3 bg-[#1B3B6F]/10 rounded-xl border border-[#1B3B6F]/20">
                            <p className="text-[#A9A9A9] text-xs mb-1 flex items-center gap-1">
                                <item.icon size={10} /> {item.label}
                            </p>
                            <p className="text-[#0A1931] text-sm font-medium truncate">{item.val}</p>
                        </div>
                    ))}
                </div>
            </div>

            {/* Şifre değiştir */}
            <div className="glass-card p-6">
                <h3 className="text-[#0A1931] font-semibold mb-4 flex items-center gap-2">
                    <Lock size={15} className="text-[#A0AEC0]" /> Şifre Değiştir
                </h3>
                <div className="space-y-3">
                    {([
                        { label: "Mevcut Şifre", key: "current" as const, placeholder: "••••••••" },
                        { label: "Yeni Şifre", key: "next" as const, placeholder: "En az 6 haneli" },
                        { label: "Yeni Şifre (Tekrar)", key: "confirm" as const, placeholder: "••••••••" },
                    ]).map(({ label, key, placeholder }) => (
                        <div key={key}>
                            <label className="text-[#A0AEC0] text-xs mb-1 block">{label}</label>
                            <input
                                type="password"
                                value={pwForm[key]}
                                onChange={e => setPwForm(p => ({ ...p, [key]: e.target.value }))}
                                placeholder={placeholder}
                                className="w-full bg-[#1B3B6F]/15 border border-[#1B3B6F]/30 rounded-xl px-4 py-2.5 text-[#0A1931] text-sm placeholder-[#A9A9A9] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F] transition-all"
                            />
                        </div>
                    ))}
                </div>

                {pwStatus && (
                    <p className={`text-xs mt-3 ${pwStatus.ok ? "text-green-400" : "text-red-400"}`}>
                        {pwStatus.ok ? "✅" : "❌"} {pwStatus.msg}
                    </p>
                )}

                <button
                    onClick={handlePasswordChange}
                    disabled={saving || !pwForm.current || !pwForm.next || !pwForm.confirm}
                    className="mt-4 w-full py-2.5 bg-[#1B3B6F] hover:bg-[#1B3B6F]/90 disabled:opacity-40 text-white text-sm font-medium rounded-xl transition-all"
                >
                    {saving ? "Kaydediliyor..." : "Şifreyi Güncelle"}
                </button>
            </div>

            {/* Çıkış */}
            <div className="glass-card p-6 border border-red-500/15">
                <h3 className="text-[#0A1931] font-semibold mb-1 flex items-center gap-2"><LogOut size={15} className="text-red-400" /> Oturumdan Çık</h3>
                <p className="text-[#A9A9A9] text-xs mb-4">Tüm cihazlarda oturumu kapat.</p>
                <button
                    onClick={logout}
                    className="px-5 py-2 bg-red-500/10 hover:bg-red-500/20 text-red-400 text-sm font-medium rounded-xl border border-red-500/25 transition-all"
                >
                    Çıkış Yap
                </button>
            </div>
        </div>
    );
}
