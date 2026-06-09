"use client";

import React, { createContext, useContext, useState, ReactNode } from 'react';
import { uploadApi } from '@/lib/api/upload';
import { courseApi } from '@/lib/api/courses';
import { mediaLibraryApi } from '@/lib/api/mediaLibrary';
import { useAuth } from '@/contexts/AuthContext';
import { UploadCloud, CheckCircle2, XCircle, X, ChevronUp, ChevronDown, Loader2 } from 'lucide-react';

interface UploadTask {
    id: string;
    file: File;
    title: string;
    progress: number;
    status: 'pending' | 'uploading' | 'processing' | 'success' | 'error';
    courseId: string | null;
    folderId?: string;
    error?: string;
    assetId?: string;
    assetStatus?: string;
    transcodeProgress?: number;
    speed?: number;
    etaSeconds?: number;
}

interface GlobalUploadContextType {
    uploads: UploadTask[];
    startUpload: (courseId: string | null, title: string, file: File, durationSeconds: number, folderId?: string) => void;
    removeUpload: (id: string) => void;
    clearCompleted: () => void;
}

const GlobalUploadContext = createContext<GlobalUploadContextType | undefined>(undefined);

function formatEta(seconds: number | undefined): string {
    if (!seconds || seconds <= 0) return '';
    if (seconds < 60) return `~${Math.round(seconds)} sn`;
    const m = Math.floor(seconds / 60);
    return `~${m} dk`;
}

export function GlobalUploadProvider({ children }: { children: ReactNode }) {
    const [uploads, setUploads] = useState<UploadTask[]>([]);
    const [isMinimized, setIsMinimized] = useState(false);
    const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null);
    const { currentTenantId, token } = useAuth();

    // Poll for asset status if there are any uploads in "success" state but not "Ready"
    const pollingIds = uploads
        .filter(u => u.status === 'success' && u.assetId && u.assetStatus !== 'Ready' && u.assetStatus !== 'Failed')
        .map(u => u.assetId!)
        .join(',');

    React.useEffect(() => {
        if (!pollingIds) return;

        let tick = 0;
        const interval = setInterval(async () => {
            tick++;
            const idsToPoll = pollingIds.split(',');

            // Her turda SADECE progress (tek toplu istek)
            try {
                const progressDict = await mediaLibraryApi.getTranscodeProgress(idsToPoll);
                setUploads(prev => prev.map(t => {
                    if (t.assetId && progressDict[t.assetId] !== undefined) {
                        const d: any = progressDict[t.assetId];
                        return { ...t, transcodeProgress: (d.percentage ?? d.Percentage ?? 0), speed: (d.speed ?? d.Speed ?? 0), etaSeconds: (d.etaSeconds ?? d.EtaSeconds ?? 0) };
                    }
                    return t;
                }));
            } catch (e) {
                console.error("Failed to fetch transcode progress", e);
            }

            // Durum kontrolü: Her 10 saniyede bir (2 turda bir), BÜTÜN bekleyen assetlerin durumunu kontrol et
            if (tick % 2 === 0) {
                for (const assetId of idsToPoll) {
                    try {
                        const asset = await mediaLibraryApi.getAsset(assetId);
                        setUploads(prev => prev.map(t => {
                            if (t.assetId === assetId && t.assetStatus !== asset.status) {
                                return { 
                                    ...t, 
                                    assetStatus: asset.status,
                                    status: asset.status === 'Ready' ? 'success' : t.status
                                };
                            }
                            return t;
                        }));
                    } catch (e) { /* sessiz */ }
                }
            }
        }, 5000);

        return () => clearInterval(interval);
    }, [pollingIds]);

    const startUpload = async (courseId: string | null, title: string, file: File, durationSeconds: number, folderId?: string) => {
        const id = Math.random().toString(36).substring(7);
        const newTask: UploadTask = { id, file, title, progress: 0, status: 'pending', courseId, folderId };
        
        setUploads(prev => [...prev, newTask]);
        setIsMinimized(false);

        try {
            if (!token) throw new Error("Oturum süresi dolmuş veya geçersiz.");

            setUploads(prev => prev.map(t => t.id === id ? { ...t, status: 'uploading' } : t));
            
            // 1. Get presigned URL
            const presigned = await uploadApi.getPresignedUrl(token, currentTenantId ?? "", file.name, file.type);

            // 2. Upload file directly to BunnyCDN
            await uploadApi.uploadMediaWithProgress(presigned.uploadUrl, file, (progress, etaSeconds) => {
                setUploads(prev => prev.map(t => t.id === id ? { ...t, progress, etaSeconds } : t));
            });

            setUploads(prev => prev.map(t => t.id === id ? { ...t, status: 'processing', progress: 100 } : t));

            // 3. Create Media Asset in Database
            const asset = await mediaLibraryApi.createAsset({
                title: title,
                type: 'Video',
                filePath: presigned.publicUrl,
                durationSeconds: durationSeconds,
                folderId: folderId || null
            });

            // 4. Assign to course if courseId is provided
            if (courseId) {
                await mediaLibraryApi.assignMediaToCourse(courseId, asset.id);
            }

            setUploads(prev => prev.map(t => t.id === id ? { ...t, status: 'success', assetId: asset.id, assetStatus: asset.status } : t));
        } catch (error: any) {
            console.error("Upload failed:", error);
            const errorMessage = error instanceof Error ? error.message : "Yükleme başarısız oldu.";
            setUploads(prev => prev.map(t => t.id === id ? { ...t, status: 'error', error: errorMessage } : t));
        }
    };

    const removeUpload = (id: string) => setUploads(prev => prev.filter(t => t.id !== id));
    const clearCompleted = () => setUploads(prev => prev.filter(t => t.status !== 'success' && t.status !== 'error'));

    const activeUploadsCount = uploads.filter(u => 
        u.status === 'uploading' || 
        u.status === 'pending' || 
        u.status === 'processing' || 
        (u.status === 'success' && u.assetStatus !== 'Ready' && u.assetStatus !== 'Failed')
    ).length;
    
    const completedUploadsCount = uploads.filter(u => 
        u.status === 'success' && (u.assetStatus === 'Ready' || u.assetStatus === 'Failed')
    ).length;
    
    const totalUploadsCount = uploads.length;
    const formatDuration = (sec: number) => {
        const h = Math.floor(sec / 3600);
        const m = Math.floor((sec % 3600) / 60);
        const s = sec % 60;
        if (h > 0) return `${h}s ${m}d`;
        if (m > 0) return `${m}d ${s}sn`;
        return `${s} saniye`;
    };

    const totalEtaSeconds = uploads.reduce((acc, u) => acc + (u.etaSeconds || 0), 0);

    return (
        <GlobalUploadContext.Provider value={{ uploads, startUpload, removeUpload, clearCompleted }}>
            {children}
            {totalUploadsCount > 0 && (
                <div className={`fixed bottom-6 right-6 z-[9999] w-[420px] bg-white dark:bg-[#09090B] border border-gray-200 dark:border-white/10 rounded-2xl shadow-2xl transition-all duration-300 flex flex-col overflow-hidden ${isMinimized ? 'h-[64px]' : 'h-auto max-h-[400px]'}`}>
                    
                    {/* Header */}
                    <div 
                        className="h-[64px] px-5 py-3 flex items-center justify-between cursor-pointer hover:bg-gray-50 dark:hover:bg-white/5 transition-colors border-b border-gray-100 dark:border-white/5 shrink-0"
                        onClick={() => setIsMinimized(!isMinimized)}
                    >
                        <div className="flex items-center gap-3">
                            <div className="p-2 bg-blue-50 dark:bg-blue-500/10 rounded-lg text-blue-600 dark:text-blue-400">
                                {activeUploadsCount > 0 ? <Loader2 className="w-5 h-5 animate-spin" /> : <CheckCircle2 className="w-5 h-5 text-green-500" />}
                            </div>
                            <div>
                                <h4 className="text-sm font-semibold text-gray-900 dark:text-white">
                                    {activeUploadsCount > 0 ? `${totalUploadsCount} öğeden ${completedUploadsCount}'si hazır` : `${completedUploadsCount} öğe hazır`}
                                </h4>
                                <p className="text-[11px] text-gray-500 dark:text-[#A0AEC0]">
                                    {activeUploadsCount > 0 ? `Arka planda işleniyor... • Kalan Süre: ${totalEtaSeconds > 0 ? formatEta(totalEtaSeconds) : 'Hesaplanıyor...'}` : 'Tüm işlemler tamamlandı'}
                                </p>
                            </div>
                        </div>
                        <div className="flex items-center gap-1">
                            <button className="p-1.5 text-gray-400 hover:text-gray-900 dark:hover:text-white rounded-lg transition-colors">
                                {isMinimized ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
                            </button>
                            <button onClick={(e) => { e.stopPropagation(); setUploads([]); }} className="p-1.5 text-gray-400 hover:text-red-500 rounded-lg transition-colors">
                                <X className="w-4 h-4" />
                            </button>
                        </div>
                    </div>

                    {/* Body */}
                    {!isMinimized && (
                        <div className="p-2 overflow-y-auto flex-1 space-y-1 scrollbar-thin scrollbar-thumb-gray-200 dark:scrollbar-thumb-white/10">
                            {uploads.map(upload => (
                                <div key={upload.id} className="p-3 bg-gray-50 dark:bg-white/5 rounded-xl border border-transparent dark:border-white/5 flex items-center justify-between group hover:border-gray-200 dark:hover:border-white/10 transition-colors">
                                    <div className="flex items-center gap-3 min-w-0 flex-1">
                                        {upload.status === 'pending' && <UploadCloud className="w-5 h-5 text-gray-400" />}
                                        {upload.status === 'uploading' && <Loader2 className="w-5 h-5 text-blue-500 animate-spin" />}
                                        {upload.status === 'processing' && <Loader2 className="w-5 h-5 text-purple-500 animate-spin" />}
                                        {upload.status === 'success' && <CheckCircle2 className="w-5 h-5 text-green-500" />}
                                        {upload.status === 'error' && <XCircle className="w-5 h-5 text-red-500 shrink-0 mt-0.5" />}
                                        
                                        <div className="min-w-0 flex-1">
                                            <p className={`text-[13px] font-medium truncate ${upload.status === 'error' || upload.assetStatus === 'Failed' ? 'text-red-600 dark:text-red-400' : 'text-gray-900 dark:text-white'}`}>{upload.title}</p>
                                            {upload.status === 'uploading' && (
                                                <div className="mt-1.5">
                                                    <div className="flex justify-between items-center mb-1">
                                                        <p className="text-[11px] text-blue-600 dark:text-blue-400 font-medium">Yükleniyor...</p>
                                                        <span className="flex items-center gap-1.5 tabular-nums">
                                                            {upload.etaSeconds !== undefined && upload.etaSeconds > 0 && <span className="text-[10px] text-blue-500/70">{formatEta(upload.etaSeconds)}</span>}
                                                            <span className="text-[10px] font-bold text-blue-600 dark:text-blue-400">%{upload.progress}</span>
                                                        </span>
                                                    </div>
                                                    <div className="h-1.5 w-full bg-blue-100 dark:bg-blue-900/30 rounded-full overflow-hidden">
                                                        <div className="h-full bg-blue-500 rounded-full transition-all duration-500 ease-out" style={{ width: `${upload.progress}%` }} />
                                                    </div>
                                                </div>
                                            )}
                                            {upload.status === 'processing' && <p className="text-[11px] text-purple-500 mt-0.5">Sisteme kaydediliyor...</p>}
                                            {upload.status === 'success' && upload.assetStatus !== 'Ready' && upload.assetStatus !== 'Failed' && (
                                                <div className="mt-1.5">
                                                    <div className="flex justify-between items-center mb-1">
                                                        <p className="text-[11px] text-purple-600 dark:text-purple-400 font-medium flex items-center gap-1">
                                                            <span className="relative flex h-1.5 w-1.5">
                                                                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-purple-400 opacity-75"></span>
                                                                <span className="relative inline-flex rounded-full h-1.5 w-1.5 bg-purple-500"></span>
                                                            </span>
                                                            {(upload.transcodeProgress ?? 0) > 0 ? 'GPU ile işleniyor' : 'Kuyrukta • başlıyor'}
                                                        </p>
                                                        {(upload.transcodeProgress ?? 0) > 0 && (
                                                            <span className="flex items-center gap-1.5 tabular-nums">
                                                                {upload.etaSeconds && upload.etaSeconds > 0 && <span className="text-[10px] text-purple-500/70">{formatEta(upload.etaSeconds)}</span>}
                                                                {upload.speed && upload.speed > 0 && <span className="text-[10px] text-gray-400 dark:text-gray-500">{upload.speed}x</span>}
                                                                <span className="text-[10px] font-bold text-purple-600 dark:text-purple-400">%{upload.transcodeProgress}</span>
                                                            </span>
                                                        )}
                                                    </div>
                                                    <div className="h-1.5 w-full bg-purple-100 dark:bg-purple-900/30 rounded-full overflow-hidden">
                                                        {(upload.transcodeProgress ?? 0) > 0 ? (
                                                            <div className="h-full bg-purple-500 rounded-full transition-all duration-700 ease-out" style={{ width: `${upload.transcodeProgress}%` }} />
                                                        ) : (
                                                            <div className="h-full w-1/3 bg-purple-500 rounded-full animate-[indeterminate_1.2s_ease-in-out_infinite]" />
                                                        )}
                                                    </div>
                                                </div>
                                            )}
                                            {upload.status === 'success' && upload.assetStatus === 'Failed' && (
                                                <div className="mt-1.5 p-2 bg-red-100 dark:bg-red-900/30 border border-red-200 dark:border-red-800/50 rounded-lg">
                                                    <p className="text-[11px] font-bold text-red-600 dark:text-red-400 mb-0.5">İşlem Başarısız</p>
                                                    <p className="text-[10px] text-red-500 dark:text-red-400 leading-relaxed">Video işlenirken bir sorun oluştu. Biçim desteklenmiyor veya dosya bozuk olabilir.</p>
                                                </div>
                                            )}
                                            {upload.status === 'success' && upload.assetStatus === 'Ready' && (
                                                <div className="mt-1.5">
                                                    <p className="text-[11px] text-green-600 dark:text-green-400 font-bold flex items-center gap-1.5">
                                                        İşlem Tamamlandı ve Yayına Hazır ✓
                                                    </p>
                                                </div>
                                            )}
                                            {upload.status === 'error' && (
                                                <div className="mt-1.5 p-2 bg-red-100 dark:bg-red-900/30 border border-red-200 dark:border-red-800/50 rounded-lg">
                                                    <p className="text-[11px] font-bold text-red-600 dark:text-red-400 mb-0.5">Yükleme Hatası</p>
                                                    <p className="text-[10px] text-red-500 dark:text-red-400 leading-relaxed">{upload.error || "Ağ bağlantısı koptu veya sunucu yanıt vermedi."}</p>
                                                </div>
                                            )}
                                        </div>
                                    </div>
                                    <button 
                                        onClick={() => {
                                            if (upload.status === 'uploading' || upload.status === 'processing' || (upload.status === 'success' && upload.assetStatus !== 'Ready' && upload.assetStatus !== 'Failed')) {
                                                setConfirmCancelId(upload.id);
                                            } else {
                                                removeUpload(upload.id);
                                            }
                                        }} 
                                        className="ml-3 p-1.5 text-gray-400 opacity-0 group-hover:opacity-100 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-500/10 rounded-lg transition-all shrink-0"
                                        title="Kaldır / İptal Et"
                                    >
                                        <X className="w-4 h-4" />
                                    </button>
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            )}
            {/* İptal Onay Modalı */}
            {confirmCancelId && (
                <div className="fixed inset-0 z-[10000] flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
                    <div className="bg-white dark:bg-[#09090B] border border-gray-200 dark:border-white/10 rounded-2xl w-full max-w-sm p-6 shadow-2xl animate-in fade-in zoom-in-95 duration-200">
                        <h2 className="text-lg font-bold text-gray-900 dark:text-white mb-2">İşlemi İptal Et</h2>
                        <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">Bu video hala işleniyor veya yükleniyor. İptal edip listeden kaldırmak istediğinize emin misiniz?</p>
                        <div className="flex justify-end gap-3">
                            <button
                                onClick={() => setConfirmCancelId(null)}
                                className="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-white/10 rounded-xl hover:bg-gray-200 dark:hover:bg-white/20 transition-colors"
                            >
                                Hayır, Devam Etsin
                            </button>
                            <button
                                onClick={() => {
                                    removeUpload(confirmCancelId);
                                    setConfirmCancelId(null);
                                }}
                                className="px-4 py-2 text-sm font-medium text-white bg-red-600 rounded-xl hover:bg-red-700 transition-colors shadow-sm"
                            >
                                Evet, İptal Et
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </GlobalUploadContext.Provider>
    );
}

export function useGlobalUpload() {
    const context = useContext(GlobalUploadContext);
    if (context === undefined) {
        throw new Error('useGlobalUpload must be used within a GlobalUploadProvider');
    }
    return context;
}
