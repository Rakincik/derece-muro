"use client";

import { useEffect, useRef, useState, useCallback, useMemo } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { questionApi, courseApi, type QuestionDto, type CourseDto } from "@/lib/api";
import { compressImage } from "@/lib/imageUtils";
import {
    MessageSquare, Search, Send, Plus, X, Mic, MicOff, Trash2,
    Image as ImageIcon, StickyNote, Upload, CheckCircle2, Clock, AlertCircle,
    ChevronDown, Check
} from "lucide-react";
import { ConfirmDialog } from "@/components/confirm-dialog";

// ── Helpers ───────────────────────────────────────────────────────────────────
const getFileUrl = (path: string | null) => {
    if (!path) return "";
    if (path.startsWith("http") || path.startsWith("blob:") || path.startsWith("data:")) return path;
    let hostname = "localhost";
    if (typeof window !== "undefined") {
        hostname = window.location.hostname;
    }
    const base = process.env.NEXT_PUBLIC_API_URL?.replace("localhost", hostname).replace("/api/v1", "") || `http://${hostname}:5292`;
    return `${base}${path.startsWith("/") ? "" : "/"}${path}`;
};

const statusStyles: Record<string, { bg: string; text: string; dot: string }> = {
    Bekliyor: { bg: "bg-amber-50", text: "text-amber-700", dot: "bg-amber-500" },
    Yanıtlandı: { bg: "bg-emerald-50", text: "text-emerald-700", dot: "bg-emerald-500" },
    Kapandı: { bg: "bg-[#E2E8F0]/40", text: "text-[#A9A9A9]", dot: "bg-[#A0AEC0]" },
};

function mapQuestionStatus(status: string) {
    const s = status?.toLowerCase() || "";
    if (s === "answered") return "Yanıtlandı";
    if (s === "closed") return "Kapandı";
    return "Bekliyor";
}

// ── Custom Course Select ──────────────────────────────────────────────────────
function CustomCourseSelect({ 
    courses, 
    value, 
    onChange 
}: { 
    courses: CourseDto[], 
    value: string, 
    onChange: (id: string) => void 
}) {
    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState("");
    const ref = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (ref.current && !ref.current.contains(event.target as Node)) {
                setOpen(false);
            }
        };
        document.addEventListener("mousedown", handleClickOutside);
        return () => document.removeEventListener("mousedown", handleClickOutside);
    }, []);

    const selected = courses.find(c => c.id === value);
    const filtered = courses.filter(c => c.title.toLowerCase().includes(search.toLowerCase()));

    return (
        <div ref={ref} className="relative">
            <button
                type="button"
                onClick={() => setOpen(!open)}
                className="w-full bg-[#F8FAFC] border border-[#E2E8F0] rounded-xl px-4 py-2.5 text-sm font-medium text-left flex justify-between items-center focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:border-[#1B3B6F] focus:bg-white transition-all shadow-sm"
            >
                <span className={`block truncate ${selected ? 'text-[#0A1931]' : 'text-[#A9A9A9]'}`}>
                    {selected ? selected.title : "Lütfen bir ders seçiniz..."}
                </span>
                <ChevronDown size={16} className={`text-[#A0AEC0] transition-transform ${open ? 'rotate-180' : ''}`} />
            </button>
            
            {open && (
                <div className="absolute z-[200] w-full mt-1 bg-white border border-[#E2E8F0] rounded-xl shadow-xl overflow-hidden max-h-60 flex flex-col">
                    <div className="p-2 border-b border-[#E2E8F0] shrink-0 sticky top-0 bg-white z-10">
                        <div className="relative">
                            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                            <input
                                type="text"
                                placeholder="Ders ara..."
                                value={search}
                                onChange={e => setSearch(e.target.value)}
                                className="w-full bg-[#F8FAFC] border border-[#E2E8F0] rounded-lg pl-8 pr-3 py-1.5 text-xs text-[#0A1931] placeholder-[#A0AEC0] focus:outline-none focus:border-[#1B3B6F]/50 transition-colors"
                            />
                        </div>
                    </div>
                    <div className="overflow-y-auto p-1 custom-scroll">
                        {filtered.length === 0 ? (
                            <div className="p-3 text-center text-xs text-[#A0AEC0]">Bulunamadı</div>
                        ) : (
                            filtered.map(c => (
                                <button
                                    key={c.id}
                                    type="button"
                                    onClick={() => {
                                        onChange(c.id);
                                        setOpen(false);
                                        setSearch("");
                                    }}
                                    className={`w-full text-left px-3 py-2 text-xs rounded-lg transition-colors flex items-center justify-between ${value === c.id ? 'bg-[#F0F4FF] text-[#1B3B6F] font-bold' : 'text-[#5A6A7A] hover:bg-[#F8FAFC]'}`}
                                >
                                    <span className="truncate pr-2">{c.title}</span>
                                    {value === c.id && <Check size={14} className="text-[#1B3B6F] shrink-0" />}
                                </button>
                            ))
                        )}
                    </div>
                </div>
            )}
        </div>
    );
}

// ── Audio Recorder Hook ───────────────────────────────────────────────────────
function useAudioRecorder() {
    const [recording, setRecording] = useState(false);
    const [audioBlob, setAudioBlob] = useState<Blob | null>(null);
    const [audioUrl, setAudioUrl] = useState<string | null>(null);
    const [seconds, setSeconds] = useState(0);
    const mediaRef = useRef<MediaRecorder | null>(null);
    const chunksRef = useRef<BlobPart[]>([]);
    const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

    const start = useCallback(async () => {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            const mr = new MediaRecorder(stream);
            chunksRef.current = [];
            mr.ondataavailable = e => chunksRef.current.push(e.data);
            mr.onstop = () => {
                const blob = new Blob(chunksRef.current, { type: "audio/webm" });
                const url = URL.createObjectURL(blob);
                setAudioBlob(blob);
                setAudioUrl(url);
                stream.getTracks().forEach(t => t.stop());
            };
            mr.start();
            mediaRef.current = mr;
            setRecording(true);
            setSeconds(0);
            timerRef.current = setInterval(() => setSeconds(s => s + 1), 1000);
        } catch {
            window.dispatchEvent(new CustomEvent("toast:show", { detail: { message: "Mikrofon erişimi reddedildi.", type: "warning" } }));
        }
    }, []);

    const stop = useCallback(() => {
        mediaRef.current?.stop();
        setRecording(false);
        if (timerRef.current) clearInterval(timerRef.current);
    }, []);

    const clear = useCallback(() => {
        setAudioBlob(null);
        setAudioUrl(null);
        setSeconds(0);
    }, []);

    const formatTime = (s: number) => `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;

    return { recording, audioBlob, audioUrl, seconds, formatTime, start, stop, clear };
}

// ── Image Preview ─────────────────────────────────────────────────────────────
function ImagePreview({ url, onClose }: { url: string; onClose: () => void }) {
    return (
        <div className="fixed inset-0 z-[200] flex items-center justify-center bg-black/80 backdrop-blur-sm" onClick={onClose}>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={getFileUrl(url)} alt="Soru görseli" className="max-w-[90vw] max-h-[85vh] rounded-2xl object-contain shadow-2xl" />
            <button className="absolute top-4 right-4 text-white/70 hover:text-white text-2xl" onClick={onClose}>✕</button>
        </div>
    );
}

// ── Note Editor ───────────────────────────────────────────────────────────────
function NoteEditor({ questionId, initial, token, tenantId, onSaved }: {
    questionId: string; initial: string | null;
    token: string; tenantId: string; onSaved: (note: string | null) => void;
}) {
    const [text, setText] = useState(initial ?? "");
    const [saving, setSaving] = useState(false);
    const [editing, setEditing] = useState(false);

    const save = async () => {
        setSaving(true);
        try {
            await questionApi.updateNote(token, tenantId, questionId, text.trim() || null);
            onSaved(text.trim() || null);
            setEditing(false);
        } finally {
            setSaving(false);
        }
    };

    if (!editing) {
        return (
            <div className="flex items-start gap-2 cursor-pointer group bg-amber-50/50 p-3 rounded-xl border border-amber-100 hover:border-amber-300 transition-colors" onClick={() => setEditing(true)}>
                <StickyNote size={14} className="text-amber-500 mt-0.5 shrink-0" />
                <div className="flex-1">
                    {text ? (
                        <p className="text-[#5A6A7A] text-xs leading-relaxed whitespace-pre-wrap">{text}</p>
                    ) : (
                        <p className="text-[#A0AEC0] text-xs italic group-hover:text-[#5A6A7A] transition-colors">Kişisel not ekle (sadece siz görebilirsiniz)...</p>
                    )}
                </div>
            </div>
        );
    }

    return (
        <div className="bg-amber-50/50 p-3 rounded-xl border border-amber-200 space-y-2">
            <textarea
                value={text}
                onChange={e => setText(e.target.value)}
                autoFocus
                rows={3}
                placeholder="Kişisel notunuzu buraya yazın..."
                className="w-full bg-white border border-amber-200 rounded-lg px-3 py-2 text-[#0A1931] text-xs placeholder-[#A9A9A9] focus:outline-none focus:ring-1 focus:ring-amber-400 resize-none"
            />
            <div className="flex gap-2 justify-end">
                <button onClick={() => setEditing(false)} className="px-3 py-1.5 text-xs text-[#A0AEC0] hover:text-[#0A1931] bg-white border border-[#E2E8F0] rounded-lg transition-colors">İptal</button>
                <button onClick={save} disabled={saving} className="px-3 py-1.5 text-xs text-amber-700 bg-amber-100 border border-amber-200 rounded-lg hover:bg-amber-200 transition-all disabled:opacity-50">
                    {saving ? "Kaydediliyor..." : "Kaydet"}
                </button>
            </div>
        </div>
    );
}

// ── Main Page ─────────────────────────────────────────────────────────────────
export default function QuestionsPage() {
    const { token, currentTenantId: tenantId } = useAuth();

    const [questions, setQuestions] = useState<QuestionDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [selectedId, setSelectedId] = useState<string | null>(null);
    const [search, setSearch] = useState("");
    const [statusFilter, setStatusFilter] = useState("all");
    const [lightboxUrl, setLightboxUrl] = useState<string | null>(null);
    const [confirmConfig, setConfirmConfig] = useState<{open: boolean, title: string, message: string, onConfirm: () => void}>({ open: false, title: "", message: "", onConfirm: () => {} });

    // Form Modal State
    const [showForm, setShowForm] = useState(false);
    const [courses, setCourses] = useState<CourseDto[]>([]);
    const [form, setForm] = useState({ instructorId: "", subject: "", body: "", courseId: "", note: "" });
    const [imageFile, setImageFile] = useState<File | null>(null);
    const [imagePreview, setImagePreview] = useState<string | null>(null);
    const [sending, setSending] = useState(false);
    const [formError, setFormError] = useState<string | null>(null);

    const audio = useAudioRecorder();
    const fileInputRef = useRef<HTMLInputElement>(null);

    // ── Fetch Questions ──
    useEffect(() => {
        if (!token || !tenantId) return;
        setLoading(true);
        questionApi.list(token, tenantId)
            .then(data => {
                setQuestions(data);
                if (data.length > 0 && !selectedId) setSelectedId(data[0].id);
            })
            .catch(() => setQuestions([]))
            .finally(() => setLoading(false));
    }, [token, tenantId]);

    const filtered = useMemo(() => {
        return questions.filter(q => {
            const matchSearch = search === "" || q.subject.toLowerCase().includes(search.toLowerCase()) || q.instructorFullName.toLowerCase().includes(search.toLowerCase());
            const status = mapQuestionStatus(q.status);
            const matchStatus = statusFilter === "all" || status === statusFilter;
            return matchSearch && matchStatus;
        });
    }, [questions, search, statusFilter]);

    const selected = questions.find(q => q.id === selectedId) || null;
    const answeredCount = questions.filter(q => mapQuestionStatus(q.status) === "Yanıtlandı").length;
    const pendingCount = questions.length - answeredCount;

    // ── Form Handlers ──
    const openForm = async () => {
        setShowForm(true);
        if (courses.length === 0 && token && tenantId) {
            try {
                const cors = await courseApi.list(token, tenantId);
                setCourses(cors);
                if (cors.length > 0) {
                    const firstCourse = cors[0];
                    setForm(f => ({ ...f, courseId: firstCourse.id, instructorId: firstCourse.instructorId || "" }));
                }
            } catch { }
        }
    };

    const handleCourseChange = (courseId: string) => {
        const sel = courses.find(c => c.id === courseId);
        setForm(f => ({ ...f, courseId, instructorId: sel?.instructorId || "" }));
    };

    const handleSend = async () => {
        if (!token || !tenantId) return;
        if (!form.instructorId || !form.subject.trim() || !form.body.trim()) {
            setFormError("Eğitmen, konu ve soru alanları zorunludur.");
            return;
        }

        let imageUrl: string | undefined;
        if (imageFile) {
            try {
                imageUrl = await compressImage(imageFile);
            } catch {
                imageUrl = await new Promise<string>(res => {
                    const reader = new FileReader();
                    reader.onload = () => res(reader.result as string);
                    reader.readAsDataURL(imageFile);
                });
            }
        }

        let audioUrl: string | undefined;
        if (audio.audioBlob) {
            audioUrl = await new Promise<string>(res => {
                const reader = new FileReader();
                reader.onload = () => res(reader.result as string);
                reader.readAsDataURL(audio.audioBlob!);
            });
        }

        setSending(true);
        setFormError(null);
        try {
            const q = await questionApi.ask(token, tenantId, {
                instructorId: form.instructorId,
                subject: form.subject.trim(),
                body: form.body.trim(),
                imageUrl,
                audioUrl,
                note: form.note.trim() || undefined,
                courseId: form.courseId || undefined,
            });
            setQuestions(prev => [q, ...prev]);
            setSelectedId(q.id);
            window.dispatchEvent(new CustomEvent("toast:show", { detail: { message: "Soru Gönderildi", type: "success" } }));
            
            setForm({ instructorId: form.instructorId, subject: "", body: "", courseId: "", note: "" });
            setImageFile(null);
            setImagePreview(null);
            audio.clear();
            setShowForm(false);
        } catch (e: any) {
            setFormError(e.message || "Hata oluştu.");
            window.dispatchEvent(new CustomEvent("toast:show", { detail: { message: e.message || "Soru gönderilemedi.", type: "error" } }));
        } finally {
            setSending(false);
        }
    };

    const updateLocalNote = (id: string, note: string | null) => {
        setQuestions(prev => prev.map(q => q.id === id ? { ...q, note } : q));
    };

    const handleDelete = async () => {
        if (!selected || !token || !tenantId) return;
        if (!confirm("Bu soruyu silmek istedi�inize emin misiniz?")) return;

    };

    return (
        <>
        <ConfirmDialog
            open={confirmConfig.open}
            onClose={() => setConfirmConfig(prev => ({ ...prev, open: false }))}
            onConfirm={confirmConfig.onConfirm}
            title={confirmConfig.title}
            message={confirmConfig.message}
            confirmText="Evet, Sil"
            cancelText="İptal"
            variant="danger"
        />
        <div className="space-y-6 pt-16 md:pt-0">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-[#0A1931] flex items-center gap-2">
                        <MessageSquare size={24} className="text-[#1B3B6F]" /> Soru Sor
                    </h1>
                    <p className="text-sm text-[#A9A9A9] mt-1">
                        {questions.length > 0 
                            ? `${questions.length} soru • ${pendingCount} yanıt bekliyor`
                            : "Eğitmenlerinize soru sorun"}
                    </p>
                </div>
                <button
                    onClick={openForm}
                    className="flex items-center gap-2 px-5 py-2.5 bg-[#1B3B6F] hover:bg-[#0A1931] text-white text-sm font-semibold rounded-xl transition-all shadow-md hover:shadow-lg active:scale-95"
                >
                    <Plus size={16} /> Yeni Soru
                </button>
            </div>

            {/* Split Pane Layout */}
            <div className="flex flex-col lg:grid lg:grid-cols-5 gap-6 h-auto lg:h-[calc(100vh-240px)]">
                {/* Left Pane: List */}
                <div className="lg:col-span-2 bg-white rounded-2xl border border-[#E2E8F0]/60 flex flex-col h-[350px] lg:h-auto overflow-hidden shrink-0">
                    <div className="p-4 border-b border-[#E2E8F0]/60 space-y-2">
                        <div className="relative">
                            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                            <input 
                                type="text" 
                                placeholder="Konu veya eğitmen ara..." 
                                value={search} 
                                onChange={e => setSearch(e.target.value)}
                                className="w-full pl-8 pr-3 py-2 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-lg focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20" 
                            />
                        </div>
                        <div className="flex flex-wrap gap-2">
                            <button onClick={() => setStatusFilter("all")} className={`flex-1 min-w-[70px] py-1.5 px-2 text-[11px] sm:text-xs rounded-lg transition-colors ${statusFilter === "all" ? "bg-[#0A1931] text-white" : "bg-[#E2E8F0]/30 text-[#5A6A7A] hover:bg-[#E2E8F0]/60"}`}>
                                Tümü ({questions.length})
                            </button>
                            <button onClick={() => setStatusFilter("Bekliyor")} className={`flex-1 min-w-[70px] py-1.5 px-2 text-[11px] sm:text-xs rounded-lg transition-colors ${statusFilter === "Bekliyor" ? "bg-amber-100 text-amber-700 font-medium" : "bg-[#E2E8F0]/30 text-[#5A6A7A] hover:bg-amber-50"}`}>
                                ⏳ {pendingCount}
                            </button>
                            <button onClick={() => setStatusFilter("Yanıtlandı")} className={`flex-1 min-w-[70px] py-1.5 px-2 text-[11px] sm:text-xs rounded-lg transition-colors ${statusFilter === "Yanıtlandı" ? "bg-emerald-100 text-emerald-700 font-medium" : "bg-[#E2E8F0]/30 text-[#5A6A7A] hover:bg-emerald-50"}`}>
                                ✓ {answeredCount}
                            </button>
                        </div>
                    </div>
                    
                    <div className="flex-1 overflow-y-auto">
                        {loading ? (
                            <div className="p-4 space-y-3">{[1, 2, 3].map(i => <div key={i} className="h-16 rounded-lg bg-[#E2E8F0]/30 animate-pulse" />)}</div>
                        ) : filtered.length === 0 ? (
                            <div className="flex flex-col items-center justify-center h-full text-[#A0AEC0]">
                                <MessageSquare size={32} className="opacity-20 mb-2" />
                                <p className="text-sm">Soru bulunamadı</p>
                            </div>
                        ) : filtered.map(q => {
                            const status = mapQuestionStatus(q.status);
                            const ss = statusStyles[status] || statusStyles.Bekliyor;
                            return (
                                <button key={q.id} onClick={() => setSelectedId(q.id)}
                                    className={`w-full text-left px-4 py-3.5 border-b border-[#E2E8F0]/60 hover:bg-[#E2E8F0]/20 transition-colors ${selectedId === q.id ? "bg-[#F0F4FF] border-l-2 border-l-[#1B3B6F]" : ""}`}>
                                    <div className="flex items-center gap-2 mb-1">
                                        <span className="text-xs font-bold text-[#0A1931] truncate flex-1">{q.subject}</span>
                                        <span className={`w-2 h-2 rounded-full shrink-0 ${ss.dot}`} />
                                    </div>
                                    <div className="flex items-center gap-2 text-[10px] text-[#A0AEC0] mb-1">
                                        {q.imageUrl && <span className="flex items-center gap-0.5 text-blue-500 bg-blue-50 px-1 rounded"><ImageIcon size={10} /> Görsel</span>}
                                        {q.audioUrl && <span className="flex items-center gap-0.5 text-cyan-600 bg-cyan-50 px-1 rounded"><Mic size={10} /> Ses</span>}
                                    </div>
                                    <div className="flex items-center justify-between">
                                        <p className="text-[11px] text-[#A0AEC0] truncate">Eğitmen: {q.instructorFullName}</p>
                                        <p className="text-[10px] text-[#A9A9A9] whitespace-nowrap ml-2">{new Date(q.createdAt).toLocaleDateString("tr-TR", { day: 'numeric', month: 'short' })}</p>
                                    </div>
                                </button>
                            );
                        })}
                    </div>
                </div>

                {/* Right Pane: Detail */}
                <div className="lg:col-span-3 bg-white rounded-2xl border border-[#E2E8F0]/60 flex flex-col min-h-[500px] lg:min-h-0 lg:h-auto overflow-hidden">
                    {selected ? (
                        <>
                                                        <div className="px-6 py-4 border-b border-[#E2E8F0]/60 flex items-start justify-between">
                                <div>
                                    <div className="flex items-center gap-2 mb-1">
                                        <h2 className="text-base font-bold text-[#0A1931]">{selected.subject}</h2>
                                        <span className={`text-[10px] font-semibold px-2 py-0.5 rounded-lg ${(statusStyles[mapQuestionStatus(selected.status)] || statusStyles.Bekliyor).bg} ${(statusStyles[mapQuestionStatus(selected.status)] || statusStyles.Bekliyor).text}`}>
                                            {mapQuestionStatus(selected.status)}
                                        </span>
                                    </div>
                                    <p className="text-xs text-[#A0AEC0]">{selected.courseTitle || "Ders"} • {new Date(selected.createdAt).toLocaleString("tr-TR")}</p>
                                </div>
                                {mapQuestionStatus(selected.status) === "Bekliyor" && (
                                    <button onClick={handleDelete} title="Soruyu Sil"
                                        className="p-2 text-[#A0AEC0] hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors">
                                        <Trash2 size={16} />
                                    </button>
                                )}
                            </div>

                            <div className="flex-1 overflow-y-auto px-6 py-5 space-y-6">
                                {/* Student (My) question */}
                                <div className="flex gap-3">
                                    <div className="w-8 h-8 rounded-full bg-gradient-to-br from-[#1B3B6F] to-blue-800 flex items-center justify-center text-white text-xs font-bold shrink-0">
                                        Siz
                                    </div>
                                    <div className="flex-1 bg-[#F8FAFC] border border-[#E2E8F0]/60 rounded-xl p-4">
                                        <p className="text-sm text-[#0A1931] whitespace-pre-wrap leading-relaxed">{selected.body}</p>
                                        
                                        {selected.imageUrl && (
                                            <div className="mt-4">
                                                <p className="text-[#A0AEC0] text-xs mb-1.5 flex items-center gap-1"><ImageIcon size={10} /> Görsel</p>
                                                {/* eslint-disable-next-line @next/next/no-img-element */}
                                                <img 
                                                    src={getFileUrl(selected.imageUrl)} 
                                                    alt="Soru Görseli" 
                                                    className="max-h-48 rounded-lg border border-[#E2E8F0]/60 shadow-sm object-cover cursor-zoom-in hover:opacity-90" 
                                                    onClick={() => setLightboxUrl(selected.imageUrl!)}
                                                />
                                            </div>
                                        )}
                                        
                                        {selected.audioUrl && (
                                            <div className="mt-4 w-full max-w-xs bg-cyan-50/50 border border-cyan-100 rounded-lg p-2">
                                                <p className="text-cyan-700 text-[10px] font-semibold mb-1 flex items-center gap-1"><Mic size={10} /> Ses Kaydı</p>
                                                <audio src={getFileUrl(selected.audioUrl)} controls className="h-8 w-full rounded" />
                                            </div>
                                        )}
                                    </div>
                                </div>

                                {/* Answer */}
                                {mapQuestionStatus(selected.status) === "Yanıtlandı" && selected.answer ? (
                                    <div className="flex gap-3 justify-end">
                                        <div className="flex-1 bg-emerald-50/40 border border-emerald-100 rounded-xl p-4 max-w-[85%]">
                                            <div className="flex items-center justify-between mb-2">
                                                <p className="text-xs font-bold text-emerald-800 flex items-center gap-1.5">
                                                    <CheckCircle2 size={14} className="text-emerald-500" /> 
                                                    {selected.instructorFullName}
                                                </p>
                                                {selected.answeredAt && <p className="text-[10px] text-emerald-600/70">{new Date(selected.answeredAt).toLocaleString("tr-TR")}</p>}
                                            </div>
                                            <p className="text-sm text-emerald-900 whitespace-pre-wrap leading-relaxed">{selected.answer}</p>
                                        </div>
                                        <div className="w-8 h-8 rounded-full bg-emerald-500 flex items-center justify-center text-white text-xs font-bold shrink-0">
                                            👨‍🏫
                                        </div>
                                    </div>
                                ) : (
                                    <div className="flex gap-3 justify-end opacity-50">
                                        <div className="flex-1 bg-amber-50 border border-amber-100 border-dashed rounded-xl p-4 max-w-[85%] flex items-center justify-center">
                                            <p className="text-xs text-amber-700 font-medium flex items-center gap-1.5"><Clock size={14} /> Eğitmen yanıtı bekleniyor...</p>
                                        </div>
                                        <div className="w-8 h-8 rounded-full bg-amber-200 flex items-center justify-center text-amber-700 text-xs font-bold shrink-0">
                                            👨‍🏫
                                        </div>
                                    </div>
                                )}
                            </div>

                            {/* Private Note Footer */}
                            {token && tenantId && (
                                <div className="px-6 py-3 border-t border-[#E2E8F0]/60 bg-[#F8FAFC]">
                                    <NoteEditor
                                        questionId={selected.id}
                                        initial={selected.note}
                                        token={token}
                                        tenantId={tenantId}
                                        onSaved={note => updateLocalNote(selected.id, note)}
                                    />
                                </div>
                            )}
                        </>
                    ) : (
                        <div className="flex-1 flex flex-col items-center justify-center p-6 text-center text-[#A0AEC0]">
                            <MessageSquare size={40} className="opacity-20 mb-3" />
                            <p className="text-sm">Detayları görmek için listeden bir soru seçin</p>
                        </div>
                    )}
                </div>
            </div>

            {/* Lightbox */}
            {lightboxUrl && <ImagePreview url={lightboxUrl} onClose={() => setLightboxUrl(null)} />}

            {/* Ask Form Modal */}
            {showForm && (
                <div className="fixed inset-0 z-[150] flex items-center justify-center p-4 bg-[#0A1931]/60 transition-all" onClick={() => { setShowForm(false); audio.clear(); }}>
                    <div className="bg-white rounded-2xl shadow-2xl w-full max-w-xl overflow-hidden flex flex-col max-h-[90vh] animate-in fade-in zoom-in-95 duration-200" onClick={e => e.stopPropagation()}>
                        {/* Gradient Header */}
                        <div className="relative overflow-hidden px-6 py-5 bg-gradient-to-br from-[#1B3B6F] to-[#0A1931] shrink-0">
                            <div className="absolute -right-8 -top-8 w-32 h-32 rounded-full bg-white/10 blur-2xl" />
                            <div className="absolute -left-8 -bottom-8 w-24 h-24 rounded-full bg-blue-400/20 blur-2xl" />
                            <div className="flex items-center justify-between relative z-10">
                                <div className="flex items-center gap-3">
                                    <div className="w-11 h-11 rounded-xl bg-white/10 border border-white/20 flex items-center justify-center shadow-sm backdrop-blur-md">
                                        <MessageSquare size={20} className="text-white" />
                                    </div>
                                    <div>
                                        <h3 className="text-lg font-bold text-white">Eğitmene Soru Sor</h3>
                                        <p className="text-[10px] font-medium text-blue-200 uppercase tracking-widest mt-0.5">Hızlı Destek Merkezi</p>
                                    </div>
                                </div>
                                <button onClick={() => { setShowForm(false); audio.clear(); }} className="p-2 rounded-xl bg-white/10 text-white/70 hover:bg-white/20 hover:text-white transition-colors shadow-sm backdrop-blur-md">
                                    <X size={16} />
                                </button>
                            </div>
                        </div>

                        {/* Modal body */}
                        <div className="flex-1 overflow-y-auto p-6 space-y-5 bg-[#F8FAFC]">
                            <div className="bg-white p-5 rounded-xl border border-[#E2E8F0] shadow-sm space-y-5">
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                                    <div>
                                        <label className="text-[11px] font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5 block">İlgili Ders <span className="text-red-500">*</span></label>
                                        <CustomCourseSelect 
                                            courses={courses}
                                            value={form.courseId}
                                            onChange={handleCourseChange}
                                        />
                                    </div>
                                    <div>
                                        <label className="text-[11px] font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5 block">Eğitmen</label>
                                        <div className={`w-full border rounded-xl px-4 py-2.5 text-sm shadow-sm transition-all flex items-center ${form.courseId ? "bg-indigo-50/50 border-indigo-100 text-indigo-900 font-bold" : "bg-[#F8FAFC] border-[#E2E8F0] text-[#A0AEC0] cursor-not-allowed"}`}>
                                            {form.courseId ? <><span className="text-lg mr-2">👨‍🏫</span> {courses.find(c => c.id === form.courseId)?.instructorName || "Eğitmen atanmamış"}</> : "Önce ders seçiniz"}
                                        </div>
                                    </div>
                                </div>

                                <div>
                                    <label className="text-[11px] font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5 block">Konu Özeti <span className="text-red-500">*</span></label>
                                    <input
                                        type="text"
                                        value={form.subject}
                                        onChange={e => setForm(f => ({ ...f, subject: e.target.value }))}
                                        placeholder="Örn: Limit kavramında sağdan yaklaşma problemi..."
                                        className="w-full bg-[#F8FAFC] border border-[#E2E8F0] rounded-xl px-4 py-2.5 text-[#0A1931] text-sm font-medium placeholder-[#A9A9A9] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:border-[#1B3B6F] focus:bg-white transition-all shadow-sm"
                                    />
                                </div>

                                <div>
                                    <label className="text-[11px] font-bold text-[#A0AEC0] uppercase tracking-widest mb-1.5 block">Sorunuz <span className="text-red-500">*</span></label>
                                    <textarea
                                        value={form.body}
                                        onChange={e => setForm(f => ({ ...f, body: e.target.value }))}
                                        placeholder="Takıldığınız yeri detaylıca anlatabilirsiniz..."
                                        rows={4}
                                        className="w-full bg-[#F8FAFC] border border-[#E2E8F0] rounded-xl px-4 py-3 text-[#0A1931] text-sm font-medium placeholder-[#A9A9A9] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:border-[#1B3B6F] focus:bg-white resize-none transition-all shadow-sm"
                                    />
                                </div>
                            </div>

                            {/* Attachments */}
                            <div className="bg-white border border-[#E2E8F0] rounded-xl p-5 shadow-sm">
                                <p className="text-[11px] font-bold text-[#A0AEC0] uppercase tracking-widest mb-3 flex items-center gap-1.5"><ImageIcon size={14} className="text-[#1B3B6F]" /> Soru Ekleri <span className="text-[9px] lowercase text-[#A9A9A9] font-normal tracking-normal">(İsteğe bağlı)</span></p>
                                
                                <div className="flex flex-col sm:flex-row gap-4">
                                    {/* Image */}
                                    <div className="flex-1">
                                        <input
                                            ref={fileInputRef}
                                            type="file"
                                            accept="image/*"
                                            className="hidden"
                                            onChange={e => {
                                                const file = e.target.files?.[0];
                                                if (file) {
                                                    setImageFile(file);
                                                    setImagePreview(URL.createObjectURL(file));
                                                }
                                            }}
                                        />
                                        {!imagePreview ? (
                                            <button
                                                type="button"
                                                onClick={() => fileInputRef.current?.click()}
                                                className="w-full flex flex-col items-center justify-center gap-2 h-24 rounded-xl border-2 border-dashed border-[#E2E8F0] bg-[#F8FAFC] hover:bg-[#F0F4FF] hover:border-[#1B3B6F]/30 text-[#A0AEC0] hover:text-[#1B3B6F] transition-all group"
                                            >
                                                <div className="w-8 h-8 rounded-lg bg-white shadow-sm flex items-center justify-center group-hover:scale-110 transition-transform">
                                                    <Upload size={14} className="text-[#1B3B6F]" />
                                                </div>
                                                <span className="text-xs font-bold">Görsel Seç / Sürükle</span>
                                            </button>
                                        ) : (
                                            <div className="flex items-center gap-4 bg-[#F8FAFC] p-2 h-24 rounded-xl border border-[#E2E8F0] shadow-sm">
                                                {/* eslint-disable-next-line @next/next/no-img-element */}
                                                <img src={imagePreview} alt="Önizleme" className="h-full w-20 rounded-lg object-cover border border-[#E2E8F0] shadow-sm" />
                                                <div className="flex flex-col justify-center gap-1 flex-1 min-w-0 pr-2">
                                                    <p className="text-[#0A1931] text-xs font-bold truncate">{imageFile?.name}</p>
                                                    <p className="text-[#A0AEC0] text-[10px]">{(imageFile?.size! / 1024 / 1024).toFixed(2)} MB</p>
                                                    <button
                                                        type="button"
                                                        onClick={() => { setImageFile(null); setImagePreview(null); if (fileInputRef.current) fileInputRef.current.value = ''; }}
                                                        className="text-red-500 hover:text-red-600 text-[10px] font-bold flex items-center gap-1 w-max mt-1 bg-red-50 px-2 py-1 rounded"
                                                    >
                                                        <Trash2 size={10} /> Görseli Kaldır
                                                    </button>
                                                </div>
                                            </div>
                                        )}
                                    </div>

                                    {/* Audio */}
                                    <div className="flex-1">
                                        {!audio.audioUrl ? (
                                            <div className="w-full flex flex-col items-center justify-center gap-2 h-24 rounded-xl border border-[#E2E8F0] bg-[#F8FAFC] shadow-sm">
                                                <button
                                                    type="button"
                                                    onClick={audio.recording ? audio.stop : audio.start}
                                                    className={`w-full h-full flex flex-col items-center justify-center gap-2 rounded-xl transition-all group ${audio.recording
                                                        ? "bg-red-50 border-red-200"
                                                        : "hover:bg-cyan-50/50 hover:border-cyan-200"}`}
                                                >
                                                    <div className={`w-10 h-10 rounded-full flex items-center justify-center shadow-sm transition-transform ${audio.recording ? "bg-red-500 animate-pulse text-white scale-110" : "bg-white text-cyan-600 group-hover:scale-110"}`}>
                                                        {audio.recording ? <MicOff size={16} /> : <Mic size={16} />}
                                                    </div>
                                                    
                                                    {audio.recording ? (
                                                        <span className="text-red-600 text-xs font-bold flex items-center gap-1.5">
                                                            <span className="w-2 h-2 rounded-full bg-red-500 animate-ping" /> {audio.formatTime(audio.seconds)} - Durdur
                                                        </span>
                                                    ) : (
                                                        <span className="text-xs font-bold text-[#A0AEC0] group-hover:text-cyan-700">Sesli Soru Sor</span>
                                                    )}
                                                </button>
                                            </div>
                                        ) : (
                                            <div className="flex flex-col items-center justify-center bg-cyan-50/50 h-24 p-3 rounded-xl border border-cyan-100 shadow-sm relative group">
                                                <p className="text-cyan-800 text-[10px] font-bold absolute top-2 left-3 flex items-center gap-1"><Mic size={10} /> Ses Kaydınız</p>
                                                <audio controls src={audio.audioUrl} className="h-8 w-full mt-3" />
                                                <button type="button" onClick={audio.clear} className="absolute top-2 right-2 text-red-500 hover:text-red-600 hover:bg-red-50 p-1 rounded transition-colors" title="Sil">
                                                    <Trash2 size={14} />
                                                </button>
                                            </div>
                                        )}
                                    </div>
                                </div>
                            </div>
                        </div>

                        {formError && (
                            <div className="px-6 py-3 bg-red-50 border-t border-red-100 flex items-center gap-2 shrink-0">
                                <AlertCircle size={14} className="text-red-500 shrink-0" />
                                <p className="text-red-600 text-xs font-bold">{formError}</p>
                            </div>
                        )}

                        {/* Modal footer */}
                        <div className="flex items-center justify-between px-6 py-4 border-t border-[#E2E8F0] bg-white shrink-0">
                            <p className="text-[10px] text-[#A0AEC0] font-medium hidden sm:block">📝 Detaylı sorular daha hızlı çözüme ulaşır.</p>
                            <div className="flex gap-2 w-full sm:w-auto">
                                <button
                                    onClick={() => { setShowForm(false); audio.clear(); }}
                                    className="flex-1 sm:flex-none px-5 py-2.5 text-[#A0AEC0] hover:text-[#0A1931] hover:bg-[#E2E8F0]/50 rounded-xl text-sm font-bold transition-all"
                                >
                                    İptal
                                </button>
                                <button
                                    onClick={handleSend}
                                    disabled={sending || !form.instructorId || !form.subject.trim() || !form.body.trim()}
                                    className="flex-1 sm:flex-none px-6 py-2.5 bg-gradient-to-r from-[#1B3B6F] to-[#0A1931] hover:opacity-90 disabled:opacity-50 text-white rounded-xl text-sm font-bold transition-all shadow-lg shadow-[#1B3B6F]/20 flex items-center justify-center gap-2"
                                >
                                    {sending ? <span className="animate-pulse">Gönderiliyor...</span> : <><Send size={16} /> Gönder</>}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
        </>
    );
}

