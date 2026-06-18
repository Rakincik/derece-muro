"use client";

import { useState, useEffect, useRef } from "react";
import { FolderPlus, FileVideo, ChevronRight, Folder, MoreVertical, Trash2, Edit2, Play, Search, Grid, List as ListIcon, UploadCloud, BookOpen } from "lucide-react";
import { mediaLibraryApi, type MediaFolderDto, type MediaAssetDto } from "@/lib/api";
import { useToast } from "@/components/toast";
import { VideoUploaderModal } from "@/components/ui/VideoUploaderModal";
import { CourseSelectorModal } from "@/components/ui/CourseSelectorModal";
import { FolderTree } from "@/components/ui/FolderTree";
import { ConfirmModal } from "@/components/ui/ConfirmModal";
import { VideoPlayerModal } from "@/components/ui/VideoPlayerModal";
import { useGlobalUpload } from "@/components/ui/GlobalUploadManager";

import { API_URL } from "@/lib/api";

const getFileUrl = (path: string | null) => {
    if (!path) return "";
    if (path.startsWith("http") || path.startsWith("blob:") || path.startsWith("data:")) return path;
    const baseUrl = API_URL.replace("/api/v1", "");
    return `${baseUrl}${path.startsWith("/") ? "" : "/"}${path}`;
};

const FallbackImage = ({ src }: { src: string }) => {
    const [error, setError] = useState(false);
    if (error || !src) return <FileVideo size={20} className="text-gray-400" />;
    return <img src={getFileUrl(src)} alt="" className="w-full h-full object-cover" onError={() => setError(true)} />;
};

export default function MediaLibraryPage() {
    const [folders, setFolders] = useState<MediaFolderDto[]>([]);
    const [assets, setAssets] = useState<MediaAssetDto[]>([]);
    const [currentFolderId, setCurrentFolderId] = useState<string | null>(null);
    const [breadcrumbs, setBreadcrumbs] = useState<{id: string | null, name: string}[]>([{ id: null, name: "Kütüphane" }]);
    const [loading, setLoading] = useState(true);
    const [refreshTrigger, setRefreshTrigger] = useState(0);
    const { success, error: toastError } = useToast();

    const [viewMode, setViewMode] = useState<"grid" | "list">("grid");
    const [searchQuery, setSearchQuery] = useState("");

    const [isCreateFolderModalOpen, setIsCreateFolderModalOpen] = useState(false);
    const [newFolderName, setNewFolderName] = useState("");

    const [isRenameModalOpen, setIsRenameModalOpen] = useState(false);
    const [renameTarget, setRenameTarget] = useState<{ id: string, type: 'folder' | 'asset', currentName: string, currentTags?: string } | null>(null);
    const [newName, setNewName] = useState("");
    const [newTags, setNewTags] = useState("");
    
    // Bulk Selection State
    const [selectedAssetIds, setSelectedAssetIds] = useState<string[]>([]);
    const [isBulkMoveModalOpen, setIsBulkMoveModalOpen] = useState(false);
    const [bulkMoveFolderId, setBulkMoveFolderId] = useState<string | null>(null);

    const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);

    const [isCourseModalOpen, setIsCourseModalOpen] = useState(false);
    const [courseAssignTarget, setCourseAssignTarget] = useState<{ id: string, type: 'folder' | 'asset' | 'bulk', bulkIds?: string[] } | null>(null);
    const [initialCourseIds, setInitialCourseIds] = useState<string[]>([]);
    
    // Drag and Drop state
    const [draggedAssetId, setDraggedAssetId] = useState<string | null>(null);
    const [dragOverFolderId, setDragOverFolderId] = useState<string | null>(null);

    // Confirm Modal state
    const [confirmModal, setConfirmModal] = useState<{isOpen: boolean, title: string, message: string, onConfirm: () => void}>({isOpen: false, title: "", message: "", onConfirm: () => {}});

    // Video Player Modal state
    const [playingAsset, setPlayingAsset] = useState<MediaAssetDto | null>(null);

    // Pagination state
    const [pageSize, setPageSize] = useState<number | "all">(24);
    const [currentPage, setCurrentPage] = useState(1);

    // Sorting state
    const [sortBy, setSortBy] = useState<"newest" | "oldest" | "az" | "za" | "longest" | "shortest">("newest");


    const { uploads } = useGlobalUpload();
    const completedCountRef = useRef(0);
    const readyCountRef = useRef(0);

    const loadDataTimeoutRef = useRef<NodeJS.Timeout | null>(null);

    const loadData = async () => {
        setLoading(true);
        try {
            const a = await mediaLibraryApi.getAssets(currentFolderId || undefined);
            setAssets(a);
            setSelectedAssetIds([]);
        } catch (error) {
            toastError("Hata", "Veriler yüklenirken bir hata oluştu.");
        } finally {
            setLoading(false);
        }
    };

    const debouncedLoadData = () => {
        if (loadDataTimeoutRef.current) clearTimeout(loadDataTimeoutRef.current);
        loadDataTimeoutRef.current = setTimeout(() => {
            loadData();
        }, 300);
    };

    useEffect(() => {
        let shouldReload = false;
        
        const currentCompletedCount = uploads.filter(u => u.status === 'success' && u.assetId).length;
        if (currentCompletedCount !== completedCountRef.current) {
            if (currentCompletedCount > completedCountRef.current) {
                shouldReload = true;
            }
            completedCountRef.current = currentCompletedCount;
        }

        const currentReadyCount = uploads.filter(u => u.status === 'success' && u.assetStatus === 'Ready').length;
        if (currentReadyCount !== readyCountRef.current) {
            if (currentReadyCount > readyCountRef.current) {
                shouldReload = true;
            }
            readyCountRef.current = currentReadyCount;
        }

        if (shouldReload) {
            debouncedLoadData();
        }
    }, [uploads]);

    useEffect(() => {
        debouncedLoadData();
    }, [currentFolderId]);

    useEffect(() => {
        setCurrentPage(1);
    }, [searchQuery, currentFolderId, pageSize, sortBy]);



    const triggerTreeRefresh = () => setRefreshTrigger(prev => prev + 1);

    const handleTreeAction = (action: 'rename' | 'delete' | 'assign' | 'createSub', folder: MediaFolderDto | null) => {
        if (action === 'createSub') {
            setCurrentFolderId(folder?.id || null);
            setIsCreateFolderModalOpen(true);
        } else if (action === 'rename' && folder) {
            setRenameTarget({ id: folder.id, type: 'folder', currentName: folder.name });
            setNewName(folder.name);
            setIsRenameModalOpen(true);
        } else if (action === 'assign' && folder) {
            setCourseAssignTarget({ id: folder.id, type: 'folder' });
            setInitialCourseIds([]);
            setIsCourseModalOpen(true);
        } else if (action === 'delete' && folder) {
            handleRemoveFolder(folder);
        }
    };

    const handleCreateFolder = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!newFolderName.trim()) return;

        try {
            await mediaLibraryApi.createFolder({ name: newFolderName, parentFolderId: currentFolderId || undefined });
            success("Klasör oluşturuldu");
            setNewFolderName("");
            setIsCreateFolderModalOpen(false);
            triggerTreeRefresh();
            loadData();
        } catch (error) {
            toastError("Hata", "Klasör oluşturulamadı");
        }
    };

    const handleTreeSelect = (folderId: string | null, path: {id: string | null, name: string}[]) => {
        setCurrentFolderId(folderId);
        setBreadcrumbs(path);
    };

    const handleOpenAssetCourseModal = async (asset: MediaAssetDto) => {
        setCourseAssignTarget({ id: asset.id, type: 'asset' });
        try {
            const courseIds = await mediaLibraryApi.getAssetCourses(asset.id);
            setInitialCourseIds(courseIds);
        } catch {
            setInitialCourseIds([]);
        }
        setIsCourseModalOpen(true);
    };

    const handleRemoveAsset = async (asset: MediaAssetDto) => {
        setConfirmModal({
            isOpen: true,
            title: "Videoyu Sil",
            message: `"${asset.title}" videosunu tamamen silmek istediğinize emin misiniz? Bu işlem geri alınamaz.`,
            onConfirm: async () => {
                setConfirmModal(prev => ({...prev, isOpen: false}));
                try {
                    await mediaLibraryApi.deleteAsset(asset.id);
                    success("Video silindi");
                    loadData();
                } catch (error) {
                    toastError("Hata", "Video silinemedi");
                }
            }
        });
    };

    const handleRemoveFolder = async (folder: MediaFolderDto) => {
        setConfirmModal({
            isOpen: true,
            title: "Klasörü Sil",
            message: `"${folder.name}" klasörünü silmek istediğinize emin misiniz?`,
            confirmText: "Sil",
            type: "danger",
            onConfirm: async () => {
                setConfirmModal(prev => ({...prev, isOpen: false}));
                try {
                    await mediaLibraryApi.deleteFolder(folder.id);
                    success("Klasör silindi");
                    if (currentFolderId === folder.id) {
                        setCurrentFolderId(null);
                        setBreadcrumbs([{ id: null, name: "Kök Dizin" }]);
                    }
                    triggerTreeRefresh();
                    loadData();
                } catch (error: any) {
                    if (error.message === "NON_EMPTY_FOLDER") {
                        setTimeout(() => {
                            setConfirmModal({
                                isOpen: true,
                                title: "Klasör Silinemedi",
                                message: "Bu klasörün altında videolar olduğu için doğrudan silemezsiniz. Klasörü silmek için önce içindeki tüm videoları silmelisiniz.",
                                confirmText: "Yine de Sil",
                                cancelText: "Tamam",
                                type: "warning",
                                onConfirm: () => {
                                    setConfirmModal(prev => ({...prev, isOpen: false}));
                                    setTimeout(() => {
                                        setConfirmModal({
                                            isOpen: true,
                                            title: "Kalıcı Olarak Sil",
                                            message: `"${folder.name}" klasörü ve içindeki TÜM videolar kalıcı olarak silinecektir. Bu işlemi onaylıyor musunuz?`,
                                            confirmText: "Evet, Tamamen Sil",
                                            cancelText: "İptal",
                                            type: "danger",
                                            onConfirm: async () => {
                                                setConfirmModal(prev => ({...prev, isOpen: false}));
                                                try {
                                                    await mediaLibraryApi.deleteFolder(folder.id, true);
                                                    success("Klasör ve içindeki tüm videolar kalıcı olarak silindi");
                                                    if (currentFolderId === folder.id) {
                                                        setCurrentFolderId(null);
                                                        setBreadcrumbs([{ id: null, name: "Kök Dizin" }]);
                                                    }
                                                    triggerTreeRefresh();
                                                    loadData();
                                                } catch (err) {
                                                    toastError("Hata", "Zorla silme işlemi başarısız oldu");
                                                }
                                            }
                                        });
                                    }, 300);
                                }
                            });
                        }, 300);
                    } else {
                        toastError("Hata", "Klasör silinemedi");
                    }
                }
            }
        });
    };

    const handleRenameSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!renameTarget || !newName.trim()) return;

        try {
            if (renameTarget.type === 'folder') {
                await mediaLibraryApi.updateFolder(renameTarget.id, { name: newName });
                success("Klasör adı güncellendi");
            } else {
                await mediaLibraryApi.updateAsset(renameTarget.id, { title: newName, tags: newTags });
                success("Video bilgileri güncellendi");
            }
            setIsRenameModalOpen(false);
            if (renameTarget.type === 'folder') triggerTreeRefresh();
            loadData();
        } catch (error) {
            toastError("Hata", "Yeniden adlandırma başarısız oldu.");
        }
    };

    // --- Drag & Drop ---
    const handleDragStartAsset = (e: React.DragEvent, assetId: string) => {
        setDraggedAssetId(assetId);
    };

    const handleDragOverFolder = (e: React.DragEvent, folderId: string | null) => {
        e.preventDefault();
        if (draggedAssetId) {
            setDragOverFolderId(folderId);
        }
    };

    const handleDragLeaveFolder = (e: React.DragEvent) => {
        e.preventDefault();
        setDragOverFolderId(null);
    };

    const handleDropOnFolder = async (e: React.DragEvent, folderId: string | null) => {
        e.preventDefault();
        setDragOverFolderId(null);
        if (!draggedAssetId) return;

        try {
            await mediaLibraryApi.updateAsset(draggedAssetId, { folderId });
            success("Video taşındı");
            triggerTreeRefresh();
            loadData();
        } catch (error) {
            toastError("Hata", "Video taşınamadı");
        } finally {
            setDraggedAssetId(null);
        }
    };

    const handleAssignToCourse = async (addedCourseIds: string[], removedCourseIds?: string[]) => {
        if (!courseAssignTarget) return;
        try {
            if (courseAssignTarget.type === 'folder') {
                for (const courseId of addedCourseIds) {
                    await mediaLibraryApi.bulkAssignFolderToCourse(courseId, courseAssignTarget.id);
                }
                success(`Klasör içeriği kurslara tanımlandı.`);
            } else if (courseAssignTarget.type === 'bulk' && courseAssignTarget.bulkIds) {
                for (const courseId of addedCourseIds) {
                    for (const assetId of courseAssignTarget.bulkIds) {
                        await mediaLibraryApi.assignMediaToCourse(courseId, assetId);
                    }
                }
                success("Videolar seçili kurslara eklendi");
                setSelectedAssetIds([]);
            } else {
                for (const courseId of addedCourseIds) {
                    await mediaLibraryApi.assignMediaToCourse(courseId, courseAssignTarget.id);
                }
                if (removedCourseIds && removedCourseIds.length > 0) {
                    for (const courseId of removedCourseIds) {
                        await mediaLibraryApi.removeMediaFromCourse(courseId, courseAssignTarget.id);
                    }
                }
                success(`Video kurslara tanımlandı.`);
            }
            setIsCourseModalOpen(false);
            setCourseAssignTarget(null);
            setInitialCourseIds([]);
        } catch (error) {
            toastError("Hata", "Kurslara tanımlanırken bir hata oluştu.");
        }
    };

    const toggleAsset = (id: string) => {
        setSelectedAssetIds(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]);
    };

    const toggleAll = () => {
        if (selectedAssetIds.length === paginatedAssets.length) {
            setSelectedAssetIds([]);
        } else {
            setSelectedAssetIds(paginatedAssets.map(a => a.id));
        }
    };

    const handleBulkDelete = async () => {
        setConfirmModal({
            isOpen: true,
            title: "Toplu Silme",
            message: `Seçilen ${selectedAssetIds.length} videoyu silmek istediğinize emin misiniz? Bu işlem geri alınamaz.`,
            onConfirm: async () => {
                setConfirmModal(prev => ({...prev, isOpen: false}));
                try {
                    for (const id of selectedAssetIds) {
                        await mediaLibraryApi.deleteAsset(id);
                    }
                    success("Seçili videolar silindi");
                    setSelectedAssetIds([]);
                    loadData();
                } catch (error) {
                    toastError("Hata", "Bazı videolar silinemedi");
                }
            }
        });
    };

    const handleBulkMoveSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            for (const id of selectedAssetIds) {
                await mediaLibraryApi.updateAsset(id, { folderId: bulkMoveFolderId });
            }
            success("Videolar taşındı");
            setIsBulkMoveModalOpen(false);
            setSelectedAssetIds([]);
            triggerTreeRefresh();
            loadData();
        } catch (error) {
            toastError("Hata", "Videolar taşınamadı");
        }
    };

    const filteredFolders = folders.filter(f => f.name.toLowerCase().includes(searchQuery.toLowerCase()));
    
    const filteredAssets = [...assets]
        .filter(a => a.title.toLowerCase().includes(searchQuery.toLowerCase()))
        .sort((a, b) => {
            switch (sortBy) {
                case "newest":
                    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
                case "oldest":
                    return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
                case "az":
                    return a.title.localeCompare(b.title);
                case "za":
                    return b.title.localeCompare(a.title);
                case "longest":
                    return (b.durationSeconds || 0) - (a.durationSeconds || 0);
                case "shortest":
                    return (a.durationSeconds || 0) - (b.durationSeconds || 0);
                default:
                    return 0;
            }
        });

    const paginatedAssets = pageSize === "all" 
        ? filteredAssets 
        : filteredAssets.slice((currentPage - 1) * pageSize, currentPage * pageSize);
    const totalPages = pageSize === "all" ? 1 : Math.ceil(filteredAssets.length / pageSize);

    return (
        <div className="max-w-[1600px] mx-auto min-h-[calc(100vh-64px)] md:h-[calc(100vh-64px)] flex flex-col md:flex-row md:overflow-hidden bg-white">
            {/* Left Pane (30%): Folder Tree */}
            <div className="w-full md:w-[30%] md:min-w-[250px] md:max-w-[400px] h-64 md:h-full flex-shrink-0 border-b md:border-b-0 md:border-r border-gray-200 flex flex-col">
                <FolderTree 
                    activeFolderId={currentFolderId}
                    onSelect={handleTreeSelect}
                    onAction={handleTreeAction}
                    refreshTrigger={refreshTrigger}
                    dragOverFolderId={dragOverFolderId}
                    onDragOverFolder={handleDragOverFolder}
                    onDragLeaveFolder={handleDragLeaveFolder}
                    onDropOnFolder={handleDropOnFolder}
                />
            </div>

            {/* Right Pane (70%): Content */}
            <div className="flex-1 bg-gray-50 flex flex-col md:h-full overflow-visible md:overflow-hidden p-4 md:p-6">
                
                {/* Header Section */}

                {/* Header Section & Toolbar */}
                <div className="flex flex-col 2xl:flex-row justify-between items-start 2xl:items-center mb-6 gap-4 bg-white p-4 rounded-2xl border border-gray-200 shadow-sm">
                    {/* Breadcrumbs & Title */}
                    <div className="flex-1 min-w-0">
                        <div className="flex items-center flex-wrap gap-1 text-xs font-medium text-gray-500 mb-1">
                            {breadcrumbs.map((crumb, idx) => (
                                <div key={idx} className="flex items-center">
                                    <button 
                                        onClick={() => handleTreeSelect(crumb.id, breadcrumbs.slice(0, idx + 1))}
                                        className={`hover:text-blue-600 transition-colors ${idx === breadcrumbs.length - 1 ? 'text-gray-900 font-bold' : ''}`}
                                    >
                                        {crumb.name}
                                    </button>
                                    {idx < breadcrumbs.length - 1 && <ChevronRight size={14} className="mx-1 text-gray-400" />}
                                </div>
                            ))}
                        </div>
                        <h1 className="text-xl font-bold text-gray-900 tracking-tight truncate">
                            {currentFolderId ? breadcrumbs[breadcrumbs.length - 1]?.name : "Ana Klasör"}
                        </h1>
                    </div>

                    {/* Actions & Filters */}
                    <div className="flex flex-wrap items-center justify-start 2xl:justify-end gap-3 w-full 2xl:w-auto">
                        <div className="relative w-full sm:w-56">
                            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
                            <input
                                type="text"
                                placeholder="Videolarda ara..."
                                value={searchQuery}
                                onChange={(e) => setSearchQuery(e.target.value)}
                                className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 bg-gray-50 transition-all"
                            />
                        </div>

                        <select
                            value={sortBy}
                            onChange={(e) => setSortBy(e.target.value as any)}
                            className="px-3 py-2 text-sm border border-gray-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 bg-gray-50 text-gray-700 font-medium transition-all"
                        >
                            <option value="newest">En Yeni</option>
                            <option value="oldest">En Eski</option>
                            <option value="az">A'dan Z'ye</option>
                            <option value="za">Z'den A'ya</option>
                            <option value="longest">En Uzun</option>
                            <option value="shortest">En Kısa</option>
                        </select>

                        <select
                            value={pageSize}
                            onChange={(e) => setPageSize(e.target.value === "all" ? "all" : Number(e.target.value))}
                            className="px-3 py-2 text-sm border border-gray-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 bg-gray-50 text-gray-700 font-medium transition-all"
                        >
                            <option value={24}>24 Göster</option>
                            <option value={36}>36 Göster</option>
                            <option value={48}>48 Göster</option>
                            <option value="all">Tümünü Göster</option>
                        </select>

                        <div className="h-9 w-px bg-gray-200 hidden sm:block" />

                        <div className="flex items-center p-1 bg-gray-100 rounded-xl">
                            <button onClick={() => setViewMode("grid")} className={`p-1.5 rounded-lg transition-all ${viewMode === "grid" ? "bg-white shadow-sm text-blue-600" : "text-gray-500 hover:text-gray-700"}`}><Grid size={16} /></button>
                            <button onClick={() => setViewMode("list")} className={`p-1.5 rounded-lg transition-all ${viewMode === "list" ? "bg-white shadow-sm text-blue-600" : "text-gray-500 hover:text-gray-700"}`}><ListIcon size={16} /></button>
                        </div>

                        <div className="flex gap-2 w-full sm:w-auto mt-2 sm:mt-0">
                            <button 
                                onClick={() => setIsCreateFolderModalOpen(true)}
                                className="flex-1 sm:flex-none flex items-center justify-center gap-2 px-4 py-2 bg-gray-50 hover:bg-gray-100 text-gray-700 border border-gray-200 rounded-xl text-sm font-medium transition-all"
                            >
                                <FolderPlus size={16} /> <span className="hidden sm:inline">Yeni Klasör</span>
                            </button>
                            <button 
                                onClick={() => setIsUploadModalOpen(true)}
                                className="flex-1 sm:flex-none flex items-center justify-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-xl text-sm font-medium transition-all shadow-sm"
                            >
                                <UploadCloud size={16} /> <span className="hidden sm:inline">Video Yükle</span>
                            </button>
                        </div>
                    </div>
                </div>

                {/* Content Area */}
                {loading ? (
                    <div className="flex-1 flex justify-center items-center py-10 md:py-0">
                        <div className="w-10 h-10 border-4 border-blue-600 border-t-transparent rounded-full animate-spin" />
                    </div>
                ) : (
                    <div className="flex-1 md:overflow-y-auto min-h-0 pb-12 pr-1">

                        {filteredAssets.length > 0 && (
                            <div>
                                <h2 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3 px-1">Videolar</h2>
                                {viewMode === "list" ? (
                                    <div className="bg-white border border-gray-200 rounded-2xl overflow-x-auto shadow-sm">
                                        <table className="w-full text-left border-collapse min-w-[600px]">
                                            <thead>
                                                <tr className="bg-gray-50/80 border-b border-gray-200 text-xs font-semibold text-gray-500 uppercase tracking-wider">
                                                    <th className="px-5 py-3 w-10">
                                                        <input type="checkbox" onChange={toggleAll} checked={selectedAssetIds.length === paginatedAssets.length && paginatedAssets.length > 0} className="w-4 h-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500" />
                                                    </th>
                                                    <th className="px-5 py-3 font-medium">Video Adı</th>
                                                    <th className="px-5 py-3 font-medium w-32">Durum</th>
                                                    <th className="px-5 py-3 font-medium w-24">Süre</th>
                                                    <th className="px-5 py-3 font-medium text-right w-48">İşlemler</th>
                                                </tr>
                                            </thead>
                                            <tbody className="divide-y divide-gray-100">
                                                {paginatedAssets.map(asset => (
                                                    <tr 
                                                        key={asset.id} 
                                                        draggable
                                                        onDragStart={(e) => handleDragStartAsset(e, asset.id)}
                                                        className="hover:bg-blue-50/30 transition-colors group cursor-move"
                                                    >
                                                        <td className="px-5 py-3 text-center">
                                                            <input type="checkbox" checked={selectedAssetIds.includes(asset.id)} onChange={() => toggleAsset(asset.id)} className="w-4 h-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500 cursor-pointer" />
                                                        </td>
                                                        <td className="px-5 py-3">
                                                            <div className="flex items-center gap-3 min-w-0">
                                                                <div 
                                                                    className="w-14 h-9 rounded bg-gray-900 relative overflow-hidden shrink-0 cursor-pointer group-hover:ring-2 ring-blue-400 transition-all"
                                                                    onClick={() => setPlayingAsset(asset)}
                                                                >
                                                                    <FallbackImage src={asset.thumbnailPath || ""} />
                                                                    <div className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 bg-black/30 transition-opacity">
                                                                        <Play size={12} className="text-white fill-white translate-x-0.5" />
                                                                    </div>
                                                                </div>
                                                                <div className="flex flex-col min-w-0">
                                                                    <span className="font-semibold text-gray-900 text-sm truncate" title={asset.title}>{asset.title}</span>
                                                                    {asset.tags && (
                                                                        <div className="flex flex-wrap gap-1 mt-1">
                                                                            {asset.tags.split(',').map(t => t.trim()).filter(Boolean).map((t, idx) => (
                                                                                <span key={idx} className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-purple-50 text-purple-700 border border-purple-100">#{t}</span>
                                                                            ))}
                                                                        </div>
                                                                    )}
                                                                </div>
                                                            </div>
                                                        </td>
                                                        <td className="px-5 py-3">
                                                            <span className={`inline-flex items-center gap-1.5 px-2 py-1 rounded-md text-[11px] font-medium ${asset.status === 'Ready' ? 'bg-green-50 text-green-700 border border-green-100' : 'bg-amber-50 text-amber-700 border border-amber-100 animate-pulse'}`}>
                                                                <div className={`w-1.5 h-1.5 rounded-full ${asset.status === 'Ready' ? 'bg-green-500' : 'bg-amber-500'}`} />
                                                                {asset.status === 'Ready' ? 'Hazır' : asset.status === 'Uploading' ? 'Yükleniyor' : asset.status === 'Processing' ? 'İşleniyor' : 'Hata'}
                                                            </span>
                                                        </td>
                                                        <td className="px-5 py-3 text-sm text-gray-500 font-medium">
                                                            {asset.durationSeconds != null ? `${Math.floor(asset.durationSeconds / 60)}:${String(asset.durationSeconds % 60).padStart(2, '0')}` : '-'}
                                                        </td>
                                                        <td className="px-5 py-3 text-right">
                                                            <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-opacity">

                                                                <button onClick={() => { setRenameTarget({ id: asset.id, type: 'asset', currentName: asset.title, currentTags: asset.tags || "" }); setNewName(asset.title); setNewTags(asset.tags || ""); setIsRenameModalOpen(true); }} className="p-1.5 text-gray-400 hover:text-gray-700 hover:bg-gray-100 rounded-lg" title="Düzenle"><Edit2 size={16} /></button>
                                                                <button onClick={() => handleOpenAssetCourseModal(asset)} className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg" title="Derse Tanımla"><BookOpen size={16} /></button>
                                                                <button onClick={() => handleRemoveAsset(asset)} className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg" title="Sil"><Trash2 size={16} /></button>
                                                            </div>
                                                        </td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    </div>
                                ) : (
                                    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3 md:gap-4">
                                        {paginatedAssets.map(asset => (
                                            <div 
                                                key={asset.id}
                                                draggable
                                                onDragStart={(e) => handleDragStartAsset(e, asset.id)}
                                                className="group bg-white rounded-2xl border border-gray-200 overflow-hidden hover:border-blue-400 hover:shadow-lg transition-all cursor-move flex flex-col"
                                            >
                                                <div 
                                                    className="aspect-video w-full bg-gray-900 relative group-hover:opacity-90 transition-opacity cursor-pointer"
                                                    onClick={() => setPlayingAsset(asset)}
                                                >
                                                    <div className={`absolute top-2 left-2 z-10 transition-opacity ${selectedAssetIds.includes(asset.id) ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'}`} onClick={e => e.stopPropagation()}>
                                                        <input type="checkbox" checked={selectedAssetIds.includes(asset.id)} onChange={() => toggleAsset(asset.id)} className="w-5 h-5 rounded border-white shadow bg-white/50 text-blue-600 focus:ring-blue-500 cursor-pointer" />
                                                    </div>
                                                    <FallbackImage src={asset.thumbnailPath || ""} />
                                                                    {asset.durationSeconds != null && (
                                                                        <div className="absolute bottom-2 right-2 px-1.5 py-0.5 bg-black/80 text-white text-[10px] font-medium rounded-md backdrop-blur-sm">
                                                                            {Math.floor(asset.durationSeconds / 60)}:{String(asset.durationSeconds % 60).padStart(2, '0')}
                                                                        </div>
                                                                    )}
                                                    <div className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-all bg-black/30">
                                                        <div className="w-12 h-12 bg-white/95 rounded-full flex items-center justify-center shadow-2xl transform scale-90 group-hover:scale-100 transition-transform">
                                                            <Play className="w-5 h-5 text-blue-600 translate-x-0.5" fill="currentColor" />
                                                        </div>
                                                    </div>
                                                </div>
                                                <div className="p-2.5 md:p-4 flex-1 min-w-0 flex flex-col items-start gap-2 md:gap-3">
                                                    <div className="w-full min-w-0">
                                                        <h3 className="text-xs md:text-sm font-semibold text-gray-900 truncate" title={asset.title}>{asset.title}</h3>
                                                        <div className="flex items-center gap-2 mt-1">
                                                            <div className={`w-1.5 h-1.5 rounded-full ${asset.status === 'Ready' ? 'bg-green-500' : 'bg-amber-500 animate-pulse'}`} />
                                                            <p className="text-[11px] text-gray-500 font-medium">
                                                                {asset.status === 'Ready' ? 'Hazır' : 
                                                                 asset.status === 'Uploading' ? 'Yükleniyor' : 
                                                                 asset.status === 'Processing' ? 'İşleniyor' : 
                                                                 'Hata'}
                                                            </p>
                                                        </div>
                                                        {asset.tags && (
                                                            <div className="flex flex-wrap gap-1 mt-2">
                                                                {asset.tags.split(',').map(t => t.trim()).filter(Boolean).map((t, idx) => (
                                                                    <span key={idx} className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-purple-50 text-purple-700 border border-purple-100">#{t}</span>
                                                                ))}
                                                            </div>
                                                        )}
                                                    </div>
                                                    <div className="flex gap-0.5 md:gap-1 w-full justify-end border-t border-gray-50 pt-2 md:pt-3">
                                                        <button onClick={() => { setRenameTarget({ id: asset.id, type: 'asset', currentName: asset.title, currentTags: asset.tags || "" }); setNewName(asset.title); setNewTags(asset.tags || ""); setIsRenameModalOpen(true); }} className="p-1.5 md:p-2 text-gray-400 hover:text-gray-700 hover:bg-gray-100 rounded-lg" title="Düzenle"><Edit2 size={14} className="md:w-4 md:h-4" /></button>
                                                        <button onClick={() => handleOpenAssetCourseModal(asset)} className="p-1.5 md:p-2 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg" title="Derse Tanımla"><BookOpen size={14} className="md:w-4 md:h-4" /></button>
                                                        <button onClick={() => handleRemoveAsset(asset)} className="p-1.5 md:p-2 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg" title="Sil"><Trash2 size={14} className="md:w-4 md:h-4" /></button>
                                                    </div>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                )}
                                
                                {/* Pagination Controls */}
                                {totalPages > 1 && (
                                    <div className="mt-8 flex items-center justify-between border-t border-gray-200 pt-4 px-2">
                                        <div className="text-sm text-gray-500">
                                            Toplam <span className="font-medium text-gray-900">{filteredAssets.length}</span> videodan{' '}
                                            <span className="font-medium text-gray-900">{(currentPage - 1) * (pageSize as number) + 1}</span> -{' '}
                                            <span className="font-medium text-gray-900">{Math.min(currentPage * (pageSize as number), filteredAssets.length)}</span> arası gösteriliyor.
                                        </div>
                                        <div className="flex gap-1">
                                            <button
                                                onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                                                disabled={currentPage === 1}
                                                className="px-3 py-1.5 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                                            >
                                                Önceki
                                            </button>
                                            <div className="flex items-center gap-1 mx-2">
                                                {Array.from({ length: totalPages }, (_, i) => i + 1).map(page => (
                                                    <button
                                                        key={page}
                                                        onClick={() => setCurrentPage(page)}
                                                        className={`w-8 h-8 flex items-center justify-center text-sm font-medium rounded-lg ${
                                                            currentPage === page 
                                                                ? 'bg-blue-600 text-white' 
                                                                : 'text-gray-700 hover:bg-gray-100'
                                                        }`}
                                                    >
                                                        {page}
                                                    </button>
                                                ))}
                                            </div>
                                            <button
                                                onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                                                disabled={currentPage === totalPages}
                                                className="px-3 py-1.5 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                                            >
                                                Sonraki
                                            </button>
                                        </div>
                                    </div>
                                )}
                            </div>
                        )}

                        {filteredAssets.length === 0 && (
                            <div className="flex flex-col items-center justify-center py-24 text-center">
                                <div className="w-20 h-20 rounded-full bg-blue-50 flex items-center justify-center mb-6">
                                    <FolderPlus size={32} className="text-blue-600" />
                                </div>
                                <h3 className="text-xl font-bold text-gray-900 mb-2">Burası boş görünüyor</h3>
                                <p className="text-gray-500 max-w-sm mx-auto mb-8">
                                    İçeriklerinizi düzenlemeye başlamak için sol taraftan yeni bir klasör oluşturun veya ilk videonuzu yükleyin.
                                </p>
                            </div>
                        )}
                    </div>
                )}
            </div>

            {/* Create Folder Modal */}
            {isCreateFolderModalOpen && (
                <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
                    <div className="bg-white border border-gray-200 rounded-2xl w-full max-w-md p-6 shadow-2xl animate-in fade-in zoom-in-95 duration-200">
                        <h2 className="text-xl font-bold text-gray-900 mb-4">Yeni Klasör Oluştur</h2>
                        <form onSubmit={handleCreateFolder}>
                            <div className="mb-6">
                                <label className="block text-sm font-medium text-gray-700 mb-2">Klasör Adı</label>
                                <input
                                    type="text"
                                    required
                                    autoFocus
                                    value={newFolderName}
                                    onChange={(e) => setNewFolderName(e.target.value)}
                                    className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-gray-900 focus:outline-none focus:bg-white focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all"
                                    placeholder="Örn: 2026 Matematik Videoları"
                                />
                            </div>
                            <div className="flex justify-end gap-3">
                                <button
                                    type="button"
                                    onClick={() => setIsCreateFolderModalOpen(false)}
                                    className="px-5 py-2.5 rounded-xl text-gray-500 hover:text-gray-900 hover:bg-gray-100 font-medium transition-colors"
                                >
                                    İptal
                                </button>
                                <button
                                    type="submit"
                                    disabled={!newFolderName.trim()}
                                    className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-medium disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-sm"
                                >
                                    Oluştur
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
            {/* Rename Modal */}
            {isRenameModalOpen && renameTarget && (
                <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
                    <div className="bg-white border border-gray-200 rounded-2xl w-full max-w-md p-6 shadow-2xl animate-in fade-in zoom-in-95 duration-200">
                        <h2 className="text-xl font-bold text-gray-900 mb-4">{renameTarget.type === 'folder' ? 'Klasörü Yeniden Adlandır' : 'Videoyu Düzenle'}</h2>
                        <form onSubmit={handleRenameSubmit}>
                            <div className="mb-4">
                                <label className="block text-sm font-medium text-gray-700 mb-2">Ad</label>
                                <input
                                    type="text"
                                    required
                                    autoFocus
                                    value={newName}
                                    onChange={(e) => setNewName(e.target.value)}
                                    className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-gray-900 focus:outline-none focus:bg-white focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all"
                                />
                            </div>
                            {renameTarget.type === 'asset' && (
                                <div className="mb-6">
                                    <label className="block text-sm font-medium text-gray-700 mb-2">Etiketler (Virgülle Ayırın)</label>
                                    <input
                                        type="text"
                                        value={newTags}
                                        onChange={(e) => setNewTags(e.target.value)}
                                        className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-gray-900 focus:outline-none focus:bg-white focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all"
                                        placeholder="Örn: matematik, 12.sınıf, türev"
                                    />
                                </div>
                            )}
                            <div className="flex justify-end gap-3">
                                <button
                                    type="button"
                                    onClick={() => setIsRenameModalOpen(false)}
                                    className="px-5 py-2.5 rounded-xl text-gray-500 hover:text-gray-900 hover:bg-gray-100 font-medium transition-colors"
                                >
                                    İptal
                                </button>
                                <button
                                    type="submit"
                                    disabled={!newName.trim() || (newName.trim() === renameTarget.currentName && newTags.trim() === (renameTarget.currentTags || ""))}
                                    className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-medium disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-sm"
                                >
                                    Kaydet
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {isUploadModalOpen && (
                <VideoUploaderModal 
                    isOpen={true}
                    folderId={currentFolderId || undefined}
                    onClose={() => setIsUploadModalOpen(false)}
                    onSuccess={() => loadData()}
                />
            )}

            {/* Course Selector Modal */}
            {isCourseModalOpen && (
                <CourseSelectorModal 
                    initialSelectedCourseIds={initialCourseIds}
                    onClose={() => {
                        setIsCourseModalOpen(false);
                        setCourseAssignTarget(null);
                        setInitialCourseIds([]);
                    }}
                    onSelect={handleAssignToCourse}
                />
            )}

            {/* Bulk Move Modal */}
            {isBulkMoveModalOpen && (
                <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
                    <div className="bg-white border border-gray-200 rounded-2xl w-full max-w-md p-6 shadow-2xl animate-in fade-in zoom-in-95 duration-200">
                        <h2 className="text-xl font-bold text-gray-900 mb-4">Videoları Taşı</h2>
                        <form onSubmit={handleBulkMoveSubmit}>
                            <div className="mb-6">
                                <label className="block text-sm font-medium text-gray-700 mb-2">Hedef Klasör</label>
                                <select
                                    value={bulkMoveFolderId || ""}
                                    onChange={(e) => setBulkMoveFolderId(e.target.value || null)}
                                    className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-gray-900 focus:outline-none focus:bg-white focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all"
                                >
                                    <option value="">Kök Dizin (Ana Klasör)</option>
                                    {folders.map(f => (
                                        <option key={f.id} value={f.id}>{f.name}</option>
                                    ))}
                                </select>
                            </div>
                            <div className="flex justify-end gap-3">
                                <button type="button" onClick={() => setIsBulkMoveModalOpen(false)} className="px-5 py-2.5 rounded-xl text-gray-500 hover:text-gray-900 hover:bg-gray-100 font-medium transition-colors">İptal</button>
                                <button type="submit" className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-medium transition-colors shadow-sm">Taşı</button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Bulk Action Bar */}
            {selectedAssetIds.length > 0 && (
                <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 bg-gray-900 text-white px-6 py-3 rounded-full shadow-2xl flex items-center gap-4 animate-in slide-in-from-bottom-5">
                    <span className="font-medium text-sm">{selectedAssetIds.length} öğe seçildi</span>
                    <div className="h-4 w-px bg-gray-700" />
                    <button onClick={() => { setCourseAssignTarget({ id: '', type: 'bulk', bulkIds: selectedAssetIds }); setIsCourseModalOpen(true); }} className="text-sm font-medium hover:text-blue-400 transition-colors">Derse Tanımla</button>
                    <button onClick={() => setIsBulkMoveModalOpen(true)} className="text-sm font-medium hover:text-blue-400 transition-colors">Taşı</button>
                    <button onClick={handleBulkDelete} className="text-sm font-bold text-red-500 hover:text-red-400 transition-colors">Sil</button>
                    <div className="h-4 w-px bg-gray-700" />
                    <button onClick={() => setSelectedAssetIds([])} className="text-sm font-medium text-gray-400 hover:text-white transition-colors">İptal</button>
                </div>
            )}

            {/* Confirm Modal */}
            <ConfirmModal 
                isOpen={confirmModal.isOpen}
                title={confirmModal.title}
                message={confirmModal.message}
                onConfirm={confirmModal.onConfirm}
                onCancel={() => setConfirmModal(prev => ({...prev, isOpen: false}))}
                type="danger"
            />

            {/* Video Player Modal */}
            <VideoPlayerModal 
                asset={playingAsset}
                onClose={() => setPlayingAsset(null)}
            />
        </div>
    );
}

