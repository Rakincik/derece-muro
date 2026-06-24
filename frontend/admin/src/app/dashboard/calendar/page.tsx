"use client";

import { useState, useEffect, useCallback, useMemo, useRef } from "react";
import { useRouter } from "next/navigation";
import {
    Calendar as CalIcon, Plus, ChevronLeft, ChevronRight, X,
    Clock, Users, Edit3, Trash2, RefreshCw, Layers, Sun, LayoutGrid,
    BookOpen, GripVertical, Video, ExternalLink,
    Palmtree, FileText, ClipboardList, Presentation, PartyPopper
} from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/components/toast";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { calendarApi, courseApi, groupsApi, type CalendarEventDto, type CreateCalendarEventRequest, type CourseListDto, type GroupListDto } from "@/lib/api";

const eventColors: Record<string, { bg: string; border: string; text: string; dot: string }> = {
    "Canlı Ders": { bg: "bg-emerald-50", border: "border-emerald-200", text: "text-emerald-700", dot: "bg-emerald-500" },
    "Sınav": { bg: "bg-blue-50", border: "border-blue-200", text: "text-blue-700", dot: "bg-blue-500" },
    "Ödev": { bg: "bg-amber-50", border: "border-amber-200", text: "text-amber-700", dot: "bg-amber-500" },
    "Toplantı": { bg: "bg-violet-50", border: "border-violet-200", text: "text-violet-700", dot: "bg-violet-500" },
    "Etkinlik": { bg: "bg-rose-50", border: "border-rose-200", text: "text-rose-700", dot: "bg-rose-500" },
    "Tatil": { bg: "bg-orange-50", border: "border-orange-200", text: "text-orange-700", dot: "bg-orange-500" },
};
const eventIcons: Record<string, React.ElementType> = {
    "Canlı Ders": Video, "Sınav": FileText, "Ödev": ClipboardList,
    "Toplantı": Presentation, "Etkinlik": PartyPopper, "Tatil": Palmtree,
};
const getColor = (type: string) => eventColors[type] ?? eventColors["Etkinlik"];

const DAYS_TR = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];
const DAYS_FULL_TR = ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar"];
const MONTHS_TR = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];
const HOURS = Array.from({ length: 17 }, (_, i) => i + 7);

type ViewMode = "day" | "week" | "month";

function getDaysInMonth(y: number, m: number) { return new Date(y, m + 1, 0).getDate(); }
function getFirstDayOfMonth(y: number, m: number) { const d = new Date(y, m, 1).getDay(); return d === 0 ? 6 : d - 1; }
function toDateStr(d: Date) { return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`; }
function getWeekDates(date: Date): Date[] {
    const d = new Date(date); const day = d.getDay() === 0 ? 6 : d.getDay() - 1; d.setDate(d.getDate() - day);
    return Array.from({ length: 7 }, (_, i) => { const nd = new Date(d); nd.setDate(d.getDate() + i); return nd; });
}

// Helpers: parse ISO date from backend
function evDate(ev: CalendarEventDto) { return ev.startDate?.substring(0, 10) ?? ""; }
function evStartTime(ev: CalendarEventDto) { const d = new Date(ev.startDate); return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`; }
function evEndTime(ev: CalendarEventDto) { const d = new Date(ev.endDate); return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`; }
function evHour(ev: CalendarEventDto) { return new Date(ev.startDate).getHours(); }
function evEndHour(ev: CalendarEventDto) { return new Date(ev.endDate).getHours(); }

export default function CalendarPage() {
    const router = useRouter();
    const { token, currentTenantId: tenantId, user } = useAuth();
    const isInstructor = user?.role === "Eğitmen" || user?.role === "Instructor";
    const { success, error: toastError } = useToast();

    const [events, setEvents] = useState<CalendarEventDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [currentDate, setCurrentDate] = useState(new Date());
    const [selectedDate, setSelectedDate] = useState<string>(toDateStr(new Date()));
    const [showForm, setShowForm] = useState(false);
    const [editEvent, setEditEvent] = useState<CalendarEventDto | null>(null);
    const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
    const [viewMode, setViewMode] = useState<ViewMode>("month");
    const [defaultTime, setDefaultTime] = useState<string | null>(null);
    const [quickFormMode, setQuickFormMode] = useState(false);

    const [resizingEvent, setResizingEvent] = useState<{ id: string, startY: number, initialHeight: number, mode: "day" | "week" } | null>(null);
    const resizeHeightRef = useRef<number | null>(null);
    const isResizingRef = useRef(false);

    // Courses and Groups for scheduling & drag & drop
    const [courses, setCourses] = useState<CourseListDto[]>([]);
    const [groups, setGroups] = useState<GroupListDto[]>([]);
    const [courseSearch, setCourseSearch] = useState("");
    const [dragCourse, setDragCourse] = useState<CourseListDto | null>(null);
    const [dragEvent, setDragEvent] = useState<CalendarEventDto | null>(null);
    const [droppedCourse, setDroppedCourse] = useState<CourseListDto | null>(null);
    const [dropTarget, setDropTarget] = useState<{ date: string; hour?: number } | null>(null);

    const year = currentDate.getFullYear();
    const month = currentDate.getMonth();

    const load = useCallback(async () => {
        if (!token || !tenantId) return;
        setLoading(true);
        try {
            const data = await calendarApi.list(token, tenantId, { year, month: month + 1 });
            setEvents(Array.isArray(data) ? data : []);
        } catch { toastError("Hata", "Takvim yüklenemedi."); }
        finally { setLoading(false); }
    }, [token, tenantId, year, month]);

    useEffect(() => { load(); }, [token, tenantId]); // eslint-disable-line react-hooks/exhaustive-deps

    // ── Drag-to-Resize Logic ──────────────────────────────────────────────────
    useEffect(() => {
        if (!resizingEvent) return;

        const handleMouseMove = (e: MouseEvent) => {
            const deltaY = e.clientY - resizingEvent.startY;
            const newHeight = Math.max(20, resizingEvent.initialHeight + deltaY);
            resizeHeightRef.current = newHeight;
            
            // Bypass React rendering for 60fps smooth drag
            const el = document.getElementById(`event-${resizingEvent.id}`);
            if (el) el.style.height = `${newHeight}px`;
        };

        const handleMouseUp = async (e: MouseEvent) => {
            if (!resizingEvent || resizeHeightRef.current === null || !token || !tenantId) return;
            const pxPerHour = resizingEvent.mode === "day" ? 76 : 60;
            const deltaY = resizeHeightRef.current - resizingEvent.initialHeight;
            // Snapping to closest 30 min (0.5 hour) or 15 min. Let's do 15 min (0.25 hour)
            let hoursAdded = Math.round((deltaY / pxPerHour) * 4) / 4; 
            
            if (hoursAdded !== 0) {
                const ev = events.find(x => x.id === resizingEvent.id);
                if (ev) {
                    const oldStart = new Date(ev.startDate);
                    const oldEnd = new Date(ev.endDate);
                    const newEnd = new Date(oldEnd.getTime() + hoursAdded * 60 * 60 * 1000);
                    
                    if (newEnd > oldStart) {
                        try {
                            const updated = await calendarApi.update(token, tenantId, ev.id, {
                                ...ev,
                                endDate: newEnd.toISOString()
                            });
                            setEvents(prev => prev.map(x => x.id === ev.id ? updated : x));
                            success("Süre güncellendi");
                        } catch {
                            toastError("Hata", "Süre güncellenemedi.");
                        }
                    }
                }
            }
            setResizingEvent(null);
            resizeHeightRef.current = null;
            setTimeout(() => { isResizingRef.current = false; }, 100);
        };

        window.addEventListener("mousemove", handleMouseMove);
        window.addEventListener("mouseup", handleMouseUp);
        return () => {
            window.removeEventListener("mousemove", handleMouseMove);
            window.removeEventListener("mouseup", handleMouseUp);
        };
    }, [resizingEvent, events, token, tenantId]);

    useEffect(() => {
        if (!token || !tenantId) return;
        courseApi.list(token, tenantId, { pageSize: 1000 }).then(res => setCourses(res.items ?? [])).catch(() => { });
        groupsApi.list(token, tenantId, { pageSize: 1000 }).then(res => setGroups(res.items ?? [])).catch(() => { });
    }, [token, tenantId]);

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

    const goToday = () => { const now = new Date(); setCurrentDate(now); setSelectedDate(toDateStr(now)); };

    const handleDelete = async () => {
        if (!token || !tenantId || !deleteTarget) return;
        try { await calendarApi.delete(token, tenantId, deleteTarget); setEvents(prev => prev.filter(e => e.id !== deleteTarget)); setDeleteTarget(null); success("Etkinlik silindi"); }
        catch { toastError("Hata", "Silinemedi."); }
    };

    const handleSave = async (data: CreateCalendarEventRequest) => {
        if (!token || !tenantId) return;
        try {
            if (editEvent) { const u = await calendarApi.update(token, tenantId, editEvent.id, data); setEvents(prev => prev.map(e => e.id === editEvent.id ? u : e)); success("Etkinlik güncellendi"); }
            else { const c = await calendarApi.create(token, tenantId, data); setEvents(prev => [...prev, c]); success("Etkinlik oluşturuldu"); }
            setShowForm(false); setEditEvent(null);
        } catch (err: any) { toastError("Hata", err?.message || "Kaydedilemedi."); }
    };

    // ── Drag & Drop: Move Events or Prepare New Event from Course ──
    const handleDrop = async (date: string, hour?: number) => {
        if (!token || !tenantId) return;

        if (dragEvent) {
            const oldStart = new Date(dragEvent.startDate);
            const oldEnd = new Date(dragEvent.endDate);
            const durationMs = oldEnd.getTime() - oldStart.getTime();

            const newStartHour = hour ?? oldStart.getHours();
            const newStartMin = oldStart.getMinutes();

            const newStart = new Date(`${date}T${String(newStartHour).padStart(2, "0")}:${String(newStartMin).padStart(2, "0")}:00`);
            const newEnd = new Date(newStart.getTime() + durationMs);

            try {
                const updated = await calendarApi.update(token, tenantId, dragEvent.id, {
                    title: dragEvent.title,
                    startDate: newStart.toISOString(),
                    endDate: newEnd.toISOString(),
                    eventType: dragEvent.eventType,
                    description: dragEvent.description,
                    courseId: dragEvent.courseId,
                    groupId: dragEvent.groupId,
                });
                setEvents(prev => prev.map(e => e.id === dragEvent.id ? updated : e));
                success("Etkinlik Taşındı", `${dragEvent.title} yeni tarihine alındı.`);
            } catch (err: any) {
                toastError("Hata", err?.message || "Etkinlik taşınamadı.");
            } finally {
                setDragEvent(null);
                setDropTarget(null);
            }
            return;
        }

        if (dragCourse) {
            if (hour !== undefined) {
                const startStr = new Date(`${date}T${String(hour).padStart(2, "0")}:00:00`).toISOString();
                const endStr = new Date(`${date}T${String(hour + 1).padStart(2, "0")}:00:00`).toISOString();
                calendarApi.create(token, tenantId, {
                    title: `${dragCourse.title} — Canlı Ders`,
                    startDate: startStr,
                    endDate: endStr,
                    eventType: "Canlı Ders",
                    description: `${dragCourse.title} dersi için planlanan canlı oturum`,
                    courseId: dragCourse.id
                }).then(c => {
                    setEvents(prev => [...prev, c]);
                    success("Etkinlik Oluşturuldu", `${dragCourse.title} için canlı ders eklendi.`);
                }).catch(err => {
                    toastError("Hata", err?.message || "Oluşturulamadı.");
                }).finally(() => {
                    setDragCourse(null);
                    setDropTarget(null);
                });
                return;
            }

            setSelectedDate(date);
            setDefaultTime("10:00");
            setDroppedCourse(dragCourse);
            setQuickFormMode(true);
            setShowForm(true);
            setDragCourse(null);
            setDropTarget(null);
            return;
        }
    };

    // Navigate to course page when clicking an event with courseId
    const handleEventClick = (ev: CalendarEventDto) => {
        if (isResizingRef.current) return;
        if (ev.courseId) {
            router.push(`/dashboard/courses?courseId=${ev.courseId}`);
        }
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

    const filteredCourses = courses.filter(c => !courseSearch || c.title.toLocaleLowerCase('tr-TR').includes(courseSearch.toLocaleLowerCase('tr-TR')));

    const upcoming = useMemo(() => {
        const todayD = new Date(); todayD.setHours(0, 0, 0, 0);
        const weekLater = new Date(todayD); weekLater.setDate(todayD.getDate() + 7);
        return events.filter(e => { const ed = new Date(e.startDate); return ed >= todayD && ed <= weekLater; })
            .sort((a, b) => a.startDate.localeCompare(b.startDate));
    }, [events]);

    // Renders an event card — click navigates to courses if Canlı Ders
    const EventCard = ({ ev, compact }: { ev: CalendarEventDto; compact?: boolean }) => {
        const c = getColor(ev.eventType);
        const isLive = ev.eventType === "Canlı Ders";
        return (
            <div onClick={() => handleEventClick(ev)}
                draggable
                onDragStart={(e) => { e.stopPropagation(); setDragEvent(ev); }}
                onDragEnd={() => { setDragEvent(null); setDropTarget(null); }}
                className={`p-${compact ? "2" : "4"} rounded-xl border ${c.border} ${c.bg} group cursor-grab active:cursor-grabbing hover:shadow-md hover:scale-[1.01] transition-all`}>
                <div className="flex items-start justify-between">
                    <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-1.5 mb-1">
                            {(() => { const Ic = eventIcons[ev.eventType]; return Ic ? <Ic size={12} className={c.text} /> : <div className={`w-2.5 h-2.5 rounded-full ${c.dot}`} />; })()}
                            <span className={`text-xs font-bold ${c.text}`}>{ev.eventType}</span>
                            {isLive && <ExternalLink size={12} className={`${c.text} opacity-50`} />}
                        </div>
                        <p className={`text-${compact ? "sm" : "base"} font-bold text-[#0A1931] ${compact ? "truncate" : ""}`}>{ev.title}</p>
                        <p className="text-xs text-[#A0AEC0] mt-1 flex items-center gap-1.5"><Clock size={12} />{evStartTime(ev)} — {evEndTime(ev)}</p>
                    </div>
                    <div className="flex gap-1 opacity-0 group-hover:opacity-100 shrink-0 ml-2" onClick={e => e.stopPropagation()}>
                        <button onClick={() => { setEditEvent(ev); setShowForm(true); }} className="p-1.5 rounded hover:bg-white/80 text-[#A0AEC0]"><Edit3 size={14} /></button>
                        <button onClick={() => setDeleteTarget(ev.id)} className="p-1.5 rounded hover:bg-white/80 text-[#A0AEC0] hover:text-red-500"><Trash2 size={14} /></button>
                    </div>
                </div>
                {!compact && ev.groupName && <p className="text-xs text-[#A0AEC0] mt-2 flex items-center gap-1.5"><Users size={12} />{ev.groupName}</p>}
                {!compact && ev.courseName && <p className="text-xs text-[#A0AEC0] mt-1 flex items-center gap-1.5"><BookOpen size={12} />{ev.courseName}</p>}
                {!compact && ev.description && <p className="text-sm text-[#A0AEC0]/80 mt-2 line-clamp-2 leading-relaxed">{ev.description}</p>}
                {isLive && !compact && <p className="text-xs text-emerald-600 font-bold mt-2.5 flex items-center gap-1">🔗 Dersi başlatmak için tıklayın</p>}
            </div>
        );
    };

    return (
        <div className="space-y-5">
            {/* Header — clean single bar */}
            <div className="flex flex-col xl:flex-row xl:items-center justify-between bg-white rounded-2xl border border-[#E2E8F0] p-4 xl:px-6 gap-4">
                <div className="flex items-center gap-2 xl:gap-4 overflow-x-auto hide-scrollbar w-full xl:w-auto pb-1 xl:pb-0">
                    <h1 className="text-lg font-bold text-[#0A1931] flex items-center gap-2 shrink-0"><CalIcon size={20} className="text-[#1B3B6F]" /> Takvim</h1>
                    <div className="w-px h-6 bg-[#E2E8F0] shrink-0" />
                    <button onClick={() => navigate(-1)} className="p-1.5 rounded-xl hover:bg-[#E2E8F0]/40 transition-colors shrink-0"><ChevronLeft size={18} className="text-[#1B3B6F]" /></button>
                    <span className="text-sm xl:text-base font-bold text-[#0A1931] min-w-[120px] xl:min-w-[150px] text-center shrink-0">{navLabel}</span>
                    <button onClick={() => navigate(1)} className="p-1.5 rounded-xl hover:bg-[#E2E8F0]/40 transition-colors shrink-0"><ChevronRight size={18} className="text-[#1B3B6F]" /></button>
                    <button onClick={goToday} className="px-3 py-1.5 text-xs font-bold text-[#1B3B6F] hover:bg-[#E2E8F0]/40 rounded-xl transition-colors shrink-0">Bugün</button>
                </div>
                <div className="flex items-center gap-2 overflow-x-auto hide-scrollbar w-full xl:w-auto pb-1 xl:pb-0">
                    <div className="flex items-center bg-[#E2E8F0]/20 rounded-xl overflow-hidden mr-0 xl:mr-2 p-1 shrink-0">
                        {([["day", "Gün", Sun], ["week", "Hafta", Layers], ["month", "Ay", LayoutGrid]] as const).map(([val, lbl, Icon]) => (
                            <button key={val} onClick={() => setViewMode(val)}
                                className={`px-3 py-2 xl:px-5 xl:py-2.5 text-xs xl:text-sm font-bold flex items-center gap-1.5 xl:gap-2 transition-all ${viewMode === val ? "bg-[#0A1931] text-white rounded-lg shadow-sm" : "text-[#A9A9A9] hover:text-[#1B3B6F]"}`}>
                                <Icon size={14} className="xl:w-4 xl:h-4" /> <span className="hidden sm:inline">{lbl}</span>
                            </button>
                        ))}
                    </div>
                    <button onClick={load} className="p-2 xl:p-2.5 rounded-xl border border-[#E2E8F0] hover:bg-[#E2E8F0]/30 text-[#A9A9A9] transition-colors shrink-0"><RefreshCw size={14} className="xl:w-4 xl:h-4" /></button>
                    <button onClick={() => { setEditEvent(null); setShowForm(true); }}
                        className="px-3 py-2 xl:px-4 xl:py-2.5 text-xs xl:text-sm font-bold bg-[#0A1931] text-white rounded-xl hover:bg-[#1B3B6F] flex items-center gap-1.5 xl:gap-2 shadow-lg shadow-[#0A1931]/10 transition-colors shrink-0">
                        <Plus size={14} className="xl:w-4 xl:h-4" /> <span className="whitespace-nowrap">Yeni Etkinlik</span>
                    </button>
                </div>
            </div>

            <div className="flex flex-col xl:flex-row gap-4 xl:items-start" style={{ minHeight: "calc(100vh - 140px)" }}>
                {/* ── Courses Sidebar (always visible) ── */}
                <div className="w-full xl:w-64 h-[250px] xl:h-[calc(100vh-140px)] xl:sticky xl:top-6 shrink-0 bg-white rounded-2xl border border-[#E2E8F0] overflow-hidden flex flex-col shadow-sm">
                    <div className="p-4 border-b border-[#E2E8F0] bg-[#F8FAFC]">
                        <div className="flex items-center gap-2 mb-3">
                            <BookOpen size={16} className="text-emerald-600" />
                            <p className="text-sm font-bold text-[#0A1931]">Dersler</p>
                            <span className="ml-auto text-xs font-bold text-[#64748B] bg-white border border-[#E2E8F0] px-2 py-0.5 rounded-full">{courses.length}</span>
                        </div>
                        <input value={courseSearch} onChange={e => setCourseSearch(e.target.value)} placeholder="Derslerde ara..."
                            className="w-full px-3 py-2 text-xs bg-white border border-[#E2E8F0] rounded-xl text-[#0A1931] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 placeholder:text-[#94A3B8] shadow-sm transition-all" />
                    </div>
                    <div className="flex-1 overflow-y-auto p-2 space-y-1 custom-scrollbar">
                        {filteredCourses.map(c => (
                            <div key={c.id} draggable onDragStart={() => setDragCourse(c)} onDragEnd={() => { setDragCourse(null); setDropTarget(null); }}
                                onClick={() => dragCourse?.id === c.id ? setDragCourse(null) : setDragCourse(c)}
                                className={`flex items-center gap-2.5 px-3 py-3 rounded-xl transition-all group ${dragCourse?.id === c.id ? 'bg-emerald-50 border-emerald-300 ring-2 ring-emerald-100 shadow-sm' : 'bg-transparent hover:bg-emerald-50 border border-transparent hover:border-emerald-200 cursor-grab active:cursor-grabbing'}`}>
                                <GripVertical size={14} className="text-[#CBD5E1] group-hover:text-emerald-500 shrink-0" />
                                <div className="flex-1 min-w-0">
                                    <p className="text-sm font-bold text-[#1E293B] truncate group-hover:text-emerald-800">{c.title}</p>
                                    <p className="text-[11px] font-medium text-[#64748B] mt-0.5">{c.sessionCount ?? 0} oturum</p>
                                </div>
                            </div>
                        ))}
                        {filteredCourses.length === 0 && (
                            <div className="text-center py-10 text-[#94A3B8]"><BookOpen size={24} className="mx-auto opacity-20 mb-3" /><p className="text-xs font-medium">Ders bulunamadı</p></div>
                        )}
                    </div>
                    <div className="p-3 border-t border-[#E2E8F0] bg-[#F8FAFC]">
                        <p className={`text-[11px] font-bold text-center flex justify-center items-center gap-1.5 ${dragCourse ? 'text-emerald-600' : 'text-[#64748B]'}`}>
                            {dragCourse ? "✨ Şimdi takvimde bir saate dokunun" : "☘️ Sürükle bırak veya dokunarak seç"}
                        </p>
                    </div>
                </div>

                {/* ── Calendar Area (full width when sidebar hidden) ── */}
                <div className="flex-1 min-w-0">

                    {/* Day View */}
                    {viewMode === "day" && (
                        <div className="flex flex-col lg:grid lg:grid-cols-10 gap-5">
                            <div className="lg:col-span-7 bg-white rounded-2xl border border-[#E2E8F0] overflow-hidden">
                                <div className="px-6 py-4 border-b border-[#E2E8F0] bg-[#F8FAFC]">
                                    <p className="text-lg font-bold text-[#0A1931]">{DAYS_FULL_TR[dayOfWeek(currentDate)]}</p>
                                    <p className="text-xs font-bold text-[#64748B] uppercase tracking-widest mt-0.5">{selectedDate === today ? "Bugün" : selectedDate}</p>
                                </div>
                                <div className="relative" style={{ height: `${HOURS.length * 76}px` }}>
                                    {HOURS.map(h => (
                                        <div key={h} className="absolute w-full flex" style={{ top: `${(h - 7) * 76}px`, height: "76px" }}
                                            onClick={() => { 
                                                if (isResizingRef.current) return;
                                                if (dragCourse) { handleDrop(selectedDate, h); } 
                                                else { setEditEvent(null); setDefaultTime(`${String(h).padStart(2, "0")}:00`); setShowForm(true); }
                                            }}
                                            onDragOver={e => { e.preventDefault(); setDropTarget({ date: selectedDate, hour: h }); }}
                                            onDragLeave={() => setDropTarget(null)}
                                            onDrop={e => { e.preventDefault(); handleDrop(selectedDate, h); }}>
                                            <div className="w-20 text-right pr-4 text-xs font-bold text-[#94A3B8] pt-1.5">{`${String(h).padStart(2, "0")}:00`}</div>
                                            <div className={`flex-1 border-t cursor-pointer hover:bg-[#1B3B6F]/[0.02] transition-colors ${dropTarget?.date === selectedDate && dropTarget?.hour === h ? "border-emerald-400 bg-emerald-50/50" : "border-[#E2E8F0]"} relative`}>
                                                {(() => {
                                                    const evs = selectedEvents.filter(ev => evHour(ev) === h);
                                                    return evs.map((ev, idx) => {
                                                        const col = getColor(ev.eventType);
                                                        const dur = Math.max(1, evEndHour(ev) - h);
                                                        const isLive = ev.eventType === "Canlı Ders";
                                                        // Çakışan (aynı saatteki) etkinlikleri yan yana dizmek için dinamik genişlik
                                                        const widthPercent = 100 / evs.length;
                                                        const leftPercent = idx * widthPercent;
                                                        
                                                        const isResizing = resizingEvent?.id === ev.id;
                                                        const finalHeight = isResizing && resizeHeightRef.current !== null ? `${resizeHeightRef.current}px` : `${dur * 76 - 6}px`;

                                                        return (
                                                            <div key={ev.id} id={`event-${ev.id}`} onClick={() => handleEventClick(ev)}
                                                                draggable={!isResizing}
                                                                onDragStart={(e) => { if(isResizing) return e.preventDefault(); e.stopPropagation(); setDragEvent(ev); }}
                                                                onDragEnd={() => { setDragEvent(null); setDropTarget(null); }}
                                                                className={`absolute rounded-2xl p-4 border ${col.border} ${col.bg} group ${!isResizing ? 'cursor-grab active:cursor-grabbing' : ''} hover:shadow-lg hover:z-20 transition-all z-10 overflow-hidden flex flex-col`}
                                                                style={{ 
                                                                    height: finalHeight,
                                                                    left: `calc(${leftPercent}% + 8px)`,
                                                                    width: `calc(${widthPercent}% - 16px)`
                                                                }}>
                                                                <div className="absolute top-2 right-2 flex gap-1 opacity-0 group-hover:opacity-100 shrink-0 bg-white/50 backdrop-blur-sm rounded-lg p-0.5 z-20" onClick={e => e.stopPropagation()}>
                                                                    <button onClick={() => { setEditEvent(ev); setShowForm(true); }} className="p-1 rounded-md hover:bg-white text-[#64748B] hover:text-[#0A1931] shadow-sm"><Edit3 size={14} /></button>
                                                                    <button onClick={() => setDeleteTarget(ev.id)} className="p-1 rounded-md hover:bg-red-50 text-[#64748B] hover:text-red-500 shadow-sm"><Trash2 size={14} /></button>
                                                                </div>

                                                                <div className={`flex flex-col min-h-0 w-full h-full ${dur > 1 ? 'items-center justify-center text-center -mt-3' : 'justify-start'}`}>
                                                                    <div className="min-w-0 px-1 w-full">
                                                                        <p className={`font-bold ${col.text} flex items-center gap-2 truncate ${dur > 1 ? 'text-lg justify-center mb-1' : 'text-sm'}`}>
                                                                            {ev.title} {isLive && <ExternalLink size={dur > 1 ? 16 : 14} className="shrink-0 opacity-70" />}
                                                                        </p>
                                                                        <p className={`font-medium text-[#64748B] ${dur > 1 ? 'text-sm' : 'text-xs mt-1.5'}`}>
                                                                            {evStartTime(ev)} — {evEndTime(ev)}
                                                                        </p>
                                                                    </div>
                                                                    {isLive && evs.length === 1 && (
                                                                        <p className={`text-emerald-600 font-bold truncate pb-1 ${dur > 1 ? 'text-sm mt-3' : 'text-xs mt-2.5'}`}>
                                                                            🔗 Dersi başlatmak için tıkla
                                                                        </p>
                                                                    )}
                                                                </div>
                                                                
                                                                {/* Resize Handle */}
                                                                <div 
                                                                    onMouseDown={(e) => {
                                                                        e.stopPropagation();
                                                                        isResizingRef.current = true;
                                                                        setResizingEvent({ id: ev.id, startY: e.clientY, initialHeight: dur * 76 - 6, mode: "day" });
                                                                        resizeHeightRef.current = dur * 76 - 6;
                                                                    }}
                                                                    className="absolute bottom-0 left-0 right-0 h-3 cursor-ns-resize hover:bg-black/10 flex items-center justify-center rounded-b-2xl group/resize"
                                                                >
                                                                    <div className="w-8 h-1 rounded-full bg-black/20 group-hover/resize:bg-black/40" />
                                                                </div>
                                                            </div>
                                                        );
                                                    });
                                                })()}
                                            </div>
                                        </div>
                                    ))}
                                    {selectedDate === today && (() => {
                                        const now = new Date(); const mins = (now.getHours() - 7) * 76 + (now.getMinutes() / 60) * 76;
                                        if (mins < 0 || mins > HOURS.length * 76) return null;
                                        return (<div className="absolute left-14 right-0 z-20 flex items-center" style={{ top: `${mins}px` }}><div className="w-2.5 h-2.5 rounded-full bg-red-500" /><div className="flex-1 h-0.5 bg-red-500/60" /></div>);
                                    })()}
                                </div>
                            </div>
                            <div className="lg:col-span-3 bg-white rounded-2xl border border-[#E2E8F0] p-6 max-h-[800px] overflow-y-auto custom-scrollbar">
                                <h3 className="text-base font-bold text-[#0A1931] mb-4">📅 Bugünün Etkinlikleri</h3>
                                {selectedEvents.length > 0 ? (
                                    <div className="space-y-3">{selectedEvents.sort((a, b) => a.startDate.localeCompare(b.startDate)).map(ev => <EventCard key={ev.id} ev={ev} />)}</div>
                                ) : (<div className="text-center py-10 text-[#A0AEC0]"><CalIcon size={32} className="mx-auto opacity-20 mb-3" /><p className="text-sm font-medium">Etkinlik yok</p></div>)}
                                <div className="mt-6 pt-5 border-t border-[#E2E8F0]">
                                    <h3 className="text-base font-bold text-[#0A1931] mb-4">🔜 Yaklaşan (7 gün)</h3>
                                    {upcoming.length > 0 ? (
                                        <div className="space-y-2">{upcoming.slice(0, 8).map(ev => {
                                            const col = getColor(ev.eventType);
                                            return (<div key={ev.id} onClick={() => handleEventClick(ev)} className={`flex items-center gap-3 p-2.5 rounded-xl hover:bg-[#E2E8F0]/20 ${ev.eventType === "Canlı Ders" ? "cursor-pointer" : ""}`}>
                                                <div className={`w-2.5 h-2.5 rounded-full ${col.dot}`} />
                                                <div className="flex-1 min-w-0"><p className="text-sm font-bold text-[#0A1931] truncate">{ev.title}</p>
                                                    <p className="text-xs text-[#64748B] mt-0.5">{new Date(ev.startDate).toLocaleDateString("tr-TR", { day: "numeric", month: "long" })} · {evStartTime(ev)}</p></div>
                                            </div>);
                                        })}</div>
                                    ) : <p className="text-sm text-[#A0AEC0]">Yaklaşan etkinlik yok</p>}
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Week View */}
                    {viewMode === "week" && (
                        <div className="bg-white rounded-2xl border border-[#E2E8F0] overflow-x-auto custom-scrollbar">
                            <div className="min-w-[600px]">
                                <div className="grid grid-cols-8 border-b border-[#E2E8F0]">
                                    <div className="w-16 p-3" />
                                {weekDates.map((wd, i) => {
                                    const ds = toDateStr(wd);
                                    return (
                                        <div key={ds} className={`p-4 text-center border-l border-[#E2E8F0] cursor-pointer hover:bg-[#E2E8F0]/10 ${ds === today ? "bg-[#1B3B6F]/5" : ""}`}
                                            onClick={() => { setSelectedDate(ds); setCurrentDate(wd); setViewMode("day"); }}>
                                            <p className="text-xs font-bold text-[#A0AEC0] uppercase">{DAYS_TR[i]}</p>
                                            <p className={`text-xl font-bold mt-1 ${ds === today ? "w-10 h-10 bg-[#1B3B6F] text-white rounded-full flex items-center justify-center mx-auto" : "text-[#0A1931]"}`}>{wd.getDate()}</p>
                                        </div>
                                    );
                                })}
                            </div>
                            <div className="relative" style={{ height: `${HOURS.length * 60}px` }}>
                                {HOURS.map(h => (
                                    <div key={h} className="absolute w-full flex" style={{ top: `${(h - 7) * 60}px`, height: "60px" }}>
                                        <div className="w-16 text-right pr-3 text-xs font-medium text-[#A0AEC0] pt-1">{`${String(h).padStart(2, "0")}:00`}</div>
                                        <div className="flex-1 grid grid-cols-7 border-t border-[#E2E8F0]/30">
                                            {weekDates.map((wd, di) => {
                                                const ds = toDateStr(wd);
                                                const evs = getEventsForDate(ds).filter(ev => evHour(ev) === h);
                                                const isDropHere = dropTarget?.date === ds && dropTarget?.hour === h;
                                                return (
                                                    <div key={ds} className={`relative border-l border-[#E2E8F0]/20 cursor-pointer transition-colors ${isDropHere || dragCourse ? "hover:bg-emerald-50/30" : "hover:bg-[#1B3B6F]/[0.03]"} ${isDropHere ? "bg-emerald-50/50" : ""}`}
                                                        onClick={() => { 
                                                            if (isResizingRef.current) return;
                                                            if (dragCourse) { handleDrop(ds, h); } 
                                                            else { setSelectedDate(ds); setEditEvent(null); setDefaultTime(`${String(h).padStart(2, "0")}:00`); setShowForm(true); }
                                                        }}
                                                        onDragOver={e => { e.preventDefault(); setDropTarget({ date: ds, hour: h }); }}
                                                        onDragLeave={() => setDropTarget(null)}
                                                        onDrop={e => { e.preventDefault(); handleDrop(ds, h); }}>
                                                        {evs.map((ev, idx) => {
                                                            const col = getColor(ev.eventType);
                                                            const widthPercent = 100 / evs.length;
                                                            const leftPercent = idx * widthPercent;
                                                            const dur = Math.max(1, evEndHour(ev) - h);
                                                            const isResizing = resizingEvent?.id === ev.id;
                                                            const finalHeight = isResizing && resizeHeightRef.current !== null ? `${resizeHeightRef.current}px` : `${dur * 60 - 4}px`;
                                                            
                                                            return (
                                                                <div key={ev.id} id={`event-${ev.id}`} onClick={() => handleEventClick(ev)}
                                                                    draggable={!isResizing}
                                                                    onDragStart={(e) => { if(isResizing) return e.preventDefault(); e.stopPropagation(); setDragEvent(ev); }}
                                                                    onDragEnd={() => { setDragEvent(null); setDropTarget(null); }}
                                                                    className={`absolute top-0.5 rounded-xl px-1.5 py-1.5 border ${col.border} ${col.bg} flex flex-col ${!isResizing ? 'cursor-grab active:cursor-grabbing' : ''} hover:shadow-sm hover:z-20 z-10 overflow-hidden group`}
                                                                    style={{ 
                                                                        height: finalHeight,
                                                                        left: `calc(${leftPercent}% + 2px)`,
                                                                        width: `calc(${widthPercent}% - 4px)`
                                                                    }}>
                                                                    <div className="flex-1 min-h-0">
                                                                        <p className={`text-[9px] leading-tight font-bold ${col.text} truncate`}>{ev.title}</p>
                                                                        <p className="text-[9px] leading-none text-[#A0AEC0] mt-0.5 truncate">{evStartTime(ev)}</p>
                                                                    </div>
                                                                    {/* Resize Handle */}
                                                                    <div 
                                                                        onMouseDown={(e) => {
                                                                            e.stopPropagation();
                                                                            isResizingRef.current = true;
                                                                            setResizingEvent({ id: ev.id, startY: e.clientY, initialHeight: dur * 60 - 4, mode: "week" });
                                                                            resizeHeightRef.current = dur * 60 - 4;
                                                                        }}
                                                                        className="absolute bottom-0 left-0 right-0 h-2 cursor-ns-resize hover:bg-black/10 flex items-center justify-center rounded-b-xl group/resize"
                                                                    >
                                                                        <div className="w-4 h-0.5 rounded-full bg-black/20 group-hover/resize:bg-black/40" />
                                                                    </div>
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

                    {viewMode === "month" && (
                        <div className="space-y-4">
                            <div className="bg-white rounded-2xl border border-[#E2E8F0] overflow-x-auto custom-scrollbar">
                                <div className="min-w-[700px]">
                                    {/* Day headers */}
                                    <div className="grid grid-cols-7 border-b border-[#CBD5E1]">
                                        {DAYS_TR.map((d, i) => <div key={d} className={`text-center text-xs font-bold uppercase tracking-wider py-3 ${i >= 5 ? "text-[#94A3B8] bg-[#F8FAFC]" : "text-[#64748B]"} ${i < 6 ? "border-r border-[#E2E8F0]" : ""}`}>{d}</div>)}
                                    </div>
                                    {/* Calendar grid */}
                                    <div className="grid grid-cols-7">
                                        {loading ? Array.from({ length: 35 }).map((_, i) => <div key={i} className="min-h-[140px] bg-[#E2E8F0]/20 animate-pulse border-r border-b border-[#E2E8F0]" />) :
                                            cells.map((cell, i) => {
                                                const colIndex = i % 7;
                                                const isWeekend = colIndex >= 5;
                                                if (!cell.day) return <div key={`empty-${i}`} className={`min-h-[140px] ${colIndex < 6 ? "border-r" : ""} border-b border-[#E2E8F0] ${isWeekend ? "bg-[#F8FAFC]" : ""}`} />;
                                                const dayEvents = getEventsForDate(cell.date);
                                                const isSelected = selectedDate === cell.date;
                                                const isToday = cell.date === today;
                                                const isDrop = dropTarget?.date === cell.date;
                                                return (
                                                    <button key={cell.date} onClick={() => {
                                                            if (dragCourse) { handleDrop(cell.date); }
                                                            else { setSelectedDate(cell.date); }
                                                        }}
                                                        onDoubleClick={() => { setSelectedDate(cell.date); setCurrentDate(new Date(cell.date)); setViewMode("day"); }}
                                                        onDragOver={e => { e.preventDefault(); setDropTarget({ date: cell.date }); }}
                                                        onDragLeave={() => setDropTarget(null)}
                                                        onDrop={e => { e.preventDefault(); handleDrop(cell.date); }}
                                                        className={`min-h-[140px] max-h-[180px] p-2 text-left flex flex-col transition-all border-b border-[#E2E8F0] ${colIndex < 6 ? "border-r" : ""}
                                                            ${isWeekend && !isSelected ? "bg-[#F8FAFC]" : ""}
                                                            ${isSelected ? "bg-[#1B3B6F]/5 ring-2 ring-inset ring-[#1B3B6F]/30" : "hover:bg-[#F1F5F9]"}
                                                            ${isToday && !isSelected ? "bg-blue-50/50" : ""}
                                                            ${isDrop ? "bg-emerald-50 ring-2 ring-inset ring-emerald-300" : ""}`}>
                                                        <span className={`text-sm font-bold mb-1 ${isToday ? "w-6 h-6 bg-[#1B3B6F] text-white rounded-full flex items-center justify-center" : isWeekend ? "text-[#94A3B8]" : "text-[#1B3B6F]"}`}>{cell.day}</span>
                                                        <div className="flex-1 space-y-1 overflow-y-auto custom-scrollbar pr-1 pb-1 w-full">
                                                            {dayEvents.map(ev => {
                                                                const col = getColor(ev.eventType);
                                                                const Ic = eventIcons[ev.eventType];
                                                                return (<div key={ev.id} 
                                                                    draggable
                                                                    onDragStart={(e) => { e.stopPropagation(); setDragEvent(ev); }}
                                                                    onDragEnd={() => { setDragEvent(null); setDropTarget(null); }}
                                                                    className={`text-[10px] font-bold px-1.5 py-1 rounded-md ${col.bg} ${col.text} truncate flex items-center gap-1 border ${col.border} cursor-grab active:cursor-grabbing hover:opacity-80`}>{Ic && <Ic size={10} />}{ev.title}</div>);
                                                            })}
                                                        </div>
                                                    </button>
                                                );
                                            })}
                                    </div>
                                </div>
                            </div>
                            {/* Selected date events - compact bar below calendar */}
                            {selectedEvents.length > 0 && (
                                <div className="bg-white rounded-2xl border border-[#E2E8F0] p-5">
                                    <div className="flex items-center justify-between mb-4">
                                        <h3 className="text-sm font-bold text-[#0A1931]">
                                            {selectedDate ? `${parseInt(selectedDate.split("-")[2])} ${MONTHS_TR[parseInt(selectedDate.split("-")[1]) - 1]}` : ""} — {selectedEvents.length} etkinlik
                                        </h3>
                                        <button onClick={() => { setEditEvent(null); setShowForm(true); }} className="text-xs text-[#1B3B6F] font-bold">+ Ekle</button>
                                    </div>
                                    <div className="grid grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">{selectedEvents.map(ev => <EventCard key={ev.id} ev={ev} compact />)}</div>
                                </div>
                            )}
                        </div>
                    )}
                </div>
            </div>

            {showForm && <EventFormModal event={editEvent} defaultDate={selectedDate} defaultTime={defaultTime} droppedCourse={droppedCourse} courses={courses} groups={groups} isQuickForm={quickFormMode} onClose={() => { setShowForm(false); setEditEvent(null); setDefaultTime(null); setDroppedCourse(null); setQuickFormMode(false); }} onSave={handleSave} />}
            <ConfirmDialog open={deleteTarget !== null} onClose={() => setDeleteTarget(null)} onConfirm={handleDelete} title="Etkinliği Sil" message="Bu etkinlik kalıcı olarak silinecek." />
        </div>
    );
}

function EventFormModal({ event, defaultDate, defaultTime, droppedCourse, courses, groups, isQuickForm, onClose, onSave }: {
    event: CalendarEventDto | null; defaultDate: string | null; defaultTime: string | null; droppedCourse: CourseListDto | null; courses: CourseListDto[]; groups: GroupListDto[]; isQuickForm?: boolean; onClose: () => void; onSave: (d: CreateCalendarEventRequest) => void;
}) {
    const [form, setForm] = useState({
        title: event?.title || (droppedCourse ? `${droppedCourse.title} — Canlı Ders` : ""),
        date: event ? event.startDate.substring(0, 10) : (defaultDate || ""),
        endDate: event ? event.endDate.substring(0, 10) : (defaultDate || ""),
        startTime: event ? evStartTime(event) : (defaultTime || "09:00"),
        endTime: event ? evEndTime(event) : (defaultTime ? `${String(parseInt(defaultTime) + 1).padStart(2, "0")}:00` : "10:00"),
        eventType: event?.eventType || (droppedCourse ? "Canlı Ders" : "Etkinlik"),
        description: event?.description || (droppedCourse ? `${droppedCourse.title} dersi için planlanan canlı oturum` : ""),
        courseId: event?.courseId || droppedCourse?.id || "",
        groupId: event?.groupId || "",
    });

    const isFullDay = form.eventType === "Tatil" || form.eventType === "Etkinlik";
    const typeLocked = !!droppedCourse || !!event?.courseId;

    const buildPayload = (): CreateCalendarEventRequest => ({
        title: form.title,
        startDate: isFullDay ? new Date(`${form.date}T00:00:00`).toISOString() : new Date(`${form.date}T${form.startTime}:00`).toISOString(),
        endDate: isFullDay ? new Date(`${form.endDate}T23:59:59`).toISOString() : new Date(`${form.date}T${form.endTime}:00`).toISOString(),
        eventType: form.eventType,
        description: form.description || undefined,
        courseId: form.courseId || undefined,
        groupId: form.groupId || undefined,
    });

    const isLive = form.eventType === "Canlı Ders";
    const hasCourse = !!form.courseId;
    const hasGroup = !!form.groupId;
    const hasTarget = hasCourse || hasGroup;
    const isValidLive = isLive ? hasCourse : true;
    const isFormValid = form.title && form.date && hasTarget && isValidLive;

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4" onClick={onClose}>
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" />
            <div className="relative w-full max-w-md bg-white rounded-2xl shadow-2xl overflow-hidden" onClick={e => e.stopPropagation()}>
                <div className="relative overflow-hidden px-6 py-5 bg-gradient-to-r from-[#0A1931]/5 to-[#1B3B6F]/5">
                    <div className="absolute -right-6 -top-6 w-24 h-24 rounded-full bg-[#1B3B6F] opacity-[0.04]" />
                    <div className="flex items-center justify-between relative z-10">
                        <div className="flex items-center gap-3">
                            <div className="w-12 h-12 rounded-xl bg-[#1B3B6F]/10 flex items-center justify-center"><CalIcon size={20} className="text-[#1B3B6F]" /></div>
                            <div><h2 className="text-lg font-bold text-[#0A1931]">{event ? "Etkinliği Düzenle" : "Yeni Etkinlik"}</h2><p className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest">Takvim Yönetimi</p></div>
                        </div>
                        <button onClick={onClose} className="p-2 rounded-xl bg-white/80 text-[#A0AEC0] hover:text-[#0A1931] shadow-sm"><X size={18} /></button>
                    </div>
                </div>
                <div className="p-6 space-y-5">
                    {!isQuickForm && (
                        <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Başlık *</label>
                            <input type="text" value={form.title} onChange={e => setForm(p => ({ ...p, title: e.target.value }))}
                                className="w-full px-4 py-3 text-sm font-medium bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 text-[#0A1931]" placeholder="Etkinlik adı" /></div>
                    )}
                    {!isQuickForm && (
                        <div className={`grid ${isFullDay ? 'grid-cols-2' : 'grid-cols-3'} gap-4`}>
                            <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">{isFullDay ? "Başlangıç Tarihi" : "Tarih"}</label>
                                <input type="date" value={form.date} onChange={e => setForm(p => ({ ...p, date: e.target.value }))}
                                    className="w-full px-3 py-3 text-sm bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none text-[#0A1931]" /></div>
                            {isFullDay ? (
                                <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Bitiş Tarihi</label>
                                <input type="date" value={form.endDate} onChange={e => setForm(p => ({ ...p, endDate: e.target.value }))}
                                    className="w-full px-3 py-3 text-sm bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none text-[#0A1931]" /></div>
                            ) : (
                                <>
                                    <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Başlangıç</label>
                                        <input type="time" value={form.startTime} onChange={e => setForm(p => ({ ...p, startTime: e.target.value }))}
                                            className="w-full px-3 py-3 text-sm bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none text-[#0A1931]" /></div>
                                    <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Bitiş</label>
                                        <input type="time" value={form.endTime} onChange={e => setForm(p => ({ ...p, endTime: e.target.value }))}
                                            className="w-full px-3 py-3 text-sm bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none text-[#0A1931]" /></div>
                                </>
                            )}
                        </div>
                    )}
                    {isQuickForm && (
                        <div className="grid grid-cols-2 gap-4">
                            <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Başlangıç Saati</label>
                                <input type="time" value={form.startTime} onChange={e => setForm(p => ({ ...p, startTime: e.target.value }))}
                                    className="w-full px-3 py-3 text-sm bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none text-[#0A1931]" /></div>
                            <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Bitiş Saati</label>
                                <input type="time" value={form.endTime} onChange={e => setForm(p => ({ ...p, endTime: e.target.value }))}
                                    className="w-full px-3 py-3 text-sm bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none text-[#0A1931]" /></div>
                        </div>
                    )}
                    {!isQuickForm && (
                        <>
                            <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Tür</label>
                                <div className="grid grid-cols-3 gap-2">
                                    {Object.keys(eventColors).map(t => {
                                        const c = getColor(t);
                                        const Ic = eventIcons[t];
                                        return (<button key={t} type="button" 
                                            disabled={typeLocked && form.eventType !== t}
                                            onClick={() => setForm(p => ({ ...p, eventType: t }))}
                                            className={`py-2.5 rounded-xl text-xs font-bold border transition-all text-center flex items-center justify-center gap-1.5 
                                                ${form.eventType === t ? `${c.bg} ${c.border} ${c.text} ring-2 ring-offset-1` : 
                                                typeLocked ? "bg-gray-50 text-gray-300 border-gray-100 cursor-not-allowed opacity-50" :
                                                "bg-white text-[#A9A9A9] border-[#E2E8F0] hover:bg-[#F8FAFC]"}`}>
                                            {Ic && <Ic size={14} />}{t}</button>);
                                    })}
                                </div></div>
                            <div className={`grid ${isInstructor ? 'grid-cols-1' : 'grid-cols-2'} gap-4`}>
                                <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Hedef Ders</label>
                                    <select value={form.courseId} onChange={e => setForm(p => ({ ...p, courseId: e.target.value }))}
                                        className={`w-full px-4 py-3 text-sm font-medium bg-[#E2E8F0]/10 border rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 text-[#0A1931] ${isLive && !hasCourse ? 'border-red-400 ring-1 ring-red-400/50' : 'border-[#E2E8F0]'}`}>
                                        <option value="">-- Ders Seç --</option>
                                        {courses.map(c => <option key={c.id} value={c.id}>{c.title}</option>)}
                                    </select>
                                </div>
                                {!isInstructor && (
                                    <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Hedef Grup</label>
                                        <select value={form.groupId} onChange={e => setForm(p => ({ ...p, groupId: e.target.value }))}
                                            className="w-full px-4 py-3 text-sm font-medium bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 text-[#0A1931]">
                                            <option value="">-- Grup Seç --</option>
                                            {groups.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
                                        </select>
                                    </div>
                                )}
                            </div>
                        </>
                    )}
                    <div><label className="text-xs font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Açıklama</label>
                        <textarea value={form.description} onChange={e => setForm(p => ({ ...p, description: e.target.value }))}
                            className="w-full px-4 py-3 text-sm bg-[#E2E8F0]/10 border border-[#E2E8F0] rounded-xl focus:outline-none resize-none h-20 text-[#0A1931]" placeholder="Etkinlik detayları" /></div>
                </div>
                <div className="flex flex-col sm:flex-row sm:items-center justify-between px-6 py-5 border-t border-[#E2E8F0] bg-[#E2E8F0]/10 rounded-b-2xl gap-3">
                    <div className="text-[11px] font-bold">
                        {isFormValid ? (
                            <span className="text-emerald-600">✓ Kaydetmeye Hazır</span>
                        ) : (
                            <span className="text-red-500">
                                ⚠ {isLive && !hasCourse ? "Canlı ders için 'Ders' seçimi zorunludur." : !hasTarget ? "Grup veya Ders seçimi zorunludur." : "Zorunlu alanları doldurun."}
                            </span>
                        )}
                    </div>
                    <div className="flex gap-3">
                        <button onClick={onClose} className="px-5 py-2.5 text-sm font-bold text-[#A9A9A9] hover:text-[#0A1931]">İptal</button>
                        <button disabled={!isFormValid} onClick={() => onSave(buildPayload())}
                            className="px-6 py-2.5 text-sm font-bold text-white bg-[#0A1931] rounded-xl hover:bg-[#1B3B6F] shadow-lg shadow-black/10 disabled:opacity-40">
                            {event ? "💾 Güncelle" : "Oluştur"}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
