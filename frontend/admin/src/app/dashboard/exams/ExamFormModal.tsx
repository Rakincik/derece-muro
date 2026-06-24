"use client";
import { useState, useEffect, useCallback } from "react";
import { X, ChevronRight, Check, FileText, Upload, Clock, BarChart3, Users, Rocket, Minus, Plus, User, Search, ChevronDown, BookOpen, Eye } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { groupsApi, courseApi, userApi, uploadApi } from "@/lib/api";

// ── Exam Templates ─────────────────────────────────────────────────
interface ExamSection { name: string; start: number; end: number }
interface ExamTemplate {
    label: string; group: string; questionCount: number; duration: number;
    options: number; penalty: number; sections: ExamSection[];
    defaultWeights?: Record<number, number>;
}

// Helper: section range'e göre katsayı oluştur
function buildWeights(sections: { start: number; end: number; weight: number }[]): Record<number, number> {
    const w: Record<number, number> = {};
    for (const s of sections) {
        for (let i = s.start; i <= s.end; i++) w[i] = s.weight;
    }
    return w;
}

const EXAM_TEMPLATES: Record<string, ExamTemplate> = {
    TYT: {
        label: "TYT", group: "YKS", questionCount: 120, duration: 165, options: 5, penalty: 0.25,
        sections: [{ name: "Türkçe", start: 1, end: 40 }, { name: "Sosyal Bilimler", start: 41, end: 60 }, { name: "Temel Matematik", start: 61, end: 100 }, { name: "Fen Bilimleri", start: 101, end: 120 }],
        defaultWeights: buildWeights([
            { start: 1, end: 40, weight: 1.32 },   // Türkçe
            { start: 41, end: 60, weight: 1.36 },   // Sosyal Bilimler
            { start: 61, end: 100, weight: 1.32 },  // Matematik
            { start: 101, end: 120, weight: 1.36 }, // Fen Bilimleri
        ]),
    },
    "AYT Sayısal": {
        label: "AYT Sayısal", group: "YKS", questionCount: 80, duration: 180, options: 5, penalty: 0.25,
        sections: [{ name: "Matematik", start: 1, end: 40 }, { name: "Fizik", start: 41, end: 54 }, { name: "Kimya", start: 55, end: 67 }, { name: "Biyoloji", start: 68, end: 80 }],
        defaultWeights: buildWeights([
            { start: 1, end: 40, weight: 3.00 },   // Matematik
            { start: 41, end: 54, weight: 2.85 },   // Fizik
            { start: 55, end: 67, weight: 3.07 },   // Kimya
            { start: 68, end: 80, weight: 3.07 },   // Biyoloji
        ]),
    },
    "AYT Sözel": {
        label: "AYT Sözel", group: "YKS", questionCount: 80, duration: 180, options: 5, penalty: 0.25,
        sections: [{ name: "Edebiyat", start: 1, end: 24 }, { name: "Tarih-1", start: 25, end: 34 }, { name: "Coğrafya-1", start: 35, end: 40 }, { name: "Tarih-2", start: 41, end: 51 }, { name: "Coğrafya-2", start: 52, end: 62 }, { name: "Felsefe", start: 63, end: 74 }, { name: "Din Kültürü", start: 75, end: 80 }],
        defaultWeights: buildWeights([
            { start: 1, end: 24, weight: 3.00 },    // Edebiyat
            { start: 25, end: 34, weight: 2.80 },   // Tarih-1
            { start: 35, end: 40, weight: 3.33 },   // Coğrafya-1
            { start: 41, end: 51, weight: 2.72 },   // Tarih-2
            { start: 52, end: 62, weight: 2.72 },   // Coğrafya-2
            { start: 63, end: 74, weight: 2.50 },   // Felsefe
            { start: 75, end: 80, weight: 3.33 },   // Din Kültürü
        ]),
    },
    "AYT Eşit Ağırlık": {
        label: "AYT EA", group: "YKS", questionCount: 80, duration: 180, options: 5, penalty: 0.25,
        sections: [{ name: "Edebiyat", start: 1, end: 24 }, { name: "Tarih-1", start: 25, end: 34 }, { name: "Coğrafya-1", start: 35, end: 40 }, { name: "Matematik", start: 41, end: 80 }],
        defaultWeights: buildWeights([
            { start: 1, end: 24, weight: 3.00 },    // Edebiyat
            { start: 25, end: 34, weight: 2.80 },   // Tarih-1
            { start: 35, end: 40, weight: 3.33 },   // Coğrafya-1
            { start: 41, end: 80, weight: 3.00 },   // Matematik
        ]),
    },
    YDT: {
        label: "YDT", group: "YKS", questionCount: 80, duration: 120, options: 5, penalty: 0.25,
        sections: [{ name: "Yabancı Dil", start: 1, end: 80 }],
        defaultWeights: buildWeights([{ start: 1, end: 80, weight: 3.75 }]),
    },
    LGS: {
        label: "LGS", group: "Lise/Ortaokul", questionCount: 90, duration: 135, options: 4, penalty: 0,
        sections: [{ name: "Türkçe", start: 1, end: 20 }, { name: "T.C. İnk. Tarihi", start: 21, end: 30 }, { name: "Din Kültürü", start: 31, end: 40 }, { name: "İngilizce", start: 41, end: 45 }, { name: "Matematik", start: 46, end: 65 }, { name: "Fen Bilimleri", start: 66, end: 85 }],
        defaultWeights: buildWeights([
            { start: 1, end: 20, weight: 4.00 },    // Türkçe
            { start: 21, end: 30, weight: 1.00 },   // T.C. İnk. Tarihi
            { start: 31, end: 40, weight: 1.00 },   // Din Kültürü
            { start: 41, end: 45, weight: 1.00 },   // İngilizce
            { start: 46, end: 65, weight: 4.00 },   // Matematik
            { start: 66, end: 85, weight: 4.00 },   // Fen Bilimleri
        ]),
    },
    KPSS_GY: {
        label: "KPSS Genel Yetenek", group: "KPSS", questionCount: 60, duration: 70, options: 5, penalty: 0.25,
        sections: [{ name: "Türkçe", start: 1, end: 30 }, { name: "Matematik", start: 31, end: 60 }],
        defaultWeights: buildWeights([
            { start: 1, end: 30, weight: 1.00 },     // Türkçe
            { start: 31, end: 60, weight: 1.00 },    // Matematik
        ]),
    },
    KPSS_GK: {
        label: "KPSS Genel Kültür", group: "KPSS", questionCount: 60, duration: 70, options: 5, penalty: 0.25,
        sections: [{ name: "Tarih", start: 1, end: 10 }, { name: "Coğrafya", start: 11, end: 20 }, { name: "Vatandaşlık", start: 21, end: 30 }, { name: "Güncel", start: 31, end: 40 }, { name: "Hukuk", start: 41, end: 50 }, { name: "İktisat", start: 51, end: 60 }],
        defaultWeights: buildWeights([
            { start: 1, end: 10, weight: 1.00 },
            { start: 11, end: 20, weight: 1.00 },
            { start: 21, end: 30, weight: 1.00 },
            { start: 31, end: 40, weight: 1.00 },
            { start: 41, end: 50, weight: 1.00 },
            { start: 51, end: 60, weight: 1.00 },
        ]),
    },
    KPSS_EB: {
        label: "KPSS Eğitim Bilimleri", group: "KPSS", questionCount: 80, duration: 100, options: 5, penalty: 0.25,
        sections: [{ name: "Eğitim Bilimleri", start: 1, end: 80 }],
        defaultWeights: buildWeights([{ start: 1, end: 80, weight: 1.00 }]),
    },
    OABT: {
        label: "ÖABT", group: "KPSS", questionCount: 75, duration: 150, options: 5, penalty: 0.25,
        sections: [{ name: "Alan Bilgisi", start: 1, end: 75 }],
        defaultWeights: buildWeights([{ start: 1, end: 75, weight: 1.00 }]),
    },
    ALES: {
        label: "ALES", group: "Diğer", questionCount: 100, duration: 150, options: 5, penalty: 0.25,
        sections: [{ name: "Sözel", start: 1, end: 50 }, { name: "Sayısal", start: 51, end: 100 }],
        defaultWeights: buildWeights([
            { start: 1, end: 50, weight: 1.00 },
            { start: 51, end: 100, weight: 1.00 },
        ]),
    },
    YDS: {
        label: "YDS", group: "Diğer", questionCount: 80, duration: 180, options: 5, penalty: 0,
        sections: [{ name: "Yabancı Dil", start: 1, end: 80 }],
        defaultWeights: buildWeights([{ start: 1, end: 80, weight: 1.25 }]),
    },
    DGS: {
        label: "DGS", group: "Diğer", questionCount: 120, duration: 150, options: 5, penalty: 0.25,
        sections: [{ name: "Sözel", start: 1, end: 60 }, { name: "Sayısal", start: 61, end: 120 }],
        defaultWeights: buildWeights([
            { start: 1, end: 60, weight: 1.00 },
            { start: 61, end: 120, weight: 1.00 },
        ]),
    },
    Deneme: { label: "Deneme", group: "Serbest", questionCount: 40, duration: 60, options: 5, penalty: 0.25, sections: [] },
    Quiz: { label: "Quiz", group: "Serbest", questionCount: 10, duration: 15, options: 4, penalty: 0, sections: [] },
    Genel: { label: "Genel", group: "Serbest", questionCount: 20, duration: 30, options: 5, penalty: 0.25, sections: [] },
};

const TEMPLATE_GROUPS = ["YKS", "Lise/Ortaokul", "KPSS", "Diğer", "Serbest"] as const;

// ── Form Data Type ──────────────────────────────────────────────────
export interface ExamFormData {
    title: string; description: string; examType: string; questionCount: number;
    optionCount: number; durationMinutes: number; pdfUrl: string; solutionPdfUrl: string;
    startDate: string; endDate: string; showResults: boolean;
    answerKey: Record<number, string>;
    questionWeights: Record<number, number>;
    questionTopics: Record<number, string>;
    sections: ExamSection[];
    assignments: { targetType: string; targetId: string; targetName: string }[];
    resultMode: string; resultPublishDate: string; wrongPenaltyWeight: number;
    maxScore: number;
    baseScore: number;
    virtualParticipantCount: number;
    status: string;
}

interface Props { onClose: () => void; onSave: (d: ExamFormData) => void; initialData?: any; }

const STEPS = [
    { id: 1, label: "Bilgiler", icon: FileText },
    { id: 2, label: "PDF Yükleme", icon: Upload },
    { id: 3, label: "Zamanlama", icon: Clock },
    { id: 4, label: "Optik Form", icon: BarChart3 },
    { id: 5, label: "Atama", icon: Users },
    { id: 6, label: "Görünüm", icon: Eye },
    { id: 7, label: "Yayınla", icon: Rocket },
];

type GroupDto = { id: string; name: string; memberCount: number };

export default function ExamFormModal({ onClose, onSave, initialData }: Props) {
    const { token, currentTenantId } = useAuth();
    const tenantId = currentTenantId;
    const [step, setStep] = useState(1);
    const [groups, setGroups] = useState<GroupDto[]>([]);
    const [students, setStudents] = useState<{ id: string; name: string }[]>([]);
    const [assignTab, setAssignTab] = useState<"group" | "user" | "course">("group");
    const [studentSearch, setStudentSearch] = useState("");
    const [courses, setCourses] = useState<{ id: string; name: string; studentCount: number }[]>([]);
    const [pdfUploading, setPdfUploading] = useState(false);
    const [solUploading, setSolUploading] = useState(false);

    const defTemplate = EXAM_TEMPLATES[initialData?.examType || "TYT"] || EXAM_TEMPLATES["TYT"];
    const [f, setF] = useState<ExamFormData>(() => {
        if (initialData) {
            return {
                title: initialData.title || "", description: initialData.description || "",
                examType: initialData.examType || "TYT", questionCount: initialData.questionCount || defTemplate.questionCount,
                optionCount: initialData.optionCount || defTemplate.options, durationMinutes: initialData.durationMinutes || defTemplate.duration,
                pdfUrl: initialData.pdfUrl || "", solutionPdfUrl: initialData.solutionPdfUrl || "",
                startDate: initialData.startDate ? new Date(initialData.startDate).toISOString().slice(0, 16) : "",
                endDate: initialData.endDate ? new Date(initialData.endDate).toISOString().slice(0, 16) : "",
                showResults: initialData.showResults ?? true, answerKey: initialData.answerKey || {},
                questionWeights: initialData.questionWeights || defTemplate.defaultWeights || {},
                questionTopics: initialData.questionTopics || {},
                sections: initialData.sections || defTemplate.sections,
                assignments: initialData.assignments || [],
                resultMode: initialData.resultMode || "immediate",
                resultPublishDate: initialData.resultPublishDate ? new Date(initialData.resultPublishDate).toISOString().slice(0, 16) : "",
                wrongPenaltyWeight: initialData.wrongPenaltyWeight ?? defTemplate.penalty,
                maxScore: initialData.maxScore ?? 100, baseScore: initialData.baseScore ?? 0, virtualParticipantCount: initialData.virtualParticipantCount || 0,
                status: initialData.status || "Taslak",
            };
        }
        return {
            title: "", description: "", examType: "TYT", questionCount: defTemplate.questionCount,
            optionCount: defTemplate.options, durationMinutes: defTemplate.duration,
            pdfUrl: "", solutionPdfUrl: "", startDate: "", endDate: "",
            showResults: true, answerKey: {}, questionWeights: defTemplate.defaultWeights ?? {}, questionTopics: {},
            sections: defTemplate.sections, assignments: [],
            resultMode: "immediate", resultPublishDate: "", wrongPenaltyWeight: defTemplate.penalty,
            maxScore: 100, baseScore: 0, virtualParticipantCount: 0,
            status: "Taslak",
        };
    });

    // fetch groups, courses and students from API
    useEffect(() => {
        if (!token || !tenantId) return;
        
        groupsApi.list(token, tenantId, { pageSize: 1000 }).then(res => {
            const items = (res as any).items || res;
            setGroups(Array.isArray(items) ? items.map((g: any) => ({ id: g.id, name: g.name, memberCount: g.memberCount ?? g.studentCount ?? 0 })) : []);
        }).catch(() => { });

        courseApi.list(token, tenantId, { pageSize: 1000 }).then(res => {
            const items = (res as any).items || res;
            setCourses(Array.isArray(items) ? items.map((c: any) => ({ id: c.id, name: c.title, studentCount: c.sessionCount || 0 })) : []);
        }).catch(() => { });

        userApi.list(token, tenantId, { pageSize: 2000 }).then(res => {
            const items = (res as any).items || res;
            // Only list students for exam assignment
            const studentList = Array.isArray(items) ? items.filter((u: any) => u.role === "Student" || u.role === "Öğrenci") : [];
            setStudents(studentList.map((u: any) => ({ id: u.id, name: `${u.firstName} ${u.lastName}` })));
        }).catch(() => { });

    }, [token, tenantId]);

    const upd = (k: string, v: unknown) => setF(p => ({ ...p, [k]: v }));
    const options = Array.from({ length: f.optionCount }, (_, i) => String.fromCharCode(65 + i));
    const filledCount = Object.keys(f.answerKey).length;

    // Apply template when examType changes
    const applyTemplate = useCallback((type: string) => {
        const t = EXAM_TEMPLATES[type];
        if (!t) return;
        setF(prev => ({
            ...prev,
            examType: type,
            questionCount: t.questionCount,
            optionCount: t.options,
            durationMinutes: t.duration,
            wrongPenaltyWeight: t.penalty,
            sections: t.sections,
            answerKey: {},
            questionWeights: t.defaultWeights ?? {},
            questionTopics: {},
        }));
    }, []);

    const toggleAnswer = (q: number, opt: string) => {
        setF(prev => {
            const next = { ...prev.answerKey };
            if (next[q] === opt) delete next[q]; else next[q] = opt;
            return { ...prev, answerKey: next };
        });
    };

    const setWeight = (q: number, w: number) => {
        setF(prev => ({ ...prev, questionWeights: { ...prev.questionWeights, [q]: Math.round(w * 1000) / 1000 } }));
    };

    const setTopic = (q: number, val: string) => {
        setF(prev => ({ ...prev, questionTopics: { ...prev.questionTopics, [q]: val } }));
    };

    const toggleGroup = (g: GroupDto) => {
        setF(prev => {
            const exists = prev.assignments.some(a => a.targetId === g.id && a.targetType === "Group");
            return {
                ...prev,
                assignments: exists
                    ? prev.assignments.filter(a => !(a.targetId === g.id && a.targetType === "Group"))
                    : [...prev.assignments, { targetType: "Group", targetId: g.id, targetName: g.name }]
            };
        });
    };

    const toggleStudent = (s: { id: string; name: string }) => {
        setF(prev => {
            const exists = prev.assignments.some(a => a.targetId === s.id && a.targetType === "User");
            return {
                ...prev,
                assignments: exists
                    ? prev.assignments.filter(a => !(a.targetId === s.id && a.targetType === "User"))
                    : [...prev.assignments, { targetType: "User", targetId: s.id, targetName: s.name }]
            };
        });
    };

    const canNext = () => {
        if (step === 1) return f.title.trim().length > 0 && f.questionCount >= 1;
        return true;
    };

    // file upload handler
    const handleFileUpload = async (file: File, type: "exam" | "solution") => {
        if (!token || !tenantId) return;
        const setter = type === "exam" ? setPdfUploading : setSolUploading;
        setter(true);
        try {
            const preRes = await uploadApi.getPresignedUrl(token, tenantId, file.name, file.type || "application/pdf");
            const uploadReq = await fetch(preRes.uploadUrl, { method: "PUT", body: file, headers: { "Content-Type": file.type || "application/pdf" } });
            if (!uploadReq.ok) throw new Error("Yükleme başarısız");
            upd(type === "exam" ? "pdfUrl" : "solutionPdfUrl", preRes.publicUrl);
        } catch (err) {
            console.error("PDF upload failed:", err);
            alert("Dosya yüklenirken bir hata oluştu.");
        } finally {
            setter(false);
        }
    };

    const totalWeight = Object.values(f.questionWeights).reduce((a, b) => a + b, 0);

    const inp = "w-full px-3 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0A1931]/10 focus:border-[#A0AEC0] transition-all";

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4" onClick={onClose}>
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
            <div className="relative w-full max-w-2xl bg-white rounded-2xl shadow-2xl max-h-[90vh] overflow-hidden flex flex-col" onClick={e => e.stopPropagation()}>
                {/* Header */}
                <div className="px-6 py-4 border-b border-[#E2E8F0]/60 shrink-0">
                    <div className="flex items-center justify-between mb-3">
                        <h2 className="text-lg font-bold text-[#0A1931]">Yeni Sınav Oluştur</h2>
                        <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/40 text-[#A0AEC0]"><X size={18} /></button>
                    </div>
                    {/* Stepper */}
                    <div className="flex items-center gap-1">
                        {STEPS.map((s, i) => (
                            <div key={s.id} className="flex items-center flex-1">
                                <button onClick={() => { if (s.id < step) setStep(s.id); }}
                                    className={`flex items-center gap-1.5 px-2 py-1.5 rounded-lg text-[10px] font-bold uppercase tracking-wider transition-all w-full justify-center
                                    ${step === s.id ? "bg-[#0A1931] text-white shadow-lg" :
                                            step > s.id ? "bg-[#E2E8F0]/40 text-[#A9A9A9]" : "bg-[#E2E8F0]/20 text-[#A0AEC0]"}`}>
                                    {step > s.id ? <Check size={10} /> : <s.icon size={10} />}
                                    <span className="hidden sm:inline">{s.label}</span>
                                </button>
                                {i < STEPS.length - 1 && <ChevronRight size={10} className="text-[#A9A9A9] mx-0.5 shrink-0" />}
                            </div>
                        ))}
                    </div>
                </div>

                {/* Content */}
                <div className="flex-1 overflow-y-auto p-6">
                    {/* Step 1: Bilgiler */}
                    {step === 1 && (
                        <div className="space-y-4">
                            <div className="text-center mb-2"><p className="text-xs text-[#A0AEC0]">Sınav temel bilgilerini girin</p></div>
                            <div>
                                <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Sınav Adı *</label>
                                <input type="text" value={f.title} onChange={e => upd("title", e.target.value)} className={inp} placeholder="TYT Deneme Sınavı 1" />
                            </div>
                            <div>
                                <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Açıklama</label>
                                <textarea value={f.description} onChange={e => upd("description", e.target.value)} className={`${inp} h-16 resize-none`} placeholder="Opsiyonel" />
                            </div>

                            {/* Exam Type Selector — Grouped Dropdown */}
                            <div>
                                <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Sınav Tipi</label>
                                <select value={f.examType} onChange={e => applyTemplate(e.target.value)} className={inp}>
                                    {TEMPLATE_GROUPS.map(group => {
                                        const types = Object.entries(EXAM_TEMPLATES).filter(([, t]) => t.group === group);
                                        return (
                                            <optgroup key={group} label={group}>
                                                {types.map(([key, t]) => <option key={key} value={key}>{t.label}</option>)}
                                            </optgroup>
                                        );
                                    })}
                                </select>
                            </div>

                            {/* Auto-filled fields */}
                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Süre (dk)</label>
                                    <input type="number" value={f.durationMinutes} onChange={e => upd("durationMinutes", Math.max(1, +e.target.value))} min={1} className={inp} />
                                </div>
                                <div>
                                    <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Yanlış Doğru Götürme</label>
                                    <select value={f.wrongPenaltyWeight} onChange={e => upd("wrongPenaltyWeight", +e.target.value)} className={inp}>
                                        <option value={0}>Yok</option>
                                        <option value={0.25}>4 Yanlış = 1 Doğru (0.25)</option>
                                        <option value={0.33}>3 Yanlış = 1 Doğru (0.33)</option>
                                    </select>
                                </div>
                            </div>
                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Taban Puan</label>
                                    <input type="number" value={f.baseScore} onChange={e => upd("baseScore", Math.max(0, +e.target.value))} min={0} className={inp} placeholder="0" />
                                    <p className="text-[9px] text-[#A0AEC0] mt-1">Sınava girişte verilen puan (ör. TYT: 100)</p>
                                </div>
                                <div>
                                    <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Tavan Puan</label>
                                    <input type="number" value={f.maxScore} onChange={e => upd("maxScore", Math.max(f.baseScore, +e.target.value))} min={f.baseScore} className={inp} placeholder="100" />
                                    <p className="text-[9px] text-[#A0AEC0] mt-1">Sınavın toplam değeri (ör. TYT: 500, Quiz: 100)</p>
                                </div>
                            </div>
                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Soru Sayısı</label>
                                    <div className="flex items-center gap-2">
                                        <button onClick={() => upd("questionCount", Math.max(1, f.questionCount - 1))}
                                            className="w-9 h-9 rounded-lg border border-[#E2E8F0] flex items-center justify-center hover:bg-[#E2E8F0]/30 transition-all"><Minus size={14} /></button>
                                        <input type="number" value={f.questionCount} onChange={e => upd("questionCount", Math.max(1, +e.target.value))} min={1} max={300}
                                            className={`${inp} text-center flex-1`} />
                                        <button onClick={() => upd("questionCount", Math.min(300, f.questionCount + 1))}
                                            className="w-9 h-9 rounded-lg border border-[#E2E8F0] flex items-center justify-center hover:bg-[#E2E8F0]/30 transition-all"><Plus size={14} /></button>
                                    </div>
                                </div>
                                <div>
                                    <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Seçenek Sayısı</label>
                                    <select value={f.optionCount} onChange={e => upd("optionCount", +e.target.value)} className={inp}>
                                        <option value={4}>4 (A-D)</option><option value={5}>5 (A-E)</option>
                                    </select>
                                </div>
                            </div>


                            {/* Editable Section Builder */}
                            <div>
                                <div className="flex items-center justify-between mb-2">
                                    <label className="text-xs font-medium text-[#1B3B6F]">Optik Form Bölümleri</label>
                                    <button onClick={() => {
                                        const lastEnd = f.sections.length > 0 ? f.sections[f.sections.length - 1].end : 0;
                                        if (lastEnd < f.questionCount) {
                                            setF(p => ({ ...p, sections: [...p.sections, { name: "Yeni Bölüm", start: lastEnd + 1, end: Math.min(lastEnd + 20, f.questionCount) }] }));
                                        }
                                    }} className="text-[10px] font-bold text-[#0A1931] px-2 py-1 rounded-lg border border-[#E2E8F0] hover:bg-[#E2E8F0]/30 transition-all flex items-center gap-1">
                                        <Plus size={10} /> Bölüm Ekle
                                    </button>
                                </div>
                                {f.sections.length === 0 ? (
                                    <p className="text-[10px] text-[#A0AEC0] italic">Bölüm eklenmedi — tüm sorular tek liste olarak gösterilir</p>
                                ) : (
                                    <div className="space-y-2">
                                        {f.sections.map((sec, i) => (
                                            <div key={i} className="flex items-center gap-2 p-2 rounded-lg bg-[#E2E8F0]/15 border border-[#E2E8F0]/40">
                                                <input type="text" value={sec.name} onChange={e => {
                                                    const s = [...f.sections]; s[i] = { ...s[i], name: e.target.value }; setF(p => ({ ...p, sections: s }));
                                                }} className="flex-1 px-2 py-1 text-xs font-bold bg-white border border-[#E2E8F0] rounded-md focus:outline-none focus:ring-1 focus:ring-[#0A1931]/20" placeholder="Bölüm adı" />
                                                <div className="flex items-center gap-1 text-[10px] text-[#A0AEC0]">
                                                    <input type="number" value={sec.start} min={1} max={f.questionCount} onChange={e => {
                                                        const s = [...f.sections]; s[i] = { ...s[i], start: Math.max(1, +e.target.value) }; setF(p => ({ ...p, sections: s }));
                                                    }} className="w-12 px-1 py-1 text-[10px] text-center bg-white border border-[#E2E8F0] rounded-md" />
                                                    <span>—</span>
                                                    <input type="number" value={sec.end} min={sec.start} max={f.questionCount} onChange={e => {
                                                        const s = [...f.sections]; s[i] = { ...s[i], end: Math.min(f.questionCount, +e.target.value) }; setF(p => ({ ...p, sections: s }));
                                                    }} className="w-12 px-1 py-1 text-[10px] text-center bg-white border border-[#E2E8F0] rounded-md" />
                                                </div>
                                                <button onClick={() => { const s = [...f.sections]; s.splice(i, 1); setF(p => ({ ...p, sections: s })); }}
                                                    className="p-1 rounded hover:bg-red-50 text-red-400 hover:text-red-600 transition-all"><X size={12} /></button>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>
                    )}

                    {/* Step 2: PDF Yükleme */}
                    {step === 2 && (
                        <div className="space-y-4">
                            <div className="text-center mb-2"><p className="text-xs text-[#A0AEC0]">Sınav kitapçığı ve çözüm dosyalarını yükleyin</p></div>
                            {/* Exam PDF */}
                            <div className={`border-2 border-dashed rounded-xl p-6 text-center transition-all ${f.pdfUrl ? "border-emerald-300 bg-emerald-50/30" : "border-[#E2E8F0] bg-[#E2E8F0]/10"}`}>
                                <Upload size={28} className={`mx-auto mb-2 ${f.pdfUrl ? "text-emerald-400" : "text-[#A0AEC0]"}`} />
                                <p className="text-sm font-bold text-[#0A1931] mb-1">Sınav Kitapçığı (PDF)</p>
                                <p className="text-[10px] text-[#A0AEC0] mb-3 uppercase tracking-widest font-semibold">Öğrencilerin göreceği sınav dosyası</p>
                                {f.pdfUrl ? (
                                    <div className="flex items-center justify-center gap-2">
                                        <span className="text-xs text-emerald-600 font-bold flex items-center gap-1"><Check size={12} /> PDF yüklendi</span>
                                        <button onClick={() => upd("pdfUrl", "")} className="text-xs text-red-400 hover:text-red-600 font-bold">Kaldır</button>
                                    </div>
                                ) : (
                                    <label className="inline-block px-4 py-2 bg-[#0A1931] text-white text-xs font-bold rounded-xl cursor-pointer hover:bg-[#1B3B6F] transition-all">
                                        {pdfUploading ? "Yükleniyor..." : "PDF Seç"}
                                        <input type="file" accept=".pdf" className="hidden" onChange={e => { if (e.target.files?.[0]) handleFileUpload(e.target.files[0], "exam"); }} />
                                    </label>
                                )}
                            </div>
                            {/* Solution PDF */}
                            <div className={`border-2 border-dashed rounded-xl p-6 text-center transition-all ${f.solutionPdfUrl ? "border-emerald-300 bg-emerald-50/30" : "border-emerald-200 bg-emerald-50/10"}`}>
                                <FileText size={28} className={`mx-auto mb-2 ${f.solutionPdfUrl ? "text-emerald-400" : "text-emerald-300"}`} />
                                <p className="text-sm font-medium text-[#0A1931] mb-1">Çözüm Kitapçığı (PDF)</p>
                                <p className="text-[10px] text-[#A0AEC0] mb-3 uppercase tracking-widest font-semibold">Opsiyonel — sınav sonrası paylaşılır</p>
                                {f.solutionPdfUrl ? (
                                    <div className="flex items-center justify-center gap-2">
                                        <span className="text-xs text-emerald-600 font-bold flex items-center gap-1"><Check size={12} /> Çözüm PDF yüklendi</span>
                                        <button onClick={() => upd("solutionPdfUrl", "")} className="text-xs text-red-400 hover:text-red-600 font-bold">Kaldır</button>
                                    </div>
                                ) : (
                                    <label className="inline-block px-4 py-2 bg-emerald-500 text-white text-xs font-bold rounded-xl cursor-pointer hover:bg-emerald-600 transition-all">
                                        {solUploading ? "Yükleniyor..." : "Çözüm PDF Seç"}
                                        <input type="file" accept=".pdf" className="hidden" onChange={e => { if (e.target.files?.[0]) handleFileUpload(e.target.files[0], "solution"); }} />
                                    </label>
                                )}
                            </div>
                        </div>
                    )}

                    {/* Step 3: Zamanlama */}
                    {step === 3 && (
                        <div className="space-y-4">
                            <div className="text-center mb-2"><p className="text-xs text-[#A0AEC0]">Sınavın erişilebilir olacağı tarih aralığını belirleyin</p></div>
                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Başlangıç Tarihi</label>
                                    <input type="datetime-local" value={f.startDate} onChange={e => upd("startDate", e.target.value)} className={inp} />
                                </div>
                                <div>
                                    <label className="block text-xs font-medium text-[#1B3B6F] mb-1.5">Bitiş Tarihi</label>
                                    <input type="datetime-local" value={f.endDate} onChange={e => upd("endDate", e.target.value)} className={inp} />
                                </div>
                            </div>

                            {/* Result Mode */}
                            <div className="space-y-2">
                                <label className="block text-xs font-medium text-[#1B3B6F] mb-1">Sonuç Açıklama</label>
                                {[
                                    { v: "immediate", l: "Sınav bitince hemen göster", d: "Öğrenciler cevaplarını gönderdikten sonra sonuçlarını görür" },
                                    { v: "scheduled", l: "Belirlenen tarihte açıkla", d: "Sonuçlar belirlediğiniz tarih ve saatte otomatik açılır" },
                                    { v: "manual", l: "Manuel olarak açıklarım", d: "Sonuçları siz açana kadar gizli kalır" },
                                ].map(opt => (
                                    <div key={opt.v} onClick={() => upd("resultMode", opt.v)}
                                        className={`flex items-start gap-3 p-3 rounded-xl border-2 cursor-pointer transition-all ${f.resultMode === opt.v
                                            ? "border-[#0A1931] bg-[#0A1931]/5" : "border-[#E2E8F0] hover:border-[#A0AEC0]"}`}>
                                        <div className={`w-4 h-4 rounded-full border-2 mt-0.5 flex items-center justify-center shrink-0 ${f.resultMode === opt.v ? "border-[#0A1931]" : "border-[#A0AEC0]"}`}>
                                            {f.resultMode === opt.v && <div className="w-2 h-2 rounded-full bg-[#0A1931]" />}
                                        </div>
                                        <div>
                                            <p className="text-sm font-bold text-[#0A1931]">{opt.l}</p>
                                            <p className="text-[10px] text-[#A0AEC0]">{opt.d}</p>
                                        </div>
                                    </div>
                                ))}
                            </div>

                            {/* Scheduled date */}
                            {f.resultMode === "scheduled" && (
                                <div className="p-3 rounded-xl bg-amber-50 border border-amber-200">
                                    <label className="block text-xs font-medium text-amber-700 mb-1.5">Sonuç Açıklama Tarihi *</label>
                                    <input type="datetime-local" value={f.resultPublishDate} onChange={e => upd("resultPublishDate", e.target.value)}
                                        className={`${inp} border-amber-200 bg-white`} />
                                </div>
                            )}

                            {f.startDate && f.endDate && (
                                <div className="p-3 rounded-xl bg-blue-50 border border-blue-100">
                                    <p className="text-xs font-medium text-blue-700">📅 Sınav {new Date(f.startDate).toLocaleString("tr-TR")} ile {new Date(f.endDate).toLocaleString("tr-TR")} arasında aktif olacak.</p>
                                </div>
                            )}
                        </div>
                    )}

                    {/* Step 4: Optik Form + Katsayı */}
                    {step === 4 && (
                        <div className="space-y-4">
                            <div className="flex items-center justify-between">
                                <div>
                                    <p className="text-xs text-[#A0AEC0]">{filledCount}/{f.questionCount} soru işaretlendi</p>
                                    <p className="text-[10px] text-[#A0AEC0] mt-0.5">Toplam katsayı: <span className="font-bold text-[#0A1931]">{totalWeight.toFixed(3)}</span></p>
                                </div>
                                <div className="flex gap-1">
                                    <button onClick={() => {
                                        if (filledCount > 0 && !window.confirm(`${filledCount} sorunun cevap anahtarı silinecek. Emin misiniz?`)) return;
                                        setF(p => ({ ...p, answerKey: {} }));
                                    }}
                                        className="text-[10px] px-2 py-1 rounded bg-[#E2E8F0]/40 text-[#A9A9A9] hover:bg-[#E2E8F0]">Temizle</button>
                                </div>
                            </div>
                            <div className="w-full h-3 bg-[#E2E8F0]/40 rounded-full overflow-hidden">
                                <div className="h-full bg-[#0A1931] rounded-full transition-all" style={{ width: `${(filledCount / f.questionCount) * 100}%` }} />
                            </div>
                            <div className="overflow-y-auto max-h-[40vh]">
                                {f.sections.length > 0 ? (
                                    /* Sectioned view for TYT/AYT etc */
                                    <div className="space-y-4">
                                        {f.sections.map(sec => (
                                            <div key={sec.name}>
                                                <div className="sticky top-0 bg-[#0A1931] text-white px-3 py-1.5 rounded-lg z-10 text-xs font-bold uppercase tracking-wider mb-1">
                                                    {sec.name} ({sec.start}-{sec.end})
                                                </div>
                                                <table className="w-full">
                                                    <thead className="sticky top-8 bg-white z-[5]">
                                                        <tr className="border-b-2 border-[#E2E8F0]/60">
                                                            <th className="py-1 px-2 text-left text-[10px] font-bold text-[#A9A9A9] w-10">#</th>
                                                            <th className="py-1 px-2 text-left text-[10px] font-bold text-[#A9A9A9] w-28">Konu (Kazanım)</th>
                                                            {options.map(o => <th key={o} className="py-1 px-1 text-center text-[10px] font-bold text-[#A9A9A9] w-8">{o}</th>)}
                                                            <th className="py-1 px-1 text-center text-[10px] font-bold text-[#A9A9A9] w-8">✓</th>
                                                            <th className="py-1 px-1 text-center text-[10px] font-bold text-[#A9A9A9] w-20">Katsayı</th>
                                                        </tr>
                                                    </thead>
                                                    <tbody>
                                                        {Array.from({ length: sec.end - sec.start + 1 }, (_, i) => sec.start + i).map(q => (
                                                            <tr key={q} className={`border-b border-[#E2E8F0] ${f.answerKey[q] ? "" : "bg-amber-50/30"}`}>
                                                                <td className="py-0.5 px-2 text-[10px] font-bold text-[#A9A9A9]">{q - sec.start + 1}</td>
                                                                <td className="py-0.5 px-1">
                                                                    <input type="text" value={f.questionTopics[q] || ""} onChange={e => setTopic(q, e.target.value)}
                                                                        placeholder="Opsiyonel"
                                                                        className="w-full px-2 py-0.5 text-[9px] border border-[#E2E8F0] rounded-md focus:outline-none focus:ring-1 focus:ring-[#0A1931]/20" />
                                                                </td>
                                                                {options.map(o => (
                                                                    <td key={o} className="py-0.5 px-1 text-center">
                                                                        <button onClick={() => toggleAnswer(q, o)}
                                                                            className={`w-6 h-6 rounded-md border-2 text-[9px] font-bold transition-all ${f.answerKey[q] === o
                                                                                ? "bg-[#0A1931] border-[#0A1931] text-white scale-105"
                                                                                : "border-[#E2E8F0]/60 text-[#A0AEC0] hover:border-[#A0AEC0]"}`}>{o}</button>
                                                                    </td>
                                                                ))}
                                                                <td className="py-0.5 px-1 text-center">
                                                                    {f.answerKey[q] ? <Check size={12} className="text-emerald-500 mx-auto" /> : <span className="text-[#A9A9A9] text-[10px]">—</span>}
                                                                </td>
                                                                <td className="py-0.5 px-1">
                                                                    <input type="number" step="0.001" min="0" value={f.questionWeights[q] ?? 1} onChange={e => setWeight(q, +e.target.value)}
                                                                        className="w-16 mx-auto block px-1 py-0.5 text-[10px] text-center border border-[#E2E8F0] rounded-md focus:outline-none focus:ring-1 focus:ring-[#0A1931]/20" />
                                                                </td>
                                                            </tr>
                                                        ))}
                                                    </tbody>
                                                </table>
                                            </div>
                                        ))}
                                    </div>
                                ) : (
                                    /* Flat view for Deneme/Quiz/Genel */
                                    <table className="w-full">
                                        <thead className="sticky top-0 bg-white z-10">
                                            <tr className="border-b-2 border-[#E2E8F0]/60">
                                                <th className="py-2 px-3 text-left text-xs font-bold text-[#A9A9A9] w-14">#</th>
                                                <th className="py-2 px-2 text-left text-xs font-bold text-[#A9A9A9] w-32">Konu (Kazanım)</th>
                                                {options.map(o => <th key={o} className="py-2 px-1 text-center text-xs font-bold text-[#A9A9A9] w-12">{o}</th>)}
                                                <th className="py-2 px-2 text-center text-xs font-bold text-[#A9A9A9] w-12">✓</th>
                                                <th className="py-2 px-2 text-center text-xs font-bold text-[#A9A9A9] w-24">Katsayı</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {Array.from({ length: f.questionCount }, (_, i) => i + 1).map(q => (
                                                <tr key={q} className={`border-b border-[#E2E8F0] ${q % 2 === 0 ? "bg-[#E2E8F0]/10" : ""} ${f.answerKey[q] ? "" : "bg-amber-50/30"}`}>
                                                    <td className="py-1 px-3 text-xs font-bold text-[#A9A9A9]">{q}</td>
                                                    <td className="py-1 px-1">
                                                        <input type="text" value={f.questionTopics[q] || ""} onChange={e => setTopic(q, e.target.value)}
                                                            placeholder="Konu (Opsiyonel)"
                                                            className="w-full px-2 py-1 text-[10px] border border-[#E2E8F0] rounded-lg focus:outline-none focus:ring-1 focus:ring-[#0A1931]/20" />
                                                    </td>
                                                    {options.map(o => (
                                                        <td key={o} className="py-1 px-1 text-center">
                                                            <button onClick={() => toggleAnswer(q, o)}
                                                                className={`w-7 h-7 rounded-lg border-2 text-[10px] font-bold transition-all ${f.answerKey[q] === o
                                                                    ? "bg-[#0A1931] border-[#0A1931] text-white scale-110 shadow-sm"
                                                                    : "border-[#E2E8F0]/60 text-[#A0AEC0] hover:border-[#E2E8F0]"}`}>{o}</button>
                                                        </td>
                                                    ))}
                                                    <td className="py-1 px-2 text-center">
                                                        {f.answerKey[q] ? <Check size={14} className="text-emerald-500 mx-auto" /> : <span className="text-[#A9A9A9]">—</span>}
                                                    </td>
                                                    <td className="py-1 px-2">
                                                        <input type="number" step="0.001" min="0" value={f.questionWeights[q] ?? 1} onChange={e => setWeight(q, +e.target.value)}
                                                            className="w-20 mx-auto block px-2 py-1 text-xs text-center border border-[#E2E8F0] rounded-lg focus:outline-none focus:ring-1 focus:ring-[#0A1931]/20" />
                                                    </td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                )}
                            </div>
                        </div>
                    )}

                    {/* Step 5: Atama */}
                    {step === 5 && (
                        <div className="space-y-4">
                            <div className="text-center mb-2"><p className="text-xs text-[#A0AEC0]">Bu sınavı kimlere atamak istiyorsunuz?</p></div>

                            {/* Tab toggle */}
                            <div className="flex rounded-xl border border-[#E2E8F0] overflow-hidden">
                                <button onClick={() => setAssignTab("group")}
                                    className={`flex-1 py-2.5 text-xs font-bold flex items-center justify-center gap-1.5 transition-all ${assignTab === "group" ? "bg-[#0A1931] text-white" : "text-[#A0AEC0] hover:bg-[#E2E8F0]/20"}`}>
                                    <Users size={14} /> Gruplara
                                </button>
                                <button onClick={() => setAssignTab("course")}
                                    className={`flex-1 py-2.5 text-xs font-bold flex items-center justify-center gap-1.5 transition-all ${assignTab === "course" ? "bg-[#0A1931] text-white" : "text-[#A0AEC0] hover:bg-[#E2E8F0]/20"}`}>
                                    <BookOpen size={14} /> Derslere
                                </button>
                                <button onClick={() => setAssignTab("user")}
                                    className={`flex-1 py-2.5 text-xs font-bold flex items-center justify-center gap-1.5 transition-all ${assignTab === "user" ? "bg-[#0A1931] text-white" : "text-[#A0AEC0] hover:bg-[#E2E8F0]/20"}`}>
                                    <User size={14} /> Bireysel
                                </button>
                            </div>

                            {assignTab === "group" ? (
                                <div className="space-y-2">
                                    {groups.length === 0 ? (
                                        <div className="text-center py-8 text-[#A0AEC0]">
                                            <Users size={32} className="mx-auto opacity-30 mb-2" />
                                            <p className="text-sm">Henüz grup oluşturulmamış</p>
                                        </div>
                                    ) : groups.map(g => {
                                        const selected = f.assignments.some(a => a.targetId === g.id && a.targetType === "Group");
                                        return (
                                            <button key={g.id} onClick={() => toggleGroup(g)}
                                                className={`w-full flex items-center justify-between p-4 rounded-2xl border-2 transition-all text-left ${selected
                                                    ? "border-[#0A1931] bg-[#0A1931]/5 shadow-sm" : "border-[#E2E8F0]/60 hover:border-[#A0AEC0] bg-white"}`}>
                                                <div className="flex items-center gap-3">
                                                    <div className={`w-10 h-10 rounded-xl flex items-center justify-center text-sm font-bold ${selected ? "bg-[#0A1931] text-white" : "bg-[#E2E8F0]/40 text-[#A0AEC0]"}`}>
                                                        {selected ? <Check size={18} /> : <Users size={18} />}
                                                    </div>
                                                    <div>
                                                        <p className="text-sm font-semibold text-[#0A1931]">{g.name}</p>
                                                        <p className="text-xs text-[#A0AEC0]">{g.memberCount} öğrenci</p>
                                                    </div>
                                                </div>
                                                {selected && <span className="text-xs font-bold text-[#0A1931] bg-[#E2E8F0]/40 px-2 py-1 rounded-lg">Seçildi</span>}
                                            </button>
                                        );
                                    })}
                                </div>
                            ) : assignTab === "course" ? (
                                <div className="space-y-2">
                                    {courses.length === 0 ? (
                                        <div className="text-center py-8 text-[#A0AEC0]">
                                            <BookOpen size={32} className="mx-auto opacity-30 mb-2" />
                                            <p className="text-sm">Ders listesi yüklenecek</p>
                                        </div>
                                    ) : courses.map(c => {
                                        const selected = f.assignments.some(a => a.targetId === c.id && a.targetType === "Course");
                                        return (
                                            <button key={c.id} onClick={() => {
                                                setF(prev => {
                                                    const exists = prev.assignments.some(a => a.targetId === c.id && a.targetType === "Course");
                                                    return {
                                                        ...prev, assignments: exists
                                                            ? prev.assignments.filter(a => !(a.targetId === c.id && a.targetType === "Course"))
                                                            : [...prev.assignments, { targetType: "Course", targetId: c.id, targetName: c.name }]
                                                    };
                                                });
                                            }} className={`w-full flex items-center justify-between p-4 rounded-2xl border-2 transition-all text-left ${selected
                                                ? "border-[#0A1931] bg-[#0A1931]/5 shadow-sm" : "border-[#E2E8F0]/60 hover:border-[#A0AEC0] bg-white"}`}>
                                                <div className="flex items-center gap-3">
                                                    <div className={`w-10 h-10 rounded-xl flex items-center justify-center text-sm font-bold ${selected ? "bg-[#0A1931] text-white" : "bg-[#E2E8F0]/40 text-[#A0AEC0]"}`}>
                                                        {selected ? <Check size={18} /> : <BookOpen size={18} />}
                                                    </div>
                                                    <div>
                                                        <p className="text-sm font-semibold text-[#0A1931]">{c.name}</p>
                                                        <p className="text-xs text-[#A0AEC0]">{c.studentCount} öğrenci</p>
                                                    </div>
                                                </div>
                                                {selected && <span className="text-xs font-bold text-[#0A1931] bg-[#E2E8F0]/40 px-2 py-1 rounded-lg">Seçildi</span>}
                                            </button>
                                        );
                                    })}
                                </div>
                            ) : (
                                <div className="space-y-3">
                                    <div className="relative">
                                        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                                        <input type="text" placeholder="Öğrenci ara..." value={studentSearch} onChange={e => setStudentSearch(e.target.value)}
                                            className={`${inp} pl-9`} />
                                    </div>
                                    {students.length === 0 ? (
                                        <div className="text-center py-8 text-[#A0AEC0]">
                                            <User size={32} className="mx-auto opacity-30 mb-2" />
                                            <p className="text-sm">Öğrenci listesi yüklenecek</p>
                                            <p className="text-[10px] mt-1">Grup atama tercih edilir</p>
                                        </div>
                                    ) : students.filter(s => !studentSearch || s.name.toLowerCase().includes(studentSearch.toLowerCase())).map(s => {
                                        const selected = f.assignments.some(a => a.targetId === s.id && a.targetType === "User");
                                        return (
                                            <button key={s.id} onClick={() => toggleStudent(s)}
                                                className={`w-full flex items-center justify-between p-3 rounded-xl border-2 transition-all text-left ${selected
                                                    ? "border-[#0A1931] bg-[#0A1931]/5" : "border-[#E2E8F0]/60 hover:border-[#A0AEC0]"}`}>
                                                <div className="flex items-center gap-3">
                                                    <div className={`w-8 h-8 rounded-lg flex items-center justify-center ${selected ? "bg-[#0A1931] text-white" : "bg-[#E2E8F0]/40 text-[#A0AEC0]"}`}>
                                                        {selected ? <Check size={14} /> : <User size={14} />}
                                                    </div>
                                                    <p className="text-sm font-medium text-[#0A1931]">{s.name}</p>
                                                </div>
                                            </button>
                                        );
                                    })}
                                </div>
                            )}

                            {f.assignments.length > 0 && (
                                <div className="p-3 rounded-xl bg-emerald-50 border border-emerald-100">
                                    <p className="text-xs font-bold text-emerald-700">
                                        ✅ {f.assignments.filter(a => a.targetType === "Group").length} grup,{" "}
                                        {f.assignments.filter(a => a.targetType === "User").length} bireysel seçildi
                                        {f.assignments.filter(a => a.targetType === "Group").length > 0 && (
                                            <> — toplam {f.assignments.filter(a => a.targetType === "Group").reduce((s, a) => s + (groups.find(g => g.id === a.targetId)?.memberCount || 0), 0)} öğrenci</>
                                        )}
                                    </p>
                                </div>
                            )}
                        </div>
                    )}

                    {/* Step 6: Görünüm */}
                    {step === 6 && (
                        <div className="space-y-5">
                            <div className="text-center mb-2">
                                <div className="w-12 h-12 rounded-xl bg-[#0A1931]/10 flex items-center justify-center mx-auto mb-2"><Eye size={22} className="text-[#0A1931]" /></div>
                                <p className="text-xs text-[#A0AEC0]">Öğrenci tarafında nasıl görüneceğini ayarlayın</p>
                            </div>

                            {/* Sanal Katılımcı */}
                            <div className="p-4 rounded-xl bg-amber-50/60 border border-amber-200/60">
                                <div className="flex items-start gap-3">
                                    <div className="w-8 h-8 rounded-lg bg-amber-100 flex items-center justify-center shrink-0 mt-0.5"><Users size={14} className="text-amber-600" /></div>
                                    <div className="flex-1">
                                        <label className="block text-xs font-bold text-amber-800 mb-1">Sanal Katılımcı Sayısı</label>
                                        <p className="text-[10px] text-amber-600 mb-2">Öğrenci sonuç ekranında gerçek katılımcı sayısı yerine bu değer gösterilir. Sıralama orantılı olarak hesaplanır.</p>
                                        <input type="number" value={f.virtualParticipantCount} onChange={e => upd("virtualParticipantCount", Math.max(0, +e.target.value))} min={0}
                                            className="w-full px-3 py-2 text-sm bg-white border border-amber-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-300/30" placeholder="0 = devre dışı (gerçek sayı gösterilir)" />
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Step 7: Yayınla */}
                    {step === 7 && (
                        <div className="space-y-4">
                            <div className="text-center mb-4">
                                <div className="w-16 h-16 rounded-2xl bg-[#0A1931] flex items-center justify-center mx-auto mb-3 shadow-xl"><Rocket size={28} className="text-white" /></div>
                                <h3 className="text-lg font-bold text-[#0A1931]">Sınav Hazır!</h3>
                                <p className="text-xs text-[#A0AEC0]">Aşağıdaki özeti inceleyin ve yayınlayın</p>
                            </div>
                            <div className="bg-[#E2E8F0]/20 rounded-xl p-4 space-y-2">
                                {[
                                    ["Sınav Adı", f.title],
                                    ["Tip", EXAM_TEMPLATES[f.examType]?.label || f.examType],
                                    ["Soru / Seçenek", `${f.questionCount} soru, ${f.optionCount} seçenek`],
                                    ["Süre", `${f.durationMinutes} dk`],
                                    ["Yanlış D. Götürme", f.wrongPenaltyWeight === 0 ? "Yok" : `${f.wrongPenaltyWeight}`],
                                    ["Puanlama", `${f.baseScore} → ${f.maxScore} puan`],
                                    ["Sınav PDF", f.pdfUrl ? "✅ Yüklendi" : "⚠️ Yüklenmedi"],
                                    ["Çözüm PDF", f.solutionPdfUrl ? "✅ Yüklendi" : "— Opsiyonel"],
                                    ["Tarih", f.startDate && f.endDate ? `${new Date(f.startDate).toLocaleDateString("tr-TR")} → ${new Date(f.endDate).toLocaleDateString("tr-TR")}` : "⚠️ Belirlenmedi"],
                                    ["Sonuç Açıklama", f.resultMode === "immediate" ? "Hemen" : f.resultMode === "scheduled" ? `${new Date(f.resultPublishDate).toLocaleString("tr-TR")}` : "Manuel"],
                                    ["Cevap Anahtarı", `${filledCount}/${f.questionCount} işaretlendi`],
                                    ["Atama", f.assignments.length > 0 ? `${f.assignments.filter(a => a.targetType === "Group").length} grup, ${f.assignments.filter(a => a.targetType === "Course").length} ders, ${f.assignments.filter(a => a.targetType === "User").length} bireysel` : "⚠️ Atanmadı"],
                                    ["Sanal Katılımcı", f.virtualParticipantCount > 0 ? `${f.virtualParticipantCount} kişi` : "Devre dışı"],
                                ].map(([k, v]) => (
                                    <div key={String(k)} className="flex justify-between text-sm py-1"><span className="text-[#A0AEC0]">{k}</span><span className="font-medium text-[#0A1931]">{String(v)}</span></div>
                                ))}
                            </div>
                            <div className="grid grid-cols-2 gap-3">
                                <button onClick={() => onSave({ ...f, status: "Taslak" })}
                                    className="py-3 rounded-xl border-2 border-[#E2E8F0] text-sm font-semibold text-[#1B3B6F] hover:bg-[#E2E8F0]/20 transition-all">
                                    📝 Taslak Olarak Kaydet
                                </button>
                                <button onClick={() => onSave({ ...f, status: "Yayında" })}
                                    className="py-3 rounded-xl bg-[#0A1931] text-sm font-bold text-white hover:bg-[#1B3B6F] transition-all shadow-lg active:scale-[0.98]">
                                    🚀 Yayına Al
                                </button>
                            </div>
                        </div>
                    )}
                </div>

                {/* Footer */}
                {step < 7 && (
                    <div className="flex items-center justify-between px-6 py-4 border-t border-[#E2E8F0]/60 bg-[#E2E8F0]/15 shrink-0">
                        {step > 1 ? <button onClick={() => setStep(s => s - 1)} className="px-4 py-2.5 text-sm font-medium text-[#1B3B6F] hover:text-[#0A1931] transition-colors">← Geri</button> : <div />}
                        <div className="flex items-center gap-2">
                            <span className="text-xs text-[#A0AEC0]">{step}/7</span>
                            <button onClick={() => setStep(s => s + 1)} disabled={!canNext()}
                                className="px-6 py-3 text-sm font-bold text-white bg-[#0A1931] rounded-xl hover:bg-[#1B3B6F] shadow-lg transition-all disabled:opacity-40 disabled:cursor-not-allowed active:scale-[0.98]">
                                Devam →
                            </button>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}

