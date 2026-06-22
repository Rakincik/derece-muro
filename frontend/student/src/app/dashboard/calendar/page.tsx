"use client";

import React, { useState, useEffect, useCallback, useMemo } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { studentCalendarApi, type CalendarEventDto } from "@/lib/api";
import {
    Calendar as CalIcon, ChevronLeft, ChevronRight, RefreshCw,
    Clock, Users, Sun, Layers, LayoutGrid, BookOpen, ExternalLink
} from "lucide-react";
import { useRouter } from "next/navigation";

const eventColors: Record<string, { bg: string; border: string; text: string; dot: string }> = {
    "Canlı Ders": { bg: "bg-emerald-50", border: "border-emerald-200", text: "text-emerald-700", dot: "bg-emerald-500" },
    "Sınav": { bg: "bg-blue-50", border: "border-blue-200", text: "text-blue-700", dot: "bg-blue-500" },
    "Ödev": { bg: "bg-amber-50", border: "border-amber-200", text: "text-amber-700", dot: "bg-amber-500" },
    "Toplantı": { bg: "bg-violet-50", border: "border-violet-200", text: "text-violet-700", dot: "bg-violet-500" },
    "Etkinlik": { bg: "bg-rose-50", border: "border-rose-200", text: "text-rose-700", dot: "bg-rose-500" },
    "Tatil": { bg: "bg-orange-50", border: "border-orange-200", text: "text-orange-700", dot: "bg-orange-500" },
};
const getColor = (type: string) => eventColors[type] ?? eventColors["Etkinlik"];

const DAYS_TR = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];
const DAYS_FULL_TR = ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar"];
const MONTHS_TR = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];
const HOURS = Array.from({ length: 15 }, (_, i) => i + 7);

type ViewMode = "day" | "week" | "month";

function getDaysInMonth(y: number, m: number) { return new Date(y, m + 1, 0).getDate(); }
function getFirstDayOfMonth(y: number, m: number) { const d = new Date(y, m, 1).getDay(); return d === 0 ? 6 : d - 1; }
function toDateStr(d: Date) { return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`; }
function getWeekDates(date: Date): Date[] {
    const d = new Date(date); const day = d.getDay() === 0 ? 6 : d.getDay() - 1; d.setDate(d.getDate() - day);
    return Array.from({ length: 7 }, (_, i) => { const nd = new Date(d); nd.setDate(d.getDate() + i); return nd; });
}

function evDate(ev: CalendarEventDto) { return ev.startDate?.substring(0, 10) ?? ""; }
function evStartTime(ev: CalendarEventDto) { const d = new Date(ev.startDate); return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`; }
function evEndTime(ev: CalendarEventDto) { const d = new Date(ev.endDate); return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`; }
function evHour(ev: CalendarEventDto) { return new Date(ev.startDate).getHours(); }
function evEndHour(ev: CalendarEventDto) { return new Date(ev.endDate).getHours(); }

const EventCard = React.memo(({ ev, compact }: { ev: CalendarEventDto; compact?: boolean }) => {
    const router = useRouter();
    const c = getColor(ev.eventType);
    const isLive = ev.eventType === "Canlı Ders";
    return (
        <div 
            onClick={() => { if (isLive) router.push('/dashboard/live'); }}
            className={`${compact ? "p-2" : "p-3"} rounded-xl border ${c.border} ${c.bg} ${isLive ? "cursor-pointer hover:shadow-md hover:scale-[1.01]" : ""} transition-all`}
        >
            <div className="flex items-start justify-between">
                <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-1.5 mb-0.5">
                        <div className={`w-2 h-2 rounded-full ${c.dot}`} />
                        <span className={`text-[10px] font-bold ${c.text}`}>{ev.eventType}</span>
                        {isLive && <ExternalLink size={9} className={`${c.text} opacity-50`} />}
                    </div>
                    <p className={`${compact ? "text-[11px]" : "text-sm"} font-bold text-[#0A1931] ${compact ? "truncate" : ""}`}>{ev.title}</p>
                    <p className="text-[10px] text-[#A0AEC0] mt-0.5 flex items-center gap-1"><Clock size={9} />{evStartTime(ev)} — {evEndTime(ev)}</p>
                </div>
            </div>
            {!compact && ev.groupName && <p className="text-[9px] text-[#A0AEC0] mt-1 flex items-center gap-1"><Users size={9} />{ev.groupName}</p>}
            {!compact && ev.courseTitle && <p className="text-[9px] text-[#A0AEC0] mt-0.5 flex items-center gap-1"><BookOpen size={9} />{ev.courseTitle}</p>}
            {!compact && ev.description && <p className="text-[10px] text-[#A0AEC0]/70 mt-1.5 line-clamp-2">{ev.description}</p>}
            {isLive && !compact && <p className="text-[9px] text emerald-500 font-bold mt-1.5">🔗 Derse katılmak için tıklayın</p>}
        </div>
    );
});
EventCard.displayName = "EventCard";

export default function StudentCalendarPage() {
    const router = useRouter();
    const { token, currentTenantId: tenantId } = useAuth();

    const [events, setEvents] = useState<CalendarEventDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [currentDate, setCurrentDate] = useState(new Date());
    const [selectedDate, setSelectedDate] = useState<string>(toDateStr(new Date()));
    const [viewMode, setViewMode] = useState<ViewMode>("month");

    const year = currentDate.getFullYear();
    const month = currentDate.getMonth();

    const load = useCallback(async () => {
        if (!token || !tenantId) return;
        setLoading(true);
        try {
            const start = new Date(year, month - 1, 1);
            const end = new Date(year, month + 2, 0);
            const data = await studentCalendarApi.getEvents(token, tenantId, start.toISOString(), end.toISOString());
            setEvents(Array.isArray(data) ? data : []);
        } catch { /* silent */ }
        finally { setLoading(false); }
    }, [token, tenantId, year, month]);

    useEffect(() => { load(); }, [load]);

    const today = toDateStr(new Date());
    const daysInMonth = getDaysInMonth(year, month);
    const firstDay = getFirstDayOfMonth(year, month);
    const getEventsForDate = (date: string) => events.filter(e => evDate(e) === date);
    const selectedEvents = getEventsForDate(selectedDate);

    const navigate = (dir: number) => {
        const d = new Date(currentDate);
        if (viewMode === "day") d.setDate(d.getDate() + dir);
        else if (viewMode === "week") d.setDate(d.getDate() + dir * 7);
        else d.setMonth(d.getMonth() + dir);
        setCurrentDate(d);
        if (viewMode === "day") setSelectedDate(toDateStr(d));
    };

    const goToday = () => { 
        const now = new Date(); 
        setCurrentDate(now); 
        setSelectedDate(toDateStr(now)); 
        setViewMode("day");
    };

    const cells: { day: number | null; date: string }[] = [];
    for (let i = 0; i < firstDay; i++) cells.push({ day: null, date: "" });
    for (let d = 1; d <= daysInMonth; d++) cells.push({ day: d, date: `${year}-${String(month + 1).padStart(2, "0")}-${String(d).padStart(2, "0")}` });

    const weekDates = getWeekDates(currentDate);
    const dayOfWeek = (d: Date) => { const day = d.getDay(); return day === 0 ? 6 : day - 1; };

    const navLabel = viewMode === "day"
        ? `${parseInt(selectedDate.split("-")[2])} ${MONTHS_TR[parseInt(selectedDate.split("-")[1]) - 1]} ${year}`
        : viewMode === "week"
            ? `${weekDates[0].getDate()} – ${weekDates[6].getDate()} ${MONTHS_TR[weekDates[6].getMonth()]} ${weekDates[6].getFullYear()}`
            : `${MONTHS_TR[month]} ${year}`;

    const upcoming = useMemo(() => {
        const todayD = new Date(); todayD.setHours(0, 0, 0, 0);
        const weekLater = new Date(todayD); weekLater.setDate(todayD.getDate() + 7);
        return events.filter(e => { const ed = new Date(e.startDate); return ed >= todayD && ed <= weekLater; })
            .sort((a, b) => a.startDate.localeCompare(b.startDate));
    }, [events]);

    return (
        <div className="space-y-5 pt-16 md:pt-0">
            {/* Header */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-[#0A1931] flex items-center gap-2"><CalIcon size={22} className="text-[#1B3B6F]" /> Takvim</h1>
                    <p className="text-sm text-[#A9A9A9] mt-0.5">Canlı derslerinizi ve etkinliklerinizi takip edin</p>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                    <div className="flex items-center bg-white border border-[#E2E8F0] rounded-xl overflow-hidden">
                        {([["day", "Günlük", Sun], ["week", "Haftalık", Layers], ["month", "Aylık", LayoutGrid]] as const).map(([val, lbl, Icon]) => (
                            <button key={val} onClick={() => setViewMode(val)}
                                className={`px-3 py-2 text-xs font-bold flex items-center gap-1.5 transition-all ${viewMode === val ? "bg-[#0A1931] text-white" : "text-[#A9A9A9] hover:bg-[#E2E8F0]/20"}`}>
                                <Icon size={12} /> {lbl}
                            </button>
                        ))}
                    </div>
                    <button onClick={goToday} className="px-3 py-2 text-xs font-bold bg-white border border-[#E2E8F0] rounded-xl text-[#1B3B6F] hover:bg-[#E2E8F0]/20">Bugün</button>
                    <button onClick={load} className="p-2 rounded-xl bg-white border border-[#E2E8F0] hover:bg-[#E2E8F0]/20 text-[#A9A9A9]"><RefreshCw size={14} /></button>
                </div>
            </div>

            {/* Stats */}
            <div className="flex overflow-x-auto sm:grid sm:grid-cols-3 lg:grid-cols-5 gap-3 pb-2 snap-x hide-scrollbar">
                {Object.entries(eventColors).map(([type, c]) => (
                    <div key={type} className={`${c.bg} rounded-xl p-3 flex items-center gap-2 border ${c.border} shrink-0 w-36 sm:w-auto snap-center`}>
                        <div className={`w-2.5 h-2.5 rounded-full ${c.dot}`} />
                        <span className={`text-xs font-bold ${c.text}`}>{type}</span>
                        <span className={`text-xs font-bold ${c.text} ml-auto`}>{events.filter(e => e.eventType === type).length}</span>
                    </div>
                ))}
            </div>

            {/* Navigation */}
            <div className="flex items-center justify-between bg-white rounded-2xl border border-[#E2E8F0] px-5 py-3">
                <button onClick={() => navigate(-1)} className="p-2 rounded-xl hover:bg-[#E2E8F0]/30"><ChevronLeft size={18} className="text-[#1B3B6F]" /></button>
                <h2 className="text-lg font-bold text-[#0A1931]">{navLabel}</h2>
                <button onClick={() => navigate(1)} className="p-2 rounded-xl hover:bg-[#E2E8F0]/30"><ChevronRight size={18} className="text-[#1B3B6F]" /></button>
            </div>

            {/* Calendar Area */}
            <div>
                {/* Day View */}
                {viewMode === "day" && (
                    <div className="flex flex-col lg:grid lg:grid-cols-10 gap-5">
                        <div className="lg:col-span-7 bg-white rounded-2xl border border-[#E2E8F0] overflow-hidden">
                            <div className="px-5 py-3 border-b border-[#E2E8F0] bg-[#E2E8F0]/10">
                                <p className="text-sm font-bold text-[#0A1931]">{DAYS_FULL_TR[dayOfWeek(currentDate)]}</p>
                                <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest">{selectedDate === today ? "Bugün" : selectedDate}</p>
                            </div>
                            <div className="relative" style={{ height: `${HOURS.length * 64}px` }}>
                                {HOURS.map(h => (
                                    <div key={h} className="absolute w-full flex" style={{ top: `${(h - 7) * 64}px`, height: "64px" }}>
                                        <div className="w-16 text-right pr-3 text-[10px] font-medium text-[#A0AEC0] pt-0.5">{`${String(h).padStart(2, "0")}:00`}</div>
                                        <div className="flex-1 border-t border-[#E2E8F0]/40 relative">
                                            {selectedEvents.filter(ev => evHour(ev) === h).map(ev => {
                                                const col = getColor(ev.eventType);
                                                const dur = Math.max(1, evEndHour(ev) - h);
                                                const isLive = ev.eventType === "Canlı Ders";
                                                return (
                                                    <div key={ev.id}
                                                        onClick={(e) => { if (isLive) { e.stopPropagation(); router.push('/dashboard/live'); } }}
                                                        className={`absolute left-1 right-4 rounded-xl p-2.5 border ${col.border} ${col.bg} ${isLive ? "cursor-pointer hover:shadow-lg" : ""} transition-all z-10`}
                                                        style={{ height: `${dur * 64 - 4}px` }}>
                                                        <div className="flex items-start justify-between">
                                                            <div>
                                                                <p className={`text-xs font-bold ${col.text} flex items-center gap-1`}>{ev.title} {isLive && <ExternalLink size={10} />}</p>
                                                                <p className="text-[10px] text-[#A0AEC0] mt-0.5">{evStartTime(ev)} — {evEndTime(ev)}</p>
                                                            </div>
                                                        </div>
                                                        {ev.courseTitle && <p className="text-[9px] text-[#A0AEC0] mt-0.5 flex items-center gap-1"><BookOpen size={9} />{ev.courseTitle}</p>}
                                                        {isLive && <p className="text-[9px] text-emerald-500 font-bold mt-1">🔗 Derse katıl</p>}
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    </div>
                                ))}
                                {selectedDate === today && (() => {
                                    const now = new Date(); const mins = (now.getHours() - 7) * 64 + (now.getMinutes() / 60) * 64;
                                    if (mins < 0 || mins > HOURS.length * 64) return null;
                                    return (<div className="absolute left-14 right-0 z-20 flex items-center" style={{ top: `${mins}px` }}><div className="w-2.5 h-2.5 rounded-full bg-red-500" /><div className="flex-1 h-0.5 bg-red-500/60" /></div>);
                                })()}
                            </div>
                        </div>
                        <div className="lg:col-span-3 bg-white rounded-2xl border border-[#E2E8F0] p-5 max-h-[700px] overflow-y-auto">
                            <h3 className="text-sm font-bold text-[#0A1931] mb-3">📅 Bugünün Etkinlikleri</h3>
                            {selectedEvents.length > 0 ? (
                                <div className="space-y-2">{selectedEvents.sort((a, b) => a.startDate.localeCompare(b.startDate)).map(ev => <EventCard key={ev.id} ev={ev} />)}</div>
                            ) : (<div className="text-center py-8 text-[#A0AEC0]"><CalIcon size={28} className="mx-auto opacity-20 mb-2" /><p className="text-xs">Etkinlik yok</p></div>)}
                            <div className="mt-5 pt-4 border-t border-[#E2E8F0]">
                                <h3 className="text-sm font-bold text-[#0A1931] mb-3">🔜 Yaklaşan (7 gün)</h3>
                                {upcoming.length > 0 ? (
                                    <div className="space-y-1.5">{upcoming.slice(0, 8).map(ev => {
                                        const col = getColor(ev.eventType);
                                        return (<div key={ev.id} className="flex items-center gap-2 p-2 rounded-lg hover:bg-[#E2E8F0]/15">
                                            <div className={`w-2 h-2 rounded-full ${col.dot}`} />
                                            <div className="flex-1 min-w-0"><p className="text-xs font-medium text-[#0A1931] truncate">{ev.title}</p>
                                                <p className="text-[9px] text-[#A0AEC0]">{new Date(ev.startDate).toLocaleDateString("tr-TR", { day: "numeric", month: "short" })} · {evStartTime(ev)}</p></div>
                                        </div>);
                                    })}</div>
                                ) : <p className="text-xs text-[#A0AEC0]">Yaklaşan etkinlik yok</p>}
                            </div>
                        </div>
                    </div>
                )}

                {/* Week View */}
                {viewMode === "week" && (
                    <div className="bg-white rounded-2xl border border-[#E2E8F0] overflow-x-auto">
                        <div className="min-w-[800px]">
                            <div className="grid grid-cols-8 border-b border-[#E2E8F0]">
                            <div className="w-16 p-3" />
                            {weekDates.map((wd, i) => {
                                const ds = toDateStr(wd);
                                return (
                                    <div key={ds} className={`p-3 text-center border-l border-[#E2E8F0] cursor-pointer hover:bg-[#E2E8F0]/10 ${ds === today ? "bg-[#1B3B6F]/5" : ""}`}
                                        onClick={() => { setSelectedDate(ds); setCurrentDate(wd); setViewMode("day"); }}>
                                        <p className="text-[10px] font-bold text-[#A0AEC0] uppercase">{DAYS_TR[i]}</p>
                                        <p className={`text-lg font-bold mt-0.5 ${ds === today ? "w-8 h-8 bg-[#1B3B6F] text-white rounded-full flex items-center justify-center mx-auto" : "text-[#0A1931]"}`}>{wd.getDate()}</p>
                                    </div>
                                );
                            })}
                        </div>
                        <div className="relative" style={{ height: `${HOURS.length * 52}px` }}>
                            {HOURS.map(h => (
                                <div key={h} className="absolute w-full flex" style={{ top: `${(h - 7) * 52}px`, height: "52px" }}>
                                    <div className="w-16 text-right pr-3 text-[9px] font-medium text-[#A0AEC0] pt-0.5">{`${String(h).padStart(2, "0")}:00`}</div>
                                    <div className="flex-1 grid grid-cols-7 border-t border-[#E2E8F0]/30">
                                        {weekDates.map((wd) => {
                                            const ds = toDateStr(wd);
                                            const evs = getEventsForDate(ds).filter(ev => evHour(ev) === h);
                                            return (
                                                <div key={ds} className="relative border-l border-[#E2E8F0]/20 cursor-pointer hover:bg-[#1B3B6F]/[0.03] transition-colors"
                                                    onClick={() => { setSelectedDate(ds); setCurrentDate(wd); setViewMode("day"); }}>
                                                    {evs.map(ev => {
                                                        const col = getColor(ev.eventType);
                                                        return (
                                                            <div key={ev.id}
                                                                onClick={(e) => { if (ev.eventType === "Canlı Ders") { e.stopPropagation(); router.push('/dashboard/live'); } }}
                                                                className={`absolute inset-x-0.5 top-0.5 rounded-lg px-1.5 py-1 border ${col.border} ${col.bg} ${ev.eventType === "Canlı Ders" ? "cursor-pointer" : ""} z-10`}
                                                                style={{ height: `${Math.max(1, evEndHour(ev) - h) * 52 - 4}px` }}>
                                                                <p className={`text-[9px] font-bold ${col.text} truncate`}>{ev.title}</p>
                                                                <p className="text-[8px] text-[#A0AEC0] truncate">{evStartTime(ev)}</p>
                                                            </div>
                                                        );
                                                    })}
                                                </div>
                                            );
                                        })}
                                    </div>
                                </div>
                            ))}
                        </div>
                        </div>
                    </div>
                )}

                {/* Month View */}
                {viewMode === "month" && (
                    <div className="flex flex-col lg:grid lg:grid-cols-10 gap-5">
                        <div className="lg:col-span-7 bg-white rounded-2xl border border-[#E2E8F0] p-2 sm:p-5 overflow-x-hidden sm:overflow-x-auto">
                            <div className="min-w-0">
                                <div className="grid grid-cols-7 mb-2">
                                {DAYS_TR.map(d => <div key={d} className="text-center text-[10px] sm:text-xs font-bold text-[#A0AEC0] py-2">{d}</div>)}
                            </div>
                            <div className="grid grid-cols-7 gap-1">
                                {loading ? Array.from({ length: 35 }).map((_, i) => <div key={i} className="h-24 rounded-xl bg-[#E2E8F0]/20 animate-pulse" />) :
                                    cells.map((cell, i) => {
                                        if (!cell.day) return <div key={`empty-${i}`} className="h-24" />;
                                        const dayEvents = getEventsForDate(cell.date);
                                        const isSelected = selectedDate === cell.date;
                                        const isToday = cell.date === today;
                                        return (
                                            <button key={cell.date} onClick={() => setSelectedDate(cell.date)}
                                                onDoubleClick={() => { setSelectedDate(cell.date); setCurrentDate(new Date(cell.date)); setViewMode("day"); }}
                                                className={`aspect-square sm:h-24 sm:aspect-auto rounded-lg sm:rounded-xl p-1 sm:p-2 text-center sm:text-left flex flex-col transition-all hover:bg-[#E2E8F0]/20
                                                    ${isSelected ? "bg-[#1B3B6F]/5 ring-2 ring-[#1B3B6F]/20" : ""}
                                                    ${isToday && !isSelected ? "bg-[#E2E8F0]/20" : ""}`}>
                                                <span className={`text-[10px] sm:text-xs font-bold mb-1 mx-auto sm:mx-0 ${isToday ? "w-5 h-5 sm:w-6 sm:h-6 bg-[#1B3B6F] text-white rounded-full flex items-center justify-center" : "text-[#1B3B6F]"}`}>{cell.day}</span>
                                                
                                                {/* Desktop: Texts */}
                                                <div className="hidden sm:flex flex-1 flex-col space-y-0.5 overflow-hidden w-full">
                                                    {dayEvents.slice(0, 2).map(ev => {
                                                        const col = getColor(ev.eventType);
                                                        return (<div key={ev.id} className={`text-[9px] font-medium px-1.5 py-0.5 rounded ${col.bg} ${col.text} truncate w-full`}>{ev.title}</div>);
                                                    })}
                                                    {dayEvents.length > 2 && <span className="text-[9px] text-[#A0AEC0] px-1">+{dayEvents.length - 2}</span>}
                                                </div>

                                                {/* Mobile: Dots */}
                                                <div className="flex sm:hidden flex-wrap gap-0.5 justify-center mt-auto w-full pb-1">
                                                    {dayEvents.slice(0, 3).map(ev => {
                                                        const col = getColor(ev.eventType);
                                                        return (<div key={ev.id} className={`w-1.5 h-1.5 rounded-full ${col.dot}`} />);
                                                    })}
                                                    {dayEvents.length > 3 && <div className="w-1 h-1 rounded-full bg-[#A0AEC0] self-center" />}
                                                </div>
                                            </button>
                                        );
                                    })}
                                </div>
                            </div>
                        </div>
                        <div className="lg:col-span-3 bg-white rounded-2xl border border-[#E2E8F0] p-5 max-h-[650px] overflow-y-auto">
                            <h3 className="text-sm font-bold text-[#0A1931] mb-4">
                                {selectedDate ? `${parseInt(selectedDate.split("-")[2])} ${MONTHS_TR[parseInt(selectedDate.split("-")[1]) - 1]}` : "Tarih Seçin"}
                            </h3>
                            {selectedEvents.length > 0 ? (
                                <div className="space-y-3">{selectedEvents.map(ev => <EventCard key={ev.id} ev={ev} />)}</div>
                            ) : (
                                <div className="flex flex-col items-center justify-center py-12 text-[#A0AEC0]">
                                    <CalIcon size={32} className="mx-auto opacity-20 mb-2" /><p className="text-sm">Bu tarihte etkinlik yok</p>
                                </div>
                            )}
                            <div className="mt-5 pt-4 border-t border-[#E2E8F0]">
                                <h3 className="text-sm font-bold text-[#0A1931] mb-3">🔜 Yaklaşan (7 gün)</h3>
                                {upcoming.length > 0 ? (
                                    <div className="space-y-1.5">{upcoming.slice(0, 8).map(ev => {
                                        const col = getColor(ev.eventType);
                                        return (<div key={ev.id} className="flex items-center gap-2 p-2 rounded-lg hover:bg-[#E2E8F0]/15">
                                            <div className={`w-2 h-2 rounded-full ${col.dot}`} />
                                            <div className="flex-1 min-w-0"><p className="text-xs font-medium text-[#0A1931] truncate">{ev.title}</p>
                                                <p className="text-[9px] text-[#A0AEC0]">{new Date(ev.startDate).toLocaleDateString("tr-TR", { day: "numeric", month: "short" })} · {evStartTime(ev)}</p></div>
                                        </div>);
                                    })}</div>
                                ) : <p className="text-xs text-[#A0AEC0]">Yaklaşan etkinlik yok</p>}
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}
