"use client";

import React, { useState, useEffect } from 'react';
import { ChevronRight, ChevronDown, Folder, MoreVertical, Plus, Edit2, Trash2, BookOpen } from 'lucide-react';
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
}

const FolderNode: React.FC<FolderNodeProps> = ({ folder, level, activeFolderId, path, onSelect, onAction, dragOverFolderId, onDragOverFolder, onDragLeaveFolder, onDropOnFolder }) => {
    const [isExpanded, setIsExpanded] = useState(false);
    const [subFolders, setSubFolders] = useState<MediaFolderDto[]>([]);
    const [isLoading, setIsLoading] = useState(false);

    const currentPath = [...path, { id: folder.id, name: folder.name }];

    const hasChildren = folder.subFolderCount > 0;
    const isActive = activeFolderId === folder.id;

    const toggleExpand = async (e: React.MouseEvent) => {
        e.stopPropagation();
        if (!hasChildren) return;
        
        if (!isExpanded && subFolders.length === 0) {
            setIsLoading(true);
            try {
                const fetched = await mediaLibraryApi.getFolders(folder.id);
                setSubFolders(fetched);
            } catch (err) {
                console.error("Failed to load subfolders", err);
            } finally {
                setIsLoading(false);
            }
        }
        setIsExpanded(!isExpanded);
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
                        <span className="truncate text-sm select-none block w-full">{folder.name}</span>
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
                        />
                    ))}
                </div>
            )}
        </div>
    );
};

interface FolderTreeProps {
    activeFolderId: string | null;
    onSelect: (folderId: string | null, path: {id: string | null, name: string}[]) => void;
    onAction: (action: 'rename' | 'delete' | 'assign' | 'createSub', folder: MediaFolderDto | null) => void;
    refreshTrigger: number; // A prop to trigger re-fetching root folders
    dragOverFolderId: string | null;
    onDragOverFolder: (e: React.DragEvent, folderId: string | null) => void;
    onDragLeaveFolder: (e: React.DragEvent) => void;
    onDropOnFolder: (e: React.DragEvent, folderId: string | null) => void;
}

export function FolderTree({ activeFolderId, onSelect, onAction, refreshTrigger, dragOverFolderId, onDragOverFolder, onDragLeaveFolder, onDropOnFolder }: FolderTreeProps) {
    const [rootFolders, setRootFolders] = useState<MediaFolderDto[]>([]);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        loadRootFolders();
    }, [refreshTrigger]);

    const loadRootFolders = async () => {
        setIsLoading(true);
        try {
            const fetched = await mediaLibraryApi.getFolders(undefined); // fetch root
            setRootFolders(fetched);
        } catch (error) {
            console.error("Failed to load root folders", error);
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div className="w-full h-full flex flex-col bg-white border-r border-gray-200">
            {/* Header */}
            <div className="p-4 border-b border-gray-100 flex items-center justify-between">
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

            {/* Tree */}
            <div className="flex-1 overflow-y-auto p-2">
                <div 
                    className={`flex items-center gap-2 py-2 px-3 mb-2 cursor-pointer rounded-lg transition-colors ${dragOverFolderId === null ? 'bg-blue-100 ring-2 ring-blue-400' : activeFolderId === null ? 'bg-blue-50 text-blue-700 font-medium' : 'hover:bg-gray-100 text-gray-700'}`}
                    onClick={() => onSelect(null, [{id: null, name: 'Ana Klasör'}])}
                    onDragOver={(e) => onDragOverFolder(e, null)}
                    onDragLeave={onDragLeaveFolder}
                    onDrop={(e) => onDropOnFolder(e, null)}
                >
                    <Folder size={18} className={activeFolderId === null ? 'text-blue-600' : 'text-gray-400'} />
                    <span className="text-sm font-semibold">Ana Klasör</span>
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
                            />
                        ))}
                        {rootFolders.length === 0 && (
                            <div className="p-4 text-center text-xs text-gray-500">
                                Klasör bulunamadı
                            </div>
                        )}
                    </div>
                )}
            </div>
        </div>
    );
}

