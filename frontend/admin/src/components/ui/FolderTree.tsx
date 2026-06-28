"use client";

import React, { useState, useEffect } from 'react';
import { ChevronRight, ChevronDown, Folder, Plus, Edit2, Trash2, BookOpen, Search, X } from 'lucide-react';
import { mediaLibraryApi, type MediaFolderDto } from '@/lib/api';
import { Tooltip } from '@/components/ui/Tooltip';

interface FolderNodeProps {
    folder: MediaFolderDto;
    level: number;
    activeFolderId: string | null;
    path: {id: string | null, name: string}[];
    onSelect: (folderId: string | null, path: {id: string | null, name: string}[]) => void;
    onAction: (action: 'rename' | 'delete' | 'assign' | 'createSub', folder: MediaFolderDto) => void;
    dragOverFolderId: string | null;
    onDragOverFolder: (e: React.DragEvent, folderId: string | null) => void;
    onDragLeaveFolder: (e: React.DragEvent) => void;
    onDropOnFolder: (e: React.DragEvent, folderId: string | null) => void;
    expandedFolderIds: Set<string>;
    onToggleExpand: (folderId: string, expand: boolean) => void;
}

const FolderNode: React.FC<FolderNodeProps> = ({ 
    folder, level, activeFolderId, path, onSelect, onAction, 
    dragOverFolderId, onDragOverFolder, onDragLeaveFolder, onDropOnFolder,
    expandedFolderIds, onToggleExpand
}) => {
    const [subFolders, setSubFolders] = useState<MediaFolderDto[]>([]);
    const [isLoading, setIsLoading] = useState(false);

    const isExpanded = expandedFolderIds.has(folder.id);
    const currentPath = [...path, { id: folder.id, name: folder.name }];

    const hasChildren = folder.subFolderCount > 0;
    const isActive = activeFolderId === folder.id;

    // Reactively fetch subfolders when expanded programmatically or manually
    useEffect(() => {
        if (isExpanded && subFolders.length === 0 && hasChildren && !isLoading) {
            setIsLoading(true);
            mediaLibraryApi.getFolders(folder.id)
                .then(fetched => {
                    setSubFolders(fetched);
                })
                .catch(err => {
                    console.error("Failed to load subfolders", err);
                })
                .finally(() => {
                    setIsLoading(false);
                });
        }
    }, [isExpanded, hasChildren]);

    const toggleExpand = (e: React.MouseEvent) => {
        e.stopPropagation();
        if (!hasChildren) return;
        onToggleExpand(folder.id, !isExpanded);
    };

    const handleSelect = () => {
        onSelect(folder.id, currentPath);
    };

    return (
        <div className="w-full">
            <div 
                className={`group flex items-center justify-between py-1.5 px-2 cursor-pointer rounded-lg transition-colors ${dragOverFolderId === folder.id ? 'bg-blue-100 ring-2 ring-blue-400' : isActive ? 'bg-blue-50 text-blue-700 font-medium' : 'hover:bg-gray-100 text-gray-700'}`}
                style={{ paddingLeft: `${level * 16 + 8}px` }}
                onClick={handleSelect}
                onDragOver={(e) => onDragOverFolder(e, folder.id)}
                onDragLeave={onDragLeaveFolder}
                onDrop={(e) => onDropOnFolder(e, folder.id)}
            >
                <div className="flex items-center gap-2 flex-1 min-w-0">
                    <button 
                        onClick={toggleExpand}
                        className={`p-0.5 rounded hover:bg-gray-200 transition-colors ${hasChildren ? 'visible' : 'invisible'} ${isActive ? 'hover:bg-blue-100' : ''}`}
                    >
                        {isLoading ? (
                            <div className="w-4 h-4 border-2 border-blue-600 border-t-transparent rounded-full animate-spin" />
                        ) : isExpanded ? (
                            <ChevronDown size={16} className={isActive ? 'text-blue-600' : 'text-gray-500'} />
                        ) : (
                            <ChevronRight size={16} className={isActive ? 'text-blue-600' : 'text-gray-500'} />
                        )}
                    </button>
                    <Folder size={16} className={isActive ? 'text-blue-600 shrink-0' : 'text-gray-400 shrink-0'} />
                    <Tooltip content={folder.name} className="min-w-0 flex-1" position="top">
                        <span className="text-[14.5px] font-semibold select-none block w-full break-words whitespace-normal leading-tight pr-2 py-0.5">{folder.name}</span>
                    </Tooltip>
                </div>

                {/* Inline Actions */}
                <div className="flex items-center gap-1 ml-auto shrink-0">
                    <Tooltip content="Alt Klasör Ekle">
                        <button 
                            onClick={(e) => { e.stopPropagation(); onAction('createSub', folder); }}
                            className="p-1 rounded-md text-gray-500 hover:text-blue-600 hover:bg-blue-100 transition-colors"
                        >
                            <Plus size={16} />
                        </button>
                    </Tooltip>
                    <Tooltip content="Yeniden Adlandır">
                        <button 
                            onClick={(e) => { e.stopPropagation(); onAction('rename', folder); }}
                            className="p-1 rounded-md text-gray-500 hover:text-blue-600 hover:bg-blue-100 transition-colors"
                        >
                            <Edit2 size={16} />
                        </button>
                    </Tooltip>
                    <Tooltip content="Derse Tanımla">
                        <button 
                            onClick={(e) => { e.stopPropagation(); onAction('assign', folder); }}
                            className="p-1 rounded-md text-gray-500 hover:text-blue-600 hover:bg-blue-100 transition-colors"
                        >
                            <BookOpen size={16} />
                        </button>
                    </Tooltip>
                    <Tooltip content="Sil">
                        <button 
                            onClick={(e) => { e.stopPropagation(); onAction('delete', folder); }}
                            className="p-1 rounded-md text-gray-500 hover:text-red-600 hover:bg-red-100 transition-colors"
                        >
                            <Trash2 size={16} />
                        </button>
                    </Tooltip>
                </div>
            </div>

            {isExpanded && subFolders.length > 0 && (
                <div className="flex flex-col">
                    {subFolders.map(sub => (
                        <FolderNode 
                            key={sub.id} 
                            folder={sub} 
                            level={level + 1} 
                            activeFolderId={activeFolderId} 
                            path={currentPath}
                            onSelect={onSelect}
                            onAction={onAction}
                            dragOverFolderId={dragOverFolderId}
                            onDragOverFolder={onDragOverFolder}
                            onDragLeaveFolder={onDragLeaveFolder}
                            onDropOnFolder={onDropOnFolder}
                            expandedFolderIds={expandedFolderIds}
                            onToggleExpand={onToggleExpand}
                        />
                    ))}
                </div>
            )}
        </div>
    );
};

const formatBytes = (bytes: number, decimals = 1) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
};

interface FolderTreeProps {
    activeFolderId: string | null;
    onSelect: (folderId: string | null, path: {id: string | null, name: string}[]) => void;
    onAction: (action: 'rename' | 'delete' | 'assign' | 'createSub', folder: MediaFolderDto | null) => void;
    refreshTrigger: number;
    dragOverFolderId: string | null;
    onDragOverFolder: (e: React.DragEvent, folderId: string | null) => void;
    onDragLeaveFolder: (e: React.DragEvent) => void;
    onDropOnFolder: (e: React.DragEvent, folderId: string | null) => void;
}

export function FolderTree({ activeFolderId, onSelect, onAction, refreshTrigger, dragOverFolderId, onDragOverFolder, onDragLeaveFolder, onDropOnFolder }: FolderTreeProps) {
    const [rootFolders, setRootFolders] = useState<MediaFolderDto[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [searchQuery, setSearchQuery] = useState('');
    const [debouncedSearchQuery, setDebouncedSearchQuery] = useState('');
    const [expandedFolderIds, setExpandedFolderIds] = useState<Set<string>>(new Set());
    const [diskUsage, setDiskUsage] = useState<{ totalSpace: number; freeSpace: number; usedSpace: number; percentage: number } | null>(null);

    useEffect(() => {
        mediaLibraryApi.getDiskUsage()
            .then(setDiskUsage)
            .catch(err => console.error("Failed to load disk usage", err));
    }, [refreshTrigger]);

    useEffect(() => {
        const handler = setTimeout(() => {
            setDebouncedSearchQuery(searchQuery);
        }, 300);
        return () => clearTimeout(handler);
    }, [searchQuery]);

    useEffect(() => {
        loadFolders();
    }, [refreshTrigger, debouncedSearchQuery]);

    const loadFolders = async () => {
        setIsLoading(true);
        try {
            const fetched = await mediaLibraryApi.getFolders(undefined, debouncedSearchQuery || undefined);
            setRootFolders(fetched);
        } catch (error) {
            console.error("Failed to load folders", error);
        } finally {
            setIsLoading(false);
        }
    };

    const handleToggleExpand = (folderId: string, expand: boolean) => {
        setExpandedFolderIds(prev => {
            const next = new Set(prev);
            if (expand) next.add(folderId);
            else next.delete(folderId);
            return next;
        });
    };

    return (
        <div className="w-full h-full flex flex-col bg-white border-r border-gray-200">
            {/* Header */}
            <div className="p-4 border-b border-gray-100 flex flex-col gap-3">
                <div className="flex items-center justify-between">
                    <h2 className="font-semibold text-gray-900">Klasörler</h2>
                    <Tooltip content="Yeni Klasör" position="bottom">
                        <button 
                            onClick={() => onAction('createSub', null)}
                            className="p-1.5 text-gray-500 hover:bg-gray-100 rounded-lg transition-colors"
                        >
                            <Plus size={18} />
                        </button>
                    </Tooltip>
                </div>
                
                {/* Search Bar */}
                <div className="relative">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
                    <input
                        type="text"
                        placeholder="Klasörlerde ara..."
                        value={searchQuery}
                        onChange={(e) => setSearchQuery(e.target.value)}
                        className="w-full pl-9 pr-8 py-1.5 text-xs border border-gray-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 bg-gray-50/50 transition-all"
                    />
                    {searchQuery && (
                        <button 
                            onClick={() => setSearchQuery('')}
                            className="absolute right-2.5 top-1/2 -translate-y-1/2 p-0.5 text-gray-400 hover:text-gray-600 rounded-full hover:bg-gray-200/50 transition-colors"
                        >
                            <X size={14} />
                        </button>
                    )}
                </div>
            </div>

            {/* Tree */}
            <div className="flex-1 overflow-y-auto p-2">
                {debouncedSearchQuery ? (
                    isLoading ? (
                        <div className="flex justify-center p-4">
                            <div className="w-5 h-5 border-2 border-blue-600 border-t-transparent rounded-full animate-spin" />
                        </div>
                    ) : (
                        <div className="flex flex-col gap-1">
                            <div className="text-[11px] font-medium text-gray-500 px-3 py-1 mb-1 bg-gray-50 rounded-lg">
                                Arama Sonuçları ({rootFolders.length})
                            </div>
                            {rootFolders.map(folder => {
                                const pathString = ['Ana Klasör', ...(folder.path || []).slice(0, -1).map(p => p.name)].join(' > ');
                                const isActive = activeFolderId === folder.id;
                                
                                return (
                                    <div 
                                        key={folder.id}
                                        onClick={() => {
                                            const selectPath = [
                                                { id: null, name: 'Ana Klasör' },
                                                ...(folder.path || []).map(p => ({ id: p.id, name: p.name }))
                                            ];
                                            
                                            // Expand all parent folders in the path
                                            setExpandedFolderIds(prev => {
                                                const next = new Set(prev);
                                                (folder.path || []).slice(0, -1).forEach(p => {
                                                    if (p.id) next.add(p.id);
                                                });
                                                return next;
                                            });

                                            // Clear search query to show tree
                                            setSearchQuery('');
                                            
                                            // Select folder
                                            onSelect(folder.id, selectPath);
                                        }}
                                        className={`group flex flex-col py-2 px-3 cursor-pointer rounded-lg transition-colors ${isActive ? 'bg-blue-50 text-blue-700 font-medium' : 'hover:bg-gray-100 text-gray-700'}`}
                                    >
                                        <div className="flex items-center gap-2 min-w-0">
                                            <Folder size={16} className={isActive ? 'text-blue-600 shrink-0' : 'text-gray-400 shrink-0'} />
                                            <span className="text-[14.5px] font-semibold select-none block flex-1 break-words whitespace-normal leading-tight pr-2 py-0.5">{folder.name}</span>
                                            
                                            <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity ml-auto shrink-0">
                                                <Tooltip content="Alt Klasör Ekle">
                                                    <button 
                                                        onClick={(e) => { e.stopPropagation(); onAction('createSub', folder); }}
                                                        className="p-1 rounded-md text-gray-500 hover:text-blue-600 hover:bg-blue-100 transition-colors"
                                                    >
                                                        <Plus size={14} />
                                                    </button>
                                                </Tooltip>
                                                <Tooltip content="Yeniden Adlandır">
                                                    <button 
                                                        onClick={(e) => { e.stopPropagation(); onAction('rename', folder); }}
                                                        className="p-1 rounded-md text-gray-500 hover:text-blue-600 hover:bg-blue-100 transition-colors"
                                                    >
                                                        <Edit2 size={14} />
                                                    </button>
                                                </Tooltip>
                                                <Tooltip content="Sil">
                                                    <button 
                                                        onClick={(e) => { e.stopPropagation(); onAction('delete', folder); }}
                                                        className="p-1 rounded-md text-gray-500 hover:text-red-600 hover:bg-red-100 transition-colors"
                                                    >
                                                        <Trash2 size={14} />
                                                    </button>
                                                </Tooltip>
                                            </div>
                                        </div>
                                        <span className="text-[10px] text-gray-400 truncate pl-6 block" title={pathString}>
                                            {pathString}
                                        </span>
                                    </div>
                                );
                            })}
                            {rootFolders.length === 0 && (
                                <div className="p-4 text-center text-xs text-gray-500">
                                    Klasör bulunamadı
                                </div>
                            )}
                        </div>
                    )
                ) : (
                    <>
                        <div 
                            className={`flex items-center gap-2 py-2 px-3 mb-2 cursor-pointer rounded-lg transition-colors ${dragOverFolderId === null ? 'bg-blue-100 ring-2 ring-blue-400' : activeFolderId === null ? 'bg-blue-50 text-blue-700 font-medium' : 'hover:bg-gray-100 text-gray-700'}`}
                            onClick={() => onSelect(null, [{id: null, name: 'Ana Klasör'}])}
                            onDragOver={(e) => onDragOverFolder(e, null)}
                            onDragLeave={onDragLeaveFolder}
                            onDrop={(e) => onDropOnFolder(e, null)}
                        >
                            <Folder size={18} className={activeFolderId === null ? 'text-blue-600' : 'text-gray-400'} />
                            <span className="text-[14.5px] font-semibold">Ana Klasör</span>
                        </div>

                        {isLoading ? (
                            <div className="flex justify-center p-4">
                                <div className="w-5 h-5 border-2 border-blue-600 border-t-transparent rounded-full animate-spin" />
                            </div>
                        ) : (
                            <div className="flex flex-col gap-0.5">
                                {rootFolders.map(folder => (
                                    <FolderNode 
                                        key={folder.id} 
                                        folder={folder} 
                                        level={0} 
                                        activeFolderId={activeFolderId}
                                        path={[{id: null, name: 'Ana Klasör'}]}
                                        onSelect={onSelect}
                                        onAction={onAction}
                                        dragOverFolderId={dragOverFolderId}
                                        onDragOverFolder={onDragOverFolder}
                                        onDragLeaveFolder={onDragLeaveFolder}
                                        onDropOnFolder={onDropOnFolder}
                                        expandedFolderIds={expandedFolderIds}
                                        onToggleExpand={handleToggleExpand}
                                    />
                                ))}
                                {rootFolders.length === 0 && (
                                    <div className="p-4 text-center text-xs text-gray-500">
                                        Klasör bulunamadı
                                    </div>
                                )}
                            </div>
                        )}
                    </>
                )}
            </div>

            {/* Storage Usage Widget */}
            {diskUsage && (
                <div className="p-4 border-t border-gray-100 bg-white flex flex-col gap-2 shrink-0 select-none">
                    <div className="flex items-center justify-between text-xs font-bold text-gray-500">
                        <span className="flex items-center gap-1.5 leading-tight">
                            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="text-gray-400 shrink-0"><rect x="2" y="2" width="20" height="8" rx="2" ry="2"></rect><rect x="2" y="14" width="20" height="8" rx="2" ry="2"></rect><line x1="6" y1="6" x2="6.01" y2="6"></line><line x1="6" y1="18" x2="6.01" y2="18"></line></svg>
                            Depolama Alanı Kullanım Yüzdesi
                        </span>
                        <span className={`text-[11px] font-extrabold shrink-0 ${diskUsage.percentage > 90 ? 'text-red-500' : 'text-[#1B3B6F]'}`}>
                            %{diskUsage.percentage}
                        </span>
                    </div>
                    <div className="w-full bg-gray-100 rounded-full h-1.5 overflow-hidden border border-gray-200/50">
                        <div 
                            className={`h-full transition-all duration-500 ${diskUsage.percentage > 90 ? 'bg-red-500 animate-pulse' : 'bg-[#1B3B6F]'}`}
                            style={{ width: `${diskUsage.percentage}%` }}
                        />
                    </div>
                </div>
            )}
        </div>
    );
}
