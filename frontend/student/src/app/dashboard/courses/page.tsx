"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { courseApi, getFileUrl, type CourseDto } from "@/lib/api";
import Link from "next/link";
import Image from "next/image";
import { LayoutGrid, List } from "lucide-react";

export default function CoursesPage() {
    const { token, currentTenantId: tenantId } = useAuth();
    const [courses, setCourses] = useState<CourseDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState("");
    const [viewMode, setViewMode] = useState<"grid" | "list">("list");

    useEffect(() => {
        if (!token || !tenantId) return;
        courseApi.list(token, tenantId)
            .then(setCourses)
            .catch(console.error)
            .finally(() => setLoading(false));
    }, [token, tenantId]);

    const filtered = courses.filter(c =>
        c.title.toLowerCase().includes(search.toLowerCase())
    );

    return (
        <div className="w-full max-w-[1600px] mx-auto px-4 lg:px-8">
            {/* Header */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8">
                <div>
                    <h1 className="text-2xl font-bold text-[#0A1931]">Derslerim</h1>
                    <p className="text-[#A9A9A9] text-sm mt-1">{courses.length} kurs kayıtlı</p>
                </div>
                <div className="flex items-center gap-2">
                    <input
                        type="text"
                        placeholder="Kurs ara..."
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                        className="px-4 py-2 bg-white border border-[#E2E8F0] rounded-xl text-[#0A1931] text-sm placeholder-[#A0AEC0] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:border-[#1B3B6F] w-full sm:w-56 transition-all shadow-sm"
                    />
                    <div className="flex bg-[#E2E8F0]/30 p-1 rounded-xl">
                        <button
                            onClick={() => setViewMode("list")}
                            className={`p-1.5 rounded-lg transition-colors ${viewMode === "list" ? "bg-white shadow-sm text-[#1B3B6F]" : "text-[#A0AEC0] hover:text-[#0A1931]"}`}
                        >
                            <List size={18} />
                        </button>
                        <button
                            onClick={() => setViewMode("grid")}
                            className={`p-1.5 rounded-lg transition-colors ${viewMode === "grid" ? "bg-white shadow-sm text-[#1B3B6F]" : "text-[#A0AEC0] hover:text-[#0A1931]"}`}
                        >
                            <LayoutGrid size={18} />
                        </button>
                    </div>
                </div>
            </div>

            {loading ? (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    {[...Array(6)].map((_, i) => (
                        <div key={i} className="glass-card p-5 animate-pulse">
                            <div className="aspect-video bg-[#1B3B6F]/50 rounded-lg mb-4" />
                            <div className="h-4 bg-[#1B3B6F]/50 rounded w-3/4 mb-2" />
                            <div className="h-3 bg-[#1B3B6F]/50 rounded w-1/2" />
                        </div>
                    ))}
                </div>
            ) : filtered.length === 0 ? (
                <div className="text-center py-20">
                    <p className="text-5xl mb-4">📭</p>
                    <p className="text-[#A0AEC0] text-lg font-medium">Kurs bulunamadı</p>
                    <p className="text-[#1B3B6F] text-sm mt-1">Farklı bir arama terimi deneyin</p>
                </div>
            ) : (
                viewMode === "grid" ? (
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
                        {filtered.map((course) => (
                            <Link
                                key={course.id}
                                href={`/dashboard/courses/${course.id}`}
                                className="glass-card overflow-hidden hover:border-[#A0AEC0] transition-all flex flex-row md:flex-col group"
                            >
                                {/* Thumbnail */}
                                <div className="w-28 sm:w-40 md:w-full h-24 sm:h-28 md:h-auto md:aspect-video bg-gradient-to-br from-indigo-900/50 to-purple-900/50 relative overflow-hidden shrink-0 m-2.5 md:m-0 rounded-xl md:rounded-none">
                                    {course.thumbnailUrl ? (
                                        <Image src={getFileUrl(course.thumbnailUrl)} alt={course.title} width={400} height={225} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500" unoptimized />
                                    ) : (
                                        <div className="w-full h-full flex items-center justify-center">
                                            <span className="text-3xl md:text-5xl opacity-30 group-hover:opacity-50 transition-opacity">📚</span>
                                        </div>
                                    )}
                                    <div className="absolute top-1.5 left-1.5 md:top-3 md:right-3 md:left-auto">
                                        <span className="px-1.5 py-0.5 md:px-2 md:py-1 bg-black/60 backdrop-blur-md text-[9px] md:text-xs text-white rounded-md md:rounded-full font-medium">
                                            {course.sessionCount} içerik
                                        </span>
                                    </div>
                                </div>

                                {/* Info */}
                                <div className="p-3 md:p-5 flex-1 min-w-0 flex flex-col justify-center">
                                    <h3 className="text-[#0A1931] font-bold text-xs md:text-sm mb-1.5 md:mb-2 line-clamp-2 group-hover:text-[#1B3B6F] transition-colors leading-tight">
                                        {course.title}
                                    </h3>
                                    {course.description && (
                                        <p className="text-[#A9A9A9] text-[10px] md:text-xs line-clamp-1 md:line-clamp-2 mb-2 md:mb-3">{course.description}</p>
                                    )}
                                    {/* İlerleme çubuğu */}
                                    {(course.completionPercentage ?? 0) > 0 && (
                                        <div className="mb-2 md:mb-3">
                                            <div className="flex items-center justify-between mb-1">
                                                <span className="text-[10px] md:text-xs text-[#A9A9A9]">İlerleme</span>
                                                <span className={`text-[10px] md:text-xs font-semibold ${(course.completionPercentage ?? 0) >= 100 ? 'text-emerald-500' : 'text-[#1B3B6F]'}`}>
                                                    %{Math.round(course.completionPercentage ?? 0)}
                                                </span>
                                            </div>
                                            <div className="h-1.5 bg-[#E2E8F0] rounded-full overflow-hidden">
                                                <div
                                                    className={`h-full rounded-full transition-all ${(course.completionPercentage ?? 0) >= 100 ? 'bg-emerald-500' : 'bg-[#1B3B6F]'}`}
                                                    style={{ width: `${Math.min(100, course.completionPercentage ?? 0)}%` }}
                                                />
                                            </div>
                                        </div>
                                    )}
                                    <div className="flex items-center justify-between mt-auto">
                                        <span className="text-[10px] md:text-xs text-[#A0AEC0] hidden md:block">{course.sessionCount} içerik</span>
                                        <span className="text-[#1B3B6F] text-[10px] md:text-xs font-bold group-hover:underline ml-auto">
                                            {(course.completionPercentage ?? 0) >= 100 ? '✓ Tamamlandı' : 'Devam Et →'}
                                        </span>
                                    </div>
                                </div>
                            </Link>
                        ))}
                    </div>
                ) : (
                    <div className="flex flex-col gap-3">
                        {filtered.map(course => (
                            <Link
                                key={course.id}
                                href={`/dashboard/courses/${course.id}`}
                                className="bg-white rounded-xl border border-[#E2E8F0]/60 p-3 sm:p-4 hover:border-blue-300 hover:shadow-md transition-all flex items-center justify-between group"
                            >
                                <div className="flex items-center gap-4 flex-1 min-w-0">
                                    <div className="w-12 h-12 sm:w-16 sm:h-12 bg-gradient-to-br from-indigo-900/50 to-purple-900/50 rounded-lg overflow-hidden shrink-0 relative hidden sm:block">
                                        {course.thumbnailUrl ? (
                                            <Image src={getFileUrl(course.thumbnailUrl)} alt={course.title} width={64} height={48} className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-500" unoptimized />
                                        ) : (
                                            <div className="w-full h-full flex items-center justify-center">
                                                <span className="text-xl opacity-50">📚</span>
                                            </div>
                                        )}
                                    </div>
                                    <div className="min-w-0">
                                        <h3 className="text-sm font-bold text-[#0A1931] group-hover:text-blue-600 transition-colors truncate">{course.title}</h3>
                                        <div className="text-[10px] sm:text-xs text-[#A0AEC0] mt-0.5">{course.sessionCount} içerik</div>
                                    </div>
                                </div>
                                <div className="flex items-center gap-6 shrink-0 ml-4">
                                    {/* İlerleme */}
                                    {(course.completionPercentage ?? 0) > 0 && (
                                        <div className="w-24 sm:w-32 hidden md:block">
                                            <div className="flex items-center justify-between mb-1">
                                                <span className="text-[10px] text-[#A9A9A9]">İlerleme</span>
                                                <span className={`text-[10px] font-semibold ${(course.completionPercentage ?? 0) >= 100 ? 'text-emerald-500' : 'text-[#1B3B6F]'}`}>%{Math.round(course.completionPercentage ?? 0)}</span>
                                            </div>
                                            <div className="h-1.5 bg-[#E2E8F0] rounded-full overflow-hidden">
                                                <div className={`h-full rounded-full ${(course.completionPercentage ?? 0) >= 100 ? 'bg-emerald-500' : 'bg-[#1B3B6F]'}`} style={{ width: `${Math.min(100, course.completionPercentage ?? 0)}%` }} />
                                            </div>
                                        </div>
                                    )}
                                    <span className="text-[#1B3B6F] text-xs font-bold group-hover:translate-x-1 transition-transform whitespace-nowrap">
                                        {(course.completionPercentage ?? 0) >= 100 ? '✓' : 'Devam Et →'}
                                    </span>
                                </div>
                            </Link>
                        ))}
                    </div>
                )
            )}
        </div>
    );
}
