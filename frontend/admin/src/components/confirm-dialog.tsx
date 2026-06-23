"use client";

import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { AlertTriangle } from "lucide-react";

interface ConfirmDialogProps {
    open: boolean;
    onClose: () => void;
    onConfirm: () => void;
    title?: string;
    message?: React.ReactNode;
    confirmText?: string;
    cancelText?: string;
    variant?: "danger" | "warning" | "info";
}

const variantStyles = {
    danger: { icon: "#ef4444", iconBg: "#fef2f2", btn: "bg-red-600 hover:bg-red-700 text-white" },
    warning: { icon: "#f59e0b", iconBg: "#fffbeb", btn: "bg-amber-600 hover:bg-amber-700 text-white" },
    info: { icon: "#3b82f6", iconBg: "#eff6ff", btn: "bg-blue-600 hover:bg-blue-700 text-white" },
};

export function ConfirmDialog({
    open, onClose, onConfirm,
    title = "Emin misiniz?",
    message = "Bu işlem geri alınamaz.",
    confirmText = "Evet, Sil",
    cancelText = "İptal",
    variant = "danger",
}: ConfirmDialogProps) {
    const [mounted, setMounted] = useState(false);
    useEffect(() => setMounted(true), []);

    if (!open || !mounted) return null;
    const v = variantStyles[variant];

    return createPortal(
        <div className="fixed inset-0 z-[99999] flex items-center justify-center p-4" onClick={onClose}>
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
            <div className="relative w-full max-w-sm bg-white rounded-2xl shadow-xl animate-fade-in p-6 text-center"
                onClick={e => e.stopPropagation()}>
                <div className="w-14 h-14 rounded-full mx-auto mb-4 flex items-center justify-center"
                    style={{ background: v.iconBg }}>
                    <AlertTriangle size={28} style={{ color: v.icon }} />
                </div>
                <h3 className="text-lg font-bold text-[#0A1931] mb-2">{title}</h3>
                <p className="text-sm text-[#A9A9A9] mb-6">{message}</p>
                <div className="flex gap-3">
                    <button onClick={onClose}
                        className="flex-1 py-2.5 rounded-xl text-sm font-medium bg-[#E2E8F0]/40 text-[#1B3B6F] hover:bg-[#E2E8F0] transition-colors">
                        {cancelText}
                    </button>
                    <button onClick={() => { onConfirm(); onClose(); }}
                        className={`flex-1 py-2.5 rounded-xl text-sm font-medium transition-colors ${v.btn}`}>
                        {confirmText}
                    </button>
                </div>
            </div>
        </div>,
        document.body
    );
}
