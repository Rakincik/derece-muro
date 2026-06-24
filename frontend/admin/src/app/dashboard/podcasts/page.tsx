"use client";

import { useState, useEffect, useRef } from "react";
import {
    Headphones, Plus, Search, Trash2, X, Play, Pause,
    Clock, Mic, Sparkles, Loader2, Volume2, FileText,
    CheckCircle, AlertCircle, ChevronDown
} from "lucide-react";
import { useToast } from "@/components/toast";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { podcastApi, PodcastDto, courseApi, CourseListDto } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import { Tooltip } from "@/components/ui/Tooltip";

const VOICES = [
    { value: "tr-TR-Chirp3-HD-Achird",    label: "Kaan (Erkek)",   gender: "male" },
    { value: "tr-TR-Chirp3-HD-Enceladus", label: "Emre (Erkek)",   gender: "male" },
    { value: "tr-TR-Chirp3-HD-Charon",    label: "Burak (Erkek)",  gender: "male" },
    { value: "tr-TR-Chirp3-HD-Fenrir",    label: "Mert (Erkek)",   gender: "male" },
    { value: "tr-TR-Chirp3-HD-Puck",      label: "Cem (Erkek)",    gender: "male" },
    { value: "tr-TR-Chirp3-HD-Achernar",  label: "Elif (Kadın)",   gender: "female" },
    { value: "tr-TR-Chirp3-HD-Aoede",     label: "Zeynep (Kadın)", gender: "female" },
    { value: "tr-TR-Chirp3-HD-Kore",      label: "Ayşe (Kadın)",   gender: "female" },
    { value: "tr-TR-Chirp3-HD-Leda",      label: "Selin (Kadın)",  gender: "female" },
    { value: "tr-TR-Chirp3-HD-Zephyr",    label: "Deniz (Kadın)",  gender: "female" },
];

function formatDuration(sec: number | null) {
    if (!sec) return "--:--";
    const m = Math.floor(sec / 60);
    const s = sec % 60;
    return `${m}:${s.toString().padStart(2, "0")}`;
}

function StatusBadge({ status }: { status: PodcastDto["status"] }) {
    const map: Record<string, { bg: string; text: string; dot: string; label: string }> = {
        Ready: { bg: "bg-emerald-50", text: "text-emerald-700", dot: "bg-emerald-500", label: "Hazır" },
        Processing: { bg: "bg-amber-50", text: "text-amber-700", dot: "bg-amber-500", label: "İşleniyor" },
        Failed: { bg: "bg-red-50", text: "text-red-700", dot: "bg-red-500", label: "Hatalı" },
        Draft: { bg: "bg-[#E2E8F0]", text: "text-[#1B3B6F]", dot: "bg-[#A0AEC0]", label: "Taslak" },
        Generating: { bg: "bg-blue-50", text: "text-blue-700", dot: "bg-blue-500", label: "Oluşturuluyor" },
        Pending: { bg: "bg-[#E2E8F0]", text: "text-[#A9A9A9]", dot: "bg-[#A9A9A9]", label: "Bekliyor" },
    };
    const s = map[status] ?? { bg: "bg-[#E2E8F0]", text: "text-[#A9A9A9]", dot: "bg-[#A9A9A9]", label: status ?? "Bilinmiyor" };
    return (
        <span className={`text-[10px] font-semibold px-2 py-0.5 rounded-lg ${s.bg} ${s.text} inline-flex items-center gap-1`}>
            <span className={`w-1.5 h-1.5 rounded-full ${s.dot}`} /> {s.label}
        </span>
    );
}

/* ────────────────────────────── Audio Player Card ─────────────────────────── */
function PodcastCard({
    podcast, onDelete, audioUrl,
}: {
    podcast: PodcastDto;
    onDelete: (id: string) => void;
    audioUrl: string;
}) {
    const audioRef = useRef<HTMLAudioElement>(null);
    const [playing, setPlaying] = useState(false);
    const [current, setCurrent] = useState(0);
    const [duration, setDuration] = useState(podcast.durationSeconds ?? 0);
    const [showScript, setShowScript] = useState(false);

    const toggle = () => {
        const a = audioRef.current;
        if (!a) return;
        if (playing) { a.pause(); } else { a.play(); }
        setPlaying(!playing);
    };

    const colors = ["#6366f1", "#ec4899", "#10b981", "#f59e0b", "#8b5cf6", "#ef4444", "#06b6d4", "#3b82f6"];
    const color = colors[parseInt(podcast.id.replace(/-/g, "").slice(0, 8), 16) % colors.length];

    return (
        <div className="bg-white rounded-xl border border-[#E2E8F0]/60 p-5 hover:shadow-md transition-all group">
            <div className="flex items-start gap-4">
                {/* Thumbnail / play button */}
                <button
                    onClick={toggle}
                    disabled={podcast.status !== "Ready"}
                    className="w-16 h-16 rounded-xl shrink-0 flex items-center justify-center relative disabled:opacity-50 disabled:cursor-not-allowed"
                    style={{ background: `linear-gradient(135deg, ${color}30, ${color}60)` }}
                >
                    {podcast.status === "Processing" ? (
                        <Loader2 size={18} className="animate-spin" style={{ color }} />
                    ) : playing ? (
                        <Pause size={20} style={{ color }} />
                    ) : (
                        <Play size={20} style={{ color }} className="ml-0.5" />
                    )}
                    {playing && (
                        <div className="absolute -bottom-0.5 inset-x-2 flex gap-0.5 justify-center">
                            {[1, 2, 3, 4, 5].map(i => (
                                <div key={i} className="w-1 rounded-full animate-pulse"
                                    style={{ background: color, height: `${6 + (i % 3) * 4}px`, animationDelay: `${i * 0.1}s` }} />
                            ))}
                        </div>
                    )}
                </button>

                {/* Info */}
                <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                        <h3 className="text-sm font-bold text-[#0A1931] truncate">{podcast.title}</h3>
                        <StatusBadge status={podcast.status} />
                    </div>
                    {podcast.courseTitle && (
                        <p className="text-xs text-[#1B3B6F] font-medium mb-1">{podcast.courseTitle}</p>
                    )}
                    <div className="flex items-center gap-4 text-xs text-[#A0AEC0]">
                        <span className="flex items-center gap-1"><Clock size={10} /> {formatDuration(podcast.durationSeconds)}</span>
                        <span>{new Date(podcast.createdAt).toLocaleDateString("tr-TR")}</span>
                    </div>

                    {/* Progress bar */}
                    {podcast.status === "Ready" && (
                        <div className="mt-2 h-1 bg-[#E2E8F0]/40 rounded-full overflow-hidden">
                            <div className="h-full rounded-full transition-all" style={{ background: color, width: duration ? `${(current / duration) * 100}%` : "0%" }} />
                        </div>
                    )}
                </div>

                {/* Actions */}
                <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
                    {podcast.generatedScript && (
                        <Tooltip content="Script'i göster"><button onClick={() => setShowScript(s => !s)}
                            className="p-2 rounded-lg hover:bg-blue-50 text-[#A0AEC0] hover:text-blue-600">
                            <FileText size={14} />
                        </button></Tooltip>
                    )}
                    <button onClick={() => onDelete(podcast.id)}
                        className="p-2 rounded-lg hover:bg-red-50 text-[#A0AEC0] hover:text-red-600">
                        <Trash2 size={14} />
                    </button>
                </div>
            </div>

            {/* Generated script accordion */}
            {showScript && podcast.generatedScript && (
                <div className="mt-4 p-4 bg-[#E2E8F0]/20 rounded-xl text-xs text-[#1B3B6F] leading-relaxed whitespace-pre-wrap border border-[#E2E8F0]/60">
                    {podcast.generatedScript}
                </div>
            )}

            {/* Hidden audio element */}
            <audio
                ref={audioRef}
                src={audioUrl}
                onTimeUpdate={(e) => setCurrent((e.target as HTMLAudioElement).currentTime)}
                onLoadedMetadata={(e) => setDuration((e.target as HTMLAudioElement).duration)}
                onEnded={() => setPlaying(false)}
            />
        </div>
    );
}

/* ────────────────────────────── Generate Modal ─────────────────────────────── */
function GenerateModal({ onClose, onSuccess }: { onClose: () => void; onSuccess: (p: PodcastDto) => void }) {
    const { token, currentTenantId } = useAuth();
    const { success, error } = useToast();

    const [form, setForm] = useState({ title: "", rawText: "", voice: "tr-TR-Chirp3-HD-Achird", courseId: "" });
    const [loading, setLoading] = useState(false);
    const [courses, setCourses] = useState<CourseListDto[]>([]);
    const charCount = form.rawText.length;

    useEffect(() => {
        if (!token || !currentTenantId) return;
        courseApi.list(token, currentTenantId, { pageSize: 1000 })
            .then(r => setCourses(r.items))
            .catch(() => {});
    }, [token, currentTenantId]);

    const handleGenerate = async () => {
        if (!form.title.trim() || !form.rawText.trim()) return;
        setLoading(true);
        try {
            const result = await podcastApi.generate(token!, currentTenantId!, {
                title: form.title,
                rawText: form.rawText,
                voice: form.voice,
                ...(form.courseId ? { courseId: form.courseId } : {}),
            });
            success("Podcast Oluşturuldu", "AI script yazılıyor ve ses üretiliyor...");
            onSuccess(result);
            onClose();
        } catch (e: unknown) {
            error("Hata", (e as Error).message || "Podcast üretilemedi");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4" onClick={onClose}>
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
            <div className="relative w-full max-w-2xl bg-white rounded-2xl shadow-2xl animate-fade-in" onClick={e => e.stopPropagation()}>

                {/* Header */}
                <div className="flex items-center justify-between px-6 py-4 border-b border-[#E2E8F0]/60">
                    <div className="flex items-center gap-2">
                        <div className="w-8 h-8 rounded-lg bg-[#E2E8F0]/40 flex items-center justify-center">
                            <Sparkles size={16} className="text-[#1B3B6F]" />
                        </div>
                        <div>
                            <h2 className="text-sm font-bold text-[#0A1931]">AI ile Podcast Üret</h2>
                            <p className="text-[11px] text-[#A0AEC0]">Gemini → edge-tts pipeline — tamamen ücretsiz</p>
                        </div>
                    </div>
                    <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/40 text-[#A0AEC0]">
                        <X size={18} />
                    </button>
                </div>

                {/* Body */}
                <div className="p-6 space-y-4">
                    {/* AI Flow indicator */}
                    <div className="flex items-center gap-2 p-3 bg-[#E2E8F0]/30 rounded-xl text-xs text-[#0A1931]">
                        <div className="flex items-center gap-1.5 font-medium">
                            <FileText size={12} /> Ham Metin
                        </div>
                        <span className="text-[#A0AEC0]">→</span>
                        <div className="flex items-center gap-1.5 font-medium">
                            <Sparkles size={12} /> Gemini AI Script
                        </div>
                        <span className="text-[#A0AEC0]">→</span>
                        <div className="flex items-center gap-1.5 font-medium">
                            <Volume2 size={12} /> MP3 Ses
                        </div>
                    </div>

                    {/* Title */}
                    <div>
                        <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Podcast Başlığı</label>
                        <input
                            type="text"
                            value={form.title}
                            onChange={e => setForm(f => ({ ...f, title: e.target.value }))}
                            placeholder="Örn: Newton'un Hareket Yasaları"
                            className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20"
                        />
                    </div>

                    {/* Raw text */}
                    <div>
                        <div className="flex items-center justify-between mb-1.5">
                            <label className="text-xs font-medium text-[#1B3B6F]">Ham Konu Metni</label>
                            <span className={`text-[10px] ${charCount > 3000 ? "text-red-400" : "text-[#A0AEC0]"}`}>{charCount} / 3000</span>
                        </div>
                        <textarea
                            value={form.rawText}
                            onChange={e => setForm(f => ({ ...f, rawText: e.target.value.slice(0, 3000) }))}
                            placeholder="Konuyla ilgili notlarınızı, ders içeriğinizi veya özetinizi buraya yapıştırın. AI bunu profesyonel bir podcast script'ine dönüştürecek..."
                            className="w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 resize-none h-36"
                        />
                    </div>

                    {/* Voice selection */}
                    <div>
                        <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Ses Seçimi</label>
                        <div className="relative">
                            <select
                                value={form.voice}
                                onChange={e => setForm(f => ({ ...f, voice: e.target.value }))}
                                className="w-full appearance-none px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 pr-10"
                            >
                                <optgroup label="Erkek Sesler">
                                    {VOICES.filter(v => v.gender === "male").map(v => (
                                        <option key={v.value} value={v.value}>{v.label}</option>
                                    ))}
                                </optgroup>
                                <optgroup label="Kadın Sesler">
                                    {VOICES.filter(v => v.gender === "female").map(v => (
                                        <option key={v.value} value={v.value}>{v.label}</option>
                                    ))}
                                </optgroup>
                            </select>
                            <ChevronDown size={14} className="absolute right-3 top-1/2 -translate-y-1/2 text-[#A0AEC0] pointer-events-none" />
                        </div>
                    </div>

                    {/* Course assignment */}
                    <div>
                        <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Derse Ata <span className="text-[#A0AEC0] font-normal">(opsiyonel)</span></label>
                        <div className="relative">
                            <select
                                value={form.courseId}
                                onChange={e => setForm(f => ({ ...f, courseId: e.target.value }))}
                                className="w-full appearance-none px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 pr-10"
                            >
                                <option value="">Ders seçiniz...</option>
                                {courses.map(c => (
                                    <option key={c.id} value={c.id}>{c.title}</option>
                                ))}
                            </select>
                            <ChevronDown size={14} className="absolute right-3 top-1/2 -translate-y-1/2 text-[#A0AEC0] pointer-events-none" />
                        </div>
                    </div>

                    {/* Info note */}
                    <div className="flex items-start gap-2 p-3 bg-amber-50 rounded-xl text-xs text-amber-700">
                        <Clock size={12} className="mt-0.5 shrink-0" />
                        <span>Üretim 10-30 saniye sürebilir. Lütfen bekleyin.</span>
                    </div>
                </div>

                {/* Footer */}
                <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-[#E2E8F0]/60 bg-[#E2E8F0]/15 rounded-b-2xl">
                    <button onClick={onClose} disabled={loading}
                        className="px-4 py-2.5 text-sm font-medium text-[#1B3B6F] bg-white border border-[#E2E8F0] rounded-xl hover:bg-[#E2E8F0]/20 disabled:opacity-50">
                        İptal
                    </button>
                    <button
                        onClick={handleGenerate}
                        disabled={loading || !form.title.trim() || !form.rawText.trim()}
                        className="px-5 py-2.5 text-sm font-medium text-white bg-[#1B3B6F] rounded-xl hover:bg-[#0A1931] shadow-sm shadow-[#0A1931]/25 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
                    >
                        {loading ? (
                            <><Loader2 size={14} className="animate-spin" /> Üretiliyor...</>
                        ) : (
                            <><Sparkles size={14} /> Podcast Üret</>
                        )}
                    </button>
                </div>
            </div>
        </div>
    );
}

/* ────────────────────────────── Main Page ──────────────────────────────────── */
export default function PodcastsPage() {
    const { token, currentTenantId } = useAuth();
    const { success, error } = useToast();

    const [podcasts, setPodcasts] = useState<PodcastDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState("");
    const [showGenerate, setShowGenerate] = useState(false);
    const [deleteTarget, setDeleteTarget] = useState<string | null>(null);

    const fetchPodcasts = async () => {
        if (!token || !currentTenantId) return;
        setLoading(true);
        try {
            const result = await podcastApi.list(token, currentTenantId, { pageSize: 50 });
            setPodcasts(result.items);
        } catch (e: unknown) {
            error("Yükleme Hatası", (e as Error).message);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { fetchPodcasts(); }, [token, currentTenantId]);

    const handleDelete = async (id: string) => {
        try {
            await podcastApi.delete(token!, currentTenantId!, id);
            setPodcasts(prev => prev.filter(p => p.id !== id));
            success("Podcast Silindi");
        } catch (e: unknown) {
            error("Silinemedi", (e as Error).message);
        }
        setDeleteTarget(null);
    };

    const handleGenerated = (p: PodcastDto) => {
        setPodcasts(prev => [p, ...prev]);
    };

    const filtered = podcasts.filter(p =>
        !search || p.title.toLowerCase().includes(search.toLowerCase())
    );

    const readyCount = podcasts.filter(p => p.status === "Ready").length;
    const totalMinutes = podcasts.reduce((s, p) => s + (p.durationSeconds ?? 0), 0);

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-[#0A1931] flex items-center gap-2">
                        <Headphones size={24} className="text-[#1B3B6F]" /> Podcast
                    </h1>
                    <p className="text-sm text-[#A9A9A9] mt-1">AI ile sesli içerik üretin</p>
                </div>
                <button
                    onClick={() => setShowGenerate(true)}
                    className="px-4 py-2 text-xs font-medium bg-[#1B3B6F] text-white rounded-lg hover:bg-[#0A1931] transition-colors flex items-center gap-1.5 shadow-sm shadow-[#0A1931]/25"
                >
                    <Sparkles size={14} /> AI ile Üret
                </button>
            </div>

            {/* Stats */}
            <div className="grid grid-cols-4 gap-4">
                {[
                    { label: "Toplam Podcast", value: podcasts.length, icon: Headphones, color: "text-[#1B3B6F]" },
                    { label: "Yayında", value: readyCount, icon: CheckCircle, color: "text-emerald-600" },
                    { label: "Toplam Dakika", value: Math.floor(totalMinutes / 60), icon: Clock, color: "text-blue-600" },
                    { label: "Hatalı", value: podcasts.filter(p => p.status === "Failed").length, icon: AlertCircle, color: "text-red-600" },
                ].map(s => (
                    <div key={s.label} className="bg-white rounded-xl border border-[#E2E8F0]/60 p-4 flex items-center gap-3">
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

            {/* Search */}
            <div className="bg-white rounded-xl border border-[#E2E8F0]/60 p-4">
                <div className="relative">
                    <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                    <input
                        type="text"
                        placeholder="Podcast ara..."
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                        className="w-full pl-9 pr-4 py-2 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-lg focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20"
                    />
                </div>
            </div>

            {/* List */}
            <div className="space-y-3">
                {loading ? (
                    Array.from({ length: 3 }).map((_, i) => (
                        <div key={i} className="bg-white rounded-xl border border-[#E2E8F0]/60 p-5 animate-pulse">
                            <div className="flex gap-4">
                                <div className="w-16 h-16 rounded-xl bg-[#E2E8F0]/40" />
                                <div className="flex-1 space-y-2">
                                    <div className="h-4 bg-[#E2E8F0]/40 rounded w-1/3" />
                                    <div className="h-3 bg-[#E2E8F0]/40 rounded w-1/4" />
                                </div>
                            </div>
                        </div>
                    ))
                ) : filtered.length === 0 ? (
                    <div className="bg-white rounded-2xl border border-[#E2E8F0]/60 flex flex-col items-center justify-center py-16 text-[#A0AEC0]">
                        <Mic size={40} className="opacity-30 mb-3" />
                        <p className="text-sm font-medium">Henüz podcast yok</p>
                        <p className="text-xs mt-1">AI ile üret butonuna tıklayarak başla</p>
                    </div>
                ) : (
                    filtered.map(p => (
                        <PodcastCard
                            key={p.id}
                            podcast={p}
                            onDelete={id => setDeleteTarget(id)}
                            audioUrl={podcastApi.audioUrl(p.id)}
                        />
                    ))
                )}
            </div>

            {/* Modals */}
            {showGenerate && (
                <GenerateModal
                    onClose={() => setShowGenerate(false)}
                    onSuccess={handleGenerated}
                />
            )}
            <ConfirmDialog
                open={deleteTarget !== null}
                onClose={() => setDeleteTarget(null)}
                onConfirm={() => deleteTarget && handleDelete(deleteTarget)}
                title="Podcast Sil"
                message="Bu podcast ve ses dosyası kalıcı olarak silinecek."
            />
        </div>
    );
}
