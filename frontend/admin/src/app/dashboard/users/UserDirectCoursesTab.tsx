"use client";
import { useState, useEffect, useCallback } from "react";
import { createPortal } from "react-dom";
import Link from "next/link";
import { userApi, courseApi, type CourseListDto } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/components/toast";
import { BookOpen, Search, Plus, Trash2, Loader2, X, AlertCircle, Info, Calendar } from "lucide-react";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { Tooltip } from "@/components/ui/Tooltip";

export function UserDirectCoursesTab({ userId, userRole }: { userId: string; userRole?: string }) {
    const { token, currentTenantId: tenantId } = useAuth();
    const { success, error: toastError } = useToast();
    const [courses, setCourses] = useState<(CourseListDto & { expiresAt?: string | null })[]>([]);
    const [loading, setLoading] = useState(true);
    const [removeTarget, setRemoveTarget] = useState<(CourseListDto & { expiresAt?: string | null }) | null>(null);
    const [addModalOpen, setAddModalOpen] = useState(false);
    const [searchQuery, setSearchQuery] = useState("");
    
    // For Add Modal
    const [searchResults, setSearchResults] = useState<CourseListDto[]>([]);
    const [searchLoading, setSearchLoading] = useState(false);
    const [addingCourse, setAddingCourse] = useState<string | null>(null);

    // For Inline Expiration Date
    const [editingExpirationFor, setEditingExpirationFor] = useState<string | null>(null);
    const [expirationInputValue, setExpirationInputValue] = useState("");

    const isInstructor = userRole === "Instructor" || userRole === "Eğitmen";

    const fetchCourses = useCallback(async () => {
        if (!token || !tenantId) return;
        setLoading(true);
        try {
            if (isInstructor) {
                const data = await courseApi.list(token, tenantId, { instructorId: userId, pageSize: 1000 });
                setCourses(data.items);
            } else {
                const data = await userApi.getDirectCourses(token, tenantId, userId);
                setCourses(data);
            }
        } catch {
            toastError("Hata", "Ders listesi alınamadı.");
        } finally {
            setLoading(false);
        }
    }, [token, tenantId, userId, isInstructor]);

    useEffect(() => {
        fetchCourses();
    }, [fetchCourses]);

    const handleRemove = async () => {
        if (!removeTarget || !token || !tenantId) return;
        try {
            if (isInstructor) {
                const currentInstructorIds = removeTarget.instructors?.map(i => i.id) || [];
                const updatedInstructorIds = currentInstructorIds.filter(id => id !== userId);
                await courseApi.update(token, tenantId, removeTarget.id, { instructorIds: updatedInstructorIds });
            } else {
                await courseApi.removeStudent(token, tenantId, removeTarget.id, userId);
            }
            setCourses(c => c.filter(x => x.id !== removeTarget.id));
            success(isInstructor ? "Ders sahipliği kaldırıldı." : "Ders ataması kaldırıldı.");
        } catch {
            toastError("Hata", "İşlem yapılamadı.");
        } finally {
            setRemoveTarget(null);
        }
    };

    const searchSystemCourses = useCallback(async (query: string) => {
        if (!token || !tenantId) return;
        setSearchLoading(true);
        try {
            const data = await courseApi.list(token, tenantId, { search: query, pageSize: 50 });
            const enrolledIds = new Set(courses.map(c => c.id));
            setSearchResults(data.items.filter(c => !enrolledIds.has(c.id)));
        } catch {
            // ignore
        } finally {
            setSearchLoading(false);
        }
    }, [token, tenantId, courses]);

    useEffect(() => {
        if (!addModalOpen) {
            setSearchResults([]);
            return;
        }
        const timer = setTimeout(() => {
            searchSystemCourses(searchQuery);
        }, 500);
        return () => clearTimeout(timer);
    }, [searchQuery, addModalOpen, searchSystemCourses]);

    const handleAdd = async (courseId: string) => {
        if (!token || !tenantId) return;
        setAddingCourse(courseId);
        try {
            if (isInstructor) {
                const targetCourse = searchResults.find(c => c.id === courseId);
                const currentInstructorIds = targetCourse?.instructors?.map(i => i.id) || [];
                const updatedInstructorIds = Array.from(new Set([...currentInstructorIds, userId]));
                await courseApi.update(token, tenantId, courseId, { instructorIds: updatedInstructorIds });
                success("Ders eğitmene atandı.");
            } else {
                await courseApi.assignStudent(token, tenantId, courseId, userId);
                success("Öğrenci derse atandı.");
            }
            setAddModalOpen(false);
            setSearchQuery("");
            fetchCourses();
        } catch {
            toastError("Hata", "Ders eklenemedi.");
        } finally {
            setAddingCourse(null);
        }
    };

    const handleUpdateExpiration = async (courseId: string, expiresAt: string | null) => {
        if (!token || !tenantId) return;
        try {
            await courseApi.updateStudentExpiration(token, tenantId, courseId, userId, expiresAt);
            setCourses(prev => prev.map(c => c.id === courseId ? { ...c, expiresAt } : c));
            success("Erişim bitiş tarihi güncellendi.");
        } catch {
            toastError("Hata", "Tarih güncellenemedi.");
        } finally {
            setEditingExpirationFor(null);
        }
    };

    const [mounted, setMounted] = useState(false);
    useEffect(() => setMounted(true), []);

    return (
        <div className="space-y-6 animate-fade-in">
            <div className="flex items-center justify-between">
                <div>
                    <h3 className="text-lg font-bold text-[#0A1931]">{isInstructor ? "Eğitmenin Dersleri" : "Özel Atanmış Dersler"}</h3>
                    <p className="text-sm text-[#A0AEC0]">{isInstructor ? "Eğitmenin sahip olduğu ve yönettiği dersler." : "Öğrencinin doğrudan dahil edildiği dersler."}</p>
                </div>
                <button onClick={() => setAddModalOpen(true)} className="flex items-center gap-2 px-4 py-2 bg-indigo-50 text-indigo-600 rounded-xl hover:bg-indigo-100 font-bold text-sm transition-colors border border-indigo-100">
                    <Plus size={16} /> Ders Ekle
                </button>
            </div>

            {loading ? (
                <div className="py-12 flex justify-center"><Loader2 className="animate-spin text-[#A0AEC0]" size={32} /></div>
            ) : courses.length === 0 ? (
                <div className="py-16 flex flex-col items-center justify-center text-center bg-[#E2E8F0]/10 rounded-3xl border border-[#E2E8F0]/50 border-dashed">
                    <div className="w-16 h-16 rounded-full bg-[#E2E8F0]/30 flex items-center justify-center mb-4 text-[#A0AEC0]">
                        <BookOpen size={28} />
                    </div>
                    <h4 className="text-lg font-bold text-[#0A1931]">Kayıtlı Ders Yok</h4>
                    <p className="text-sm text-[#A0AEC0] max-w-sm mt-1">{isInstructor ? "Eğitmene atanmış bir ders bulunmuyor." : "Öğrenci henüz özel olarak hiçbir derse atanmamış."}</p>
                </div>
            ) : (
                <div className="border border-[#E2E8F0]/60 rounded-2xl overflow-hidden bg-white shadow-sm">
                    <table className="w-full text-left">
                        <thead className="bg-[#E2E8F0]/20 border-b border-[#E2E8F0]/60">
                            <tr>
                                <th className="px-6 py-4 text-xs font-semibold text-[#A9A9A9]">Ders Adı</th>
                                <th className="px-6 py-4 text-xs font-semibold text-[#A9A9A9]">Tür</th>
                                {!isInstructor && (
                                    <th className="px-6 py-4 text-xs font-semibold text-[#A9A9A9]">
                                        <div className="flex items-center gap-1 group relative w-fit">
                                            Erişim Bitiş Tarihi
                                            <Info size={14} className="text-[#A0AEC0] group-hover:text-indigo-500 cursor-help transition-colors" />
                                            <div className="absolute top-full left-1/2 -translate-x-1/2 mt-2 w-64 p-2 bg-gray-900 text-white text-xs rounded-lg opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all shadow-xl z-50">
                                                Buradan belirleyeceğiniz tarih geçtiğinde, öğrencinin bu derse olan özel erişimi otomatik olarak kesilir.
                                            </div>
                                        </div>
                                    </th>
                                )}
                                <th className="px-6 py-4 text-xs font-semibold text-[#A9A9A9] text-right">İşlem</th>
                            </tr>
                        </thead>
                        <tbody>
                            {courses.map((c) => (
                                <tr key={c.id} className="border-b border-[#E2E8F0]/60 last:border-0 hover:bg-[#E2E8F0]/10">
                                    <td className="px-6 py-4">
                                        <Link 
                                            href={`/dashboard/courses?courseId=${c.id}`}
                                            className="text-sm font-bold text-[#0A1931] hover:text-indigo-600 transition-colors cursor-pointer hover:underline decoration-indigo-400 decoration-2"
                                        >
                                            {c.title}
                                        </Link>
                                    </td>
                                    <td className="px-6 py-4">
                                        <p className="text-xs font-medium text-[#A0AEC0]">{c.courseType}</p>
                                    </td>
                                    {!isInstructor && (
                                        <td className="px-6 py-4">
                                            {editingExpirationFor === c.id ? (
                                                <div className="flex items-center gap-2">
                                                    <input
                                                        type="date"
                                                        value={expirationInputValue}
                                                        onChange={e => setExpirationInputValue(e.target.value)}
                                                        className="px-2 py-1 bg-white border border-[#E2E8F0] rounded-md text-xs focus:outline-none focus:border-indigo-500 text-[#0A1931]"
                                                    />
                                                    <button 
                                                        onClick={() => handleUpdateExpiration(c.id, expirationInputValue ? new Date(expirationInputValue).toISOString() : null)}
                                                        className="px-2 py-1 bg-indigo-500 text-white text-xs font-bold rounded-md hover:bg-indigo-600 transition-colors">
                                                        Kaydet
                                                    </button>
                                                    <button 
                                                        onClick={() => { setEditingExpirationFor(null); }}
                                                        className="px-2 py-1 bg-gray-100 text-gray-500 text-xs font-bold rounded-md hover:bg-gray-200 transition-colors">
                                                        İptal
                                                    </button>
                                                </div>
                                            ) : (
                                                <div 
                                                    onClick={() => {
                                                        setEditingExpirationFor(c.id);
                                                        setExpirationInputValue(c.expiresAt ? new Date(c.expiresAt).toISOString().split('T')[0] : "");
                                                    }}
                                                    className="flex items-center gap-2 px-3 py-1.5 bg-[#F8FAFC] border border-[#E2E8F0]/80 rounded-lg w-fit cursor-pointer hover:border-indigo-300 hover:bg-indigo-50 transition-colors group">
                                                    <Calendar size={14} className={c.expiresAt && new Date(c.expiresAt) < new Date() ? "text-red-500" : "text-indigo-500"} />
                                                    <span className={`text-xs font-bold ${!c.expiresAt ? "text-[#A0AEC0] group-hover:text-indigo-600" : new Date(c.expiresAt) < new Date() ? "text-red-600" : "text-[#0A1931]"}`}>
                                                        {c.expiresAt ? new Date(c.expiresAt).toLocaleDateString("tr-TR") : "Süresiz"}
                                                    </span>
                                                </div>
                                            )}
                                        </td>
                                    )}
                                    <td className="px-6 py-4 text-right">
                                        <Tooltip content="Kaldır"><button onClick={() => setRemoveTarget(c)} className="p-2 text-[#A0AEC0] hover:text-red-500 hover:bg-red-50 rounded-lg transition-all">
                                            <Trash2 size={16} />
                                        </button></Tooltip>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            <ConfirmDialog
                open={!!removeTarget}
                title="Dersi Kaldır"
                message={<>{isInstructor ? "Eğitmenin" : "Öğrencinin"} <span className="font-bold text-[#0A1931]">{removeTarget?.title}</span> dersinden ilişiğini kesmek istediğinize emin misiniz?</>}
                onConfirm={handleRemove}
                onClose={() => setRemoveTarget(null)}
                confirmText="Evet, Kaldır"
                cancelText="İptal"
            />

            {addModalOpen && mounted && createPortal(
                <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
                    <div className="bg-white rounded-[2rem] shadow-2xl w-full max-w-lg flex flex-col overflow-hidden animate-fade-in-up">
                        <div className="flex items-center justify-between p-6 border-b border-[#E2E8F0]/60 bg-[#E2E8F0]/15">
                            <h2 className="text-xl font-black text-[#0A1931]">Ders Seç ve Ata</h2>
                            <button onClick={() => { setAddModalOpen(false); setSearchQuery(""); }} className="p-2 text-[#A0AEC0] hover:text-[#0A1931] rounded-xl"><X size={20} /></button>
                        </div>
                        <div className="p-6">
                            <div className="relative mb-6">
                                <Search size={18} className="absolute left-4 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                                <input
                                    type="text"
                                    placeholder="Ders adı ara..."
                                    value={searchQuery}
                                    onChange={(e) => setSearchQuery(e.target.value)}
                                    className="w-full pl-12 pr-4 py-3 bg-[#E2E8F0]/20 border border-[#E2E8F0]/60 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500 transition-all font-medium"
                                />
                            </div>

                            <div className="space-y-2 max-h-[300px] overflow-y-auto">
                                {searchLoading && <div className="py-4 flex justify-center"><Loader2 className="animate-spin text-[#A0AEC0]" size={20} /></div>}
                                {!searchLoading && searchResults.length === 0 && (
                                    <p className="text-center text-sm text-[#A0AEC0] py-4">Ders bulunamadı.</p>
                                )}
                                {!searchLoading && searchResults.map(c => (
                                    <div key={c.id} className="flex items-center justify-between p-4 rounded-xl border border-[#E2E8F0]/60 hover:border-indigo-200 hover:bg-indigo-50/30 transition-all">
                                        <div>
                                            <p className="text-sm font-bold text-[#0A1931]">{c.title}</p>
                                            <p className="text-xs text-[#A0AEC0]">{c.courseType}</p>
                                        </div>
                                        <button 
                                            onClick={() => handleAdd(c.id)}
                                            disabled={addingCourse === c.id}
                                            className="px-3 py-1.5 bg-indigo-50 text-indigo-600 rounded-lg hover:bg-indigo-100 font-bold text-xs disabled:opacity-50 flex items-center gap-2">
                                            {addingCourse === c.id ? <Loader2 size={12} className="animate-spin" /> : <Plus size={12} />} Ekle
                                        </button>
                                    </div>
                                ))}
                            </div>
                        </div>
                    </div>
                </div>,
                document.body
            )}
        </div>
    );
}
