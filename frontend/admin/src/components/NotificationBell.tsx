"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import { createPortal } from "react-dom";
import { Bell, X, CheckCheck } from "lucide-react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { notificationApi, type NotificationDto } from "@/lib/api";
import { useNotifications } from "@/hooks/useNotifications";

const TYPE_COLORS: Record<string, string> = {
    Acil: "bg-red-500",
    Uyarı: "bg-amber-500",
    Başarı: "bg-emerald-500",
    Bilgi: "bg-blue-500",
};

export default function NotificationBell() {
    const { token, currentTenantId: tenantId } = useAuth();
    const [unread, setUnread] = useState(0);
    const [items, setItems] = useState<NotificationDto[]>([]);
    const [open, setOpen] = useState(false);
    const [selectedNotif, setSelectedNotif] = useState<NotificationDto | null>(null);
    const dropdownRef = useRef<HTMLDivElement>(null);
    const [mounted, setMounted] = useState(false);

    useEffect(() => setMounted(true), []);

    // İlk yükleme: unread count + son 10 bildirim
    const loadInitial = useCallback(async () => {
        if (!token || !tenantId) return;
        try {
            const [countRes, listRes] = await Promise.all([
                notificationApi.unreadCount(token, tenantId),
                notificationApi.list(token, tenantId, 1, 10),
            ]);
            setUnread(countRes);
            setItems(listRes.items ?? listRes);
        } catch { /* sessiz */ }
    }, [token, tenantId]);

    // SignalR: anlık bildirim — polling yerine
    useNotifications({
        onReceive: (notif) => {
            setItems(prev => [notif, ...prev].slice(0, 10));
            setUnread(prev => prev + 1);
        },
    });

    useEffect(() => { loadInitial(); }, [loadInitial]);

    // Dropdown dışına tıklamada kapat
    useEffect(() => {
        const handler = (e: MouseEvent) => {
            if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node))
                setOpen(false);
        };
        document.addEventListener("mousedown", handler);
        return () => document.removeEventListener("mousedown", handler);
    }, []);

    const handleMarkAllRead = async () => {
        if (!token || !tenantId) return;
        try {
            await notificationApi.markAllRead(token, tenantId);
            setUnread(0);
            setItems(prev => prev.map(n => ({ ...n, isRead: true })));
        } catch { /* sessiz */ }
    };

    const handleNotifClick = async (n: NotificationDto) => {
        setSelectedNotif(n);
        setOpen(false); // Dropdown'u kapat
        if (!n.isRead && token && tenantId) {
            try {
                await notificationApi.markRead(token, tenantId, n.id);
                setItems(prev => prev.map(x => x.id === n.id ? { ...x, isRead: true } : x));
                setUnread(prev => Math.max(0, prev - 1));
            } catch { /* sessiz */ }
        }
    };

    return (
        <div className="relative" ref={dropdownRef}>
            {/* Zil butonu */}
            <button
                id="notification-bell-btn"
                onClick={() => { setOpen(o => !o); if (!open) loadInitial(); }}
                className="relative p-2 rounded-xl hover:bg-[#E2E8F0]/40 text-[#A9A9A9] hover:text-[#1B3B6F] transition-colors"
                aria-label="Bildirimler"
            >
                <Bell size={20} />
                {unread > 0 && (
                    <span className="absolute top-1 right-1 min-w-[18px] h-[18px] bg-red-500 text-white text-[10px] font-bold rounded-full flex items-center justify-center px-1 shadow-sm animate-bounce-once">
                        {unread > 99 ? "99+" : unread}
                    </span>
                )}
            </button>

            {/* Dropdown */}
            {open && (
                <div className="absolute left-full ml-4 top-0 w-80 bg-white rounded-2xl shadow-2xl border border-[#E2E8F0]/60 z-[100] overflow-hidden animate-dropdown origin-top-left">
                    {/* Header */}
                    <div className="flex items-center justify-between px-4 py-3 border-b border-[#E2E8F0]/60">
                        <h3 className="text-sm font-bold text-[#0A1931] flex items-center gap-2">
                            <Bell size={14} className="text-[#1B3B6F]" />
                            Bildirimler
                            {unread > 0 && (
                                <span className="bg-red-100 text-red-600 text-[10px] font-bold px-1.5 py-0.5 rounded-full">
                                    {unread}
                                </span>
                            )}
                        </h3>
                        <div className="flex items-center gap-1">
                            {unread > 0 && (
                                <button
                                    onClick={handleMarkAllRead}
                                    className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/40 text-[#A0AEC0] hover:text-[#1B3B6F] transition-colors"
                                    title="Tümünü okundu işaretle"
                                >
                                    <CheckCheck size={14} />
                                </button>
                            )}
                            <button onClick={() => setOpen(false)} className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/40 text-[#A0AEC0]">
                                <X size={14} />
                            </button>
                        </div>
                    </div>

                    {/* Liste */}
                    <div className="max-h-[340px] overflow-y-auto divide-y divide-[#E2E8F0]">
                        {items.length === 0 ? (
                            <div className="py-10 text-center">
                                <p className="text-2xl mb-2">🔕</p>
                                <p className="text-[#A0AEC0] text-xs">Bildirim yok</p>
                            </div>
                        ) : (
                            items.map(n => (
                                <div key={n.id}
                                    onClick={() => handleNotifClick(n)}
                                    className={`px-4 py-3 flex items-start gap-3 transition-colors cursor-pointer ${!n.isRead ? "bg-[#E2E8F0]/30" : "hover:bg-[#E2E8F0]/20"}`}
                                >
                                    <span className={`w-2 h-2 rounded-full mt-1.5 shrink-0 ${TYPE_COLORS[n.type ?? ""] ?? "bg-[#A0AEC0]"}`} />
                                    <div className="flex-1 min-w-0">
                                        <p className={`text-xs font-semibold truncate ${n.isRead ? "text-[#A9A9A9]" : "text-[#0A1931]"}`}>{n.title}</p>
                                        <p className="text-[11px] text-[#A0AEC0] line-clamp-2 mt-0.5">{n.body}</p>
                                    </div>
                                    {!n.isRead && <span className="w-1.5 h-1.5 bg-[#1B3B6F] rounded-full mt-1.5 shrink-0" />}
                                </div>
                            ))
                        )}
                    </div>

                    {/* Footer */}
                    <div className="px-4 py-3 border-t border-[#E2E8F0]/60 bg-[#E2E8F0]/20">
                        <Link
                            href="/dashboard/notifications"
                            onClick={() => setOpen(false)}
                            className="text-xs text-[#1B3B6F] font-semibold hover:text-[#0A1931] transition-colors"
                        >
                            Tümünü gör →
                        </Link>
                    </div>
                </div>
            )}

            {/* Bildirim Detay Modalı */}
            {selectedNotif && mounted && createPortal(
                <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4" style={{ position: 'fixed' }}>
                    <div className="absolute inset-0 bg-[#0A1931]/40 backdrop-blur-sm transition-opacity duration-300" onClick={() => setSelectedNotif(null)} />
                    <div className="relative w-full max-w-md bg-white rounded-2xl shadow-[0_20px_50px_rgba(10,25,49,0.15)] animate-dropdown overflow-hidden flex flex-col">
                        {/* Decorative Top Accent */}
                        <div className={`h-1.5 w-full ${TYPE_COLORS[selectedNotif.type ?? ""] ?? "bg-[#1B3B6F]"}`} />
                        
                        <div className="flex items-center justify-between px-6 py-4 border-b border-[#E2E8F0]/50 bg-white">
                            <div className="flex items-center gap-2.5">
                                <span className="flex h-7 w-7 items-center justify-center rounded-full bg-[#E2E8F0]/30">
                                    <Bell size={14} className="text-[#1B3B6F]" />
                                </span>
                                <h3 className="text-sm font-extrabold text-[#0A1931] tracking-tight uppercase">Bildirim Detayı</h3>
                            </div>
                            <button onClick={() => setSelectedNotif(null)} className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/60 text-[#A0AEC0] hover:text-[#0A1931] transition-all">
                                <X size={16} strokeWidth={2.5} />
                            </button>
                        </div>
                        
                        <div className="p-6 bg-gradient-to-b from-white to-[#F8FAFC]">
                            <h4 className="text-[15px] font-bold text-[#1B3B6F] mb-4 leading-snug">
                                {selectedNotif.title}
                            </h4>
                            <div className="p-4 rounded-xl bg-white border border-[#E2E8F0]/60 shadow-sm mb-6">
                                <p className="text-[13px] text-[#4A5568] leading-relaxed whitespace-pre-wrap">
                                    {selectedNotif.body}
                                </p>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-[11px] font-semibold text-[#A0AEC0] bg-[#E2E8F0]/40 px-2.5 py-1.5 rounded-md">
                                    {new Date(selectedNotif.createdAt).toLocaleString("tr-TR", { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' })}
                                </span>
                                <button 
                                    onClick={() => setSelectedNotif(null)}
                                    className="text-[12px] font-bold text-[#1B3B6F] bg-[#1B3B6F]/5 hover:bg-[#1B3B6F]/10 px-4 py-2 rounded-lg transition-colors"
                                >
                                    Kapat
                                </button>
                            </div>
                        </div>
                    </div>
                </div>,
                document.body
            )}
        </div>
    );
}
