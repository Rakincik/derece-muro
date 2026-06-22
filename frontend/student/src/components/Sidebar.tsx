"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useEffect, useState } from "react";
import { notificationApi, tenantApi, type UserTenantDto, type TenantBrandingDto } from "@/lib/api";
import NotificationsModal from "./NotificationsModal";
import {
    LayoutDashboard, BookOpen, Radio, CalendarDays,
    ClipboardList, FileText, BookMarked, CalendarCheck,
    Mic2, MessageCircleQuestion, Bell, LogOut, ChevronDown, Headset
} from "lucide-react";

const sections = [
    {
        title: "ÖĞRENİM",
        items: [
            { href: "/dashboard", label: "Ana Sayfa", icon: LayoutDashboard },
            { href: "/dashboard/courses", label: "Derslerim", icon: BookOpen },
            { href: "/dashboard/live", label: "Canlı Dersler", icon: Radio, liveIndicator: true },
            { href: "/dashboard/calendar", label: "Takvim", icon: CalendarDays },
        ]
    },
    {
        title: "AKADEMİK",
        items: [
            { href: "/dashboard/assignments", label: "Ödevlerim", icon: ClipboardList },
            { href: "/dashboard/exams", label: "Sınavlarım", icon: FileText },
            { href: "/dashboard/notes", label: "Notlarım", icon: BookMarked },
            { href: "/dashboard/attendance", label: "Devam Takibim", icon: CalendarCheck },
        ]
    },
    {
        title: "İLETİŞİM & EKSTRA",
        items: [
            { href: "/dashboard/podcast", label: "Podcast", icon: Mic2 },
            { href: "/dashboard/questions", label: "Soru Sor", icon: MessageCircleQuestion },
            { href: "/dashboard/support", label: "Teknik Destek", icon: Headset },
        ]
    }
];

export default function Sidebar({ isOpen }: { isOpen?: boolean }) {
    const pathname = usePathname();
    const router = useRouter();
    const { user, logout, token, currentTenantId } = useAuth();
    const [unreadCount, setUnreadCount] = useState(0);
    const [branding, setBranding] = useState<TenantBrandingDto | null>(null);
    const [showNotifications, setShowNotifications] = useState(false);
    const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({});

    const toggleSection = (title: string) => {
        setExpandedSections(prev => ({ ...prev, [title]: prev[title] === undefined ? false : !prev[title] }));
    };

    useEffect(() => {
        if (!token || !currentTenantId) return;
        notificationApi.unreadCount(token, currentTenantId)
            .then(setUnreadCount)
            .catch(() => { });
    }, [token, currentTenantId]);

    useEffect(() => {
        tenantApi.getBranding(currentTenantId ?? undefined)
            .then(setBranding)
            .catch(() => { });
    }, [currentTenantId]);

    const brandName = branding?.name || "MURO";
    const brandInitial = brandName.charAt(0).toUpperCase();
    const primaryColor = branding?.primaryColor || "#1B3B6F";

    const tenantName = user?.tenants?.find((t: UserTenantDto) => t.tenantId === currentTenantId)?.tenantName
        || user?.tenants?.[0]?.tenantName
        || "Öğrenci Portalı";

    const handleLogout = () => {
        logout();
        router.push("/");
    };

    return (
        <>
        <aside 
            className={`sidebar w-[280px] md:w-[260px] flex flex-col h-screen fixed left-0 top-0 z-[70] md:z-50 border-r border-[#1B3B6F]/20 bg-[#0A1931] transition-transform duration-300 md:translate-x-0 ${!isOpen ? "-translate-x-full" : ""}`}
            style={isOpen ? { transform: 'translateX(0)' } : undefined}
        >
            {/* Logo + Notifications */}
            <div className="px-6 py-7 flex items-center justify-between relative">
                <img 
                    src="/monopol_logo.png" 
                    alt={brandName} 
                    className="w-36 h-auto object-contain drop-shadow-md" 
                />
                <button
                    onClick={() => setShowNotifications(true)}
                    className="relative p-2 text-[#A0AEC0] hover:text-white hover:bg-[#1B3B6F]/30 rounded-xl transition-all"
                    title="Bildirimler"
                >
                    <Bell size={20} />
                    {unreadCount > 0 && (
                        <span className="absolute top-1.5 right-1.5 w-2.5 h-2.5 bg-red-500 rounded-full border border-[#0A1931] shadow-[0_0_8px_rgba(239,68,68,0.6)] animate-pulse" />
                    )}
                </button>
            </div>

            {/* Nav */}
            <nav className="flex-1 overflow-y-auto px-3 py-2 space-y-5">
                {sections.map((section) => (
                    <div key={section.title}>
                        <button 
                            onClick={() => toggleSection(section.title)}
                            className="w-full flex items-center justify-between px-3 mb-2 group"
                        >
                            <p className="text-xs font-bold tracking-wider text-[#A9A9A9] uppercase group-hover:text-white transition-colors">
                                {section.title}
                            </p>
                            <ChevronDown size={12} className={`text-[#A9A9A9] transition-transform duration-200 ${expandedSections[section.title] === false ? 'rotate-180' : ''}`} />
                        </button>
                        <div className={`space-y-0.5 overflow-hidden transition-all duration-300 ${expandedSections[section.title] === false ? 'max-h-0 opacity-0' : 'max-h-[500px] opacity-100'}`}>
                            {section.items.map((item) => {
                                const isActive = item.href === "/dashboard"
                                    ? pathname === "/dashboard"
                                    : pathname.startsWith(item.href);
                                const Icon = item.icon;
                                
                                return (
                                    <Link
                                        key={item.href}
                                        href={item.href}
                                        prefetch={false}
                                        className={`group relative flex items-center gap-3 px-4 py-2.5 rounded-xl text-[13px] font-bold uppercase tracking-wider transition-all duration-200 ${isActive
                                            ? "bg-[#1B3B6F] text-white shadow-lg shadow-[#0A1931]/40"
                                            : "text-[#A0AEC0] hover:bg-[#1B3B6F]/20 hover:text-[#E2E8F0]"
                                            }`}
                                    >
                                        <Icon size={18} strokeWidth={isActive ? 2.5 : 1.5} className={isActive ? "text-white" : "text-[#A9A9A9] group-hover:text-[#E2E8F0]"} />
                                        <span className="flex-1">{item.label}</span>
                                        {item.liveIndicator && (
                                            <span className="w-1.5 h-1.5 rounded-full bg-red-500 animate-pulse flex-shrink-0 shadow-[0_0_8px_rgba(239,68,68,0.6)]" />
                                        )}
                                        {isActive && (
                                            <div className="absolute right-4 w-1.5 h-1.5 rounded-full bg-[#A9A9A9] shadow-[0_0_8px_rgba(169,169,169,0.5)]" />
                                        )}
                                    </Link>
                                );
                            })}
                        </div>
                    </div>
                ))}

                {/* SİSTEM Area Section */}
                <div>
                    <button 
                        onClick={() => toggleSection('SİSTEM')}
                        className="w-full flex items-center justify-between px-3 mb-2 group"
                    >
                        <p className="text-xs font-bold tracking-wider text-[#A9A9A9] uppercase group-hover:text-white transition-colors">
                            SİSTEM
                        </p>
                        <ChevronDown size={12} className={`text-[#A9A9A9] transition-transform duration-200 ${expandedSections['SİSTEM'] === false ? 'rotate-180' : ''}`} />
                    </button>
                    <div className={`space-y-0.5 overflow-hidden transition-all duration-300 ${expandedSections['SİSTEM'] === false ? 'max-h-0 opacity-0' : 'max-h-[500px] opacity-100'}`}>
                        <button
                            onClick={() => setShowNotifications(true)}
                            className={`w-full text-left group relative flex items-center gap-3 px-4 py-2.5 rounded-xl text-[13px] font-bold uppercase tracking-wider transition-all duration-200 ${showNotifications
                                ? "bg-[#1B3B6F] text-white shadow-lg shadow-[#0A1931]/40"
                                : "text-[#A0AEC0] hover:bg-[#1B3B6F]/20 hover:text-[#E2E8F0]"
                                }`}
                        >
                            <Bell size={18} strokeWidth={showNotifications ? 2.5 : 1.5} className={showNotifications ? "text-white" : "text-[#A9A9A9] group-hover:text-[#E2E8F0]"} />
                            <span className="flex-1">Bildirimler</span>
                            {unreadCount > 0 && (
                                <span className="px-1.5 py-0.5 bg-[#1B3B6F] border border-[#1B3B6F]/50 text-white text-xs rounded-full font-medium min-w-[18px] text-center flex-shrink-0 shadow-sm">
                                    {unreadCount > 9 ? "9+" : unreadCount}
                                </span>
                            )}
                        </button>
                    </div>
                </div>
            </nav>

            {/* User footer */}
            <div className="p-4 mx-3 mb-6 rounded-2xl bg-[#1B3B6F]/15 border border-[#1B3B6F]/20">
                <div className="flex items-center gap-3">
                    <Link href="/dashboard/profile" className="flex items-center gap-3 flex-1 min-w-0 group cursor-pointer">
                        <div className="w-10 h-10 rounded-2xl flex items-center justify-center text-white text-xs font-bold shrink-0 bg-[#1B3B6F] border border-[#A0AEC0]/10 transition-transform group-hover:scale-105">
                            {user?.firstName?.[0]}{user?.lastName?.[0]}
                        </div>
                        <div className="flex-1 min-w-0">
                            <p className="text-[13px] font-semibold text-white truncate group-hover:text-blue-200 transition-colors">
                                {user?.firstName} {user?.lastName}
                            </p>
                            <p className="text-[11px] text-[#A9A9A9] truncate">Öğrenci</p>
                        </div>
                    </Link>
                    <button
                        onClick={handleLogout}
                        className="p-1.5 rounded-lg text-[#A9A9A9] hover:text-red-400 hover:bg-red-400/10 transition-all shrink-0"
                        title="Çıkış Yap"
                    >
                        <LogOut size={16} />
                    </button>
                </div>
            </div>
        </aside>

        {showNotifications && (
            <NotificationsModal 
                onClose={() => setShowNotifications(false)} 
                onUnreadCountUpdate={setUnreadCount}
            />
        )}
        </>
    );
}
