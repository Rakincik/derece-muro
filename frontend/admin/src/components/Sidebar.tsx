"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import Image from "next/image";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import NotificationBell from "@/components/NotificationBell";
import { tenantApi, type TenantBrandingDto } from "@/lib/api";
import {
    LayoutDashboard, Users, FolderTree, BookOpen, FileText,
    CalendarDays, ClipboardList, Bell, MessageCircleQuestion,
    HeadphonesIcon, Mic2, BarChart3, Wallet,
    LogOut, ChevronRight, Trophy, User, CalendarCheck, Package, Shield,
    PlaySquare, ChevronDown, Building2
} from "lucide-react";

const sections = [
    {
        title: "ANA MENÜ",
        items: [
            { label: "Ana Sayfa", href: "/dashboard", icon: LayoutDashboard, roles: ["Admin", "SuperAdmin", "Assistant", "Instructor"] },
        ]
    },
    {
        title: "YÖNETİM",
        items: [
            { label: "Kullanıcılar", href: "/dashboard/users", icon: Users, roles: ["Admin", "SuperAdmin", "Assistant"] },
            { label: "Gruplar", href: "/dashboard/groups", icon: FolderTree, roles: ["Admin", "SuperAdmin", "Assistant"], featureKey: "groups" },
            { label: "Paketler", href: "/dashboard/packages", icon: Package, roles: ["Admin", "SuperAdmin", "Assistant"], featureKey: "packages" },
            { label: "Dersler", href: "/dashboard/courses", icon: BookOpen, roles: ["Admin", "SuperAdmin", "Assistant", "Instructor"] },
        ]
    },
    {
        title: "AKADEMİK",
        items: [
            { label: "Sınavlar", href: "/dashboard/exams", icon: FileText, roles: ["Admin", "SuperAdmin", "Assistant"], featureKey: "exams" },
            { label: "Takvim", href: "/dashboard/calendar", icon: CalendarDays, roles: ["Admin", "SuperAdmin", "Assistant", "Instructor"], featureKey: "calendar" },
            { label: "Ödevler", href: "/dashboard/assignments", icon: ClipboardList, roles: ["Admin", "SuperAdmin", "Assistant", "Instructor"], featureKey: "assignments" },
        ]
    },
    {
        title: "İLETİŞİM",
        items: [
            { label: "Bildirimler", href: "/dashboard/notifications", icon: Bell, roles: ["Admin", "SuperAdmin", "Assistant"], featureKey: "notifications" },
            { label: "Soru Sor", href: "/dashboard/questions", icon: MessageCircleQuestion, roles: ["Admin", "SuperAdmin", "Assistant", "Instructor"], featureKey: "questions" },
            { label: "Destek", href: "/dashboard/support", icon: HeadphonesIcon, roles: ["Admin", "SuperAdmin", "Assistant"], featureKey: "support" },
        ]
    },
    {
        title: "İÇERİK",
        items: [
            { label: "Medya Kütüphanesi", href: "/dashboard/media", icon: PlaySquare, roles: ["Admin", "SuperAdmin", "Assistant"], featureKey: "mediaLibrary" },
            { label: "Podcast", href: "/dashboard/podcasts", icon: Mic2, roles: ["Admin", "SuperAdmin", "Assistant"], featureKey: "podcast" },
        ]
    },
    {
        title: "ANALİZ",
        items: [
            { label: "Performans", href: "/dashboard/analytics", icon: BarChart3, roles: ["Admin", "SuperAdmin", "Assistant"], featureKey: "analytics" },
            { label: "Sınav Sonuçları", href: "/dashboard/exam-results", icon: Trophy, roles: ["Admin", "SuperAdmin", "Assistant", "Instructor"], featureKey: "examResults" },
            { label: "Öğrenci Karnesi", href: "/dashboard/student-scorecard", icon: User, roles: ["Admin", "SuperAdmin", "Assistant"], featureKey: "studentScorecard" },
            { label: "Devam Raporu", href: "/dashboard/course-attendance", icon: CalendarCheck, roles: ["Admin", "SuperAdmin", "Assistant"], featureKey: "attendance" },
            { label: "Muhasebe", href: "/dashboard/accounting", icon: Wallet, roles: ["Admin", "SuperAdmin", "Accountant"], featureKey: "accounting" },
            { label: "Güvenlik", href: "/dashboard/audit-trail", icon: Shield, roles: ["Admin", "SuperAdmin"] },
        ]
    }
];

import { Menu, X } from "lucide-react"; // Make sure to import X

// ...

const roleTranslations: Record<string, string> = {
    "SuperAdmin": "Süper Admin",
    "Admin": "Admin",
    "Instructor": "Eğitmen",
    "Assistant": "Asistan",
    "Accountant": "Muhasebe",
    "Student": "Öğrenci",
    "Teacher": "Öğretmen"
};

export default function Sidebar({ isOpen, onClose }: { isOpen?: boolean, onClose?: () => void }) {
    const pathname = usePathname();
    const { user, logout, currentTenantId, switchTenant } = useAuth();
    const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({});
    const [tenantDropdownOpen, setTenantDropdownOpen] = useState(false);

    const toggleSection = (title: string) => {
        setExpandedSections(prev => ({ ...prev, [title]: prev[title] === undefined ? false : !prev[title] }));
    };

    const [branding, setBranding] = useState<TenantBrandingDto | null>(null);

    useEffect(() => {
        tenantApi.getBranding(currentTenantId ?? undefined)
            .then(setBranding)
            .catch(() => { /* use defaults */ });
    }, [currentTenantId]);

    const brandName = "MURO";
    const brandInitial = brandName.charAt(0).toUpperCase();
    const primaryColor = branding?.primaryColor || "#1B3B6F";

    const currentTenant = user?.tenants.find(t => t.tenantId === currentTenantId);
    let featuresDict: Record<string, boolean> = {};
    if (currentTenant?.features) {
        try {
            featuresDict = JSON.parse(currentTenant.features);
        } catch { }
    }

    return (
        <>
            {/* Mobile Overlay */}
            {isOpen && (
                <div 
                    className="fixed inset-0 bg-black/50 z-40 lg:hidden"
                    onClick={onClose}
                />
            )}
            <aside className={`w-[260px] flex flex-col h-screen fixed left-0 top-0 z-50 border-r border-[#1B3B6F]/20 bg-[#0A1931] transition-transform duration-300 ease-in-out lg:translate-x-0 ${isOpen ? 'translate-x-0' : '-translate-x-full'}`}>
                {/* Logo + Bell */}
                <div className="px-6 py-7 flex items-center justify-between relative">
                    <img 
                        src="/admin/monopol_logo.png" 
                        alt={brandName} 
                        className="w-36 h-auto object-contain drop-shadow-md" 
                    />
                    <div className="hidden lg:block">
                        <NotificationBell />
                    </div>
                    <button 
                        onClick={onClose}
                        className="lg:hidden p-1.5 absolute right-4 top-1/2 -translate-y-1/2 text-[#A9A9A9] hover:text-white rounded-lg hover:bg-white/10"
                    >
                        <X size={20} />
                    </button>
                </div>

            {/* Tenant Switcher — sadece birden fazla kurum varsa göster */}
            {user && user.tenants.length > 1 && (
                <div className="px-3 pb-3">
                    <div className="relative">
                        <button
                            onClick={() => setTenantDropdownOpen(!tenantDropdownOpen)}
                            className="w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl bg-[#1B3B6F]/15 border border-[#1B3B6F]/20 hover:bg-[#1B3B6F]/25 transition-all group"
                        >
                            <div className="w-7 h-7 rounded-lg bg-[#1B3B6F]/30 flex items-center justify-center shrink-0">
                                <Building2 size={14} className="text-[#A0AEC0]" />
                            </div>
                            <div className="flex-1 min-w-0 text-left">
                                <p className="text-[11px] text-[#A0AEC0]/70 font-medium leading-none mb-0.5">Aktif Kurum</p>
                                <p className="text-[12px] text-white font-semibold truncate">
                                    {currentTenant?.tenantName || "Kurum Seçilmedi"}
                                </p>
                            </div>
                            <ChevronDown
                                size={14}
                                className={`text-[#A0AEC0] transition-transform duration-200 ${tenantDropdownOpen ? "rotate-180" : ""}`}
                            />
                        </button>

                        {/* Dropdown */}
                        {tenantDropdownOpen && (
                            <div className="absolute left-0 right-0 top-full mt-1 bg-[#0F2847] border border-[#1B3B6F]/30 rounded-xl shadow-2xl shadow-black/40 overflow-hidden z-50">
                                {user.tenants
                                    .filter(t => t.status === "active" || t.status === "Active")
                                    .map((tenant) => (
                                        <button
                                            key={tenant.tenantId}
                                            onClick={() => {
                                                switchTenant(tenant.tenantId);
                                                setTenantDropdownOpen(false);
                                            }}
                                            className={`w-full flex items-center gap-2.5 px-3 py-2.5 text-left transition-all ${
                                                tenant.tenantId === currentTenantId
                                                    ? "bg-[#1B3B6F]/30 text-white"
                                                    : "text-[#A0AEC0] hover:bg-[#1B3B6F]/15 hover:text-white"
                                            }`}
                                        >
                                            <div className={`w-2 h-2 rounded-full shrink-0 ${
                                                tenant.tenantId === currentTenantId ? "bg-emerald-400" : "bg-[#A0AEC0]/30"
                                            }`} />
                                            <div className="flex-1 min-w-0">
                                                <p className="text-[12px] font-semibold truncate">{tenant.tenantName}</p>
                                                <p className="text-[10px] text-[#A0AEC0]/60 font-mono uppercase">{tenant.tenantCode}</p>
                                            </div>
                                            {tenant.tenantId === currentTenantId && (
                                                <span className="text-[9px] font-bold text-emerald-400 uppercase">Aktif</span>
                                            )}
                                        </button>
                                    ))}
                            </div>
                        )}
                    </div>
                </div>
            )}

            {/* Navigation */}
            <nav className="flex-1 overflow-y-auto px-3 py-2 space-y-5">
                {sections.map((section) => {
                    const filteredItems = section.items.filter(item => {
                        const hasRole = !user?.role || item.roles.includes(user.role);
                        const hasFeature = !("featureKey" in item) || featuresDict[(item as any).featureKey] === true;
                        
                        // Eğer kuruma özel özellik tanımı yoksa (features boşsa), varsayılan olarak açık kabul et 
                        // veya kapalı kabul et. Åimdilik kapalı kabul ediyoruz (sadece olanlar girsin)
                        // Ancak features objesi hiç yoksa (eski tenant), hepsine izin ver:
                        const finalHasFeature = currentTenant?.features ? hasFeature : true;

                        return hasRole && finalHasFeature;
                    });
                    
                    if (filteredItems.length === 0) return null;

                    return (
                        <div key={section.title}>
                            <button 
                                onClick={() => toggleSection(section.title)}
                                className="w-full flex items-center justify-between px-3 mb-2 group"
                            >
                                <p className="text-xs font-bold tracking-wider text-[#A9A9A9] uppercase group-hover:text-white transition-colors">
                                    {section.title}
                                </p>
                                {section.title !== "ANA MENÜ" && (
                                    <ChevronDown size={12} className={`text-[#A9A9A9] transition-transform duration-200 ${expandedSections[section.title] === false ? 'rotate-180' : ''}`} />
                                )}
                            </button>
                            <div className={`space-y-0.5 overflow-hidden transition-all duration-300 ${expandedSections[section.title] === false ? 'max-h-0 opacity-0' : 'max-h-[500px] opacity-100'}`}>
                                {filteredItems.map((item) => {
                                    const isActive = pathname === item.href ||
                                        (item.href !== "/dashboard" && pathname?.startsWith(item.href));
                                    const Icon = item.icon;
                                    return (
                                        <Link
                                            key={item.href}
                                            href={item.href}
                                            prefetch={false}
                                            className={`group flex items-center gap-3 px-4 py-2.5 rounded-xl text-[13px] font-bold uppercase tracking-wider transition-all duration-200 ${isActive
                                                ? "bg-[#1B3B6F] text-white shadow-lg shadow-[#0A1931]/40"
                                                : "text-[#A0AEC0] hover:bg-[#1B3B6F]/20 hover:text-[#E2E8F0]"
                                                }`}
                                        >
                                            <Icon size={18} strokeWidth={isActive ? 2.5 : 1.5} className={isActive ? "text-white" : "text-[#A9A9A9] group-hover:text-[#E2E8F0]"} />
                                            <span className="flex-1">{item.label}</span>
                                            {isActive && (
                                                <div className="w-1.5 h-1.5 rounded-full bg-[#A9A9A9] shadow-[0_0_8px_rgba(169,169,169,0.5)]" />
                                            )}
                                        </Link>
                                    );
                                })}
                            </div>
                        </div>
                    );
                })}
            </nav>

            {/* User Section */}
            <div className="p-4 mx-3 mb-6 rounded-2xl bg-[#1B3B6F]/15 border border-[#1B3B6F]/20">
                <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-2xl flex items-center justify-center text-white text-xs font-bold shrink-0 bg-[#1B3B6F] border border-[#A0AEC0]/10">
                        {user?.firstName?.[0]}{user?.lastName?.[0]}
                    </div>
                    <div className="flex-1 min-w-0">
                        <p className="text-[13px] font-semibold text-white truncate">
                            {user?.firstName} {user?.lastName}
                        </p>
                        <p className="text-[11px] text-[#A9A9A9] truncate">{user?.role ? (roleTranslations[user.role] || user.role) : ""}</p>
                    </div>
                    <button
                        onClick={logout}
                        className="p-1.5 rounded-lg text-[#A9A9A9] hover:text-red-400 hover:bg-red-400/10 transition-all"
                        title="Çıkış Yap"
                    >
                        <LogOut size={16} />
                    </button>
                </div>
            </div>
        </aside>
        </>
    );
}
