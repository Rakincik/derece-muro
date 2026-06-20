"use client";

import { useAuth } from "@/contexts/AuthContext";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Building2, ChevronRight, Shield, LogOut } from "lucide-react";

const roleTranslations: Record<string, string> = {
    "SuperAdmin": "Süper Admin",
    "Admin": "Admin",
    "Instructor": "Eğitmen",
    "Assistant": "Asistan",
    "Accountant": "Muhasebe",
    "Student": "Öğrenci",
    "Teacher": "Öğretmen"
};

export default function SelectTenantPage() {
    const { user, isLoading, currentTenantId, switchTenant, logout } = useAuth();
    const router = useRouter();
    const [hoveredId, setHoveredId] = useState<string | null>(null);
    const [selectedId, setSelectedId] = useState<string | null>(null);

    // Zaten tenant seçilmişse dashboard'a yönlendir
    useEffect(() => {
        if (!isLoading && currentTenantId) {
            router.replace("/dashboard");
        }
    }, [currentTenantId, isLoading, router]);

    // Login olmamışsa login'e yönlendir
    useEffect(() => {
        if (!isLoading && !user) {
            router.replace("/login");
        }
    }, [user, isLoading, router]);

    // Tek kurumu varsa otomatik seç
    useEffect(() => {
        if (!isLoading && user && user.tenants.length === 1) {
            switchTenant(user.tenants[0].tenantId);
            router.replace("/dashboard");
        }
    }, [user, isLoading, switchTenant, router]);

    const handleSelect = (tenantId: string) => {
        setSelectedId(tenantId);
        switchTenant(tenantId);
        // Kısa gecikme ile animasyonu göster
        setTimeout(() => router.replace("/dashboard"), 300);
    };

    if (isLoading || !user || currentTenantId || user.tenants.length <= 1) {
        return (
            <div className="min-h-screen flex items-center justify-center bg-[#0A1931]">
                <div className="w-10 h-10 border-4 border-[#1B3B6F] border-t-[#A0AEC0] rounded-full animate-spin" />
            </div>
        );
    }

    const tenants = user.tenants.filter(t => t.status === "active" || t.status === "Active");

    // Kart renk paleti
    const cardColors = [
        { bg: "from-[#1B3B6F]/30 to-[#1B3B6F]/10", border: "border-[#1B3B6F]/40", accent: "#3B82F6" },
        { bg: "from-[#065F46]/30 to-[#065F46]/10", border: "border-[#065F46]/40", accent: "#10B981" },
        { bg: "from-[#7C3AED]/30 to-[#7C3AED]/10", border: "border-[#7C3AED]/40", accent: "#8B5CF6" },
        { bg: "from-[#DC2626]/30 to-[#DC2626]/10", border: "border-[#DC2626]/40", accent: "#EF4444" },
        { bg: "from-[#D97706]/30 to-[#D97706]/10", border: "border-[#D97706]/40", accent: "#F59E0B" },
        { bg: "from-[#0891B2]/30 to-[#0891B2]/10", border: "border-[#0891B2]/40", accent: "#06B6D4" },
    ];

    return (
        <div className="min-h-screen bg-[#0A1931] flex flex-col">
            {/* Header */}
            <header className="flex items-center justify-between px-8 py-6">
                <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-2xl bg-[#1B3B6F] flex items-center justify-center">
                        <span className="text-white font-bold text-lg">M</span>
                    </div>
                    <div>
                        <h1 className="text-white font-bold text-lg tracking-tight">MURO</h1>
                        <p className="text-[#A0AEC0] text-xs">Yönetim Paneli</p>
                    </div>
                </div>
                <button
                    onClick={logout}
                    className="flex items-center gap-2 px-4 py-2 rounded-xl text-[#A0AEC0] hover:text-red-400 hover:bg-red-400/10 transition-all text-sm"
                >
                    <LogOut size={16} />
                    Çıkış
                </button>
            </header>

            {/* Main Content */}
            <main className="flex-1 flex flex-col items-center justify-center px-4 pb-16">
                {/* Welcome Section */}
                <div className="text-center mb-12">
                    <div className="inline-flex items-center gap-2 px-4 py-2 rounded-full bg-[#1B3B6F]/20 border border-[#1B3B6F]/30 mb-6">
                        <Shield size={14} className="text-[#A0AEC0]" />
                        <span className="text-xs font-medium text-[#A0AEC0] uppercase tracking-wider">
                            {user.role ? (roleTranslations[user.role] || user.role) : ""}
                        </span>
                    </div>
                    <h2 className="text-3xl font-black text-white mb-3 tracking-tight">
                        Hoş geldiniz, {user.firstName}
                    </h2>
                    <p className="text-[#A0AEC0] text-base max-w-md mx-auto">
                        Yönetmek istediğiniz kurumu seçin
                    </p>
                </div>

                {/* Tenant Cards Grid */}
                <div className={`grid gap-4 w-full max-w-3xl ${
                    tenants.length <= 2 ? "grid-cols-1 sm:grid-cols-2" :
                    tenants.length <= 4 ? "grid-cols-1 sm:grid-cols-2" :
                    "grid-cols-1 sm:grid-cols-2 lg:grid-cols-3"
                }`}>
                    {tenants.map((tenant, index) => {
                        const colors = cardColors[index % cardColors.length];
                        const isHovered = hoveredId === tenant.tenantId;
                        const isSelected = selectedId === tenant.tenantId;

                        return (
                            <button
                                key={tenant.tenantId}
                                onClick={() => handleSelect(tenant.tenantId)}
                                onMouseEnter={() => setHoveredId(tenant.tenantId)}
                                onMouseLeave={() => setHoveredId(null)}
                                className={`
                                    group relative p-6 rounded-2xl border backdrop-blur-sm
                                    bg-gradient-to-br ${colors.bg} ${colors.border}
                                    hover:scale-[1.02] active:scale-[0.98]
                                    transition-all duration-300 ease-out
                                    text-left cursor-pointer
                                    ${isSelected ? "ring-2 ring-white/40 scale-[1.02]" : ""}
                                `}
                                style={{
                                    animationDelay: `${index * 80}ms`,
                                    animation: "fadeInUp 0.5s ease-out both",
                                }}
                            >
                                {/* Icon + Info */}
                                <div className="flex items-start gap-4">
                                    <div
                                        className="w-12 h-12 rounded-xl flex items-center justify-center shrink-0 transition-transform duration-300"
                                        style={{
                                            backgroundColor: `${colors.accent}20`,
                                            transform: isHovered ? "rotate(-5deg) scale(1.1)" : "none",
                                        }}
                                    >
                                        <Building2
                                            size={22}
                                            style={{ color: colors.accent }}
                                        />
                                    </div>
                                    <div className="flex-1 min-w-0">
                                        <h3 className="text-white font-bold text-base truncate mb-1">
                                            {tenant.tenantName}
                                        </h3>
                                        <p className="text-[#A0AEC0] text-xs font-mono uppercase tracking-wider">
                                            {tenant.tenantCode}
                                        </p>
                                    </div>
                                    <ChevronRight
                                        size={18}
                                        className={`text-[#A0AEC0] transition-all duration-300 mt-1 ${
                                            isHovered ? "translate-x-1 text-white" : ""
                                        }`}
                                    />
                                </div>

                                {/* Role Badge */}
                                <div className="mt-4 flex items-center gap-2">
                                    <span
                                        className="inline-flex items-center px-2.5 py-1 rounded-lg text-[10px] font-bold uppercase tracking-wider"
                                        style={{
                                            backgroundColor: `${colors.accent}15`,
                                            color: colors.accent,
                                        }}
                                    >
                                        {roleTranslations[tenant.role] || tenant.role}
                                    </span>
                                </div>

                                {/* Selected Indicator */}
                                {isSelected && (
                                    <div className="absolute inset-0 rounded-2xl border-2 border-white/30 pointer-events-none" />
                                )}
                            </button>
                        );
                    })}
                </div>

                {/* Footer Info */}
                <p className="mt-8 text-[#A0AEC0]/60 text-xs text-center">
                    {tenants.length} kurum erişiminiz bulunmaktadır • Sidebar üzerinden kurum değiştirebilirsiniz
                </p>
            </main>

            {/* Animation Keyframes */}
            <style jsx>{`
                @keyframes fadeInUp {
                    from {
                        opacity: 0;
                        transform: translateY(20px);
                    }
                    to {
                        opacity: 1;
                        transform: translateY(0);
                    }
                }
            `}</style>
        </div>
    );
}
