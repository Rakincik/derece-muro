"use client";

import { useEffect, useState, useCallback, useRef, useMemo } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { examApi, getFileUrl, type ExamDetailDto, type MyExamResultDto } from "@/lib/api";
import { useToast } from "@/components/ToastProvider";
import dynamic from "next/dynamic";
const SecurePdfViewer = dynamic(() => import("@/components/SecurePdfViewer"), { ssr: false });

const OPTION_LABELS = ["A", "B", "C", "D", "E", "F"];

function formatTime(secs: number) {
    const m = Math.floor(secs / 60);
    const s = secs % 60;
    return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
}

function CountdownTimer({ initialSeconds, onComplete }: { initialSeconds: number, onComplete: () => void }) {
    const [timeLeft, setTimeLeft] = useState(initialSeconds);
    useEffect(() => {
        const timer = setInterval(() => {
            setTimeLeft(t => {
                if (t <= 1) {
                    clearInterval(timer);
                    onComplete();
                    return 0;
                }
                return t - 1;
            });
        }, 1000);
        return () => clearInterval(timer);
    }, [onComplete]);

    return (
        <div className={`px-4 py-2 rounded-xl font-mono text-lg font-bold ${timeLeft < 300 ? "bg-red-500/15 text-red-400 border border-red-500/30 animate-pulse" : "bg-white/5 text-[#0A1931] border border-white/10"}`}>
            ⏳ {formatTime(timeLeft)}
        </div>
    );
}

type Phase = "loading" | "ready" | "solving" | "confirm" | "done" | "error" | "already_submitted" | "missing_key";

export default function ExamSolvePage() {
    const { token, currentTenantId: tenantId, user } = useAuth();
    const params = useParams();
    const router = useRouter();
    const examId = params.examId as string;

    const [exam, setExam] = useState<ExamDetailDto | null>(null);
    const [phase, setPhase] = useState<Phase>("loading");
    const [answers, setAnswers] = useState<Record<number, string>>({});
    const [result, setResult] = useState<MyExamResultDto | null>(null);
    const [myResults, setMyResults] = useState<MyExamResultDto[]>([]);
    const [submitting, setSubmitting] = useState(false);
    const startedAt = useRef<string>(new Date().toISOString());
    const answersRef = useRef<Record<number, string>>({});
    const { showToast } = useToast();

    // Hangi soruya scroll edildi
    const [activeQ, setActiveQ] = useState(1);
    const [activeSectionIdx, setActiveSectionIdx] = useState(0);
    const [showPdfMobile, setShowPdfMobile] = useState(false);

    // Güvenlik State'leri
    const [isSecurityWarningOpen, setIsSecurityWarningOpen] = useState(false);
    const [isFullscreen, setIsFullscreen] = useState(true);

    const sections = useMemo<{name: string, start: number, end: number}[] | null>(() => {
        if (!exam?.sectionsJson) return null;
        try {
            return JSON.parse(exam.sectionsJson);
        } catch { return null; }
    }, [exam?.sectionsJson]);

    // iframe icinde sidebar'i gizle
    useEffect(() => {
        if (typeof window !== "undefined" && window.location.search.includes("hideSidebar=true")) {
            const style = document.createElement("style");
            style.innerHTML = `
                .sidebar, header, nav, .mobile-tab-bar { display: none !important; } 
                .main-content { padding: 0 !important; margin: 0 !important; max-width: 100% !important; min-height: 100vh !important; }
            `;
            document.head.appendChild(style);
            return () => { document.head.removeChild(style); };
        }
    }, []);

    const currentQuestions = useMemo(() => {
        const total = exam?.questionCount ?? 0;
        if (sections && sections[activeSectionIdx]) {
            const sec = sections[activeSectionIdx];
            return Array.from({ length: sec.end - sec.start + 1 }, (_, i) => i + sec.start);
        }
        return Array.from({ length: total }, (_, i) => i + 1);
    }, [sections, activeSectionIdx, exam?.questionCount]);

    useEffect(() => {
        if (!token || !tenantId || !examId) return;
        Promise.all([
            examApi.getById(token, tenantId, examId),
            examApi.myResults(token, tenantId).catch(() => []) // gracefully handle error
        ])
        .then(async ([e, results]) => {
            setExam(e);
            setMyResults(results);
            
            const existingResult = results.find(r => r.examId === examId);
            if (existingResult) {
                setResult(existingResult);
                setPhase("done"); // already submitted, go straight to scorecard
            } else if (!e.answerKey || Object.keys(e.answerKey).length === 0) {
                setPhase("missing_key");
            } else {
                try {
                    const draft = await examApi.getDraft(token, tenantId, examId);
                    if (draft && Object.keys(draft).length > 0) {
                        setAnswers(draft);
                    } else {
                        const localDraft = localStorage.getItem(`exam_draft_${examId}`);
                        if (localDraft) setAnswers(JSON.parse(localDraft));
                    }
                } catch { /* ignore */ }
                setPhase("ready");
            }
        })
        .catch(() => setPhase("error"));
    }, [token, tenantId, examId]);

    // Senkronize answersRef
    useEffect(() => {
        answersRef.current = answers;
    }, [answers]);

    // Auto-Save
    useEffect(() => {
        if (phase !== "solving" || !examId) return;

        const localInterval = setInterval(() => {
            if (Object.keys(answersRef.current).length > 0) {
                localStorage.setItem(`exam_draft_${examId}`, JSON.stringify(answersRef.current));
            }
        }, 10000);

        const serverInterval = setInterval(() => {
            if (token && tenantId && Object.keys(answersRef.current).length > 0) {
                examApi.saveDraft(token, tenantId, examId, answersRef.current).catch(console.error);
            }
        }, 60000);

        return () => {
            clearInterval(localInterval);
            clearInterval(serverInterval);
        };
    }, [phase, examId, token, tenantId]);

    const requestFullscreen = async () => {
        try {
            if (document.documentElement.requestFullscreen) {
                await document.documentElement.requestFullscreen();
            }
        } catch (err) {
            console.error("Fullscreen error", err);
        }
    };

    const startExam = async () => {
        startedAt.current = new Date().toISOString();
        await requestFullscreen();
        setPhase("solving");
    };

    // Güvenlik Dinleyicileri (Security Hooks)
    useEffect(() => {
        if (phase !== "solving") return;

        const handleFullscreenChange = () => {
            setIsFullscreen(!!document.fullscreenElement);
        };

        const handleVisibilityChange = () => {
            if (document.hidden) {
                setIsSecurityWarningOpen(true);
            }
        };

        const handleBlur = () => {
            setIsSecurityWarningOpen(true);
        };

        const handleKeyDown = (e: KeyboardEvent) => {
            // F12
            if (e.key === "F12") {
                e.preventDefault();
            }
            // PrintScreen
            if (e.key === "PrintScreen") {
                e.preventDefault();
                setIsSecurityWarningOpen(true);
                navigator.clipboard?.writeText(""); // Clipboboard'u temizle
            }
            // Ctrl/Cmd + Shift + I/J/C (DevTools)
            if ((e.ctrlKey || e.metaKey) && e.shiftKey && ["I", "J", "C"].includes(e.key.toUpperCase())) {
                e.preventDefault();
            }
            // Ctrl/Cmd + U (View Source)
            if ((e.ctrlKey || e.metaKey) && e.key.toUpperCase() === "U") {
                e.preventDefault();
            }
            // Ctrl/Cmd + P (Print)
            if ((e.ctrlKey || e.metaKey) && e.key.toUpperCase() === "P") {
                e.preventDefault();
            }
            // Ctrl/Cmd + C / V (Copy/Paste)
            if ((e.ctrlKey || e.metaKey) && ["C", "V"].includes(e.key.toUpperCase())) {
                e.preventDefault();
            }
        };

        const handleContextMenu = (e: MouseEvent) => e.preventDefault();
        const handleCopy = (e: ClipboardEvent) => e.preventDefault();

        document.addEventListener("fullscreenchange", handleFullscreenChange);
        document.addEventListener("visibilitychange", handleVisibilityChange);
        window.addEventListener("blur", handleBlur);
        document.addEventListener("keydown", handleKeyDown);
        document.addEventListener("contextmenu", handleContextMenu);
        document.addEventListener("copy", handleCopy);

        // Başlangıç kontrolü
        if (!document.fullscreenElement) {
            setIsFullscreen(false);
        }

        return () => {
            document.removeEventListener("fullscreenchange", handleFullscreenChange);
            document.removeEventListener("visibilitychange", handleVisibilityChange);
            window.removeEventListener("blur", handleBlur);
            document.removeEventListener("keydown", handleKeyDown);
            document.removeEventListener("contextmenu", handleContextMenu);
            document.removeEventListener("copy", handleCopy);
        };
    }, [phase]);

    const handleSubmit = useCallback(async (auto = false) => {
        if (!token || !tenantId || !exam) return;
        if (!auto && phase !== "confirm") { setPhase("confirm"); return; }
        setSubmitting(true);
        try {
            const res = await examApi.submitAnswers(token, tenantId, examId, answers, startedAt.current);
            setResult(res);
            setPhase("done");
            localStorage.removeItem(`exam_draft_${examId}`);
        } catch (e: unknown) {
            showToast(e instanceof Error ? e.message : "Gönderim başarısız.", "error");
            setPhase("solving");
        } finally {
            setSubmitting(false);
        }
    }, [token, tenantId, exam, examId, answers, phase]);

    const answered = Object.keys(answers).length;
    const total = exam?.questionCount ?? 0;
    const optionCount = exam?.optionCount ?? 5;
    const options = OPTION_LABELS.slice(0, optionCount);

    // ── LOADING ────────────────────────────────────────────────────────────────
    if (phase === "loading") {
        return (
            <div className="flex items-center justify-center py-24">
                <div className="w-8 h-8 border-2 border-violet-500 border-t-transparent rounded-full animate-spin" />
            </div>
        );
    }

    if (phase === "error" || !exam) {
        return (
            <div className="text-center py-20">
                <p className="text-5xl mb-4">âš ï¸</p>
                <p className="text-[#0A1931] font-semibold mb-4">Sınav yüklenemedi.</p>
                <Link href="/dashboard/exams" className="text-violet-400 text-sm hover:underline">â† Sınavlara Dön</Link>
            </div>
        );
    }

    if (phase === "missing_key") {
        return (
            <div className="text-center py-20">
                <p className="text-5xl mb-4">ğŸ”’</p>
                <p className="text-[#0A1931] font-semibold mb-2">Bu sınav henüz başlatılamaz.</p>
                <p className="text-[#A9A9A9] text-sm mb-6">Cevap anahtarı girilmediği için değerlendirme yapılamamaktadır.</p>
                <Link href="/dashboard/exams" className="text-violet-400 text-sm hover:underline">â† Sınavlara Dön</Link>
            </div>
        );
    }

    // ── READY (Başlamadan) ─────────────────────────────────────────────────────
    if (phase === "ready") {
        return (
            <div className="max-w-xl mx-auto text-center py-12">
                <div className="w-20 h-20 rounded-2xl bg-gradient-to-br from-violet-500 to-indigo-600 flex items-center justify-center text-4xl mx-auto mb-6 shadow-xl shadow-violet-500/25">
                    ğŸ“‹
                </div>
                <h1 className="text-2xl font-bold text-[#0A1931] mb-2">{exam.title}</h1>
                {exam.description && <p className="text-[#A0AEC0] text-sm mb-6">{exam.description}</p>}

                <div className="glass-card p-5 mb-8 grid grid-cols-3 gap-4 text-center">
                    <div>
                        <p className="text-2xl font-bold gradient-text">{exam.questionCount}</p>
                        <p className="text-[#A9A9A9] text-xs mt-1">Soru</p>
                    </div>
                    <div>
                        <p className="text-2xl font-bold text-blue-400">{exam.durationMinutes ? `${exam.durationMinutes} dk` : "âˆ"}</p>
                        <p className="text-[#A9A9A9] text-xs mt-1">Süre</p>
                    </div>
                    <div>
                        <p className="text-2xl font-bold text-amber-400">{exam.wrongPenaltyWeight}</p>
                        <p className="text-[#A9A9A9] text-xs mt-1">Yanlis Katsayısı</p>
                    </div>
                </div>

                {exam.pdfUrl && (
                    <a
                        href={getFileUrl(exam.pdfUrl)}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center gap-2 px-5 py-2.5 bg-white/5 border border-white/10 text-[#A0AEC0] text-sm rounded-xl hover:bg-white/10 transition-all mb-4 mr-3"
                    >
                        📄 Sınav Kağıdını Aç
                    </a>
                )}

                <button
                    onClick={startExam}
                    className="inline-flex items-center gap-2 px-8 py-3 bg-violet-600 hover:bg-violet-500 text-[#0A1931] font-semibold rounded-xl transition-all shadow-lg shadow-violet-500/25"
                >
                    â–¶ Sınava Başla
                </button>

                <div className="mt-6">
                    <Link href="/dashboard/exams" className="text-[#1B3B6F] text-sm hover:text-[#A0AEC0]">â† Geri Dön</Link>
                </div>
            </div>
        );
    }

    // ── SOLVING ────────────────────────────────────────────────────────────────
    if (phase === "solving" || phase === "confirm") {
        return (
            <div className="max-w-7xl mx-auto select-none">
                {/* Güvenlik Katmanı: Tam Ekran Uyarı Modalı */}
                {!isFullscreen && phase === "solving" && (
                    <div className="fixed inset-0 bg-[#0A1931] z-[100] flex flex-col items-center justify-center p-6 text-center">
                        <div className="w-24 h-24 bg-red-500/20 text-red-500 rounded-full flex items-center justify-center text-4xl mb-6">
                            ğŸ–¥ï¸
                        </div>
                        <h2 className="text-3xl font-bold text-white mb-4">Sınava Tam Ekranda Devam Etmelisiniz</h2>
                        <p className="text-[#A0AEC0] mb-8 max-w-lg">
                            Güvenlik kuralları gereği bu sınav sadece tam ekran modunda çözülebilir. Sınav ekranından ayrılmanız ihlal sayılacaktır. Lütfen sınava dönmek için aşağıdaki butona tıklayın.
                        </p>
                        <button
                            onClick={async () => {
                                await requestFullscreen();
                                setIsFullscreen(true);
                            }}
                            className="px-8 py-4 bg-violet-600 hover:bg-violet-500 text-white font-bold rounded-xl transition-all shadow-lg"
                        >
                            Tam Ekrana Dön
                        </button>
                    </div>
                )}

                {/* Güvenlik Katmanı: Odak Kaybı / Kural İhlali Uyarı Modalı */}
                {isSecurityWarningOpen && isFullscreen && phase === "solving" && (
                    <div className="fixed inset-0 bg-black/80 backdrop-blur-md z-[110] flex items-center justify-center p-6 text-center">
                        <div className="glass-card p-10 max-w-md w-full bg-rose-950/30 border-rose-500/30 shadow-2xl shadow-rose-900/50">
                            <div className="text-6xl mb-6 animate-pulse">âš ï¸</div>
                            <h2 className="text-2xl font-bold text-white mb-2">Güvenlik Uyarısı</h2>
                            <p className="text-rose-200 mb-6 text-sm">
                                Sınav ekranından ayrıldığınız veya kural dışı bir eylem (sağ tık, yazdırma, ekran görüntüsü alma) gerçekleştirdiğiniz tespit edildi. Lütfen sadece sınav ekranında kalın!
                            </p>
                            <button
                                onClick={() => setIsSecurityWarningOpen(false)}
                                className="w-full py-3 bg-white text-rose-900 font-bold rounded-xl hover:bg-rose-50 transition-all"
                            >
                                Anladım, Sınava Dön
                            </button>
                        </div>
                    </div>
                )}
                {/* Üst bar */}
                <div className="flex items-center justify-between mb-6 gap-4">
                    <div>
                        <h1 className="text-[#0A1931] font-bold text-lg">{exam.title}</h1>
                        <p className="text-[#A9A9A9] text-xs">{exam.examType} • {answered}/{total} cevaplandı</p>
                    </div>
                    <div className="flex items-center gap-4">
                        {exam.durationMinutes && (
                            <CountdownTimer 
                                initialSeconds={exam.durationMinutes * 60} 
                                onComplete={() => handleSubmit(true)} 
                            />
                        )}
                        <button
                            onClick={() => setPhase("confirm")}
                            className="px-5 py-2.5 bg-violet-600 hover:bg-violet-500 text-[#0A1931] text-sm font-medium rounded-xl transition-all shadow-lg shadow-violet-500/20"
                        >
                            Sınavı Bitir â†’
                        </button>
                    </div>
                </div>

                <div className="grid grid-cols-1 lg:grid-cols-[1fr_360px] xl:grid-cols-[1fr_400px] gap-6">
                    {/* Kitapçık Alanı (Masaüstü Split Screen / Mobil Modal) */}
                    <div className={`lg:block ${showPdfMobile ? 'fixed inset-0 z-50 bg-white p-2 flex flex-col' : 'hidden'} h-[calc(100vh-140px)] rounded-2xl overflow-hidden shadow-xl border border-[#E2E8F0]/50 bg-[#E2E8F0]/20`}>
                        {showPdfMobile && (
                            <div className="flex justify-between items-center p-3 shrink-0">
                                <h3 className="font-bold text-[#0A1931]">Sınav Kitapçığı</h3>
                                <button onClick={() => setShowPdfMobile(false)} className="p-2 bg-red-100 text-red-600 rounded-xl">Kapat</button>
                            </div>
                        )}
                        {exam.pdfUrl ? (
                            <SecurePdfViewer url={getFileUrl(exam.pdfUrl)} />
                        ) : (
                            <div className="w-full h-full flex flex-col items-center justify-center text-[#A9A9A9]">
                                <p className="text-4xl mb-3">📄</p>
                                <p>Sınav kitapçığı yüklenmemiş.</p>
                            </div>
                        )}
                    </div>

                    <div className="flex flex-col gap-4 h-[calc(100vh-140px)] overflow-hidden">
                        {/* Mobil Kitapçık Aç Butonu */}
                        {exam.pdfUrl && (
                            <button onClick={() => setShowPdfMobile(true)} className="lg:hidden w-full py-3 bg-violet-100 text-violet-700 font-bold rounded-xl border border-violet-200 flex justify-center items-center gap-2 shadow-sm shrink-0">
                                📄 Kitapçığı Göster
                            </button>
                        )}

                        {/* Optik form */}
                        <div className="glass-card flex flex-col overflow-hidden h-full">
                            <div className="p-4 border-b border-[#E2E8F0]/60 shrink-0">
                                <h2 className="text-[#0A1931] text-sm font-semibold">Cevap Kağıdı</h2>
                            </div>

                            {/* Bölümler (Tabs) */}
                            {sections && (
                                <div className="flex overflow-x-auto gap-2 p-3 shrink-0 border-b border-[#E2E8F0]/60 custom-scrollbar">
                                    {sections.map((sec, idx) => (
                                        <button 
                                            key={idx}
                                            onClick={() => setActiveSectionIdx(idx)}
                                            className={`px-4 py-2 text-xs font-bold whitespace-nowrap rounded-lg transition-colors ${activeSectionIdx === idx ? 'bg-[#0A1931] text-white' : 'bg-[#E2E8F0]/30 text-[#1B3B6F] hover:bg-[#E2E8F0]/50'}`}
                                        >
                                            {sec.name}
                                        </button>
                                    ))}
                                </div>
                            )}

                            <div className="flex-1 overflow-y-auto p-4 space-y-1.5 custom-scrollbar">
                                {currentQuestions.map(qNum => (
                                    <div
                                        key={qNum}
                                        id={`q${qNum}`}
                                        className={`flex items-center gap-3 px-3 py-2.5 rounded-xl transition-all ${activeQ === qNum ? "bg-violet-500/10 border border-violet-500/30" : "hover:bg-white/3"}`}
                                        onClick={() => setActiveQ(qNum)}
                                    >
                                        <span className="text-[#A9A9A9] text-xs font-mono w-7 flex-shrink-0">
                                            {sections && sections[activeSectionIdx] ? qNum - sections[activeSectionIdx].start + 1 : qNum}.
                                        </span>
                                        <div className="flex gap-2">
                                            {options.map(opt => {
                                                const selected = answers[qNum] === opt;
                                                return (
                                                    <button
                                                        key={opt}
                                                        onClick={e => { e.stopPropagation(); setAnswers(prev => ({ ...prev, [qNum]: prev[qNum] === opt ? undefined as unknown as string : opt })); }}
                                                        className={`w-9 h-9 rounded-lg text-sm font-bold transition-all border ${selected
                                                                ? "bg-violet-600 border-violet-500 text-[#0A1931] shadow-md shadow-violet-500/25"
                                                                : "bg-white/3 border-white/10 text-[#A9A9A9] hover:border-violet-500/40 hover:text-[#0A1931]"
                                                            }`}
                                                    >
                                                        {opt}
                                                    </button>
                                                );
                                            })}
                                        </div>
                                    </div>
                                ))}
                            </div>
                            
                            {/* Alt Bilgi */}
                            <div className="p-4 border-t border-[#E2E8F0]/60 shrink-0 flex items-center justify-between text-xs font-medium text-[#A9A9A9]">
                                <span>Tümü: {answered}/{total}</span>
                                {sections && (
                                    <span className="text-violet-500">{sections[activeSectionIdx].name}: {currentQuestions.filter(q => answers[q]).length}/{currentQuestions.length}</span>
                                )}
                            </div>
                        </div>
                    </div>
                </div>

                {/* Onay modalı */}
                {phase === "confirm" && (
                    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
                        <div className="glass-card p-8 max-w-sm w-full text-center">
                            <p className="text-4xl mb-4">âš ï¸</p>
                            <h2 className="text-[#0A1931] font-bold text-lg mb-2">Sınavı Bitir?</h2>
                            <div className="flex gap-4 justify-center text-sm mb-6">
                                <span className="text-green-400">âœ“ {answered} cevaplandı</span>
                                <span className="text-[#A9A9A9]">— {total - answered} boş</span>
                            </div>
                            <p className="text-[#A9A9A9] text-sm mb-6">Gönderildikten sonra değişiklik yapılamaz.</p>
                            <div className="flex gap-3">
                                <button
                                    onClick={() => setPhase("solving")}
                                    className="flex-1 py-2.5 bg-white/5 border border-white/10 text-[#A0AEC0] rounded-xl text-sm hover:bg-white/10 transition-all"
                                >
                                    Geri Dön
                                </button>
                                <button
                                    onClick={() => handleSubmit(true)}
                                    disabled={submitting}
                                    className="flex-1 py-2.5 bg-violet-600 hover:bg-violet-500 disabled:opacity-50 text-[#0A1931] rounded-xl text-sm font-semibold transition-all"
                                >
                                    {submitting ? "Gönderiliyor..." : "Gönder âœ“"}
                                </button>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        );
    }

    // ── DONE (Sonuç) ───────────────────────────────────────────────────────────
    if (phase === "done") {
        if (!result) {
            return (
                <div className="max-w-2xl mx-auto text-center py-24 animate-fade-in-up px-4">
                    <div className="w-24 h-24 bg-[#E2E8F0] text-[#0A1931] rounded-full flex items-center justify-center mx-auto mb-6 text-4xl shadow-inner border-4 border-white">
                        â³
                    </div>
                    <h2 className="text-3xl font-black text-[#0A1931] mb-3 tracking-tight">Cevaplarınız İletildi!</h2>
                    <p className="text-[#A0AEC0] mb-8 leading-relaxed max-w-lg mx-auto">
                        Sınavınız başarıyla kaydedildi ancak sonuçlarınız şu anda işleniyor veya cevap anahtarı henüz açıklanmadı. Lütfen daha sonra tekrar kontrol edin.
                    </p>
                    <Link
                        href="/dashboard/exams"
                        className="px-8 py-3.5 bg-[#0A1931] hover:bg-[#1B3B6F] text-white font-bold rounded-xl transition-all shadow-lg shadow-[#0A1931]/20 inline-flex items-center gap-2"
                    >
                        Sınavlara Dön
                    </Link>
                </div>
            );
        }

        const isAboveAvg = result.averageScore !== null && result.score >= result.averageScore;
        const net = result.net ?? (result.correctCount - (result.wrongCount * exam!.wrongPenaltyWeight));
        
        return (
            <div className="max-w-5xl mx-auto py-10 px-4 sm:px-6 animate-fade-in-up">
                {/* Tepedeki Devasa Başarı Kartı (Hero Scorecard) */}
                <div className="relative rounded-[2.5rem] overflow-hidden mb-8 shadow-2xl shadow-indigo-900/10 bg-[#0A1931] group">
                    {/* Arkaplan Işık Efektleri */}
                    <div className="absolute top-0 right-0 w-[40rem] h-[40rem] bg-gradient-to-bl from-indigo-500/20 via-violet-500/10 to-transparent rounded-full blur-3xl -translate-y-1/2 translate-x-1/3"></div>
                    <div className="absolute bottom-0 left-0 w-64 h-64 bg-emerald-500/10 rounded-full blur-2xl translate-y-1/3 -translate-x-1/4"></div>
                    
                    <div className="relative p-8 md:p-12 flex flex-col md:flex-row items-center gap-10">
                        {/* Sol: Büyük Skor Topu */}
                        <div className="shrink-0 relative">
                            <div className="w-48 h-48 rounded-full border-[12px] border-white/5 flex flex-col items-center justify-center relative z-10 bg-[#0A1931]/50 backdrop-blur-sm shadow-inner">
                                <span className="text-sm font-bold text-white/50 uppercase tracking-widest mb-1">PUAN</span>
                                <span className="text-6xl font-black bg-clip-text text-transparent bg-gradient-to-br from-emerald-300 to-cyan-300 tracking-tighter">
                                    {result.score != null ? Number(result.score).toFixed(1) : "—"}
                                </span>
                            </div>
                            {/* Dönen dış halka efekti */}
                            <div className="absolute inset-0 rounded-full border border-emerald-400/30 animate-[spin_10s_linear_infinite] -m-2 border-dashed"></div>
                        </div>

                        {/* Orta: Sınav Bilgisi & Temel Net */}
                        <div className="flex-1 text-center md:text-left">
                            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-white/10 border border-white/10 text-white/70 text-[10px] font-bold uppercase tracking-widest mb-4">
                                {exam?.examType ?? "Sınav Karnesi"}
                            </div>
                            <h1 className="text-3xl md:text-4xl font-black text-white tracking-tight mb-2 leading-tight">
                                {exam?.title}
                            </h1>
                            <p className="text-white/50 text-sm font-medium mb-8">
                                Sınavı tamamladınız. Analiz raporunuz aşağıdadır.
                            </p>

                            <div className="flex flex-wrap items-center justify-center md:justify-start gap-4">
                                <div className="flex items-center gap-3 bg-white/5 backdrop-blur-md border border-white/10 rounded-2xl px-5 py-3">
                                    <div className="w-10 h-10 rounded-full bg-blue-500/20 text-blue-300 flex items-center justify-center font-black text-lg">
                                        ğŸ¯
                                    </div>
                                    <div>
                                        <p className="text-[10px] font-bold text-white/40 uppercase tracking-widest">Toplam Net</p>
                                        <p className="text-2xl font-black text-white">{net != null ? Number(net).toFixed(2) : "—"}</p>
                                    </div>
                                </div>
                                {result.rank !== null && (
                                    <div className="flex items-center gap-3 bg-white/5 backdrop-blur-md border border-white/10 rounded-2xl px-5 py-3">
                                        <div className="w-10 h-10 rounded-full bg-amber-500/20 text-amber-300 flex items-center justify-center font-black text-lg">
                                            ğŸ†
                                        </div>
                                        <div>
                                            <p className="text-[10px] font-bold text-white/40 uppercase tracking-widest">Sıralama</p>
                                            <p className="text-2xl font-black text-white">{result.rank}. <span className="text-sm font-medium text-white/50">sıra</span></p>
                                        </div>
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                </div>

                {/* Genel İstatistik Kartları */}
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
                    <div className="bg-white rounded-3xl p-5 border border-[#E2E8F0] shadow-sm flex items-center gap-4 hover:shadow-md transition-all">
                        <div className="w-12 h-12 rounded-2xl bg-emerald-50 flex items-center justify-center text-emerald-600 font-bold text-xl">âœ“</div>
                        <div>
                            <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest">Doğru</p>
                            <p className="text-2xl font-black text-[#0A1931]">{result.correctCount}</p>
                        </div>
                    </div>
                    <div className="bg-white rounded-3xl p-5 border border-[#E2E8F0] shadow-sm flex items-center gap-4 hover:shadow-md transition-all">
                        <div className="w-12 h-12 rounded-2xl bg-rose-50 flex items-center justify-center text-rose-600 font-bold text-xl">âœ•</div>
                        <div>
                            <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest">Yanlış</p>
                            <p className="text-2xl font-black text-[#0A1931]">{result.wrongCount}</p>
                        </div>
                    </div>
                    <div className="bg-white rounded-3xl p-5 border border-[#E2E8F0] shadow-sm flex items-center gap-4 hover:shadow-md transition-all">
                        <div className="w-12 h-12 rounded-2xl bg-slate-100 flex items-center justify-center text-slate-500 font-bold text-xl">-</div>
                        <div>
                            <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest">Boş</p>
                            <p className="text-2xl font-black text-[#0A1931]">{result.emptyCount}</p>
                        </div>
                    </div>
                    {result.averageScore != null && (
                        <div className="bg-white rounded-3xl p-5 border border-[#E2E8F0] shadow-sm flex items-center gap-4 hover:shadow-md transition-all">
                            <div className={`w-12 h-12 rounded-2xl flex items-center justify-center font-bold text-xl ${isAboveAvg ? "bg-emerald-50 text-emerald-600" : "bg-rose-50 text-rose-600"}`}>
                                {isAboveAvg ? "â†‘" : "â†“"}
                            </div>
                            <div>
                                <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest">Sınıf Ort.</p>
                                <p className="text-2xl font-black text-[#0A1931]">{Number(result.averageScore).toFixed(1)}</p>
                            </div>
                        </div>
                    )}
                </div>

                {/* Ders Bazlı Analiz (Grid) */}
                {result.sectionResults && Object.keys(result.sectionResults).length > 0 && (
                    <div className="mb-10">
                        <h3 className="text-[#0A1931] font-bold text-xl mb-5 flex items-center gap-3">
                            <span className="w-8 h-8 rounded-lg bg-[#1B3B6F]/5 flex items-center justify-center text-[#1B3B6F]">ğŸ“Š</span>
                            Bölüm Analizi
                        </h3>
                        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
                            {Object.values(result.sectionResults).map((sec, i) => {
                                const total = sec.correctCount + sec.wrongCount + sec.emptyCount;
                                const successRate = total > 0 ? (sec.net / total) * 100 : 0;
                                return (
                                    <div key={i} className="bg-white rounded-3xl p-6 border border-[#E2E8F0] shadow-sm relative overflow-hidden group hover:border-[#1B3B6F]/30 hover:shadow-xl transition-all duration-300">
                                        <div className="absolute top-0 left-0 w-1.5 h-full bg-gradient-to-b from-[#1B3B6F] to-violet-500 rounded-l-3xl"></div>
                                        <div className="flex items-center justify-between mb-4">
                                            <p className="font-bold text-[#0A1931] text-base truncate pr-2" title={sec.name}>{sec.name}</p>
                                            <span className="shrink-0 text-xs font-bold px-2.5 py-1 rounded-full bg-slate-100 text-slate-500">{total} Soru</span>
                                        </div>
                                        
                                        <div className="flex gap-2 mb-5">
                                            <div className="flex-1 bg-emerald-50 text-emerald-700 py-2 rounded-xl text-center border border-emerald-100">
                                                <span className="block text-lg font-black">{sec.correctCount}</span>
                                                <span className="text-[9px] font-bold uppercase opacity-60">Doğru</span>
                                            </div>
                                            <div className="flex-1 bg-rose-50 text-rose-700 py-2 rounded-xl text-center border border-rose-100">
                                                <span className="block text-lg font-black">{sec.wrongCount}</span>
                                                <span className="text-[9px] font-bold uppercase opacity-60">Yanlış</span>
                                            </div>
                                            <div className="flex-1 bg-slate-50 text-slate-500 py-2 rounded-xl text-center border border-slate-200">
                                                <span className="block text-lg font-black">{sec.emptyCount}</span>
                                                <span className="text-[9px] font-bold uppercase opacity-60">Boş</span>
                                            </div>
                                        </div>
                                        
                                        <div className="pt-4 border-t border-[#E2E8F0] flex items-center justify-between">
                                            <div>
                                                <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest mb-0.5">NET</p>
                                                <p className="text-2xl font-black text-[#1B3B6F] bg-clip-text text-transparent bg-gradient-to-r from-[#0A1931] to-[#1B3B6F]">{Number(sec.net).toFixed(2)}</p>
                                            </div>
                                            <div className="w-12 h-12 rounded-full border-4 border-slate-100 flex items-center justify-center relative" 
                                                style={{ background: `conic-gradient(#10B981 ${Math.max(0, successRate)}%, transparent 0)` }}>
                                                <div className="absolute inset-1 bg-white rounded-full flex items-center justify-center">
                                                    <span className="text-[10px] font-bold text-[#0A1931]">{Math.max(0, Math.round(successRate))}%</span>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                )}

                <div className="flex flex-wrap gap-4 justify-center md:justify-end border-t border-[#E2E8F0] pt-6">
                    <Link
                        href="/dashboard/exams"
                        className="px-6 py-3 bg-white border border-[#E2E8F0] text-[#0A1931] font-bold rounded-xl hover:bg-slate-50 transition-all shadow-sm"
                    >
                        â† Sınavlara Dön
                    </Link>
                    {exam?.solutionPdfUrl && (
                        <a
                            href={getFileUrl(exam.solutionPdfUrl)}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="px-6 py-3 bg-[#1B3B6F] hover:bg-[#0A1931] text-white font-bold rounded-xl transition-all shadow-lg flex items-center gap-2"
                        >
                            📄 Çözüm Kitapçığını İncele
                        </a>
                    )}
                </div>
            </div>
        );
    }

    return null;
}


