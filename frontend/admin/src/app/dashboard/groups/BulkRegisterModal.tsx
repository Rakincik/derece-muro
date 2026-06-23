import React, { useState, useRef } from "react";
import { X, Upload, Check, AlertTriangle, Users, FileDown, Folder } from "lucide-react";
import { userApi } from "@/lib/api/users";
import { API_URL } from "@/lib/api/core";

interface BulkRegisterModalProps {
    token: string;
    tenantId: string;
    preselectedGroupId?: string | null;
    preselectedGroupName?: string | null;
    groups: any[];
    onClose: () => void;
    onSuccess: () => void;
}

export function BulkRegisterModal({ token, tenantId, preselectedGroupId, preselectedGroupName, groups, onClose, onSuccess }: BulkRegisterModalProps) {
    const [selectedGroupId, setSelectedGroupId] = useState<string | null>(preselectedGroupId || null);
    const [selectedGroupName, setSelectedGroupName] = useState<string | null>(preselectedGroupName || null);
    
    const [file, setFile] = useState<File | null>(null);
    const [loading, setLoading] = useState(false);
    const [isDragging, setIsDragging] = useState(false);
    const [result, setResult] = useState<{ ok: number; fail: number; details?: any[] } | null>(null);

    const fileInputRef = useRef<HTMLInputElement>(null);

    const downloadTemplate = async () => {
        try {
            await userApi.exportTemplate(token, tenantId);
        } catch {
            alert("Şablon indirilemedi.");
        }
    };

    const handleUpload = async () => {
        if (!file || !selectedGroupId) return;
        setLoading(true);
        try {
            const res = await userApi.importExcel(token, tenantId, file, selectedGroupId);
            setResult({ ok: res.importedCount, fail: res.skippedCount, details: res.details });
        } catch (e: any) {
            setResult({ ok: 0, fail: 1, details: [{ firstName: "Hata", status: "Başarısız", reason: e.message || "Bilinmeyen hata" }] });
        } finally {
            setLoading(false);
        }
    };

    const handleDragOver = (e: React.DragEvent) => {
        e.preventDefault();
        if (selectedGroupId) setIsDragging(true);
    };

    const handleDragLeave = (e: React.DragEvent) => {
        e.preventDefault();
        setIsDragging(false);
    };

    const handleDrop = (e: React.DragEvent) => {
        e.preventDefault();
        setIsDragging(false);
        if (!selectedGroupId) return;
        if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
            const f = e.dataTransfer.files[0];
            if (f.name.endsWith('.xlsx')) setFile(f);
            else alert("Lütfen geçerli bir .xlsx dosyası yükleyin.");
        }
    };

    const getChildren = (parentId: string | null) => groups.filter((g: any) => g.parentGroupId === parentId);
    
    const renderTree = (nodes: any[], depth = 0) => {
        return nodes.map(node => {
            const children = getChildren(node.id);
            return (
                <div key={node.id} className="w-full">
                    <button
                        onClick={() => { setSelectedGroupId(node.id); setSelectedGroupName(node.name); }}
                        className={`w-full flex items-center gap-2 px-3 py-2 text-sm text-left transition-colors ${selectedGroupId === node.id ? 'bg-[#0A1931] text-white font-bold rounded-lg' : 'hover:bg-[#E2E8F0]/30 text-[#1B3B6F]'}`}
                        style={{ paddingLeft: `${(depth * 16) + 12}px` }}
                    >
                        <Folder size={14} className={selectedGroupId === node.id ? "text-white" : "text-[#A0AEC0]"} />
                        <span className="truncate">{node.name}</span>
                    </button>
                    {children.length > 0 && (
                        <div className="w-full">
                            {renderTree(children, depth + 1)}
                        </div>
                    )}
                </div>
            );
        });
    };

    if (result) {
        return (
            <div className="fixed inset-0 z-[120] flex items-center justify-center p-4">
                <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => { onClose(); onSuccess(); }} />
                <div className="relative w-full max-w-4xl max-h-[85vh] flex flex-col bg-white rounded-3xl shadow-2xl overflow-hidden animate-fade-in">
                    <div className="flex items-center justify-between p-6 border-b border-[#E2E8F0]/60 bg-[#F8FAFC]">
                        <div>
                            <h2 className="text-xl font-bold text-[#0A1931]">Yükleme Sonuçları</h2>
                            <p className="text-sm text-[#64748B] mt-1">{result.ok} Başarılı, {result.fail} Başarısız/Atlanan</p>
                        </div>
                        <button onClick={() => { onClose(); onSuccess(); }} className="p-2 bg-white border border-[#E2E8F0] rounded-xl hover:bg-[#E2E8F0]/50 text-[#A0AEC0] transition-colors"><X size={20} /></button>
                    </div>
                    <div className="flex-1 overflow-y-auto p-6">
                        <table className="w-full text-left text-sm whitespace-nowrap">
                            <thead>
                                <tr className="text-[#64748B] font-medium border-b border-[#E2E8F0]">
                                    <th className="pb-3 pr-4 font-bold">Ad Soyad</th>
                                    <th className="pb-3 px-4 font-bold">E-posta / Telefon</th>
                                    <th className="pb-3 px-4 font-bold">Durum</th>
                                    <th className="pb-3 pl-4 font-bold w-full">Açıklama</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-[#E2E8F0]/60">
                                {result.details?.map((d: any, i: number) => (
                                    <tr key={i} className="hover:bg-[#F8FAFC]/50">
                                        <td className="py-3 pr-4 font-medium text-[#0A1931]">{d.firstName} {d.lastName}</td>
                                        <td className="py-3 px-4 text-[#64748B]">{d.email}</td>
                                        <td className="py-3 px-4">
                                            <span className={`inline-flex items-center px-2 py-1 rounded-md text-[11px] font-bold ${d.status === 'Başarılı' ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700'}`}>
                                                {d.status}
                                            </span>
                                        </td>
                                        <td className="py-3 pl-4 text-xs text-[#64748B] whitespace-normal">{d.reason}</td>
                                    </tr>
                                ))}
                                {(!result.details || result.details.length === 0) && (
                                    <tr><td colSpan={4} className="py-8 text-center text-[#64748B]">Detay bilgisi bulunamadı.</td></tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                    <div className="p-4 border-t border-[#E2E8F0]/60 bg-[#F8FAFC] flex justify-end">
                        <button onClick={() => { onClose(); onSuccess(); }} className="px-6 py-2.5 bg-[#0A1931] text-white text-sm font-bold rounded-xl hover:bg-[#1B3B6F] shadow-lg transition-all">
                            Kapat
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="fixed inset-0 z-[120] flex items-center justify-center p-4">
            <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" onClick={onClose} />
            <div className="relative w-full max-w-5xl h-[90vh] flex flex-col bg-white rounded-3xl shadow-2xl overflow-hidden animate-fade-in">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-[#E2E8F0] bg-white z-10 shrink-0">
                    <div>
                        <h2 className="text-xl font-bold text-[#0A1931] flex items-center gap-2">
                            <Users className="text-[#1B3B6F]" size={22} />
                            Toplu Kayıt (Excel İle)
                        </h2>
                        <p className="text-xs font-semibold text-[#A0AEC0] mt-1.5 uppercase tracking-widest">Öğrencileri doğrudan bir gruba aktarın</p>
                    </div>
                    <div className="flex items-center gap-3">
                        <button onClick={downloadTemplate} className="px-4 py-2 text-sm font-bold bg-white text-[#1B3B6F] border border-[#E2E8F0] rounded-xl hover:bg-[#F8FAFC] transition-all flex items-center gap-2 shadow-sm">
                            <FileDown size={16} /> Şablon İndir
                        </button>
                        <button onClick={onClose} className="p-2 rounded-xl hover:bg-[#E2E8F0]/50 text-[#A0AEC0] transition-colors">
                            <X size={20} />
                        </button>
                    </div>
                </div>

                {/* Content */}
                <div className="flex flex-1 overflow-hidden">
                    {/* Left Panel: Groups Tree */}
                    <div className="w-1/3 min-w-[280px] bg-[#F8FAFC] border-r border-[#E2E8F0] flex flex-col">
                        <div className="p-4 border-b border-[#E2E8F0]/60">
                            <p className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest mb-1">Hedef Grubu Seçin</p>
                            <p className="text-[11px] font-medium text-[#64748B]">Kullanıcıların ekleneceği grubu sol taraftan işaretleyin.</p>
                        </div>
                        <div className="flex-1 overflow-y-auto p-3 space-y-1">
                            {renderTree(getChildren(null))}
                            {groups.length === 0 && (
                                <div className="text-center py-8 text-[#A0AEC0]">
                                    <Folder size={32} className="mx-auto mb-2 opacity-50" />
                                    <p className="text-sm font-medium">Grup bulunamadı.</p>
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Right Panel: Upload Area */}
                    <div className="flex-1 flex flex-col bg-white">
                        <div className="flex-1 p-8 overflow-y-auto">
                            {/* Selected Group Indicator */}
                            <div className={`p-4 rounded-2xl border ${selectedGroupId ? 'bg-blue-50/50 border-blue-200' : 'bg-amber-50/50 border-amber-200'} mb-6 flex items-center justify-between`}>
                                <div>
                                    <p className="text-[11px] font-extrabold uppercase tracking-widest text-[#A0AEC0] mb-1">Seçili Grup</p>
                                    <h3 className={`text-lg font-bold ${selectedGroupId ? 'text-[#1B3B6F]' : 'text-amber-600'}`}>
                                        {selectedGroupName || "Lütfen soldan bir grup seçin"}
                                    </h3>
                                </div>
                                {selectedGroupId && <div className="w-10 h-10 bg-blue-100 text-blue-600 rounded-full flex items-center justify-center"><Check size={20} /></div>}
                            </div>

                            {/* Upload Area */}
                            <div 
                                onDragOver={handleDragOver}
                                onDragLeave={handleDragLeave}
                                onDrop={handleDrop}
                                className={`border-2 rounded-2xl p-10 text-center transition-colors ${!selectedGroupId ? 'opacity-50 pointer-events-none' : ''} ${file ? 'border-emerald-500 bg-emerald-50/30 border-solid' : (isDragging ? 'border-blue-500 bg-blue-50/50 border-solid' : 'border-dashed border-[#E2E8F0] hover:border-[#1B3B6F]/40 hover:bg-[#F8FAFC]')}`}
                            >
                                <input type="file" accept=".xlsx" className="hidden" id="modal-bulk-file"
                                    onChange={e => { if (e.target.files?.[0]) setFile(e.target.files[0]); }} />
                                <label htmlFor="modal-bulk-file" className="cursor-pointer block">
                                    {file ? (
                                        <div className="flex flex-col items-center justify-center animate-fade-in">
                                            <div className="w-20 h-20 bg-emerald-100 text-emerald-600 rounded-full flex items-center justify-center mb-4 ring-8 ring-emerald-50 shadow-sm">
                                                <Check size={40} strokeWidth={3} />
                                            </div>
                                            <p className="text-xl font-extrabold text-emerald-700 mb-1">Dosya Hazır!</p>
                                            <p className="text-base font-semibold mb-2 truncate max-w-[400px] mx-auto text-[#0A1931]">
                                                {file.name}
                                            </p>
                                            <p className="text-sm text-emerald-600 font-medium mb-5">{(file.size / 1024).toFixed(1)} KB</p>
                                            <div className="inline-flex px-5 py-2 rounded-xl bg-white border border-emerald-200 text-sm text-emerald-700 font-bold hover:bg-emerald-50 transition-colors shadow-sm">
                                                Başka Dosya Seç
                                            </div>
                                        </div>
                                    ) : (
                                        <div className="py-4">
                                            <div className="w-16 h-16 bg-[#F0F4F8] rounded-full flex items-center justify-center mx-auto mb-4 border border-[#E2E8F0] shadow-sm">
                                                <Upload size={28} className="text-[#1B3B6F]" />
                                            </div>
                                            <p className="text-lg font-bold mb-2 text-[#0A1931]">
                                                Excel dosyası (.xlsx) seçin veya sürükleyin
                                            </p>
                                            <p className="text-sm text-[#64748B] mb-6">Şablon dosyasını doldurup yükleyebilirsiniz.</p>
                                            
                                            <div className="bg-white p-5 rounded-2xl text-left border border-[#E2E8F0] shadow-sm space-y-3 max-w-md mx-auto">
                                                <div className="flex items-start gap-3">
                                                    <div className="mt-0.5 w-5 h-5 rounded-full bg-emerald-100 flex items-center justify-center shrink-0"><Check size={12} strokeWidth={3} className="text-emerald-500" /></div> 
                                                    <p className="text-xs text-[#64748B] font-medium leading-relaxed">Şifreler otomatik olarak <strong className="text-[#0A1931]">TC + Numaranın Son 2 Hanesi</strong> olarak atanır.</p>
                                                </div>
                                                <div className="flex items-start gap-3">
                                                    <div className="mt-0.5 w-5 h-5 rounded-full bg-rose-100 flex items-center justify-center shrink-0"><AlertTriangle size={12} strokeWidth={3} className="text-rose-500" /></div> 
                                                    <p className="text-xs text-[#64748B] font-medium leading-relaxed">Telefon numaralarını başında <strong className="text-rose-600">0 olmadan</strong> giriniz!</p>
                                                </div>
                                            </div>
                                        </div>
                                    )}
                                </label>
                            </div>
                        </div>

                        {/* Footer */}
                        <div className="p-6 border-t border-[#E2E8F0] bg-[#F8FAFC] flex justify-end gap-3 shrink-0">
                            <button onClick={onClose} className="px-6 py-3 text-sm font-bold text-[#A0AEC0] hover:text-[#0A1931] transition-colors">
                                İptal
                            </button>
                            <button 
                                onClick={handleUpload} 
                                disabled={!file || !selectedGroupId || loading}
                                className="px-8 py-3 text-sm font-bold text-white bg-[#0A1931] rounded-xl hover:bg-[#1B3B6F] shadow-xl shadow-[#0A1931]/20 disabled:opacity-40 disabled:shadow-none transition-all flex items-center gap-2"
                            >
                                {loading ? (
                                    <>Yükleniyor...</>
                                ) : (
                                    <><Upload size={18} /> Excel'i Yükle ve Gruba Ekle</>
                                )}
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
