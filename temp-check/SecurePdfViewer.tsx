"use client";

import { useState } from "react";
import { Document, Page, pdfjs } from "react-pdf";

// Next.js client ortamında pdf worker ayarı
pdfjs.GlobalWorkerOptions.workerSrc = `//unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;

interface SecurePdfViewerProps {
    url: string;
    watermarkText?: string;
}

export default function SecurePdfViewer({ url, watermarkText }: SecurePdfViewerProps) {
    const [numPages, setNumPages] = useState<number>();
    const [pageNumber, setPageNumber] = useState<number>(1);
    const [scale, setScale] = useState<number>(1.0);

    const onDocumentLoadSuccess = ({ numPages }: { numPages: number }) => {
        setNumPages(numPages);
        setPageNumber(1);
    };

    const changePage = (offset: number) => {
        setPageNumber((prev) => Math.min(Math.max(1, prev + offset), numPages || 1));
    };

    const zoomIn = () => setScale(s => Math.min(s + 0.3, 3.0));
    const zoomOut = () => setScale(s => Math.max(s - 0.2, 0.6));

    return (
        <div className="flex flex-col h-full bg-[#E2E8F0]/20 select-none" onContextMenu={(e) => e.preventDefault()}>
            {/* Araç Çubuğu (Toolbar) */}
            <div className="flex items-center justify-between p-3 bg-white border-b border-[#E2E8F0] shadow-sm z-10 shrink-0 rounded-t-xl">
                <div className="flex items-center gap-2">
                    <button 
                        onClick={() => changePage(-1)} 
                        disabled={pageNumber <= 1}
                        className="w-8 h-8 flex items-center justify-center rounded-lg bg-[#E2E8F0]/50 hover:bg-[#E2E8F0] text-[#0A1931] disabled:opacity-30 transition-all font-bold"
                    >
                        ◀
                    </button>
                    <span className="text-sm font-bold text-[#1B3B6F] min-w-[60px] text-center">
                        {pageNumber} / {numPages || "-"}
                    </span>
                    <button 
                        onClick={() => changePage(1)} 
                        disabled={numPages === undefined || pageNumber >= numPages}
                        className="w-8 h-8 flex items-center justify-center rounded-lg bg-[#E2E8F0]/50 hover:bg-[#E2E8F0] text-[#0A1931] disabled:opacity-30 transition-all font-bold"
                    >
                        ▶
                    </button>
                </div>

                <div className="flex items-center gap-2">
                    <button 
                        onClick={zoomOut}
                        disabled={scale <= 0.6}
                        className="w-8 h-8 flex items-center justify-center rounded-lg bg-violet-100 hover:bg-violet-200 text-violet-700 disabled:opacity-30 transition-all font-bold text-lg"
                    >
                        -
                    </button>
                    <span className="text-xs font-bold text-violet-900 min-w-[40px] text-center">
                        {Math.round(scale * 100)}%
                    </span>
                    <button 
                        onClick={zoomIn}
                        disabled={scale >= 3.0}
                        className="w-8 h-8 flex items-center justify-center rounded-lg bg-violet-100 hover:bg-violet-200 text-violet-700 disabled:opacity-30 transition-all font-bold text-lg"
                    >
                        +
                    </button>
                </div>
            </div>

            {/* PDF Görüntüleme Alanı */}
            <div className="flex-1 overflow-auto flex justify-center p-4 custom-scrollbar bg-slate-50 relative rounded-b-xl">
                <Document
                    file={url}
                    onLoadSuccess={onDocumentLoadSuccess}
                    loading={
                        <div className="flex flex-col items-center justify-center text-[#A9A9A9] h-full mt-20">
                            <div className="w-8 h-8 border-2 border-violet-500 border-t-transparent rounded-full animate-spin mb-4" />
                            <p className="font-semibold">Sınav kitapçığı işleniyor...</p>
                        </div>
                    }
                    error={
                        <div className="flex flex-col items-center justify-center text-red-500 h-full mt-20">
                            <p className="text-4xl mb-3">⚠️</p>
                            <p className="font-bold">PDF yüklenemedi veya erişim engellendi.</p>
                        </div>
                    }
                >
                    {/* Page Wrapper for Watermark Alignment */}
                    <div className="relative shadow-xl border border-[#E2E8F0] bg-white inline-block">
                        <Page 
                            pageNumber={pageNumber} 
                            scale={scale} 
                            // Güvenlik: Yazı katmanı ve tıklanabilir linkleri tamamen kapatıyoruz ki PDF sadece bir resim olsun.
                            renderTextLayer={false}
                            renderAnnotationLayer={false}
                            className="bg-white"
                        />
                        
                        {/* Dinamik Watermark Katmanı */}
                        {watermarkText && (
                            <div className="absolute inset-0 pointer-events-none flex items-center justify-center overflow-hidden z-20 opacity-[0.12] mix-blend-multiply">
                                <div className="grid grid-cols-2 sm:grid-cols-3 gap-y-24 gap-x-12 rotate-[-35deg] scale-[2.0] transform-gpu">
                                    {Array.from({ length: 40 }).map((_, i) => (
                                        <p key={i} className="text-[#0A1931] font-black text-2xl whitespace-nowrap select-none">
                                            {watermarkText}
                                        </p>
                                    ))}
                                </div>
                            </div>
                        )}
                    </div>
                </Document>
            </div>
        </div>
    );
}
