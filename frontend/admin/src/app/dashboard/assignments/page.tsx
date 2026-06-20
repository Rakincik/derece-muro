"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import {
    Search, CheckCircle, Clock, Star, ChevronRight, X, FileDown,
    ExternalLink, Plus, Edit3, Trash2, Calendar, BookOpen, AlertTriangle,
    Upload, Filter, ArrowUpDown, BarChart3, Users, Award, FileText, Bell
} from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { assignmentApi, courseApi, notificationApi, groupsApi, userApi, uploadApi, type AssignmentListDto, type AssignmentDetailDto, type SubmissionDto, type CourseListDto, type GroupListDto, type UserDto } from "@/lib/api";
import { useToast } from "@/components/toast";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { Tooltip } from "@/components/ui/Tooltip";

// ── Grade Modal ──
function GradeModal({ submission, maxScore, assignmentId, onClose, onGraded }: {
    submission: SubmissionDto; maxScore: number; assignmentId: string;
    onClose: () => void; onGraded: (updated: SubmissionDto) => void;
}) {
    const { token, currentTenantId: tenantId } = useAuth();
    const [score, setScore] = useState(submission.score?.toString() ?? "");
    const [feedback, setFeedback] = useState(submission.feedback ?? "");
    const [saving, setSaving] = useState(false);
    const { success, error: errToast } = useToast();

    const handleSave = async () => {
        if (!token || !tenantId) return;
        const s = Number(score);
        if (isNaN(s) || s < 0 || s > maxScore) { errToast(`Not 0-${maxScore} arasında olmalı.`); return; }
        setSaving(true);
        try {
            const updated = await assignmentApi.gradeSubmission(token, tenantId, assignmentId, submission.id, { score: s, feedback });
            success("Not kaydedildi");
            onGraded(updated);
        } catch { errToast("Not kaydedilemedi"); }
        finally { setSaving(false); }
    };

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm" onClick={onClose}>
            <div className="w-full max-w-md bg-white rounded-2xl shadow-2xl animate-fade-in" onClick={e => e.stopPropagation()}>
                <div className="flex items-center justify-between px-6 py-4 border-b border-[#E2E8F0]">
                    <h2 className="font-bold text-[#0A1931]">Teslimi Notlandır</h2>
                    <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/40 text-[#A0AEC0]"><X size={16} /></button>
                </div>
                <div className="p-6 space-y-4">
                    <div className="p-3 rounded-xl bg-[#E2E8F0]/20 border border-[#E2E8F0]">
                        <p className="text-sm font-medium text-[#0A1931]">{submission.userFullName}</p>
                        <p className="text-xs text-[#A0AEC0] mt-0.5">{new Date(submission.submittedAt).toLocaleString("tr-TR")}</p>
                        {submission.comment && <p className="text-xs text-[#1B3B6F] mt-2 italic">&ldquo;{submission.comment}&rdquo;</p>}
                        {submission.fileUrl && (
                            <a href={submission.fileUrl} target="_blank" className="flex items-center gap-1 text-xs text-[#1B3B6F] hover:underline mt-2">
                                <ExternalLink size={11} /> Dosyayı görüntüle
                            </a>
                        )}
                    </div>
                    <div>
                        <label className="text-xs font-medium text-[#1B3B6F] mb-1 block">Not (max {maxScore})</label>
                        <input type="number" min={0} max={maxScore} value={score} onChange={e => setScore(e.target.value)}
                            className="w-full px-3 py-2 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20"
                            placeholder={`0 – ${maxScore}`} />
                    </div>
                    <div>
                        <label className="text-xs font-medium text-[#1B3B6F] mb-1 block">Geri Bildirim (isteğe bağlı)</label>
                        <textarea value={feedback} onChange={e => setFeedback(e.target.value)} rows={3} placeholder="Öğrenciye geri bildirim..."
                            className="w-full px-3 py-2 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 resize-none" />
                    </div>
                </div>
                <div className="flex gap-3 px-6 py-4 border-t border-[#E2E8F0] bg-[#E2E8F0]/15 rounded-b-2xl">
                    <button onClick={handleSave} disabled={saving || !score}
                        className="flex-1 py-2.5 bg-[#0A1931] hover:bg-[#1B3B6F] text-white text-sm font-bold rounded-xl disabled:opacity-50 transition-all">
                        {saving ? "Kaydediliyor..." : "Kaydet ✓"}
                    </button>
                    <button onClick={onClose} className="px-4 py-2.5 text-sm text-[#A9A9A9] hover:text-[#0A1931] transition-colors">İptal</button>
                </div>
            </div>
        </div>
    );
}

// ── Assignment Detail Panel ──
function AssignmentDetailPanel({ assignment, onClose, onDelete }: {
    assignment: AssignmentListDto; onClose: () => void; onDelete: (id: string) => void;
}) {
    const { token, currentTenantId: tenantId } = useAuth();
    const [detail, setDetail] = useState<AssignmentDetailDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [grading, setGrading] = useState<SubmissionDto | null>(null);

    useEffect(() => {
        if (!token || !tenantId) return;
        assignmentApi.getById(token, tenantId, assignment.id)
            .then(setDetail).catch(console.error).finally(() => setLoading(false));
    }, [token, tenantId, assignment.id]);

    const handleGraded = (updated: SubmissionDto) => {
        setDetail(prev => prev ? { ...prev, submissions: prev.submissions.map(s => s.id === updated.id ? updated : s) } : prev);
        setGrading(null);
    };

    const submissions = detail?.submissions ?? [];
    const graded = submissions.filter(s => s.score !== null).length;
    const avgScore = graded > 0 ? (submissions.reduce((acc, s) => acc + (s.score ?? 0), 0) / graded).toFixed(1) : null;
    const daysUntil = Math.ceil((new Date(assignment.dueDate).getTime() - Date.now()) / 86400000);
    const isOverdue = daysUntil < 0;

    return (
        <div className="fixed inset-y-0 right-0 z-40 w-[540px] bg-white shadow-2xl border-l border-[#E2E8F0] overflow-y-auto">
            <div className="sticky top-0 bg-white z-10 px-6 py-4 border-b border-[#E2E8F0]">
                <div className="flex items-center justify-between">
                    <h2 className="font-bold text-[#0A1931] truncate pr-4">{assignment.title}</h2>
                    <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/40 text-[#A0AEC0] flex-shrink-0"><X size={16} /></button>
                </div>
                <div className="flex items-center gap-2 mt-2">
                    <span className="text-xs text-[#A0AEC0]">{assignment.courseName}</span>
                    <span className="text-[#E2E8F0]">·</span>
                    <span className={`text-xs font-medium ${isOverdue ? "text-red-500" : daysUntil <= 3 ? "text-amber-500" : "text-[#A0AEC0]"}`}>
                        {isOverdue ? `${Math.abs(daysUntil)} gün gecikmiş` : daysUntil === 0 ? "Bugün son gün!" : `${daysUntil} gün kaldı`}
                    </span>
                    <span className="text-[#E2E8F0]">·</span>
                    <span className="text-xs text-[#A0AEC0]">Max: {assignment.maxScore} puan</span>
                </div>
            </div>

            {/* Stats */}
            <div className="px-6 py-4 border-b border-[#E2E8F0] grid grid-cols-3 gap-3">
                {[
                    { label: "Teslim", value: `${submissions.length}`, icon: Upload, color: "text-[#1B3B6F]" },
                    { label: "Notlandırılan", value: `${graded}`, icon: CheckCircle, color: "text-emerald-500" },
                    { label: "Ortalama", value: avgScore ?? "—", icon: BarChart3, color: "text-amber-500" },
                ].map(s => (
                    <div key={s.label} className="text-center p-3 rounded-xl bg-[#E2E8F0]/15 border border-[#E2E8F0]/60">
                        <s.icon size={16} className={`mx-auto mb-1 ${s.color}`} />
                        <p className="text-lg font-bold text-[#0A1931]">{s.value}</p>
                        <p className="text-[10px] text-[#A0AEC0] uppercase tracking-wider font-semibold">{s.label}</p>
                    </div>
                ))}
            </div>

            {/* Submissions */}
            <div className="px-6 py-5">
                <h3 className="text-sm font-semibold text-[#0A1931] mb-3 flex items-center gap-2">
                    <Users size={14} className="text-[#A0AEC0]" /> Teslimler ({submissions.length})
                </h3>
                {loading ? (
                    <div className="space-y-2">{[...Array(3)].map((_, i) => <div key={i} className="h-16 rounded-xl bg-[#E2E8F0]/40 animate-pulse" />)}</div>
                ) : submissions.length === 0 ? (
                    <div className="py-10 text-center">
                        <Upload size={32} className="mx-auto text-[#A0AEC0] opacity-30 mb-2" />
                        <p className="text-sm text-[#A0AEC0]">Henüz teslim yok</p>
                    </div>
                ) : (
                    <div className="space-y-2">
                        {submissions.map(sub => (
                            <div key={sub.id} className="p-3 rounded-xl border border-[#E2E8F0] hover:bg-[#E2E8F0]/15 transition-colors group">
                                <div className="flex items-center gap-3">
                                    <div className="w-9 h-9 rounded-xl bg-[#0A1931] flex items-center justify-center text-white text-[10px] font-bold flex-shrink-0">
                                        {(sub.userFullName || "Bilinmeyen Kullanıcı").split(" ").map(n => n[0]).join("").slice(0, 2)}
                                    </div>
                                    <div className="flex-1 min-w-0">
                                        <p className="text-sm font-medium text-[#0A1931]">{sub.userFullName || "Bilinmeyen Kullanıcı"}</p>
                                        <p className="text-xs text-[#A0AEC0]">{new Date(sub.submittedAt).toLocaleDateString("tr-TR")} {new Date(sub.submittedAt).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}</p>
                                    </div>
                                    {sub.fileUrl && (
                                        <a href={sub.fileUrl} target="_blank" className="p-1.5 rounded-lg hover:bg-[#E2E8F0]/30 text-[#A0AEC0] hover:text-[#1B3B6F]">
                                            <FileDown size={14} />
                                        </a>
                                    )}
                                    {sub.score != null ? (
                                        <span className={`text-xs font-bold px-2 py-1 rounded-lg ${sub.score / assignment.maxScore >= 0.7 ? "bg-emerald-50 text-emerald-700" : sub.score / assignment.maxScore >= 0.5 ? "bg-amber-50 text-amber-700" : "bg-red-50 text-red-700"}`}>
                                            {sub.score}/{assignment.maxScore}
                                        </span>
                                    ) : (
                                        <button onClick={() => setGrading(sub)}
                                            className="text-xs text-emerald-600 font-semibold hover:text-emerald-800 px-2 py-1 rounded-lg hover:bg-emerald-50 transition-colors">
                                            Notla →
                                        </button>
                                    )}
                                </div>
                                {sub.comment && <p className="text-xs text-[#A9A9A9] mt-2 pl-12 italic">&ldquo;{sub.comment}&rdquo;</p>}
                                {sub.feedback && <p className="text-xs text-emerald-600 mt-1 pl-12 italic">📝 {sub.feedback}</p>}
                            </div>
                        ))}
                    </div>
                )}
            </div>
            {grading && detail && (
                <GradeModal submission={grading} maxScore={detail.maxScore} assignmentId={assignment.id}
                    onClose={() => setGrading(null)} onGraded={handleGraded} />
            )}
        </div>
    );
}

// ── Create/Edit Assignment Modal ──
function AssignmentFormModal({ assignment, courses, onClose, onSaved }: {
    assignment: AssignmentListDto | null;
    courses: { id: string; title: string }[];
    onClose: () => void;
    onSaved: () => void;
}) {
    const { token, currentTenantId: tenantId } = useAuth();
    const { success, error: errToast } = useToast();
    const isEdit = !!assignment;

    const [form, setForm] = useState({
        title: assignment?.title ?? "",
        description: "",
        courseId: "",
        dueDate: assignment?.dueDate ? new Date(assignment.dueDate).toISOString().slice(0, 16) : "",
        maxScore: "100",
        fileUrl: "",
    });
    const [saving, setSaving] = useState(false);
    const [selectedFile, setSelectedFile] = useState<File | null>(null);
    const [dragOver, setDragOver] = useState(false);
    const [sendNotif, setSendNotif] = useState(true);
    const [notifMsg, setNotifMsg] = useState("");

    // Load detail for edit
    useEffect(() => {
        if (!isEdit || !token || !tenantId || !assignment) return;
        assignmentApi.getById(token, tenantId, assignment.id).then(d => {
            setForm({
                title: d.title,
                description: d.description ?? "",
                courseId: d.courseId,
                dueDate: new Date(d.dueDate).toISOString().slice(0, 16),
                maxScore: String(d.maxScore),
                fileUrl: d.fileUrl ?? "",
            });
        });
    }, [isEdit, assignment, token, tenantId]);

    const u = (k: string, v: string) => setForm(p => ({ ...p, [k]: v }));

    const handleFile = (files: FileList | null) => {
        if (files && files.length > 0) setSelectedFile(files[0]);
    };

    const handleSave = async () => {
        if (!token || !tenantId) return;
        if (!form.title.trim()) { errToast("Başlık zorunlu"); return; }
        if (!form.courseId && !isEdit) { errToast("Ders seçilmeli"); return; }
        if (!form.dueDate) { errToast("Son tarih zorunlu"); return; }

        setSaving(true);
        try {
            let finalFileUrl = form.fileUrl.trim() || undefined;

            if (selectedFile) {
                const preRes = await uploadApi.getPresignedUrl(token, tenantId, selectedFile.name, selectedFile.type || "application/octet-stream");
                const uploadReq = await fetch(preRes.uploadUrl, { method: "PUT", body: selectedFile, headers: { "Content-Type": selectedFile.type || "application/octet-stream" } });
                if (!uploadReq.ok) throw new Error("Dosya yüklenemedi");
                finalFileUrl = preRes.publicUrl;
            }

            const data = {
                title: form.title.trim(),
                description: form.description.trim() || undefined,
                courseId: form.courseId,
                dueDate: new Date(form.dueDate).toISOString(),
                maxScore: 100,
                fileUrl: finalFileUrl,
            };
            if (isEdit && assignment) {
                await assignmentApi.update(token, tenantId, assignment.id, data);
                success("Ödev güncellendi");
            } else {
                await assignmentApi.create(token, tenantId, data as any);
                success("Ödev oluşturuldu");
            }
            // Send notification
            if (sendNotif) {
                try {
                    const title = "📝 Yeni Ödev";
                    const body = notifMsg || `"${form.title.trim()}" ödevi yayınlandı. Son teslim: ${new Date(form.dueDate).toLocaleDateString("tr-TR")}`;
                    await notificationApi.bulkSend(token, tenantId, [], title, body, "assignment", undefined, undefined, false, form.courseId);
                    success("Bildirim gönderildi");
                } catch { /* notification fail is non-blocking */ }
            }
            onSaved();
        } catch (e) {
            errToast("Kayıt başarısız");
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4" onClick={onClose}>
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
            <div className="relative w-full max-w-2xl bg-white rounded-2xl shadow-2xl overflow-hidden" onClick={e => e.stopPropagation()}>
                {/* Gradient Header */}
                <div className="relative overflow-hidden px-8 py-5 bg-gradient-to-r from-[#0A1931]/5 to-[#1B3B6F]/5">
                    <div className="absolute -right-6 -top-6 w-32 h-32 rounded-full bg-[#1B3B6F] opacity-[0.04]" />
                    <div className="flex items-center justify-between relative z-10">
                        <div className="flex items-center gap-3">
                            <div className="w-12 h-12 rounded-xl bg-[#1B3B6F]/10 flex items-center justify-center">
                                <FileText size={20} className="text-[#1B3B6F]" />
                            </div>
                            <div>
                                <h2 className="text-xl font-bold text-[#0A1931]">{isEdit ? "Ödevi Düzenle" : "Yeni Ödev"}</h2>
                                <p className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest">Ödev Yönetimi</p>
                            </div>
                        </div>
                        <button onClick={onClose} className="p-2.5 rounded-xl bg-white/80 text-[#A0AEC0] hover:text-[#0A1931] shadow-sm"><X size={18} /></button>
                    </div>
                </div>

                <div className="p-8 space-y-6 max-h-[75vh] overflow-y-auto custom-scrollbar">
                    {/* Title */}
                    <div>
                        <label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Ödev Başlığı *</label>
                        <input type="text" value={form.title} onChange={e => u("title", e.target.value)}
                            className="w-full px-4 py-3 text-sm font-medium bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:bg-white text-[#0A1931] placeholder:text-[#A0AEC0]"
                            placeholder="Örn: Matematik Ödev 5 — Türev Uygulamaları" />
                    </div>

                    {/* Course + Due Date */}
                    <div className="grid grid-cols-2 gap-5">
                        <div>
                            <label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Hedef Ders *</label>
                            <select value={form.courseId} onChange={e => u("courseId", e.target.value)}
                                className={`w-full px-4 py-3 text-sm font-medium bg-[#E2E8F0]/10 border rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 text-[#0A1931] ${!form.courseId && !isEdit ? 'border-red-400 ring-1 ring-red-400/50' : 'border-[#E2E8F0]'}`}>
                                <option value="">-- Ders Seç --</option>
                                {courses.map(c => <option key={c.id} value={c.id}>{c.title}</option>)}
                            </select>
                        </div>
                        <div>
                            <label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Son Tarih *</label>
                            <input type="datetime-local" value={form.dueDate} onChange={e => u("dueDate", e.target.value)}
                                className="w-full px-4 py-3 text-sm font-medium bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 text-[#0A1931]" />
                        </div>
                    </div>

                    {/* Document Upload */}
                    <div>
                        <label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Doküman (Opsiyonel)</label>
                        <div className={`relative border-2 border-dashed rounded-xl p-6 text-center transition-all cursor-pointer ${dragOver ? "border-[#1B3B6F] bg-[#1B3B6F]/5" : "border-[#E2E8F0] bg-[#E2E8F0]/10 hover:border-[#A0AEC0]"}`}
                            onDragOver={e => { e.preventDefault(); setDragOver(true); }}
                            onDragLeave={() => setDragOver(false)}
                            onDrop={e => { e.preventDefault(); setDragOver(false); handleFile(e.dataTransfer.files); }}
                            onClick={() => document.getElementById("doc-input")?.click()}>
                            <input id="doc-input" type="file" accept=".pdf,.doc,.docx,.ppt,.pptx,.xls,.xlsx,.txt,.zip" className="hidden"
                                onChange={e => handleFile(e.target.files)} />
                            {selectedFile ? (
                                <div className="flex items-center justify-center gap-3">
                                    <FileText size={20} className="text-[#1B3B6F]" />
                                    <span className="text-base font-bold text-[#0A1931]">{selectedFile.name}</span>
                                    <span className="text-xs text-[#A0AEC0]">({(selectedFile.size / 1024).toFixed(0)} KB)</span>
                                    <button type="button" onClick={e => { e.stopPropagation(); setSelectedFile(null); }} className="p-1.5 ml-2 rounded-lg bg-red-50 text-red-500 hover:bg-red-100"><X size={16} /></button>
                                </div>
                            ) : form.fileUrl ? (
                                <div className="flex flex-col items-center justify-center">
                                    <div className="flex items-center gap-2 mb-1">
                                        <FileDown size={20} className="text-emerald-500" />
                                        <span className="text-sm font-bold text-emerald-600">Mevcut Bir Dosya Yüklü</span>
                                    </div>
                                    <p className="text-xs text-[#A0AEC0] max-w-xs truncate">{form.fileUrl}</p>
                                    <p className="text-[10px] font-bold text-[#1B3B6F] mt-2 underline">Değiştirmek için yeni dosya seçin veya sürükleyin</p>
                                </div>
                            ) : (
                                <div>
                                    <Upload size={24} className="mx-auto text-[#A0AEC0] mb-2" />
                                    <p className="text-sm font-medium text-[#A0AEC0]">Dosya sürükleyin veya <span className="text-[#1B3B6F] font-bold">tıklayın</span></p>
                                    <p className="text-xs text-[#A0AEC0] mt-1">PDF, Word, PowerPoint, Excel, ZIP</p>
                                </div>
                            )}
                        </div>
                    </div>

                    {/* URL alternative */}
                    <div>
                        <label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">veya Dosya URL</label>
                        <input type="url" value={form.fileUrl} onChange={e => u("fileUrl", e.target.value)}
                            className="w-full px-4 py-3 text-sm bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 text-[#0A1931] placeholder:text-[#A0AEC0]"
                            placeholder="https://drive.google.com/..." />
                    </div>

                    {/* Notification Toggle */}
                    <div className="p-5 rounded-xl border border-[#E2E8F0] bg-[#E2E8F0]/5">
                        <div className="flex items-center justify-between">
                            <div className="flex items-center gap-3">
                                <Bell size={20} className={sendNotif ? "text-[#1B3B6F]" : "text-[#A0AEC0]"} />
                                <div>
                                    <p className="text-sm font-bold text-[#0A1931]">Bildirim Gönder</p>
                                    <p className="text-xs text-[#A0AEC0]">Öğrencilere ödev bildirimi gönderilsin</p>
                                </div>
                            </div>
                            <button type="button" onClick={() => setSendNotif(!sendNotif)}
                                className={`w-12 h-6 rounded-full transition-all relative ${sendNotif ? "bg-[#0A1931]" : "bg-[#E2E8F0]"}`}>
                                <div className={`absolute top-0.5 w-5 h-5 rounded-full bg-white shadow-sm transition-all ${sendNotif ? "left-[26px]" : "left-0.5"}`} />
                            </button>
                        </div>
                        {sendNotif && (
                            <div className="mt-4">
                                <input value={notifMsg} onChange={e => setNotifMsg(e.target.value)}
                                    placeholder="Özel mesaj (boş bırakırsanız otomatik oluşturulur)"
                                    className="w-full px-4 py-3 text-sm bg-white border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 text-[#0A1931] placeholder:text-[#A0AEC0]" />
                            </div>
                        )}
                    </div>

                    {/* Description */}
                    <div>
                        <label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Açıklama</label>
                        <textarea value={form.description} onChange={e => u("description", e.target.value)} rows={3}
                            className="w-full px-4 py-3 text-sm bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 resize-none text-[#0A1931] placeholder:text-[#A0AEC0]"
                            placeholder="Ödev hakkında kısa açıklama..." />
                    </div>
                </div>

                <div className="flex items-center justify-between px-8 py-5 border-t border-[#E2E8F0] bg-[#E2E8F0]/10 rounded-b-2xl">
                    <p className="text-xs font-medium text-[#A0AEC0]">{form.title.trim() && form.dueDate ? "✓ Kaydetmeye Hazır" : "Zorunlu alanları doldurun"}</p>
                    <div className="flex gap-3">
                        <button onClick={onClose} className="px-5 py-2.5 text-sm font-bold text-[#A9A9A9] hover:text-[#0A1931]">İptal</button>
                        <button onClick={handleSave} disabled={saving}
                            className="px-8 py-2.5 text-sm font-bold text-white bg-[#0A1931] rounded-xl hover:bg-[#1B3B6F] shadow-lg shadow-black/10 disabled:opacity-50 flex items-center gap-2">
                            {saving ? "Kaydediliyor..." : isEdit ? "Güncelle" : "Oluştur"}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}

// ── Main Page ──
export default function AssignmentsPage() {
    const { token, currentTenantId: tenantId } = useAuth();
    const { success, error: errToast } = useToast();
    const [assignments, setAssignments] = useState<AssignmentListDto[]>([]);
    const [courses, setCourses] = useState<{ id: string; title: string }[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState("");
    const [courseFilter, setCourseFilter] = useState("all");
    const [statusFilter, setStatusFilter] = useState("all");
    const [selected, setSelected] = useState<AssignmentListDto | null>(null);

    // Modals
    const [showForm, setShowForm] = useState(false);
    const [editTarget, setEditTarget] = useState<AssignmentListDto | null>(null);
    const [deleteTarget, setDeleteTarget] = useState<string | null>(null);

    const load = useCallback(() => {
        if (!token || !tenantId) return;
        setLoading(true);
        Promise.all([
            assignmentApi.list(token, tenantId, { pageSize: 100 }).catch(() => ({ items: [], totalCount: 0 })),
            courseApi.list(token, tenantId, { pageSize: 200 }).catch(() => ({ items: [], totalCount: 0 })),
        ]).then(([aRes, cRes]) => {
            const aList = Array.isArray(aRes) ? aRes : (aRes?.items || []);
            const cList = Array.isArray(cRes) ? cRes : (cRes?.items || []);
            setAssignments(aList);
            setCourses(cList.map((c: any) => ({ id: c.id, title: c.title })));
        }).catch(console.error).finally(() => setLoading(false));
    }, [token, tenantId]);

    useEffect(() => { load(); }, [token, tenantId]); // eslint-disable-line react-hooks/exhaustive-deps

    // Filtering
    const getStatus = (a: AssignmentListDto) => {
        const daysUntil = Math.ceil((new Date(a.dueDate).getTime() - Date.now()) / 86400000);
        if (daysUntil < 0) return "overdue";
        if (a.gradedCount > 0 && a.gradedCount >= a.submissionCount && a.submissionCount > 0) return "completed";
        if (a.submissionCount > a.gradedCount) return "pending_grade";
        if (daysUntil <= 3) return "urgent";
        return "active";
    };

    const filtered = useMemo(() => {
        return (assignments || []).filter(a => {
            const ms = !search || 
                (a.title || "").toLowerCase().includes(search.toLowerCase()) || 
                (a.courseName || "").toLowerCase().includes(search.toLowerCase());
            const mc = courseFilter === "all" || a.courseId === courseFilter;
            const mst = statusFilter === "all" || getStatus(a) === statusFilter;
            return ms && mc && mst;
        });
    }, [assignments, search, courseFilter, statusFilter]);

    const safeAssignments = assignments || [];
    const totalSubmissions = safeAssignments.reduce((s, a) => s + (a.submissionCount || 0), 0);
    const totalGraded = safeAssignments.reduce((s, a) => s + (a.gradedCount || 0), 0);
    const pending = totalSubmissions - totalGraded;
    const overdue = safeAssignments.filter(a => getStatus(a) === "overdue").length;

    const handleDelete = async (id: string) => {
        if (!token || !tenantId) return;
        try {
            await assignmentApi.delete(token, tenantId, id);
            success("Ödev silindi");
            load();
        } catch { errToast("Silme başarısız"); }
        setDeleteTarget(null);
    };

    const statusBadge = (a: AssignmentListDto) => {
        const status = getStatus(a);
        const daysUntil = Math.ceil((new Date(a.dueDate).getTime() - Date.now()) / 86400000);
        switch (status) {
            case "overdue": return <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-red-50 text-red-600 border border-red-200">⏰ Süresi Geçti</span>;
            case "urgent": return <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-amber-50 text-amber-600 border border-amber-200">⚡ {daysUntil} gün kaldı</span>;
            case "completed": return <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-600 border border-emerald-200">✅ Tamamlandı</span>;
            case "pending_grade": return <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-blue-50 text-blue-600 border border-blue-200">📝 {a.submissionCount - a.gradedCount} bekliyor</span>;
            default: return <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-[#E2E8F0]/40 text-[#A9A9A9] border border-[#E2E8F0]">Aktif</span>;
        }
    };

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold text-[#0A1931] tracking-tight">Ödevler</h1>
                    <p className="text-sm font-semibold uppercase tracking-widest text-[#A9A9A9] mt-1 opacity-60">Ödev ve Teslimat Yönetimi</p>
                </div>
                <button onClick={() => { setEditTarget(null); setShowForm(true); }}
                    className="w-full sm:w-auto px-6 py-3 text-sm font-bold bg-[#0A1931] text-white rounded-xl hover:bg-[#1B3B6F] transition-all flex items-center justify-center gap-2 shadow-lg shadow-black/10">
                    <Plus size={18} /> Yeni Ödev
                </button>
            </div>

            {/* Stats */}
            <div className="flex lg:grid lg:grid-cols-4 gap-4 overflow-x-auto hide-scrollbar pb-2 snap-x">
                {[
                    { label: "Toplam Ödev", value: assignments.length, icon: FileText, color: "text-[#1B3B6F]", bg: "bg-[#E2E8F0]/15" },
                    { label: "Toplam Teslim", value: totalSubmissions, icon: Upload, color: "text-blue-600", bg: "bg-blue-50" },
                    { label: "Notlandırılan", value: totalGraded, icon: CheckCircle, color: "text-emerald-600", bg: "bg-emerald-50" },
                    { label: "Bekleyen", value: pending, icon: Clock, color: pending > 0 ? "text-amber-600" : "text-emerald-600", bg: pending > 0 ? "bg-amber-50" : "bg-emerald-50" },
                ].map(s => (
                    <div key={s.label} className="min-w-[160px] lg:min-w-0 shrink-0 snap-start bg-white rounded-2xl border border-[#E2E8F0] p-4 lg:p-5 flex items-center gap-3 lg:gap-4 group hover:shadow-md transition-all">
                        <div className={`w-12 h-12 rounded-xl ${s.bg} flex items-center justify-center`}>
                            <s.icon size={20} className={s.color} />
                        </div>
                        <div>
                            <p className="text-2xl font-bold text-[#0A1931] tracking-tight">{s.value}</p>
                            <p className="text-[10px] font-bold uppercase tracking-widest text-[#A0AEC0] mt-0.5">{s.label}</p>
                        </div>
                    </div>
                ))}
            </div>

            {/* Overdue Alert */}
            {overdue > 0 && (
                <div className="flex items-center gap-3 p-4 bg-red-50 border border-red-200 rounded-xl">
                    <AlertTriangle size={18} className="text-red-500 flex-shrink-0" />
                    <p className="text-sm text-red-700 font-medium">
                        <strong>{overdue} ödevin</strong> son teslim tarihi geçmiş! Öğrencileri bilgilendirmeyi unutmayın.
                    </p>
                </div>
            )}

            {/* Search + Filters */}
            <div className="bg-white rounded-2xl border border-[#E2E8F0] p-4 flex flex-col md:flex-row items-stretch md:items-center gap-3">
                <div className="flex-1 relative">
                    <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                    <input type="text" placeholder="Ödev veya ders ara..." value={search} onChange={e => setSearch(e.target.value)}
                        className="w-full pl-9 pr-4 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:border-[#A0AEC0]" />
                </div>
                <select value={courseFilter} onChange={e => setCourseFilter(e.target.value)}
                    className="px-4 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:border-[#A0AEC0]">
                    <option value="all">Tüm Dersler</option>
                    {courses.map(c => <option key={c.id} value={c.id}>{c.title}</option>)}
                </select>
                <select value={statusFilter} onChange={e => setStatusFilter(e.target.value)}
                    className="px-4 py-2.5 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:border-[#A0AEC0]">
                    <option value="all">Tüm Durum</option>
                    <option value="active">Aktif</option>
                    <option value="urgent">Son 3 Gün</option>
                    <option value="overdue">Süresi Geçmiş</option>
                    <option value="pending_grade">Not Bekleyen</option>
                    <option value="completed">Tamamlanan</option>
                </select>
            </div>

            {/* Assignment List */}
            <div className="space-y-2">
                {loading ? [...Array(5)].map((_, i) => <div key={i} className="h-24 rounded-xl bg-[#E2E8F0]/40 animate-pulse" />) :
                    filtered.length === 0 ? (
                        <div className="bg-white rounded-2xl border border-[#E2E8F0] py-16 text-center">
                            <FileText size={40} className="mx-auto text-[#A0AEC0] opacity-30 mb-3" />
                            <p className="text-sm text-[#A0AEC0]">Ödev bulunamadı</p>
                            <button onClick={() => { setEditTarget(null); setShowForm(true); }}
                                className="mt-4 px-5 py-2 text-xs font-bold text-[#1B3B6F] bg-[#E2E8F0]/30 rounded-xl hover:bg-[#E2E8F0]/50 transition-all">
                                + Yeni Ödev Oluştur
                            </button>
                        </div>
                    ) : filtered.map(a => {
                        const daysUntil = Math.ceil((new Date(a.dueDate).getTime() - Date.now()) / 86400000);
                        const isOverdue = daysUntil < 0;
                        return (
                            <div key={a.id}
                                className={`bg-white rounded-xl border p-5 hover:shadow-md transition-all group cursor-pointer ${isOverdue ? "border-red-200" : "border-[#E2E8F0]"}`}
                                onClick={() => setSelected(a)}>
                                <div className="flex flex-col sm:flex-row sm:items-center gap-4">
                                    <div className="flex items-center gap-3 w-full sm:w-auto flex-1 min-w-0">
                                        <div className={`w-11 h-11 rounded-xl flex items-center justify-center flex-shrink-0 ${isOverdue ? "bg-red-50" : "bg-[#E2E8F0]/20"}`}>
                                            <FileText size={18} className={isOverdue ? "text-red-400" : "text-[#1B3B6F]"} />
                                        </div>
                                        <div className="flex-1 min-w-0">
                                            <div className="flex flex-col sm:flex-row sm:items-center gap-1 sm:gap-2 mb-1 sm:mb-0.5">
                                                <h3 className="text-sm font-bold text-[#0A1931]">{a.title}</h3>
                                                <div className="self-start">{statusBadge(a)}</div>
                                            </div>
                                            <div className="flex flex-wrap items-center gap-y-1 gap-x-3 text-xs text-[#A0AEC0]">
                                                <span className="flex items-center gap-1"><BookOpen size={11} /> {a.courseName}</span>
                                                <span className="flex items-center gap-1"><Calendar size={11} /> {new Date(a.dueDate).toLocaleDateString("tr-TR")}</span>
                                                <span className="flex items-center gap-1"><Award size={11} /> Max {a.maxScore}</span>
                                            </div>
                                        </div>
                                    </div>
                                    <div className="flex items-center justify-between sm:justify-end gap-4 w-full sm:w-auto mt-2 sm:mt-0 pt-3 sm:pt-0 border-t sm:border-0 border-[#E2E8F0]">
                                        <div className="flex-1 sm:w-32 min-w-[120px]">
                                            <div className="flex justify-between mb-1">
                                                <span className="text-[11px] text-[#A0AEC0]">Teslim: {a.submissionCount || 0} | Okunan: {a.gradedCount || 0}</span>
                                                {typeof a.averageScore === 'number' && <span className="text-[11px] font-semibold text-[#1B3B6F]">Ort. {a.averageScore.toFixed(1)}</span>}
                                            </div>
                                            <div className="h-1.5 bg-[#E2E8F0]/40 rounded-full overflow-hidden">
                                                <div className="h-full bg-emerald-500 rounded-full transition-all" style={{ width: `${(a.gradedCount || 0) > 0 && (a.submissionCount || 0) > 0 ? ((a.gradedCount || 0) / (a.submissionCount || 0)) * 100 : 0}%` }} />
                                            </div>
                                        </div>
                                        <div className="flex items-center gap-1 flex-shrink-0" onClick={e => e.stopPropagation()}>
                                            <Tooltip content="Düzenle"><button onClick={() => { setEditTarget(a); setShowForm(true); }}
                                                className="p-1.5 rounded-lg hover:bg-amber-50 text-[#A0AEC0] hover:text-amber-600"><Edit3 size={15} /></button></Tooltip>
                                            <Tooltip content="Sil"><button onClick={() => setDeleteTarget(a.id)}
                                                className="p-1.5 rounded-lg hover:bg-red-50 text-[#A0AEC0] hover:text-red-600"><Trash2 size={15} /></button></Tooltip>
                                            <ChevronRight size={16} className="text-[#A0AEC0] group-hover:text-[#1B3B6F] transition-colors ml-1 hidden sm:block" />
                                        </div>
                                    </div>
                                </div>
                            </div>
                        );
                    })}
            </div>

            {/* Detail panel */}
            {selected && <AssignmentDetailPanel assignment={selected} onClose={() => { setSelected(null); load(); }} onDelete={id => { setSelected(null); setDeleteTarget(id); }} />}

            {/* Create/Edit modal */}
            {showForm && <AssignmentFormModal assignment={editTarget} courses={courses} onClose={() => { setShowForm(false); setEditTarget(null); }} onSaved={() => { setShowForm(false); setEditTarget(null); load(); }} />}

            {/* Delete confirm */}
            <ConfirmDialog open={deleteTarget !== null} onClose={() => setDeleteTarget(null)} onConfirm={() => deleteTarget && handleDelete(deleteTarget)}
                title="Ödevi Sil" message="Bu ödev ve tüm teslimleri kalıcı olarak silinecek. Emin misiniz?" />
        </div>
    );
}
