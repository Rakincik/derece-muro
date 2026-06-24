"use client";

import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, FileText, Check, ChevronRight, MonitorPlay, Plus } from 'lucide-react';
import { examApi, type ExamListDto } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { MiniQuizBuilder } from './MiniQuizBuilder';

interface ExamSelectorModalProps {
    onClose: () => void;
    onSelect: (selectedExamIds: string[]) => void;
}

export function ExamSelectorModal({ onClose, onSelect }: ExamSelectorModalProps) {
    const { token, currentTenantId } = useAuth();
    const [exams, setExams] = useState<ExamListDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [selectedExamIds, setSelectedExamIds] = useState<Set<string>>(new Set());
    const [showMiniBuilder, setShowMiniBuilder] = useState(false);

    useEffect(() => {
        const fetchExams = async () => {
            if (!token || !currentTenantId) return;
            setLoading(true);
            try {
                // Fetch all published/ready exams
                const res = await examApi.list(token, currentTenantId, { pageSize: 1000, status: "Yayında" });
                setExams(res.items || []);
            } catch (err) {
                console.error(err);
            } finally {
                setLoading(false);
            }
        };
        fetchExams();
    }, [token, currentTenantId]);

    const toggleExam = (id: string) => {
        const newSet = new Set(selectedExamIds);
        if (newSet.has(id)) newSet.delete(id);
        else newSet.add(id);
        setSelectedExamIds(newSet);
    };

    const handleConfirm = () => {
        onSelect(Array.from(selectedExamIds));
    };

    const modalContent = (
        <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4 sm:p-6 bg-[#0A1931]/60 backdrop-blur-md">
            <div className={`bg-white rounded-[2.5rem] w-full ${showMiniBuilder ? 'max-w-5xl h-[85vh]' : 'max-w-[800px] h-[70vh]'} flex flex-col shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-300 border border-[#E2E8F0]/40 transition-all`}>
                {/* Header */}
                <div className="flex items-center justify-between px-8 py-5 bg-white border-b border-[#E2E8F0]/60 z-10 relative shadow-sm">
                    <div>
                        <h2 className="text-xl font-black text-[#0A1931] tracking-tight">{showMiniBuilder ? "Hızlı Quiz Oluştur" : "Sınav / Quiz Ekle"}</h2>
                        <p className="text-sm font-medium text-[#A0AEC0]">{showMiniBuilder ? "Metin tabanlı mini bir test hazırlayın." : "Kursa dahil etmek istediğiniz sınavları seçin."}</p>
                    </div>
                    <div className="flex items-center gap-3">
                        {!showMiniBuilder && (
                            <button 
                                onClick={() => setShowMiniBuilder(true)}
                                className="px-4 py-2 bg-[#F1F5F9] text-[#1B3B6F] hover:bg-[#E2E8F0] hover:text-[#0A1931] rounded-xl font-bold text-sm transition-colors flex items-center gap-2"
                            >
                                <Plus size={16} strokeWidth={3} />
                                Yeni Hızlı Quiz Yarat
                            </button>
                        )}
                        <button onClick={onClose} className="p-2 text-[#A0AEC0] hover:text-[#0A1931] rounded-2xl hover:bg-[#E2E8F0]/50 transition-all active:scale-95">
                            <X size={24} />
                        </button>
                    </div>
                </div>

                {showMiniBuilder ? (
                    <MiniQuizBuilder 
                        onClose={() => setShowMiniBuilder(false)} 
                        onSuccess={(newExamId) => onSelect([newExamId])} 
                    />
                ) : (
                    <>
                        <div className="flex-1 overflow-y-auto p-8 bg-[#F1F5F9]/30">
                    {loading ? (
                        <div className="flex justify-center items-center h-full">
                            <div className="w-10 h-10 border-[3px] border-purple-600 border-t-transparent rounded-full animate-spin" />
                        </div>
                    ) : exams.length === 0 ? (
                        <div className="flex flex-col items-center justify-center h-full text-center">
                            <div className="w-24 h-24 rounded-full bg-[#E2E8F0]/30 flex items-center justify-center mb-6">
                                <FileText size={40} className="text-[#A0AEC0]/50" />
                            </div>
                            <h3 className="text-lg font-black text-[#0A1931]">Hiç Sınav Bulunamadı</h3>
                            <p className="text-sm font-medium text-[#A0AEC0] mt-1 max-w-[250px]">Önce 'Sınavlar' modülünden 'Yayında' statüsünde bir sınav oluşturmalısınız.</p>
                        </div>
                    ) : (
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                            {exams.map(exam => {
                                const isSelected = selectedExamIds.has(exam.id);
                                return (
                                    <div 
                                        key={exam.id} 
                                        onClick={() => toggleExam(exam.id)}
                                        className={`group relative bg-white p-5 rounded-2xl border transition-all cursor-pointer shadow-sm hover:shadow-md flex items-center gap-4 ${isSelected ? 'border-[#1B3B6F] bg-blue-50/50 ring-2 ring-[#1B3B6F]/20' : 'border-[#E2E8F0]/80 hover:border-[#A0AEC0]/40'}`}
                                    >
                                        {/* Checkbox */}
                                        <div className={`w-6 h-6 shrink-0 rounded-lg flex items-center justify-center border transition-all ${isSelected ? 'bg-[#1B3B6F] border-[#1B3B6F] text-white' : 'bg-white border-[#E2E8F0] group-hover:border-[#A0AEC0]'}`}>
                                            {isSelected && <Check size={14} strokeWidth={3} />}
                                        </div>
                                        
                                        <div className="flex-1 min-w-0">
                                            <h4 className={`text-sm font-bold truncate transition-colors ${isSelected ? 'text-[#1B3B6F]' : 'text-[#0A1931]'}`}>
                                                {exam.title}
                                            </h4>
                                            <p className="text-xs font-medium text-[#A0AEC0] mt-1 flex items-center gap-2">
                                                <span>{exam.questionCount} Soru</span>
                                                <span className="w-1 h-1 rounded-full bg-[#E2E8F0]" />
                                                <span>{exam.durationMinutes ? `${exam.durationMinutes} dk` : 'Süresiz'}</span>
                                            </p>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </div>

                {/* Footer */}
                <div className="px-8 py-5 border-t border-[#E2E8F0]/80 bg-white flex justify-between items-center relative z-10">
                    <div className="flex items-center gap-6">
                        <div className="flex flex-col">
                            <span className="text-[10px] font-black text-[#A0AEC0] uppercase tracking-widest mb-0.5">Seçilen Sınavlar</span>
                            <span className="text-base font-black text-[#1B3B6F]">{selectedExamIds.size}</span>
                        </div>
                    </div>
                    <div className="flex gap-4">
                        <button onClick={onClose} className="px-8 py-3 rounded-2xl text-[#A0AEC0] font-bold hover:text-[#0A1931] hover:bg-[#F8FAFC] transition-colors">
                            Vazgeç
                        </button>
                        <button 
                            onClick={handleConfirm}
                            disabled={selectedExamIds.size === 0}
                            className="px-8 py-3 rounded-2xl bg-[#1B3B6F] hover:bg-[#152a51] text-white font-bold shadow-lg shadow-[#1B3B6F]/20 transition-all active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed disabled:active:scale-100 flex items-center gap-2"
                        >
                            <Check size={18} />
                            Kursa Ekle
                        </button>
                    </div>
                </div>
                </>
                )}
            </div>
        </div>
    );

    if (typeof document !== 'undefined') {
        return createPortal(modalContent, document.body);
    }
    return null;
}
