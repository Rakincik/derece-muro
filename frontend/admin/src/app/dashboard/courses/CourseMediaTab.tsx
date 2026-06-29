import { useState, useEffect, useRef, useMemo } from "react";
import { createPortal } from "react-dom";
import { API_URL } from "@/lib/api/core";
import { mediaLibraryApi, courseApi, type CourseMediaDto } from "@/lib/api";
import { GripVertical, Plus, Trash2, Video, Play, Users, Check, X, Edit2, Loader2, FileText, ExternalLink } from "lucide-react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/components/toast";
import { LibrarySelectorModal } from "@/components/ui/LibrarySelectorModal";
import { ExamSelectorModal } from "@/components/ui/ExamSelectorModal";
import { Tooltip } from "@/components/ui/Tooltip";
import { CustomSelect } from "@/components/ui/CustomSelect";

export function getVideoPlaybackDetails(url: string) {
    if (!url) return { url: "", type: "video" as const };
    
    // YouTube
    const ytRegex = /(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^"&?\/\s]{11})/;
    const ytMatch = url.match(ytRegex);
    if (ytMatch && ytMatch[1]) {
        return { url: `https://www.youtube.com/embed/${ytMatch[1]}`, type: "iframe" as const };
    }
    
    // Vimeo
    const vimeoRegex = /vimeo\.com\/(?:channels\/(?:\w+\/)?|groups\/(?:[^\/]*)\/videos\/|album\/(?:\d+)\/video\/|video\/|)(\d+)(?:$|\/|\?)/;
    const vimeoMatch = url.match(vimeoRegex);
    if (vimeoMatch && vimeoMatch[1]) {
        return { url: `https://player.vimeo.com/video/${vimeoMatch[1]}`, type: "iframe" as const };
    }
    
    // If it is already an iframe embed URL
    if (url.includes("youtube.com/embed/") || url.includes("player.vimeo.com/video/")) {
        return { url, type: "iframe" as const };
    }
    
    // Standard video
    const isHls = url.includes(".m3u8") || url.includes("/hls/");
    return { url, type: (isHls ? "video" : "iframe") as "video" | "iframe" };
}

export function CourseMediaTab({ 
    courseId, 
    recordings = [], 
    sessions = [],
    onViewAttendance,
    onPlay,
    onRefreshDetail
}: { 
    courseId: string;
    recordings?: any[];
    sessions?: any[];
    onViewAttendance?: (sessionId: string) => void;
    onPlay?: (title: string, url: string, type: "video" | "iframe") => void;
    onRefreshDetail?: () => Promise<void>;
}) {
    const { user, currentTenantId } = useAuth();
    const { success, error: toastError } = useToast();
    const [activeVideo, setActiveVideo] = useState<{ id: string; url: string; type: "video" | "iframe"; title: string; vttPath?: string } | null>(null);
    const [medias, setMedias] = useState<CourseMediaDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [draggedItemIndex, setDraggedItemIndex] = useState<number | null>(null);
    const [isLibraryModalOpen, setIsLibraryModalOpen] = useState(false);
    const [isExamModalOpen, setIsExamModalOpen] = useState(false);
    const [selectedItems, setSelectedItems] = useState<Set<string>>(new Set());
    
    // Inline confirmation states
    const [inlineDeleteConfirm, setInlineDeleteConfirm] = useState<string | null>(null);
    const [bulkDeleteConfirm, setBulkDeleteConfirm] = useState(false);
    
    // Inline edit states
    const [editingMediaId, setEditingMediaId] = useState<string | null>(null);
    const [editingTitle, setEditingTitle] = useState("");
    const [editingVideoUrl, setEditingVideoUrl] = useState("");
    const [isSavingEdit, setIsSavingEdit] = useState(false);

    const [filter, setFilter] = useState<'all' | 'video' | 'recording'>('all');
    const [pageSize, setPageSize] = useState<number>(20);
    const [currentPage, setCurrentPage] = useState<number>(1);

    useEffect(() => {
        loadMedias();
    }, [courseId]);

    const hasExamsFeature = useMemo(() => {
        const t = user?.tenants.find(x => x.tenantId === currentTenantId);
        if (!t?.features) return false;
        try {
            const f = JSON.parse(t.features);
            return !!f.exams;
        } catch { return false; }
    }, [user, currentTenantId]);

    const loadMedias = async () => {
        setLoading(true);
        try {
            const data = await mediaLibraryApi.getCourseMedias(courseId);
            setMedias(data);
        } catch (error) {
            toastError("Hata", "Videolar yüklenemedi.");
        } finally {
            setLoading(false);
        }
    };

    const handleSaveEdit = async (mediaAssetId: string) => {
        if (!editingTitle.trim()) return;
        setIsSavingEdit(true);
        try {
            await mediaLibraryApi.updateAsset(mediaAssetId, { title: editingTitle });
            setMedias(prev => prev.map(m => m.mediaAssetId === mediaAssetId && m.mediaAsset ? { ...m, mediaAsset: { ...m.mediaAsset, title: editingTitle } } : m));
            setEditingMediaId(null);
            success("Başlık başarıyla güncellendi.");
        } catch {
            toastError("Hata", "Başlık güncellenemedi.");
        } finally {
            setIsSavingEdit(false);
        }
    };

    const handleSaveSessionEdit = async (sessionId: string) => {
        if (!editingTitle.trim()) return;
        setIsSavingEdit(true);
        try {
            const token = typeof window !== "undefined" ? localStorage.getItem("muro_token") : null;
            await courseApi.updateSession(token || "", currentTenantId || "", courseId, sessionId, {
                title: editingTitle,
                videoUrl: editingVideoUrl.trim() || ""
            });
            setMedias(prev => prev.map(m => m.sessionId === sessionId ? { ...m, sessionTitle: editingTitle } : m));
            setEditingMediaId(null);
            success("Ders oturumu başarıyla güncellendi.");
            if (onRefreshDetail) {
                await onRefreshDetail();
            }
        } catch {
            toastError("Hata", "Oturum güncellenemedi.");
        } finally {
            setIsSavingEdit(false);
        }
    };

    const handleDragStart = (globalIndex: number) => {
        setDraggedItemIndex(globalIndex);
    };

    const handleDragOver = (e: React.DragEvent, globalIndex: number) => {
        e.preventDefault();
        if (draggedItemIndex === null || draggedItemIndex === globalIndex) return;

        const newMedias = [...medias];
        const draggedItem = newMedias[draggedItemIndex];
        newMedias.splice(draggedItemIndex, 1);
        newMedias.splice(globalIndex, 0, draggedItem);

        // Update local orderIndex before saving
        const ordered = newMedias.map((m, i) => ({ ...m, orderIndex: i }));
        setMedias(ordered);
        setDraggedItemIndex(globalIndex);
    };

    const handleDrop = async () => {
        setDraggedItemIndex(null);
        // Save new order to backend
        try {
            await mediaLibraryApi.reorderCourseMedias(courseId, medias.map(m => m.id));
            success("Sıralama güncellendi.");
        } catch (error) {
            toastError("Hata", "Sıralama kaydedilirken hata oluştu.");
            loadMedias(); // Revert
        }
    };

    const handleBulkRemoveClick = () => {
        if (selectedItems.size > 0) {
            setBulkDeleteConfirm(true);
        }
    };

    const confirmBulkRemove = async () => {
        if (selectedItems.size === 0) return;
        const ids = Array.from(selectedItems);
        try {
            for (const id of ids) {
                await mediaLibraryApi.removeItemFromCourse(courseId, id);
            }
            setMedias(medias.filter(m => !ids.includes(m.id)));
            setSelectedItems(new Set());
            success(`${ids.length} içerik kurstan kaldırıldı.`);
        } catch (error) {
            toastError("Hata", "İçerikler kaldırılırken hata oluştu.");
        } finally {
            setBulkDeleteConfirm(false);
        }
    };

    const confirmInlineRemove = async (courseMediaId: string) => {
        try {
            await mediaLibraryApi.removeItemFromCourse(courseId, courseMediaId);
            setMedias(medias.filter(m => m.id !== courseMediaId));
            setSelectedItems(prev => {
                const newSet = new Set(prev);
                newSet.delete(courseMediaId);
                return newSet;
            });
            success("İçerik kurstan kaldırıldı.");
        } catch (error) {
            toastError("Hata", "İçerik kaldırılırken hata oluştu.");
        } finally {
            setInlineDeleteConfirm(null);
        }
    };

    const toggleItemSelection = (id: string) => {
        const newSet = new Set(selectedItems);
        if (newSet.has(id)) newSet.delete(id);
        else newSet.add(id);
        setSelectedItems(newSet);
    };

    const handleLibrarySelect = async (selectedAssetIds: string[], selectedFolderIds: string[]) => {
        try {
            // Assign folders
            for (const folderId of selectedFolderIds) {
                await mediaLibraryApi.bulkAssignFolderToCourse(courseId, folderId);
            }
            // Assign individual assets
            for (const assetId of selectedAssetIds) {
                await mediaLibraryApi.assignMediaToCourse(courseId, assetId);
            }
            
            success("Seçilen içerikler kursa eklendi.");
            setIsLibraryModalOpen(false);
            loadMedias();
        } catch (error) {
            toastError("Hata", "İçerikler atanırken bir hata oluştu.");
        }
    };

    const handleExamSelect = async (selectedExamIds: string[]) => {
        try {
            for (const examId of selectedExamIds) {
                await mediaLibraryApi.assignExamToCourse(courseId, examId);
            }
            success("Seçilen sınavlar kursa eklendi.");
            setIsExamModalOpen(false);
            loadMedias();
        } catch (error) {
            toastError("Hata", "Sınavlar eklenirken bir hata oluştu.");
        }
    };

    const isReorderEnabled = filter === 'all' && pageSize === -1;

    const combinedMedias = useMemo(() => {
        const list = [...medias];
        list.sort((a, b) => {
            let dateA = "";
            let dateB = "";
            let titleA = "";
            let titleB = "";
            
            const extractDateFromTitle = (title: string) => {
                const match = title.match(/(\d{2})\.(\d{2})\.(\d{4})/);
                if (match) {
                    return `${match[3]}-${match[2]}-${match[1]}T00:00:00Z`;
                }
                return null;
            };
            
            if (a.type === "Session" && a.sessionId) {
                const sess = sessions.find(s => s.id === a.sessionId);
                titleA = a.sessionTitle || sess?.title || "";
            } else if (a.type === "Video" && a.mediaAsset) {
                titleA = a.mediaAsset.title || "";
            } else {
                titleA = a.examTitle || "";
            }
            
            if (b.type === "Session" && b.sessionId) {
                const sess = sessions.find(s => s.id === b.sessionId);
                titleB = b.sessionTitle || sess?.title || "";
            } else if (b.type === "Video" && b.mediaAsset) {
                titleB = b.mediaAsset.title || "";
            } else {
                titleB = b.examTitle || "";
            }

            // 1. Eğer SADECE Admin panelinde Reorder (Sürükle bırak) modundaysak, her şeyi ez ve orderIndex'e göre diz
            if (isReorderEnabled) {
                if (a.orderIndex !== b.orderIndex) {
                    return (a.orderIndex || 0) - (b.orderIndex || 0);
                }
            }

            // 2. Sıralama önceliği: Title'dan çıkarılan tarihe göre (varsa)
            const parsedDateA = extractDateFromTitle(titleA);
            const parsedDateB = extractDateFromTitle(titleB);
            
            if (parsedDateA && parsedDateB && parsedDateA !== parsedDateB) {
                return parsedDateA.localeCompare(parsedDateB);
            }
            
            // 3. Sıralama önceliği: Alfabetik (numeric: true ile DERS-2 ve DERS-10'u doğru sıralar)
            if (titleA !== titleB) {
                return titleA.localeCompare(titleB, 'tr', { numeric: true, sensitivity: 'base' });
            }

            // 4. Sıralama önceliği: Admin elle sıralama yaptıysa (orderIndex)
            if (a.orderIndex !== b.orderIndex) {
                return (a.orderIndex || 0) - (b.orderIndex || 0);
            }
            
            // 5. En son çare: Oluşturulma tarihi
            let fallbackDateA = "";
            let fallbackDateB = "";
            
            if (a.type === "Session" && a.sessionId) {
                const sess = sessions.find(s => s.id === a.sessionId);
                fallbackDateA = sess?.scheduledStart || sess?.createdAt || "";
            } else if (a.type === "Video" && a.mediaAsset) {
                fallbackDateA = a.mediaAsset.createdAt || "";
            }
            
            if (b.type === "Session" && b.sessionId) {
                const sess = sessions.find(s => s.id === b.sessionId);
                fallbackDateB = sess?.scheduledStart || sess?.createdAt || "";
            } else if (b.type === "Video" && b.mediaAsset) {
                fallbackDateB = b.mediaAsset.createdAt || "";
            }
            
            if (fallbackDateA !== fallbackDateB) {
                return fallbackDateA.localeCompare(fallbackDateB);
            }

            // 6. Sıralama önceliği: ID'ye göre (Son çare)
            return a.id.localeCompare(b.id);
        });
        return list;
    }, [medias, sessions]);

    const filteredMedias = combinedMedias.filter(media => {
        const isRecording = media.type === "Session" || !!recordings.find(r => r.mediaAssetId && r.mediaAssetId === media?.mediaAsset?.id);
        if (filter === 'video' && isRecording) return false;
        if (filter === 'recording' && !isRecording) return false;
        return true;
    });

    const totalPages = Math.ceil(filteredMedias.length / pageSize);
    const paginatedMedias = pageSize === -1 ? filteredMedias : filteredMedias.slice((currentPage - 1) * pageSize, currentPage * pageSize);

    // Reset to page 1 if filter or page size changes
    useEffect(() => {
        setCurrentPage(1);
    }, [filter, pageSize]);

    if (loading) {
        return <div className="p-8 text-center text-gray-500">Yükleniyor...</div>;
    }

    return (
        <div className="relative">
            <div className="bg-white rounded-[2rem] border border-[#E2E8F0]/60 p-6 flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6 shadow-sm">
                <div>
                    <h2 className="text-xl font-black text-[#0A1931] mb-1">Medya ve Kayıtlar</h2>
                    {isReorderEnabled ? (
                        <p className="text-sm font-medium text-[#A0AEC0]">Tüm video ve ders kayıtlarının sıralamasını sürükle-bırak ile değiştirebilirsiniz.</p>
                    ) : (
                        <p className="text-sm font-medium text-amber-500">Sıralama değiştirmek için görünümü 'Tümü' (Hem Filtre Hem Gösterim) yapmalısınız.</p>
                    )}
                </div>
                <div className="flex items-center gap-3 flex-wrap">
                    {selectedItems.size > 0 && (
                        bulkDeleteConfirm ? (
                            <div className="flex items-center gap-2 animate-in fade-in slide-in-from-right-2">
                                <span className="text-sm font-bold text-red-600 px-2">{selectedItems.size} seçili öğeyi sil:</span>
                                <button 
                                    onClick={confirmBulkRemove}
                                    className="px-4 py-2.5 bg-red-500 text-white text-xs font-bold rounded-2xl hover:bg-red-600 transition-all shadow-md flex items-center gap-1"
                                >
                                    <Check size={14} /> Eminim
                                </button>
                                <button 
                                    onClick={() => setBulkDeleteConfirm(false)}
                                    className="px-4 py-2.5 bg-[#F8FAFC] text-[#A0AEC0] text-xs font-bold rounded-2xl border border-[#E2E8F0] hover:bg-[#E2E8F0] transition-all"
                                >
                                    Vazgeç
                                </button>
                            </div>
                        ) : (
                            <button 
                                onClick={handleBulkRemoveClick}
                                className="flex items-center gap-2 bg-red-50 hover:bg-red-100 text-red-600 px-5 py-2.5 rounded-2xl font-bold transition-all border border-red-200"
                            >
                                <Trash2 size={16} />
                                Seçilenleri Kaldır ({selectedItems.size})
                            </button>
                        )
                    )}
                    <CustomSelect 
                        value={filter}
                        onChange={(val) => setFilter(val as any)}
                        options={[
                            { label: "Filtre: Tümü", value: "all" },
                            { label: "Filtre: Videolar", value: "video" },
                            { label: "Filtre: Kayıtlar", value: "recording" }
                        ]}
                    />
                    <CustomSelect 
                        value={pageSize}
                        onChange={(val) => setPageSize(Number(val))}
                        options={[
                            { label: "10'lu Göster", value: 10 },
                            { label: "20'li Göster", value: 20 },
                            { label: "50'li Göster", value: 50 },
                            { label: "Tümü (Sıralamaya İzin Ver)", value: -1 }
                        ]}
                    />
                    <button 
                        onClick={() => setIsLibraryModalOpen(true)}
                        className="flex items-center gap-2 bg-[#1B3B6F] hover:bg-[#152a51] text-white px-5 py-2.5 rounded-2xl font-bold transition-all shadow-md active:scale-95"
                    >
                        <Plus size={16} />
                        Medya Ekle
                    </button>
                    {hasExamsFeature && (
                        <button 
                            onClick={() => setIsExamModalOpen(true)}
                            className="flex items-center gap-2 bg-[#1B3B6F] hover:bg-[#152a51] text-white px-5 py-2.5 rounded-2xl font-bold transition-all shadow-md active:scale-95"
                        >
                            <FileText size={16} />
                            Quiz Ekle
                        </button>
                    )}
                </div>
            </div>

            {/* Immersive Theater Modal */}
            {activeVideo && createPortal(
                <div className="fixed inset-0 z-[100] flex items-center justify-center p-6 bg-[#0A1931]/80 backdrop-blur-md animate-in fade-in duration-300">
                    {/* Click outside to close (optional, but good UX) */}
                    <div className="absolute inset-0 cursor-pointer" onClick={() => setActiveVideo(null)} />
                    
                    <div className="relative w-full max-w-4xl 2xl:max-w-5xl bg-black rounded-[2rem] overflow-hidden shadow-[0_30px_100px_rgba(0,0,0,0.5)] border border-white/20 animate-in zoom-in-95 duration-300 group">
                        {/* Fixed Close Button (Screen Top Right) */}
                        <Tooltip content="Kapat">
                            <button 
                                onClick={() => setActiveVideo(null)} 
                                className="fixed top-6 right-6 z-[110] w-12 h-12 rounded-full bg-black/40 hover:bg-red-600 hover:scale-110 text-white flex items-center justify-center backdrop-blur-md transition-all border border-white/20 shadow-2xl"
                            >
                                <X size={24} />
                            </button>
                        </Tooltip>

                        {/* Overlay Top Bar (Video Title) */}
                        <div className="absolute top-0 inset-x-0 z-10 flex items-center justify-between px-6 py-4 bg-gradient-to-b from-black/90 via-black/50 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300 pointer-events-none">
                            <div className="flex-1 min-w-0 pr-4">
                                <h3 className="text-white font-bold text-lg tracking-tight drop-shadow-md">{activeVideo.title}</h3>
                                <p className="text-white/70 text-xs mt-0.5 drop-shadow-md">{activeVideo.type === "video" ? "Video Kaydı" : "Canlı Ders Kaydı"}</p>
                            </div>
                        </div>
                        
                        {/* Video Area */}
                        <div className="w-full aspect-video bg-black relative flex flex-col">
                            <div className="flex-1 relative">
                                {activeVideo.type === "video" ? (
                                    <HlsVideoPlayer src={activeVideo.url} mediaId={activeVideo.id} vttPath={activeVideo.vttPath} />
                                ) : (
                                    <iframe 
                                        src={activeVideo.url.startsWith("/") ? `${API_URL.replace("/api/v1", "")}${activeVideo.url}` : activeVideo.url} 
                                        className="w-full h-full border-0 absolute inset-0" 
                                        allowFullScreen 
                                    />
                                )}
                            </div>
                        </div>
                    </div>
                </div>,
                document.body
            )}

            {filteredMedias.length === 0 ? (
                <div className="text-center py-16 border border-dashed border-[#E2E8F0] rounded-[2rem] bg-white shadow-sm">
                    <Video size={48} className="mx-auto text-[#A0AEC0] opacity-30 mb-4" />
                    <p className="text-sm font-bold text-[#A0AEC0]">Bu kursa henüz video veya kayıt eklenmemiş.</p>
                </div>
            ) : (
                <div className="space-y-3">
                    {paginatedMedias.map((media, displayIndex) => {
                        const globalIndex = medias.findIndex(m => m.id === media.id);
                        const recording = recordings.find(r => r.mediaAssetId && r.mediaAssetId === media.mediaAsset?.id);
                        const isRecording = !!recording;

                        return (
                            <div key={media.id} className="flex flex-col gap-2">
                                <div
                                    draggable={isReorderEnabled}
                                    onDragStart={isReorderEnabled ? () => handleDragStart(globalIndex) : undefined}
                                    onDragOver={isReorderEnabled ? (e) => handleDragOver(e, globalIndex) : undefined}
                                    onDrop={isReorderEnabled ? handleDrop : undefined}
                                    onDragEnd={isReorderEnabled ? handleDrop : undefined}
                                    className={`flex items-center gap-1.5 sm:gap-5 bg-white border shadow-sm p-2.5 sm:p-4 rounded-2xl transition-all group ${activeVideo?.id === media.id ? 'border-blue-500 ring-2 ring-blue-500/20 shadow-md' : 'border-[#E2E8F0]/60 hover:shadow-md hover:border-blue-300'} ${draggedItemIndex === globalIndex ? 'opacity-50 scale-[1.02]' : ''} ${isReorderEnabled && !(media as any).isFake ? 'cursor-grab active:cursor-grabbing' : ''}`}
                                >
                                <div className="hidden sm:flex w-10 items-center justify-center shrink-0 border-r border-[#E2E8F0]/80 pr-3">
                                    <span className="text-base font-black text-[#1B3B6F]/60 font-mono drop-shadow-sm">
                                        {(combinedMedias.findIndex(m => m.id === media.id) + 1).toString().padStart(2, '0')}
                                    </span>
                                </div>
                                
                                <div 
                                    className="p-0.5 sm:p-2 cursor-pointer shrink-0" 
                                    onClick={(e) => { e.stopPropagation(); toggleItemSelection(media.id); }}
                                >
                                    <div className={`w-5 h-5 rounded-md border-2 flex items-center justify-center transition-all ${selectedItems.has(media.id) ? 'bg-[#1B3B6F] border-[#1B3B6F] text-white' : 'border-[#A0AEC0]/40 text-transparent hover:border-[#1B3B6F]/50'}`}>
                                        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" className={selectedItems.has(media.id) ? 'opacity-100' : 'opacity-0'}><polyline points="20 6 9 17 4 12"></polyline></svg>
                                    </div>
                                </div>
                                {isReorderEnabled && !(media as any).isFake ? (
                                    <div className="hidden sm:block text-[#A0AEC0] p-1 cursor-grab group-hover:text-blue-500 transition-colors">
                                        <GripVertical size={20} />
                                    </div>
                                ) : (
                                    <div className="hidden sm:block w-5" /> // placeholder
                                )}
                                
                                {media.type === "Exam" ? (
                                    <Tooltip content="Sınav"><button 
                                        disabled
                                    >
                                        <FileText className="w-5 h-5 sm:w-6 sm:h-6" />
                                    </button></Tooltip>
                                ) : media.type === "Session" ? (() => {
                                    const rec = recordings.find(r => r.sessionId === media.sessionId && (r.playbackUrl || r.hlsPath));
                                    const sess = sessions.find(s => s.id === media.sessionId);
                                    const videoUrl = sess?.videoUrl;
                                    const canPlay = !!rec || !!videoUrl;
                                    return (
                                        <button 
                                            disabled={!canPlay}
                                            onClick={() => {
                                                if (!canPlay) return;
                                                if (activeVideo?.id === media.id) {
                                                    setActiveVideo(null);
                                                } else {
                                                    if (videoUrl) {
                                                        const details = getVideoPlaybackDetails(videoUrl);
                                                        setActiveVideo({
                                                            id: media.id,
                                                            title: media.sessionTitle || "Canlı Ders Kaydı",
                                                            url: details.url,
                                                            type: details.type
                                                        });
                                                    } else if (rec) {
                                                        const url = rec.hlsPath || rec.playbackUrl;
                                                        if (url) {
                                                            setActiveVideo({ 
                                                                id: media.id, 
                                                                title: media.sessionTitle || "Canlı Ders Kaydı",
                                                                url, 
                                                                type: rec.hlsPath ? "video" : "iframe",
                                                                vttPath: rec.thumbnailPath || undefined
                                                            });
                                                        }
                                                    }
                                                }
                                            }}
                                            className={`w-9 h-9 sm:w-14 sm:h-14 rounded-lg sm:rounded-xl flex items-center justify-center shrink-0 shadow-inner transition-all ${canPlay ? (activeVideo?.id === media.id ? 'bg-[#0A1931] text-white shadow-lg' : 'hover:scale-105 active:scale-95 cursor-pointer bg-blue-50 text-blue-600 hover:bg-blue-100') : 'bg-red-50 text-red-500/50 cursor-not-allowed'}`}
                                            title={canPlay ? (activeVideo?.id === media.id ? "Kapat" : "Oynat") : "Canlı Ders (Kayıt Yok)"}
                                        >
                                            {activeVideo?.id === media.id ? <X className="w-5 h-5 sm:w-6 sm:h-6" /> : <Play className="w-5 h-5 sm:w-6 sm:h-6" />}
                                        </button>
                                    );
                                })() : (
                                    <button 
                                        onClick={() => {
                                            if (activeVideo?.id === media.id) {
                                                setActiveVideo(null);
                                            } else if (media.mediaAsset) {
                                                const url = media.mediaAsset.hlsPath || media.mediaAsset.filePath;
                                                if (url) {
                                                    setActiveVideo({ 
                                                        id: media.id, 
                                                        title: media.mediaAsset.title,
                                                        url, 
                                                        type: media.mediaAsset.hlsPath ? "video" : "iframe",
                                                        vttPath: (media.mediaAsset as any).vttPath
                                                    });
                                                }
                                            }
                                        }}
                                        className={`w-9 h-9 sm:w-14 sm:h-14 rounded-lg sm:rounded-xl flex items-center justify-center shrink-0 shadow-inner transition-all hover:scale-105 active:scale-95 cursor-pointer ${activeVideo?.id === media.id ? 'bg-[#0A1931] text-white shadow-lg' : isRecording ? 'bg-red-50 text-red-500 hover:bg-red-100' : 'bg-blue-50 text-blue-600 hover:bg-blue-100'}`}
                                        title={activeVideo?.id === media.id ? "Kapat" : "Oynat"}
                                    >
                                        {activeVideo?.id === media.id ? <X className="w-5 h-5 sm:w-6 sm:h-6" /> : <Play className="w-5 h-5 sm:w-6 sm:h-6" />}
                                    </button>
                                )}
                                
                                {editingMediaId === media.id ? (
                                    <div className="flex-1 min-w-0 pr-4 animate-in fade-in zoom-in-95 duration-200" onClick={e => e.stopPropagation()}>
                                        {media.type === "Session" ? (
                                            <div className="flex flex-col gap-2">
                                                <div className="flex gap-2">
                                                    <input 
                                                        type="text" 
                                                        placeholder="Ders Başlığı"
                                                        value={editingTitle} 
                                                        onChange={(e) => setEditingTitle(e.target.value)}
                                                        className="w-full bg-[#F8FAFC] border-2 border-[#1B3B6F]/20 rounded-xl px-3 py-2 text-sm font-bold text-[#0A1931] focus:outline-none focus:border-[#1B3B6F] focus:ring-4 focus:ring-[#1B3B6F]/10 transition-all"
                                                        autoFocus
                                                        disabled={isSavingEdit}
                                                    />
                                                    <button onClick={() => media.sessionId && handleSaveSessionEdit(media.sessionId)} disabled={isSavingEdit} className="p-2 bg-emerald-50 text-emerald-600 hover:bg-emerald-100 hover:text-emerald-700 rounded-xl shrink-0 transition-colors">
                                                        {isSavingEdit ? <Loader2 size={18} className="animate-spin" /> : <Check size={18} />}
                                                    </button>
                                                    <button onClick={() => setEditingMediaId(null)} disabled={isSavingEdit} className="p-2 bg-red-50 text-red-500 hover:bg-red-100 hover:text-red-600 rounded-xl shrink-0 transition-colors">
                                                        <X size={18} />
                                                    </button>
                                                </div>
                                                <input 
                                                    type="text" 
                                                    placeholder="Video URL (YouTube/Vimeo/HLS vb.)"
                                                    value={editingVideoUrl} 
                                                    onChange={(e) => setEditingVideoUrl(e.target.value)}
                                                    className="w-full bg-[#F8FAFC] border-2 border-[#1B3B6F]/20 rounded-xl px-3 py-2 text-sm font-bold text-[#0A1931] focus:outline-none focus:border-[#1B3B6F] focus:ring-4 focus:ring-[#1B3B6F]/10 transition-all"
                                                    disabled={isSavingEdit}
                                                    onKeyDown={(e) => {
                                                        if (e.key === 'Enter') {
                                                            if (media.sessionId) handleSaveSessionEdit(media.sessionId);
                                                        }
                                                        if (e.key === 'Escape') setEditingMediaId(null);
                                                    }}
                                                />
                                            </div>
                                        ) : (
                                            <div className="flex items-center gap-2">
                                                <input 
                                                    type="text" 
                                                    value={editingTitle} 
                                                    onChange={(e) => setEditingTitle(e.target.value)}
                                                    className="w-full bg-[#F8FAFC] border-2 border-[#1B3B6F]/20 rounded-xl px-3 py-2 text-sm font-bold text-[#0A1931] focus:outline-none focus:border-[#1B3B6F] focus:ring-4 focus:ring-[#1B3B6F]/10 transition-all"
                                                    autoFocus
                                                    onKeyDown={(e) => {
                                                        if (e.key === 'Enter') {
                                                            if (media.mediaAssetId) handleSaveEdit(media.mediaAssetId);
                                                        }
                                                        if (e.key === 'Escape') setEditingMediaId(null);
                                                    }}
                                                    disabled={isSavingEdit}
                                                />
                                                <button onClick={() => media.mediaAssetId && handleSaveEdit(media.mediaAssetId)} disabled={isSavingEdit} className="p-2 bg-emerald-50 text-emerald-600 hover:bg-emerald-100 hover:text-emerald-700 rounded-xl shrink-0 transition-colors">
                                                    {isSavingEdit ? <Loader2 size={18} className="animate-spin" /> : <Check size={18} />}
                                                </button>
                                                <button onClick={() => setEditingMediaId(null)} disabled={isSavingEdit} className="p-2 bg-red-50 text-red-500 hover:bg-red-100 hover:text-red-600 rounded-xl shrink-0 transition-colors">
                                                    <X size={18} />
                                                </button>
                                            </div>
                                        )}
                                    </div>
                                ) : (
                                    <div className="flex-1 min-w-0">
                                        <div className="flex items-center gap-1 sm:gap-2 mb-0.5 sm:mb-1">
                                            <p className="text-[#0A1931] font-bold text-xs sm:text-base line-clamp-2 sm:truncate break-all sm:break-normal leading-tight">{media.type === "Exam" ? media.examTitle : media.type === "Session" ? media.sessionTitle : media.mediaAsset?.title}</p>
                                            {media.type === "Exam" && (
                                                <span className="px-1 py-0.5 rounded-md bg-purple-100 text-purple-700 text-[8px] sm:text-[10px] font-black uppercase tracking-wider whitespace-nowrap">
                                                    SINAV
                                                </span>
                                            )}
                                            {isRecording && (
                                                <span className="px-1 py-0.5 rounded-md bg-red-100 text-red-700 text-[8px] sm:text-[10px] font-black uppercase tracking-wider whitespace-nowrap">
                                                    CANLI
                                                </span>
                                            )}
                                        </div>
                                        <p className="text-[10px] sm:text-xs font-medium text-[#A0AEC0] flex items-center gap-1 sm:gap-2">
                                            {media.type === "Exam" ? "Öğrenci Sınavı" : media.type === "Session" ? "Canlı Ders Oturumu" : (
                                                <>
                                                    {media.mediaAsset?.durationSeconds ? `${Math.floor(media.mediaAsset.durationSeconds / 60)} dk` : 'Süre bilinmiyor'} 
                                                    <span className="w-0.5 h-0.5 sm:w-1 sm:h-1 rounded-full bg-[#E2E8F0]" /> 
                                                    <span className={media.mediaAsset?.status === 'Ready' ? 'text-emerald-500' : 'text-amber-500'}>
                                                        {media.mediaAsset?.status === 'Ready' ? 'Hazır' : media.mediaAsset?.status === 'Processing' ? 'İşleniyor' : media.mediaAsset?.status}
                                                    </span>
                                                </>
                                            )}
                                        </p>
                                    </div>
                                )}

                                <div className="flex items-center gap-0.5 sm:gap-2 shrink-0">
                                    {isRecording && recording && onViewAttendance && (
                                        <Tooltip content="Yoklama Gör"><button 
                                            onClick={() => onViewAttendance(recording.sessionId)}
                                            className="p-1 sm:p-3 text-[#A0AEC0] hover:text-blue-500 hover:bg-blue-50 rounded-xl transition-all"
                                        >
                                            <Users className="w-3.5 h-3.5 sm:w-[18px] sm:h-[18px]" />
                                        </button></Tooltip>
                                    )}
                                    {!(media as any).isFake && (
                                        inlineDeleteConfirm === media.id ? (
                                            <div className="flex items-center gap-3 animate-in fade-in slide-in-from-right-2 bg-red-50/80 p-2 rounded-xl border border-red-100">
                                                <span className="text-[10px] font-bold text-red-600/80 max-w-[180px] leading-tight">
                                                    {media.type === "Media" 
                                                        ? "💡 Bu videoyu dersten çıkarmak istiyor musunuz?" 
                                                        : "⚠️ Bu oturumu takvimden tamamen silmek istiyor musunuz?"}
                                                </span>
                                                <div className="flex items-center gap-1 shrink-0">
                                                    <button 
                                                        onClick={() => confirmInlineRemove(media.id)}
                                                        className="px-3 py-1.5 bg-red-500 text-white text-xs font-bold rounded-lg hover:bg-red-600 transition-all shadow-sm"
                                                    >
                                                        Onayla
                                                    </button>
                                                    <button 
                                                        onClick={() => setInlineDeleteConfirm(null)}
                                                        className="px-3 py-1.5 bg-white text-[#0A1931] text-xs font-bold rounded-lg border border-[#E2E8F0] hover:bg-gray-100 transition-all"
                                                    >
                                                        Vazgeç
                                                    </button>
                                                </div>
                                            </div>
                                        ) : (
                                            <>
                                                {media.type === "Media" && media.mediaAssetId && (
                                                    <Link
                                                        href="/dashboard/media"
                                                        target="_blank"
                                                        onClick={(e) => e.stopPropagation()}
                                                        className="p-1 sm:p-3 text-[#A0AEC0] hover:text-blue-600 hover:bg-blue-50 rounded-xl transition-all"
                                                        title="Kütüphanede Göster"
                                                    >
                                                        <ExternalLink className="w-3.5 h-3.5 sm:w-[18px] sm:h-[18px]" />
                                                    </Link>
                                                )}
                                                {media.type === "Media" && media.mediaAssetId && (
                                                    <Tooltip content="Başlığı Düzenle"><button 
                                                        onClick={(e) => {
                                                            e.stopPropagation();
                                                            setEditingTitle(media.mediaAsset?.title || "");
                                                            setEditingMediaId(media.id);
                                                        }}
                                                        className="p-1 sm:p-3 text-[#A0AEC0] hover:text-emerald-600 hover:bg-emerald-50 rounded-xl transition-all"
                                                    >
                                                        <Edit2 className="w-3.5 h-3.5 sm:w-[18px] sm:h-[18px]" />
                                                    </button></Tooltip>
                                                )}
                                                {media.type === "Session" && media.sessionId && (
                                                    <Tooltip content="Dersi Düzenle"><button 
                                                        onClick={(e) => {
                                                            e.stopPropagation();
                                                            setEditingTitle(media.sessionTitle || "");
                                                            const sess = sessions.find(s => s.id === media.sessionId);
                                                            setEditingVideoUrl(sess?.videoUrl || "");
                                                            setEditingMediaId(media.id);
                                                        }}
                                                        className="p-1 sm:p-3 text-[#A0AEC0] hover:text-emerald-600 hover:bg-emerald-50 rounded-xl transition-all"
                                                    >
                                                        <Edit2 className="w-3.5 h-3.5 sm:w-[18px] sm:h-[18px]" />
                                                    </button></Tooltip>
                                                )}
                                                <button 
                                                    onClick={(e) => {
                                                        e.stopPropagation();
                                                        setInlineDeleteConfirm(media.id);
                                                    }}
                                                    className="p-1 sm:p-3 text-[#A0AEC0] hover:text-red-500 hover:bg-red-50 rounded-xl transition-all"
                                                    title={media.type === "Media" ? "Dersten Çıkar" : "Tamamen Sil"}
                                                >
                                                    <Trash2 className="w-3.5 h-3.5 sm:w-[18px] sm:h-[18px]" />
                                                </button>
                                            </>
                                        )
                                    )}
                                </div>
                                </div>
                                
                                {/* The Inline Theater Box has been moved to a global modal */}
                            </div>
                        );
                    })}

                    {/* Pagination Controls */}
                    {pageSize !== -1 && totalPages > 1 && (
                        <div className="flex items-center justify-between bg-white border border-[#E2E8F0] p-4 rounded-[2rem] shadow-sm mt-4">
                            <span className="text-sm font-bold text-[#A0AEC0]">
                                Toplam <span className="text-[#0A1931]">{filteredMedias.length}</span> içerik, Sayfa {currentPage} / {totalPages}
                            </span>
                            <div className="flex gap-2">
                                <button 
                                    onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                                    disabled={currentPage === 1}
                                    className="px-4 py-2 bg-gray-50 hover:bg-gray-100 disabled:opacity-50 rounded-xl font-bold text-[#1B3B6F] transition-all"
                                >
                                    Önceki
                                </button>
                                <button 
                                    onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                                    disabled={currentPage === totalPages}
                                    className="px-4 py-2 bg-gray-50 hover:bg-gray-100 disabled:opacity-50 rounded-xl font-bold text-[#1B3B6F] transition-all"
                                >
                                    Sonraki
                                </button>
                            </div>
                        </div>
                    )}
                </div>
            )}

            {isLibraryModalOpen && (
                <LibrarySelectorModal 
                    onClose={() => setIsLibraryModalOpen(false)}
                    onSelect={handleLibrarySelect}
                />
            )}

            {isExamModalOpen && (
                <ExamSelectorModal 
                    onClose={() => setIsExamModalOpen(false)}
                    onSelect={handleExamSelect}
                />
            )}

        </div>
    );
}

// ─── HLS Player Component ──────────────────────────────────────────────────
function HlsVideoPlayer({ src, mediaId, vttPath }: { src: string; mediaId: string; vttPath?: string }) {
    const videoRef = useRef<HTMLVideoElement>(null);
    useEffect(() => {
        const video = videoRef.current;
        if (!video || !src) return;
        const fullSrc = src.startsWith("/") 
            ? `${API_URL.replace("/api/v1", "")}${src}`
            : src;
            
        let hls: any = null;
        let player: any = null;
        const storageKey = `muro_progress_${mediaId}`;

        const initPlyr = async (availableQualities: number[] = []) => {
            const plyrOptions: any = {
                controls: ['play-large', 'play', 'progress', 'current-time', 'duration', 'mute', 'volume', 'captions', 'settings', 'pip', 'airplay', 'fullscreen'],
                settings: ['captions', 'quality', 'speed'],
                speed: { selected: 1, options: [0.5, 0.75, 1, 1.25, 1.5, 2] },
                keyboard: { focused: true, global: true }
            };

            if (vttPath) {
                plyrOptions.previewThumbnails = { enabled: true, src: vttPath };
            }

            if (availableQualities.length > 0) {
                plyrOptions.quality = {
                    default: availableQualities[0],
                    options: availableQualities,
                    forced: true,
                    onChange: (newQuality: number) => {
                        if (hls) {
                            (window as any).hls = hls;
                            hls.levels.forEach((level: any, levelIndex: number) => {
                                if (level.height === newQuality) {
                                    hls.currentLevel = levelIndex;
                                }
                            });
                        }
                    }
                };
            }

            player = new Plyr(video, plyrOptions);
            
            // Auto-resume logic
            const savedTime = localStorage.getItem(storageKey);
            if (savedTime && parseFloat(savedTime) > 0) {
                // Resume a bit earlier so they remember where they were
                video.currentTime = Math.max(0, parseFloat(savedTime) - 3);
            }

            video.addEventListener('timeupdate', () => {
                if (video.currentTime > 5) { // Only save if they watched more than 5 seconds
                    localStorage.setItem(storageKey, video.currentTime.toString());
                }
            });
            
            // Clear progress if finished
            video.addEventListener('ended', () => {
                localStorage.removeItem(storageKey);
            });
        };

        const load = async () => {
            if (video.canPlayType("application/vnd.apple.mpegurl")) {
                video.src = fullSrc;
                video.addEventListener('loadedmetadata', () => {
                    initPlyr();
                });
            } else {
                const Hls = (await import("hls.js")).default;
                if (Hls.isSupported()) {
                    const hls = new Hls();
                    hls.loadSource(fullSrc);
                    hls.attachMedia(video);
                    // @ts-ignore
                    window.hls = hls;
                    hls.on(Hls.Events.MANIFEST_PARSED, (event: any, data: any) => {
                        const qualities = data.levels.map((l: any) => l.height).sort((a: number, b: number) => b - a);
                        initPlyr(qualities);
                    });
                }
            }
        };
        load();

        return () => {
            if (video) {
                localStorage.setItem(storageKey, video.currentTime.toString());
            }
            if (hls) hls.destroy();
            if (player) player.destroy();
        };
    }, [src, mediaId, vttPath]);
    return <video ref={videoRef} crossOrigin="anonymous" className="w-full h-full outline-none" />;
}
