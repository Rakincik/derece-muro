"use client";

import React, { useState, useEffect } from 'react';
import { X, Search, BookOpen, Check } from 'lucide-react';
import { courseApi, type CourseListDto } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';

interface CourseSelectorModalProps {
    onClose: () => void;
    onSelect: (courseIds: string[], removedCourseIds?: string[]) => void;
    initialSelectedCourseIds?: string[];
}

export function CourseSelectorModal({ onClose, onSelect, initialSelectedCourseIds = [] }: CourseSelectorModalProps) {
    const { token, currentTenantId: tenantId } = useAuth();
    const [courses, setCourses] = useState<CourseListDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchQuery, setSearchQuery] = useState("");
    const [selectedCourseIds, setSelectedCourseIds] = useState<Set<string>>(new Set(initialSelectedCourseIds));

    useEffect(() => {
        if (!token || !tenantId) return;
        loadCourses();
    }, [token, tenantId]);

    const loadCourses = async () => {
        if (!token || !tenantId) return;
        setLoading(true);
        try {
            const res = await courseApi.list(token, tenantId, { pageSize: 1000 });
            setCourses(res.items || []);
        } catch (error) {
            console.error("Dersler yüklenemedi", error);
        } finally {
            setLoading(false);
        }
    };

    const filteredCourses = courses.filter(c => 
        c.title.toLocaleLowerCase('tr').includes(searchQuery.toLocaleLowerCase('tr'))
    );

    const handleConfirm = () => {
        // Find which courses were added and which were removed
        const newIds = Array.from(selectedCourseIds);
        const removedIds = initialSelectedCourseIds.filter(id => !newIds.includes(id));
        const addedIds = newIds.filter(id => !initialSelectedCourseIds.includes(id));
        
        onSelect(addedIds, removedIds);
    };

    const toggleCourse = (id: string) => {
        const newSet = new Set(selectedCourseIds);
        if (newSet.has(id)) newSet.delete(id);
        else newSet.add(id);
        setSelectedCourseIds(newSet);
    };

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center sm:p-4 p-0 bg-black/40 backdrop-blur-md">
            <div className="bg-white border border-gray-100 shadow-[0_20px_60px_rgba(0,0,0,0.15)] w-full max-w-2xl max-h-[85vh] sm:max-h-[90vh] flex flex-col self-end sm:self-center animate-in fade-in slide-in-from-bottom-10 sm:zoom-in-95 duration-300 sm:rounded-3xl rounded-t-[2.5rem]">
                {/* Mobile Drag Handle */}
                <div className="w-12 h-1 bg-gray-200 rounded-full mx-auto my-3 sm:hidden" />

                {/* Header */}
                <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
                    <div className="flex items-center gap-3">
                        <div className="p-2.5 bg-blue-50 text-blue-600 rounded-xl">
                            <BookOpen size={20} />
                        </div>
                        <h2 className="text-[17px] font-bold text-gray-900 tracking-tight">Ders Seçin</h2>
                    </div>
                    <button onClick={onClose} className="p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-50 rounded-xl transition-all">
                        <X className="w-5 h-5" />
                    </button>
                </div>

                {/* Search */}
                <div className="px-6 py-4 border-b border-gray-100 bg-gray-50">
                    <div className="relative">
                        <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-5 h-5" />
                        <input
                            type="text"
                            placeholder="Ders ara..."
                            value={searchQuery}
                            onChange={e => setSearchQuery(e.target.value)}
                            className="w-full pl-10 pr-4 py-2.5 border border-gray-200 rounded-xl focus:outline-none focus:ring-4 focus:ring-blue-500/10 focus:border-blue-500 bg-white text-sm transition-all"
                        />
                    </div>
                </div>

                {/* Content */}
                <div className="flex-1 overflow-y-auto p-6 bg-white min-h-[300px] scrollbar-thin">
                    {loading ? (
                        <div className="flex justify-center items-center h-full text-gray-500">
                            <div className="w-8 h-8 border-2 border-blue-600 border-t-transparent rounded-full animate-spin" />
                        </div>
                    ) : filteredCourses.length === 0 ? (
                        <div className="flex flex-col items-center justify-center h-full text-gray-500 py-12">
                            <BookOpen size={48} className="text-gray-300 mb-4 animate-pulse" />
                            <p className="text-sm font-medium">Ders bulunamadı</p>
                        </div>
                    ) : (
                        <div className="space-y-2">
                            {filteredCourses.map(course => (
                                <div 
                                    key={course.id} 
                                    className={`flex items-center gap-4 p-3 rounded-2xl border cursor-pointer transition-all duration-200 ${selectedCourseIds.has(course.id) ? 'border-blue-500 bg-blue-50/50 shadow-sm' : 'border-gray-200 hover:border-blue-300 hover:bg-gray-50/50'}`}
                                    onClick={() => toggleCourse(course.id)}
                                >
                                    <div className={`w-5 h-5 rounded-lg border flex items-center justify-center shrink-0 transition-all ${selectedCourseIds.has(course.id) ? 'bg-blue-600 border-blue-600' : 'border-gray-300 bg-white'}`}>
                                        {selectedCourseIds.has(course.id) && <Check size={12} className="text-white stroke-[3px]" />}
                                    </div>
                                    <div className="w-10 h-10 bg-blue-100/40 rounded-xl flex items-center justify-center shrink-0">
                                        <BookOpen size={20} className="text-blue-600" />
                                    </div>
                                    <div className="flex-1 min-w-0">
                                        <p className="text-sm font-semibold text-gray-900 truncate">{course.title}</p>
                                        <p className="text-xs text-gray-500 truncate mt-0.5">{course.description || "Açıklama yok"}</p>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </div>

                {/* Footer */}
                <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 flex sm:justify-end justify-between gap-3 sm:rounded-b-3xl">
                    <button onClick={onClose} className="flex-1 sm:flex-none px-5 py-2.5 rounded-xl text-sm font-medium text-gray-600 hover:bg-gray-150 border border-gray-200 sm:border-0 transition-colors">
                        İptal
                    </button>
                    <button 
                        onClick={handleConfirm}
                        className="flex-1 sm:flex-none px-6 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 active:scale-[0.98] text-white text-sm font-medium shadow-md shadow-blue-500/10 transition-all flex items-center justify-center gap-2"
                    >
                        <Check size={16} />
                        {selectedCourseIds.size > 0 ? `${selectedCourseIds.size} Derse Tanımla` : 'Değişiklikleri Kaydet'}
                    </button>
                </div>
            </div>
        </div>
    );
}
