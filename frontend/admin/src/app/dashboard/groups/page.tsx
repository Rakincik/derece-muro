"use client";

import { useState, useMemo, useEffect, useCallback } from "react";
import {
    PiTreeStructureDuotone as FolderTree,
    PiPlusBold as Plus,
    PiUsersDuotone as Users,
    PiPencilSimpleDuotone as Edit3,
    PiTrashDuotone as Trash2,
    PiXBold as X,
    PiMagnifyingGlassDuotone as Search,
    PiBookOpenTextDuotone as BookOpen,
    PiGearDuotone as Settings,
    PiCaretDownBold as ChevronDown,
    PiCaretRightBold as ChevronRight,
    PiSpinnerGapDuotone as Loader2,
    PiArrowsClockwiseDuotone as RefreshCw,
    PiUserPlusDuotone as UserPlus,
    PiUserMinusDuotone as UserMinus,
    PiArrowRightBold as ArrowRight,
    PiCheckBold as Check,
    PiWarningDuotone as AlertTriangle,
    PiChartBarDuotone as BarChart3,
    PiCalendarDuotone as Calendar,
    PiCopyDuotone as Copy,
    PiVideoCameraDuotone,
    PiTentDuotone,
    PiNotePencilDuotone,
    PiTargetDuotone,
    PiFilePlusDuotone
} from "react-icons/pi";
import { useToast } from "@/components/toast";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { useAuth } from "@/contexts/AuthContext";
import { groupsApi, courseApi, notificationApi, userApi, type GroupListDto, type GroupDetailDto, type CourseListDto } from "@/lib/api";
import { PremiumTabs } from "@/components/ui/PremiumTabs";
import { KpiGrid } from "@/components/ui/KpiGrid";
import { BulkRegisterModal } from "./BulkRegisterModal";
import { Tooltip } from "@/components/ui/Tooltip";

type DetailTab = "members" | "courses" | "settings";

const COLOR_PRESETS = ["#6366f1", "#8b5cf6", "#3b82f6", "#ec4899", "#10b981", "#f59e0b", "#14b8a6", "#ef4444"];

const getEducationIcon = (type: string, size: number = 14) => {
    const icons: Record<string, React.ElementType> = {
        "Canlı": PiVideoCameraDuotone,
        "Offline": BookOpen,
        "Kamp": PiTentDuotone,
        "Sınav": PiNotePencilDuotone,
        "Demo": PiTargetDuotone
    };
    const Icon = icons[type];
    return Icon ? <Icon size={size} className="inline-block shrink-0" /> : null;
};

// ── Tree Item ────────────────────────────────────────────────────────────────
function GroupTreeItem({
    group, selected, expanded, hasChildren, onSelect, onToggle, onEdit, onDelete, onAddSubgroup
}: {
    group: GroupListDto; selected: boolean; expanded: boolean; hasChildren: boolean;
    onSelect: () => void; onToggle: () => void; onEdit: () => void; onDelete: () => void; onAddSubgroup: () => void;
}) {
    const isEmpty = group.memberCount === 0;
    return (
        <div
            className={`flex items-center gap-2.5 p-2 rounded-xl cursor-pointer group transition-all border ${selected ? "bg-blue-50/50 border-blue-200 ring-1 ring-blue-500/20" : "bg-transparent border-transparent hover:bg-[#F0F4F8]"}`}
            onClick={onSelect}
        >
            <button onClick={e => { e.stopPropagation(); onToggle(); }} 
                className={`shrink-0 flex items-center justify-center transition-all duration-300 ${hasChildren ? `w-6 h-6 rounded-lg ${selected ? "bg-blue-600 text-white shadow-md hover:bg-blue-700" : "bg-white text-[#1B3B6F] border border-[#E2E8F0] shadow-sm hover:border-[#A0AEC0]"}` : "w-6 text-transparent"}`}>
                {hasChildren ? (expanded ? <ChevronDown size={14} strokeWidth={3.5} /> : <ChevronRight size={14} strokeWidth={3.5} />) : <span className="w-4" />}
            </button>
            <div className="w-2.5 h-2.5 rounded-full shrink-0 shadow-sm" style={{ background: group.color ?? "#94a3b8" }} />
            <div className="flex-1 min-w-0 py-0.5">
                <p className="text-sm font-bold tracking-tight truncate text-[#0A1931]">{group.name}</p>
                <div className="flex items-center gap-1.5 mt-1 flex-wrap">
                    <span className={`flex items-center gap-1 text-[10px] font-bold px-1.5 py-0.5 rounded-[5px] shadow-sm ${selected ? "bg-blue-100 text-blue-700 border border-blue-200" : "bg-white text-[#475569] border border-[#E2E8F0]"}`}>{group.memberCount} öğrenci</span>
                    <span className={`flex items-center gap-1 text-[10px] font-bold px-1.5 py-0.5 rounded-[5px] shadow-sm ${selected ? "bg-blue-100 text-blue-700 border border-blue-200" : "bg-white text-[#475569] border border-[#E2E8F0]"}`}>{group.courseCount} ders</span>
                    {group.educationType && <span className={`flex items-center gap-1 text-[10px] font-bold px-1.5 py-0.5 rounded-[5px] shadow-sm ${selected ? "bg-indigo-100 text-indigo-700 border border-indigo-200" : "bg-indigo-50 text-indigo-700 border border-indigo-100"}`}>{getEducationIcon(group.educationType, 12)} {group.educationType}</span>}
                </div>
            </div>
            {isEmpty && <Tooltip content="Boş grup"><span className={`text-amber-400 ${selected ? "opacity-100" : "opacity-80"}`}><AlertTriangle size={12} /></span></Tooltip>}
            <div className="hidden group-hover:flex items-center gap-1">
                <Tooltip content="Alt Grup Ekle"><button onClick={e => { e.stopPropagation(); onAddSubgroup(); }}
                    className="p-1.5 rounded-lg transition-colors text-[#A0AEC0] hover:text-emerald-600 hover:bg-emerald-50">
                    <Plus size={12} />
                </button></Tooltip>
                <Tooltip content="Düzenle"><button onClick={e => { e.stopPropagation(); onEdit(); }}
                    className="p-1.5 rounded-lg transition-colors text-[#A0AEC0] hover:text-[#1B3B6F] hover:bg-[#E2E8F0]/50">
                    <Edit3 size={12} />
                </button></Tooltip>
                <Tooltip content="Sil"><button onClick={e => { e.stopPropagation(); onDelete(); }}
                    className="p-1.5 rounded-lg transition-colors text-[#A0AEC0] hover:text-red-600 hover:bg-red-50">
                    <Trash2 size={12} />
                </button></Tooltip>
            </div>
        </div>
    );
}

// ── Main Page ────────────────────────────────────────────────────────────────
export default function GroupsPage() {
    const { token, currentTenantId: tenantId } = useAuth();
    const { success, error: toastError } = useToast();

    const [groups, setGroups] = useState<GroupListDto[]>([]);
    const [detail, setDetail] = useState<GroupDetailDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [detailLoading, setDetailLoading] = useState(false);
    const [expanded, setExpanded] = useState<Set<string>>(new Set());
    const [selectedId, setSelectedId] = useState<string | null>(null);
    const [activeTab, setActiveTab] = useState<DetailTab>("members");
    const [treeSearch, setTreeSearch] = useState("");
    const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
    const [formOpen, setFormOpen] = useState(false);
    const [editGroup, setEditGroup] = useState<GroupListDto | null>(null);
    const [formName, setFormName] = useState("");
    const [formDesc, setFormDesc] = useState("");
    const [formColor, setFormColor] = useState(COLOR_PRESETS[0]);
    const [formParent, setFormParent] = useState("");
    const [formType, setFormType] = useState("");
    const [formExpirationDate, setFormExpirationDate] = useState("");
    const [formSaving, setFormSaving] = useState(false);
    const [bulkRegisterOpen, setBulkRegisterOpen] = useState(false);

    // Detail Members Pagination & Search
    const [memberPage, setMemberPage] = useState(1);
    const [memberPageSize, setMemberPageSize] = useState(10);
    const [memberSearch, setMemberSearch] = useState("");

    // Member management
    const [addMemberOpen, setAddMemberOpen] = useState(false);
    const [addMemberSearch, setAddMemberSearch] = useState("");
    const [allUsers, setAllUsers] = useState<{ id: string; fullName: string; email: string }[]>([]);
    const [selectedMembers, setSelectedMembers] = useState<Set<string>>(new Set());
    const [bulkAddSelection, setBulkAddSelection] = useState<Set<string>>(new Set());

    // Move members
    const [moveOpen, setMoveOpen] = useState(false);
    const [moveTargetGroup, setMoveTargetGroup] = useState("");
    const [copyOpen, setCopyOpen] = useState(false);
    const [copyTargetGroup, setCopyTargetGroup] = useState("");
    const [hardDeleteOpen, setHardDeleteOpen] = useState(false);

    // Clone group
    const [cloneGroupOpen, setCloneGroupOpen] = useState(false);
    const [cloneGroupName, setCloneGroupName] = useState("");
    const [cloneGroupMembers, setCloneGroupMembers] = useState(true);
    const [cloneGroupCourses, setCloneGroupCourses] = useState(true);

    // Course assignment
    const [assignCourseOpen, setAssignCourseOpen] = useState(false);
    const [assignCourseSearch, setAssignCourseSearch] = useState("");
    const [assignCourseSelection, setAssignCourseSelection] = useState<Set<string>>(new Set());
    const [allCourses, setAllCourses] = useState<{ id: string; title: string }[]>([]);
    const [loadingCourses, setLoadingCourses] = useState(false);

    const loadGroups = useCallback(async () => {
        if (!token || !tenantId) return;
        setLoading(true);
        try {
            const res = await groupsApi.list(token, tenantId, { pageSize: 200 });
            const list = Array.isArray(res) ? res : (res as { items?: GroupListDto[] }).items ?? [];
            setGroups(list);
            if (!selectedId && list.length > 0) {
                setSelectedId(list[0].id);
                setExpanded(new Set([list[0].id]));
            }
        } catch { toastError("Hata", "Gruplar yüklenemedi."); }
        finally { setLoading(false); }
    }, [token, tenantId, selectedId]);

    useEffect(() => { loadGroups(); }, [loadGroups]);

    useEffect(() => {
        if (!selectedId || !token || !tenantId) return;
        setDetailLoading(true);
        setSelectedMembers(new Set());
        setMemberPage(1);
        setMemberSearch("");
        groupsApi.get(token, tenantId, selectedId)
            .then(setDetail).catch(() => setDetail(null)).finally(() => setDetailLoading(false));
    }, [selectedId, token, tenantId]);

    // Load users for member add
    useEffect(() => {
        if (!addMemberOpen || !token || !tenantId || allUsers.length > 0) return;
        notificationApi.allUsers(token, tenantId).then(users => {
            setAllUsers(users.map(u => ({ id: u.id, fullName: `${u.firstName} ${u.lastName}`, email: u.email })));
        }).catch(() => { });
    }, [addMemberOpen, token, tenantId, allUsers.length]);

    // Load all courses for fast local assignment when modal opens
    useEffect(() => {
        if (!assignCourseOpen || !token || !tenantId || allCourses.length > 0) return;
        setLoadingCourses(true);
        courseApi.list(token, tenantId, { pageSize: 100 }).then(res => {
            console.log("FETCHED COURSES RES:", res);
            setAllCourses((res?.items || []).map(c => ({ id: c.id, title: c.title })));
        }).catch((err) => { 
            console.error("Course fetch error:", err);
            toastError("Hata", "Dersler yüklenirken bir sorun oluştu.");
        }).finally(() => {
            setLoadingCourses(false);
        });
    }, [assignCourseOpen, token, tenantId, allCourses.length]);

    const getChildren = (parentId: string | null) => groups.filter(g => g.parentGroupId === parentId);
    const rootGroups = getChildren(null);

    const filteredTree = useMemo(() => {
        if (!treeSearch) return null;
        const q = treeSearch.toLowerCase();
        return groups.filter(g => g.name.toLowerCase().includes(q));
    }, [groups, treeSearch]);

    const toggle = (id: string) => {
        const next = new Set(expanded);
        next.has(id) ? next.delete(id) : next.add(id);
        setExpanded(next);
    };

    const openCreate = () => {
        setEditGroup(null); setFormName(""); setFormDesc(""); setFormColor(COLOR_PRESETS[0]); setFormParent(""); setFormType(""); setFormExpirationDate("");
        setFormOpen(true);
    };
    
    const openCreateSubgroup = (parentId: string) => {
        setEditGroup(null); setFormName(""); setFormDesc(""); setFormColor(COLOR_PRESETS[Math.floor(Math.random() * COLOR_PRESETS.length)]); setFormParent(parentId); setFormType(""); setFormExpirationDate("");
        setFormOpen(true);
    };
    const openEdit = (g: GroupListDto) => {
        setEditGroup(g); setFormName(g.name); setFormDesc(g.description ?? ""); setFormColor(g.color ?? COLOR_PRESETS[0]); setFormParent(g.parentGroupId ?? ""); setFormType(g.educationType ?? ""); setFormExpirationDate(g.expirationDate ? g.expirationDate.split("T")[0] : "");
        setFormOpen(true);
    };

    const handleSave = async () => {
        if (!token || !tenantId || !formName.trim() || !formType) return;
        setFormSaving(true);
        try {
            const data = { name: formName.trim(), description: formDesc || undefined, color: formColor, parentGroupId: formParent || undefined, educationType: formType || undefined, expirationDate: formExpirationDate ? new Date(formExpirationDate).toISOString() : undefined };
            if (editGroup) {
                await groupsApi.update(token, tenantId, editGroup.id, data);
                success("Güncellendi", `${formName} grubu güncellendi.`);
            } else {
                await groupsApi.create(token, tenantId, data);
                success("Oluşturuldu", `${formName} grubu oluşturuldu.`);
            }
            setFormOpen(false); await loadGroups();
        } catch { toastError("Hata", "İşlem başarısız."); }
        finally { setFormSaving(false); }
    };

    const handleDelete = async () => {
        if (!deleteTarget || !token || !tenantId) return;
        try {
            await groupsApi.delete(token, tenantId, deleteTarget);
            setGroups(prev => prev.filter(g => g.id !== deleteTarget && g.parentGroupId !== deleteTarget));
            if (selectedId === deleteTarget) { setSelectedId(null); setDetail(null); }
            success("Silindi", "Grup silindi.");
        } catch (err: any) { toastError("Hata", err.message || "Silinemedi."); }
        finally { setDeleteTarget(null); }
    };

    const handleRemoveMember = async (userId: string) => {
        if (!selectedId || !token || !tenantId) return;
        try {
            await groupsApi.removeMember(token, tenantId, selectedId, userId);
            setDetail(prev => prev ? { ...prev, members: prev.members.filter(m => m.userId !== userId), memberCount: prev.memberCount - 1 } : prev);
            success("Çıkarıldı", "Öğrenci gruptan çıkarıldı.");
        } catch { toastError("Hata", "Öğrenci çıkarılamadı."); }
    };

    // Bulk add members
    const handleBulkAdd = async () => {
        if (!selectedId || !token || !tenantId || bulkAddSelection.size === 0) return;
        try {
            // Add to current group
            await groupsApi.addMembers(token, tenantId, selectedId, Array.from(bulkAddSelection));
            // Auto-inheritance: add to all child sub-groups
            const childGroups = groups.filter(g => g.parentGroupId === selectedId);
            for (const child of childGroups) {
                try {
                    await groupsApi.addMembers(token, tenantId, child.id, Array.from(bulkAddSelection));
                } catch { /* child group add may fail if already member */ }
            }
            setAddMemberOpen(false); setBulkAddSelection(new Set());
            const d = await groupsApi.get(token, tenantId, selectedId);
            setDetail(d);
            const childCount = childGroups.length;
            success("Öğrenci Eklendi", `${bulkAddSelection.size} öğrenci gruba eklendi.${childCount > 0 ? ` (${childCount} alt gruba da otomatik eklendi)` : ''}`);
            await loadGroups();
        } catch { toastError("Hata", "Öğrenci eklenemedi."); }
    };

    // Move members
    const handleMoveMembers = async () => {
        if (!selectedId || !token || !tenantId || !moveTargetGroup || selectedMembers.size === 0) return;
        try {
            await groupsApi.moveMembers(token, tenantId, selectedId, moveTargetGroup, Array.from(selectedMembers));
            setMoveOpen(false); setSelectedMembers(new Set());
            const d = await groupsApi.get(token, tenantId, selectedId);
            setDetail(d);
            success("Taşındı", `${selectedMembers.size} öğrenci taşındı.`);
            loadGroups();
        } catch { toastError("Hata", "Taşıma başarısız."); }
    };

    // Copy members
    const handleCopyMembers = async () => {
        if (!selectedId || !token || !tenantId || !copyTargetGroup || selectedMembers.size === 0) return;
        try {
            await groupsApi.addMembers(token, tenantId, copyTargetGroup, Array.from(selectedMembers));
            setCopyOpen(false); setSelectedMembers(new Set());
            success("Aktarıldı", `${selectedMembers.size} öğrenci hedefe kopyalandı.`);
            loadGroups();
        } catch { toastError("Hata", "Aktarma başarısız."); }
    };

    // Remove Selected from Group
    const handleRemoveSelectedMembers = async () => {
        if (!selectedId || !token || !tenantId || selectedMembers.size === 0) return;
        try {
            await Promise.all(Array.from(selectedMembers).map(id => groupsApi.removeMember(token, tenantId, selectedId, id)));
            setSelectedMembers(new Set());
            const d = await groupsApi.get(token, tenantId, selectedId);
            setDetail(d);
            success("Çıkarıldı", "Seçili öğrenciler gruptan çıkarıldı.");
            loadGroups();
        } catch { toastError("Hata", "Öğrenciler gruptan çıkarılamadı."); }
    };

    // Clone Group
    const handleCloneGroup = async () => {
        if (!selectedId || !token || !tenantId || !cloneGroupName.trim()) return;
        try {
            const newGroup = await groupsApi.clone(token, tenantId, selectedId, cloneGroupName, cloneGroupMembers, cloneGroupCourses);
            setCloneGroupOpen(false);
            success("Kopyalandı", "Grup başarıyla kopyalandı.");
            await loadGroups();
            if (newGroup?.id) setSelectedId(newGroup.id);
        } catch { toastError("Hata", "Grup kopyalanamadı."); }
    };

    // Hard Delete Selected Members
    const executeHardDeleteMembers = async () => {
        if (!token || !tenantId || selectedMembers.size === 0) return;
        try {
            await userApi.bulkDelete(token, tenantId, Array.from(selectedMembers));
            setHardDeleteOpen(false);
            setSelectedMembers(new Set());
            const d = await groupsApi.get(token, tenantId, selectedId!);
            setDetail(d);
            success("İmha Edildi", "Seçili kullanıcılar sistemden kalıcı olarak silindi.");
            loadGroups();
        } catch (err: any) { toastError("Hata", err.message || "Kullanıcılar silinemedi."); }
    };

    // Assign multiple courses
    const handleAssignCourses = async () => {
        if (!selectedId || !token || !tenantId || assignCourseSelection.size === 0) return;
        try {
            await Promise.all(
                Array.from(assignCourseSelection).map(courseId => 
                    groupsApi.assignCourse(token, tenantId, selectedId, courseId, "Online")
                )
            );
            setAssignCourseOpen(false); 
            setAssignCourseSearch("");
            setAssignCourseSelection(new Set());
            const d = await groupsApi.get(token, tenantId, selectedId);
            setDetail(d);
            success("Atandı", "Seçili dersler gruba atandı.");
            loadGroups();
        } catch { toastError("Hata", "Dersler atanamadı."); }
    };

    // Remove course
    const handleRemoveCourse = async (courseId: string) => {
        if (!selectedId || !token || !tenantId) return;
        try {
            await groupsApi.removeCourse(token, tenantId, selectedId, courseId);
            setDetail(prev => prev ? { ...prev, courses: prev.courses.filter(c => c.courseId !== courseId), courseCount: prev.courseCount - 1 } : prev);
            success("Çıkarıldı", "Ders gruptan çıkarıldı.");
            loadGroups();
        } catch { toastError("Hata", "Ders çıkarılamadı."); }
    };

    const handleColorChange = (g: GroupListDto, color: string) => {
        if (!token || !tenantId) return;
        groupsApi.update(token, tenantId, g.id, { color }).then(() => {
            setDetail(prev => prev ? { ...prev, color } : prev);
            setGroups(prev => prev.map(gr => gr.id === g.id ? { ...gr, color } : gr));
        }).catch(() => toastError("Hata", "Renk güncellenemedi."));
    };

    const toggleMember = (userId: string) => {
        const next = new Set(selectedMembers);
        next.has(userId) ? next.delete(userId) : next.add(userId);
        setSelectedMembers(next);
    };

    const toggleBulkAdd = (userId: string) => {
        const next = new Set(bulkAddSelection);
        next.has(userId) ? next.delete(userId) : next.add(userId);
        setBulkAddSelection(next);
    };

    const nonMembers = useMemo(() => {
        if (!detail) return [];
        const memberIds = new Set(detail.members.map(m => m.userId));
        return allUsers.filter(u => !memberIds.has(u.id) &&
            (addMemberSearch === "" || u.fullName.toLowerCase().includes(addMemberSearch.toLowerCase()) || u.email.toLowerCase().includes(addMemberSearch.toLowerCase()))
        );
    }, [allUsers, detail, addMemberSearch]);

    const filteredDetailMembers = useMemo(() => {
        if (!detail) return [];
        let list = detail.members;
        if (memberSearch) {
            const q = memberSearch.toLowerCase();
            list = list.filter(m => m.userFullName.toLowerCase().includes(q) || m.email.toLowerCase().includes(q));
        }
        return list;
    }, [detail, memberSearch]);

    const paginatedDetailMembers = useMemo(() => {
        const start = (memberPage - 1) * memberPageSize;
        return filteredDetailMembers.slice(start, start + memberPageSize);
    }, [filteredDetailMembers, memberPage, memberPageSize]);

    const detailMemberTotalPages = Math.max(1, Math.ceil(filteredDetailMembers.length / memberPageSize));

    const treeItems = filteredTree ?? rootGroups;

    // Stats
    const totalMembers = groups.reduce((s, g) => s + g.memberCount, 0);
    const totalCourses = groups.reduce((s, g) => s + g.courseCount, 0);
    const emptyGroups = groups.filter(g => g.memberCount === 0).length;

    const renderTree = (items: GroupListDto[], depth = 0) =>
        items.map(g => {
            const children = getChildren(g.id);
            const isExpanded = expanded.has(g.id);
            return (
                <div key={g.id} style={{ marginLeft: depth * 16 }}>
                    <GroupTreeItem group={g} selected={selectedId === g.id} expanded={isExpanded}
                        hasChildren={children.length > 0} onSelect={() => setSelectedId(g.id)}
                        onToggle={() => toggle(g.id)} onEdit={() => openEdit(g)} onDelete={() => setDeleteTarget(g.id)}
                        onAddSubgroup={() => openCreateSubgroup(g.id)} />
                    {isExpanded && children.length > 0 && renderTree(children, depth + 1)}
                </div>
            );
        });

    return (
        <div className="flex flex-col space-y-3 min-h-[100dvh] lg:min-h-0 lg:h-[calc(100vh-64px)] pb-16 lg:pb-0">
            {/* Header & Alert */}
            <div className="flex flex-col md:flex-row md:items-center justify-between shrink-0 gap-3">
                <div className="flex flex-col sm:flex-row sm:items-center gap-3 sm:gap-6">
                    <div>
                        <h1 className="text-lg sm:text-xl font-bold text-[#0A1931] flex items-center gap-2">
                            <FolderTree size={18} className="text-[#1B3B6F]" /> Gruplar
                        </h1>
                        <p className="text-[11px] sm:text-xs text-[#A9A9A9] mt-0.5">Öğrenci gruplarını yönetin</p>
                    </div>
                    
                    {emptyGroups > 0 && (
                        <div className="flex items-center gap-2 px-3 py-1.5 bg-amber-50 border border-amber-200 rounded-lg shadow-sm">
                            <AlertTriangle size={14} className="text-amber-500" />
                            <p className="text-[11px] text-amber-700 font-medium">
                                <strong>{emptyGroups} boş grup</strong> (⚠️)
                            </p>
                        </div>
                    )}
                </div>

                <div className="flex items-center gap-2">
                    <button onClick={loadGroups} className="p-2 rounded-xl bg-white border border-[#E2E8F0] hover:bg-[#E2E8F0]/20 text-[#A9A9A9]"><RefreshCw size={16} /></button>
                    <button onClick={openCreate} className="px-4 py-2 bg-[#0A1931] text-white text-sm font-bold rounded-xl hover:bg-[#1B3B6F] transition-colors flex items-center gap-1.5 shadow-lg shadow-black/10">
                        <Plus size={16} /> Yeni Grup
                    </button>
                </div>
            </div>

            {/* Stats (Compact) */}
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-2 shrink-0">
                {[
                    { label: "Toplam Grup", value: groups.length, icon: FolderTree, colorClass: "text-indigo-600", bgClass: "bg-indigo-50" },
                    { label: "Toplam Öğrenci", value: totalMembers, icon: Users, colorClass: "text-blue-600", bgClass: "bg-blue-50" },
                    { label: "Ders Ataması", value: totalCourses, icon: BookOpen, colorClass: "text-emerald-600", bgClass: "bg-emerald-50" },
                    { label: "Boş Grup", value: emptyGroups, icon: AlertTriangle, colorClass: emptyGroups > 0 ? "text-amber-600" : "text-teal-600", bgClass: emptyGroups > 0 ? "bg-amber-50" : "bg-teal-50" },
                ].map((k, i) => (
                    <div key={i} className="bg-white border border-[#E2E8F0] rounded-xl p-2.5 flex items-center gap-3 shadow-sm">
                        <div className={`w-8 h-8 rounded-lg flex items-center justify-center shrink-0 ${k.bgClass}`}>
                            <k.icon size={16} className={k.colorClass} />
                        </div>
                        <div>
                            <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-wider">{k.label}</p>
                            <p className="text-base font-black text-[#0A1931] leading-tight">{k.value}</p>
                        </div>
                    </div>
                ))}
            </div>

            {/* Main Content */}
            <div className="flex flex-col lg:flex-row gap-4 flex-1 min-h-0">
                {/* Left Panel - Group Tree */}
                <div className="w-full lg:w-80 h-[350px] lg:h-auto flex flex-col bg-white rounded-2xl sm:rounded-[1.5rem] border border-[#E2E8F0] shadow-sm overflow-hidden shrink-0 lg:shrink-0">
                    <div className="p-3 border-b border-[#E2E8F0] shrink-0">
                        <div className="relative">
                            <Search size={14} className="absolute left-4 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                            <input value={treeSearch} onChange={e => setTreeSearch(e.target.value)} placeholder="Grup ara..."
                                className="w-full pl-10 pr-4 py-3 text-sm font-medium bg-[#F8FAFC] border border-[#E2E8F0] rounded-xl text-[#0A1931] placeholder-[#A0AEC0] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:bg-white transition-all shadow-inner" />
                        </div>
                    </div>
                    <div className="flex-1 overflow-y-auto p-2">
                        {loading ? (
                            <div className="space-y-2 p-2">{[...Array(5)].map((_, i) => <div key={i} className="h-12 bg-[#E2E8F0]/40 rounded-xl animate-pulse" />)}</div>
                        ) : treeItems.length === 0 ? (
                            <div className="flex flex-col items-center justify-center h-40 text-[#A0AEC0]">
                                <FolderTree size={32} className="opacity-20 mb-2" />
                                <p className="text-sm">{treeSearch ? "Sonuç bulunamadı" : "Henüz grup yok"}</p>
                            </div>
                        ) : renderTree(treeItems)}
                    </div>
                    <div className="p-3 border-t border-[#E2E8F0] text-xs text-[#A0AEC0] text-center shrink-0">
                        {groups.length} grup • {groups.filter(g => g.parentGroupId !== null).length} alt grup
                    </div>
                </div>

                {/* Right Panel - Group Details */}
                <div className="flex-1 flex flex-col bg-white rounded-2xl sm:rounded-[1.5rem] border border-[#E2E8F0] shadow-sm overflow-hidden min-h-[500px] lg:min-h-0">
                    {!selectedId ? (
                        <div className="flex-1 flex items-center justify-center text-[#A0AEC0] p-8">
                            <div className="text-center">
                                <FolderTree size={48} className="opacity-20 mx-auto mb-3" />
                                <p className="text-lg font-medium">Bir grup seçin</p>
                            </div>
                        </div>
                    ) : detailLoading ? (
                        <div className="flex-1 flex items-center justify-center"><Loader2 size={32} className="text-[#A0AEC0] animate-spin" /></div>
                    ) : detail ? (
                        <>
                            {/* Detail Header - Ultra Compact */}
                            <div className="p-3 px-4 border-b border-[#E2E8F0] bg-[#F8FAFC]">
                                <div className="flex flex-col xl:flex-row xl:items-center justify-between gap-3">
                                    <div className="flex items-center flex-wrap gap-2.5 flex-1 min-w-0 pr-4">
                                        <div className="w-8 h-8 rounded-lg flex items-center justify-center shadow-sm border border-[#E2E8F0] bg-white relative overflow-hidden shrink-0">
                                            <div className="absolute inset-0 opacity-10" style={{ background: detail.color ?? "#6366f1" }} />
                                            <FolderTree size={14} style={{ color: detail.color ?? "#6366f1" }} />
                                        </div>
                                        <h2 className="text-lg font-black text-[#0A1931] tracking-tight">{detail.name}</h2>
                                        
                                        <div className="flex items-center gap-1.5 ml-1">
                                            <Tooltip content="Grubu Kopyala"><button onClick={() => { setCloneGroupName(`${detail.name} (Kopya)`); setCloneGroupMembers(true); setCloneGroupCourses(true); setCloneGroupOpen(true); }}
                                                className="px-2 py-1 bg-white border border-[#E2E8F0] hover:bg-[#F0F4F8] rounded-md text-[#64748B] hover:text-[#0A1931] font-bold text-[10px] transition-colors shadow-sm flex items-center gap-1"><Copy size={12} /> Kopyala</button></Tooltip>
                                            <button onClick={() => openEdit(detail as unknown as GroupListDto)}
                                                className="px-2 py-1 bg-white border border-[#E2E8F0] hover:bg-[#F0F4F8] rounded-md text-[#64748B] hover:text-[#0A1931] font-bold text-[10px] transition-colors shadow-sm flex items-center gap-1"><Edit3 size={12} /> Düzenle</button>
                                        </div>

                                        <div className="hidden sm:block w-px h-4 bg-[#E2E8F0] mx-1" />
                                        
                                        <div className="flex items-center flex-wrap gap-1.5">
                                            <span className="whitespace-nowrap px-2 py-0.5 bg-white border border-[#E2E8F0] text-[#475569] text-[10px] rounded-md font-bold shadow-sm flex items-center gap-1"><div className="w-1.5 h-1.5 rounded-full shrink-0" style={{ background: detail.color ?? "#6366f1" }} />{detail.memberCount} öğrenci</span>
                                            <span className="whitespace-nowrap px-2 py-0.5 bg-white border border-[#E2E8F0] text-[#475569] text-[10px] rounded-md font-bold shadow-sm">{detail.courseCount} ders</span>
                                            {detail.educationType && <span className="whitespace-nowrap px-2 py-0.5 bg-indigo-50 text-indigo-700 text-[10px] rounded-md font-bold border border-indigo-200 flex items-center gap-1">{getEducationIcon(detail.educationType, 12)} {detail.educationType}</span>}
                                            {detail.parentGroupName && <span className="whitespace-nowrap px-2 py-0.5 bg-[#F8FAFC] text-[#64748B] text-[10px] rounded-md border border-[#E2E8F0] font-bold shadow-sm">↑ {detail.parentGroupName}</span>}
                                        </div>
                                    </div>
                                    <div className="w-full xl:w-auto overflow-x-auto hide-scrollbar shrink-0">
                                        <PremiumTabs 
                                            tabs={[
                                                { id: "members", label: "Öğrenciler", icon: <Users size={12} /> },
                                                { id: "courses", label: "Dersler", icon: <BookOpen size={12} /> },
                                                { id: "settings", label: "Ayarlar", icon: <Settings size={12} /> }
                                            ]} 
                                            activeTab={activeTab} 
                                            onChange={(id) => setActiveTab(id as DetailTab)} 
                                        />
                                    </div>
                                </div>
                            </div>

                            {/* Tab Content */}
                            <div className="flex-1 overflow-y-auto p-5">
                                {/* ── Members Tab ── */}
                                {activeTab === "members" && (
                                    <>
                                        {/* Filters & Actions */}
                                        <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
                                            <div className="flex items-center gap-3 flex-1 min-w-[300px]">
                                                <input type="checkbox" 
                                                    checked={filteredDetailMembers.length > 0 && selectedMembers.size === filteredDetailMembers.length}
                                                    onChange={e => {
                                                        if (e.target.checked) setSelectedMembers(new Set(filteredDetailMembers.map(m => m.userId)));
                                                        else setSelectedMembers(new Set());
                                                    }}
                                                    className="w-4 h-4 rounded border-[#E2E8F0] text-[#1B3B6F] focus:ring-[#1B3B6F]/20 cursor-pointer"
                                                    title="Tümünü Seç"
                                                />
                                                <div className="relative flex-1 max-w-sm">
                                                    <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                                                    <input 
                                                        value={memberSearch} 
                                                        onChange={e => { setMemberSearch(e.target.value); setMemberPage(1); }} 
                                                        placeholder="Öğrencilerde ara..."
                                                        className="w-full pl-8 pr-3 py-1.5 text-xs font-medium bg-white border border-[#E2E8F0] rounded-lg text-[#0A1931] placeholder-[#A0AEC0] focus:outline-none focus:border-[#A0AEC0] shadow-sm"
                                                    />
                                                </div>
                                            </div>
                                            <div className="flex items-center gap-2">
                                                {selectedMembers.size > 0 && (
                                                    <div className="flex items-center bg-[#E2E8F0]/30 rounded-lg overflow-hidden border border-[#E2E8F0] shadow-sm divide-x divide-[#E2E8F0]">
                                                        <Tooltip content="Kopyala / Aktar"><button onClick={() => setCopyOpen(true)} className="px-3 py-1.5 text-xs text-[#1B3B6F] font-bold hover:bg-white transition-colors">
                                                            Aktar
                                                        </button></Tooltip>
                                                        <Tooltip content="Taşı"><button onClick={() => setMoveOpen(true)} className="px-3 py-1.5 text-xs text-amber-600 font-bold hover:bg-amber-50 transition-colors">
                                                            Taşı
                                                        </button></Tooltip>
                                                        <Tooltip content="Gruptan Çıkar"><button onClick={handleRemoveSelectedMembers} className="px-3 py-1.5 text-xs text-rose-500 font-bold hover:bg-rose-50 transition-colors">
                                                            Gruptan Çıkar
                                                        </button></Tooltip>
                                                        <Tooltip content="Sistemden Tamamen Sil"><button onClick={() => setHardDeleteOpen(true)} className="px-3 py-1.5 text-xs text-red-600 font-bold hover:bg-red-100 transition-colors bg-red-50">
                                                            İmha Et
                                                        </button></Tooltip>
                                                    </div>
                                                )}
                                                <button onClick={() => setBulkRegisterOpen(true)}
                                                    className="px-3 py-1.5 text-xs bg-white text-[#1B3B6F] border border-[#E2E8F0] rounded-lg flex items-center gap-1.5 hover:bg-[#F8FAFC] font-bold">
                                                    <PiFilePlusDuotone size={16} /> Excel ile Toplu Kayıt
                                                </button>
                                                <button onClick={() => { setBulkAddSelection(new Set()); setAddMemberOpen(true); }}
                                                    className="px-3 py-1.5 text-xs bg-[#0A1931] text-white rounded-lg flex items-center gap-1.5 hover:bg-[#1B3B6F] font-bold">
                                                    <UserPlus size={12} /> Öğrenci Ekle
                                                </button>
                                            </div>
                                        </div>
                                        <div className="space-y-1.5">
                                            {paginatedDetailMembers.map(m => (
                                                <div key={m.userId} className={`flex items-center gap-3.5 p-2.5 rounded-xl group transition-all ${selectedMembers.has(m.userId) ? "bg-blue-50/50 border border-blue-200" : "bg-white hover:bg-[#F8FAFC] border border-[#E2E8F0]/60 shadow-sm"}`}>
                                                    <input type="checkbox" checked={selectedMembers.has(m.userId)}
                                                        onChange={() => toggleMember(m.userId)}
                                                        className="w-4 h-4 ml-1 rounded border-[#A0AEC0] text-[#1B3B6F] focus:ring-[#1B3B6F]/20" />
                                                    <div className="w-9 h-9 rounded-full bg-[#F0F4F8] text-[#1B3B6F] border border-[#E2E8F0] flex items-center justify-center text-xs font-extrabold shadow-sm">
                                                        {m.userFullName.split(" ").map(n => n[0]).join("").slice(0, 2)}
                                                    </div>
                                                    <div className="flex-1 min-w-0">
                                                        <p className="text-sm font-bold text-[#0A1931] tracking-tight">{m.userFullName}</p>
                                                        <p className="text-[11px] font-medium text-[#64748B] truncate">{m.email}</p>
                                                    </div>
                                                    <div className="flex items-center gap-3">
                                                        <span className="text-[10px] font-bold text-[#A0AEC0] hidden sm:flex items-center gap-1 bg-[#F8FAFC] px-2 py-1 rounded-lg border border-[#E2E8F0]/50">
                                                            <Calendar size={10} />
                                                            {new Date(m.addedAt).toLocaleDateString("tr-TR", { day: "numeric", month: "short", year: "numeric" })}
                                                        </span>
                                                        <span className="w-16 text-center px-2 py-1 bg-[#E2E8F0]/30 text-[#475569] text-[10px] rounded-lg font-bold">
                                                            {m.role === "Student" ? "Öğrenci" : m.role === "Teacher" ? "Eğitmen" : m.role}
                                                        </span>
                                                        <button onClick={() => handleRemoveMember(m.userId)}
                                                            className="p-1.5 text-[#A0AEC0] hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors opacity-0 group-hover:opacity-100">
                                                            <UserMinus size={14} />
                                                        </button>
                                                    </div>
                                                </div>
                                            ))}
                                            {filteredDetailMembers.length === 0 && (
                                                <div className="text-center py-10 text-[#A0AEC0] bg-[#F8FAFC] rounded-2xl border border-[#E2E8F0] border-dashed">
                                                    <Users size={32} className="mx-auto opacity-20 mb-2" />
                                                    <p className="text-sm font-medium">{memberSearch ? "Aramanıza uygun öğrenci bulunamadı" : "Bu grupta henüz öğrenci yok"}</p>
                                                    {!memberSearch && (
                                                        <button onClick={() => { setBulkAddSelection(new Set()); setAddMemberOpen(true); }}
                                                            className="mt-3 px-4 py-2 text-xs font-bold text-white bg-[#0A1931] rounded-lg hover:bg-[#1B3B6F] shadow-md transition-colors">+ İlk Öğrenciyi Ekle</button>
                                                    )}
                                                </div>
                                            )}
                                        </div>

                                        {/* Pagination Footer */}
                                        {filteredDetailMembers.length > 0 && (
                                            <div className="flex items-center justify-between mt-4 p-3 bg-[#F8FAFC] border border-[#E2E8F0] rounded-xl">
                                                <div className="flex items-center gap-3 text-xs text-[#64748B] font-medium">
                                                    <div className="flex items-center gap-1.5">
                                                        <span>Göster:</span>
                                                        <select 
                                                            value={memberPageSize} 
                                                            onChange={e => { setMemberPageSize(Number(e.target.value)); setMemberPage(1); }}
                                                            className="bg-white border border-[#E2E8F0] rounded p-1 outline-none text-[#0A1931] font-bold"
                                                        >
                                                            <option value={10}>10</option>
                                                            <option value={20}>20</option>
                                                            <option value={50}>50</option>
                                                            <option value={100}>100</option>
                                                        </select>
                                                    </div>
                                                    <span>Toplam {filteredDetailMembers.length} kayıt</span>
                                                </div>
                                                
                                                {detailMemberTotalPages > 1 && (
                                                    <div className="flex items-center gap-1">
                                                        <button 
                                                            onClick={() => setMemberPage(p => Math.max(1, p - 1))} 
                                                            disabled={memberPage === 1} 
                                                            className="px-2 py-1.5 text-xs font-bold rounded-lg hover:bg-[#E2E8F0]/50 disabled:opacity-30 text-[#1B3B6F] transition-colors"
                                                        >
                                                            Önceki
                                                        </button>
                                                        <span className="text-xs font-bold text-[#0A1931] px-2">
                                                            {memberPage} / {detailMemberTotalPages}
                                                        </span>
                                                        <button 
                                                            onClick={() => setMemberPage(p => Math.min(detailMemberTotalPages, p + 1))} 
                                                            disabled={memberPage === detailMemberTotalPages} 
                                                            className="px-2 py-1.5 text-xs font-bold rounded-lg hover:bg-[#E2E8F0]/50 disabled:opacity-30 text-[#1B3B6F] transition-colors"
                                                        >
                                                            Sonraki
                                                        </button>
                                                    </div>
                                                )}
                                            </div>
                                        )}
                                    </>
                                )}

                                {/* ── Courses Tab ── */}
                                {activeTab === "courses" && (
                                    <>
                                        <div className="flex items-center justify-between mb-4">
                                            <p className="text-sm font-bold text-[#1B3B6F]">{detail.courses.length} Ders</p>
                                            <button onClick={() => { setAssignCourseSearch(""); setAssignCourseOpen(true); }}
                                                className="px-3 py-1.5 text-xs bg-[#0A1931] text-white rounded-lg flex items-center gap-1.5 hover:bg-[#1B3B6F] font-bold">
                                                <Plus size={12} /> Ders Ata
                                            </button>
                                        </div>
                                        <div className="space-y-2">
                                            {detail.courses.map(c => {
                                                const modeLabel = c.mode === "Both" ? "Online" : c.mode;
                                                return (
                                                <div key={c.courseId} className="flex items-center gap-3 p-3 rounded-xl bg-[#E2E8F0]/15 hover:bg-[#E2E8F0]/30 transition-colors group">
                                                    <div className="w-9 h-9 rounded-xl bg-emerald-50 flex items-center justify-center">
                                                        <BookOpen size={15} className="text-emerald-600" />
                                                    </div>
                                                    <div className="flex-1 min-w-0">
                                                        <p className="text-sm font-medium text-[#0A1931] truncate">{c.courseTitle}</p>
                                                        <p className="text-[10px] text-[#A0AEC0]">{modeLabel}</p>
                                                    </div>
                                                    <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${modeLabel === "Online" ? "bg-blue-50 text-blue-600 border border-blue-200" : "bg-amber-50 text-amber-600 border border-amber-200"}`}>
                                                        {modeLabel}
                                                    </span>
                                                    <button onClick={() => handleRemoveCourse(c.courseId)}
                                                        className="p-1.5 text-[#A0AEC0] hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors opacity-0 group-hover:opacity-100">
                                                        <Trash2 size={13} />
                                                    </button>
                                                </div>
                                                );
                                            })}
                                            {detail.courses.length === 0 && (
                                                <div className="text-center py-10 text-[#A0AEC0]">
                                                    <BookOpen size={32} className="mx-auto opacity-20 mb-2" />
                                                    <p className="text-sm">Bu gruba atanmış ders yok</p>
                                                    <button onClick={() => { setAssignCourseSearch(""); setAssignCourseOpen(true); }}
                                                        className="mt-3 px-4 py-2 text-xs font-bold text-[#1B3B6F] bg-[#E2E8F0]/30 rounded-lg hover:bg-[#E2E8F0]/50">+ Ders Ata</button>
                                                </div>
                                            )}
                                        </div>
                                    </>
                                )}

                                {/* ── Settings Tab ── */}
                                {activeTab === "settings" && (
                                    <div className="space-y-5">
                                        <div>
                                            <p className="text-sm font-bold text-[#1B3B6F] mb-3">Grup Bilgileri</p>
                                            <div className="grid grid-cols-2 gap-3 text-sm">
                                                <div className="p-3 bg-[#E2E8F0]/15 rounded-xl">
                                                    <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-wider mb-1">Oluşturma Tarihi</p>
                                                    <p className="text-[#0A1931] font-medium">{new Date(detail.createdAt).toLocaleDateString("tr-TR", { day: "numeric", month: "long", year: "numeric" })}</p>
                                                </div>
                                                <div className="p-3 bg-[#E2E8F0]/15 rounded-xl">
                                                    <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-wider mb-1">Üst Grup</p>
                                                    <p className="text-[#0A1931] font-medium">{detail.parentGroupName ?? "Kök Grup"}</p>
                                                </div>
                                            </div>
                                        </div>
                                        <div>
                                            <p className="text-sm font-bold text-[#1B3B6F] mb-3">Grup Rengi</p>
                                            <div className="flex gap-2">
                                                {COLOR_PRESETS.map(c => (
                                                    <div key={c} onClick={() => handleColorChange(detail as unknown as GroupListDto, c)}
                                                        className={`w-8 h-8 rounded-full cursor-pointer hover:scale-110 transition-transform ${detail.color === c ? "ring-2 ring-offset-2 ring-[#1B3B6F]" : ""}`}
                                                        style={{ background: c }} />
                                                ))}
                                            </div>
                                        </div>
                                        <div className="pt-4 border-t border-[#E2E8F0]">
                                            <p className="text-sm font-bold text-red-600 mb-2">Tehlikeli Bölge</p>
                                            <button onClick={() => setDeleteTarget(detail.id)}
                                                className="px-4 py-2 bg-red-50 text-red-600 border border-red-200 text-sm font-bold rounded-xl hover:bg-red-100 transition-colors flex items-center gap-2">
                                                <Trash2 size={14} /> Bu Grubu Sil
                                            </button>
                                        </div>
                                    </div>
                                )}
                            </div>
                        </>
                    ) : null}
                </div>
            </div>

            {/* ✨💎 Premium Group Form Modal ✨💎 */}
            {formOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm" onClick={() => setFormOpen(false)}>
                    <div className="bg-white rounded-2xl shadow-2xl w-full max-w-4xl max-h-[90vh] flex flex-col overflow-hidden" onClick={e => e.stopPropagation()}>
                        {/* Clean Premium Header */}
                        <div className="relative overflow-hidden px-7 py-6 border-b border-[#E2E8F0] bg-[#F8FAFC] shrink-0">
                            <div className="absolute top-0 left-0 w-full h-1" style={{ background: formColor }} />
                            <div className="flex items-center justify-between relative z-10">
                                <div className="flex items-center gap-4">
                                    <div className="w-12 h-12 rounded-xl flex items-center justify-center shadow-sm border border-[#E2E8F0] bg-white relative overflow-hidden">
                                        <div className="absolute inset-0 opacity-10" style={{ background: formColor }} />
                                        <FolderTree size={20} style={{ color: formColor }} />
                                    </div>
                                    <div>
                                        <h3 className="text-xl font-black text-[#0A1931] tracking-tight">{editGroup ? "Grubu Düzenle" : "Yeni Grup Oluştur"}</h3>
                                        <p className="text-[10px] font-extrabold text-[#64748B] uppercase tracking-widest mt-0.5">Öğrenci Grubu Yönetimi</p>
                                    </div>
                                </div>
                                <button onClick={() => setFormOpen(false)} className="w-8 h-8 flex items-center justify-center rounded-full bg-white border border-[#E2E8F0] text-[#64748B] hover:text-[#0A1931] shadow-sm transition-colors"><X size={14} strokeWidth={3} /></button>
                            </div>
                        </div>

                        <div className="grid grid-cols-5 gap-0 overflow-y-auto">
                            {/* Left: Form */}
                            <div className="col-span-3 p-6 space-y-4 border-r border-[#E2E8F0]">
                                {/* Group Type Selector */}
                                <div>
                                    <label className="text-[11px] font-extrabold text-[#64748B] uppercase tracking-widest block mb-2">Eğitim Modeli <span className="text-red-500">*</span></label>
                                    <div className="grid grid-cols-5 gap-2">
                                        {["Canlı", "Offline", "Kamp", "Sınav", "Demo"].map((label) => (
                                            <button key={label} type="button" onClick={() => setFormType(label)}
                                                className={`flex flex-col items-center justify-center gap-1.5 p-3 rounded-xl border transition-all text-center bg-white ${formType === label ? "border-indigo-600 bg-indigo-50/30 ring-2 ring-indigo-600/20 shadow-sm text-indigo-600" : "border-[#E2E8F0] hover:border-[#A0AEC0] hover:bg-[#F8FAFC] text-[#64748B]"}`}>
                                                <div className="text-[28px]">{getEducationIcon(label, 28)}</div>
                                                <span className={`text-[11px] font-bold ${formType === label ? "text-indigo-900" : "text-[#475569]"}`}>{label}</span>
                                            </button>
                                        ))}
                                    </div>
                                </div>

                                {/* Group Name */}
                                <div>
                                    <label className="text-[11px] font-extrabold text-[#64748B] uppercase tracking-widest block mb-1.5">Grup Adı <span className="text-red-500">*</span></label>
                                    <input value={formName} onChange={e => setFormName(e.target.value)} placeholder="Örn: TYT-A Sınıfı, Matematik Çalışma Grubu"
                                        className="w-full px-4 py-3 text-sm font-bold border border-[#E2E8F0] rounded-xl text-[#0A1931] bg-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500 focus:bg-white placeholder:text-[#A0AEC0] placeholder:font-medium transition-all" />
                                </div>

                                {/* Description */}
                                <div>
                                    <label className="text-[11px] font-extrabold text-[#64748B] uppercase tracking-widest block mb-1.5">Açıklama</label>
                                    <textarea value={formDesc} onChange={e => setFormDesc(e.target.value)} rows={2} placeholder="Grup hakkında kısa bir açıklama..."
                                        className="w-full px-4 py-3 text-sm border border-[#E2E8F0] rounded-xl text-[#0A1931] bg-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500 focus:bg-white resize-none placeholder:text-[#A0AEC0] transition-all" />
                                </div>

                                {/* Parent Group */}
                                <div>
                                    <label className="text-[11px] font-extrabold text-[#64748B] uppercase tracking-widest block mb-1.5">Üst Grup</label>
                                    <select value={formParent} onChange={e => setFormParent(e.target.value)}
                                        className="w-full px-4 py-3 text-sm font-medium border border-[#E2E8F0] rounded-xl text-[#0A1931] bg-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500 focus:bg-white transition-all cursor-pointer">
                                        <option value="">— Kök Grup (Bağımsız) —</option>
                                        {groups.filter(g => !editGroup || g.id !== editGroup.id).map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
                                    </select>
                                </div>

                                {/* Color Picker */}
                                <div>
                                    <label className="text-[11px] font-extrabold text-[#64748B] uppercase tracking-widest block mb-2">Grup Rengi</label>
                                    <div className="flex items-center gap-2 flex-wrap">
                                        {COLOR_PRESETS.map(c => (
                                            <div key={c} onClick={() => setFormColor(c)}
                                                className={`w-8 h-8 rounded-full cursor-pointer hover:scale-110 transition-all shadow-sm ${formColor === c ? "ring-2 ring-offset-2 ring-indigo-600 scale-110" : "opacity-80 hover:opacity-100"}`}
                                                style={{ background: c }} />
                                        ))}
                                    </div>
                                </div>
                                {/* Expiration Date */}
                                <div>
                                    <label className="text-[11px] font-extrabold text-[#64748B] uppercase tracking-widest block mb-1.5">Grubun Son Kullanma Tarihi</label>
                                    <input type="date" value={formExpirationDate} onChange={e => setFormExpirationDate(e.target.value)}
                                        className="w-full px-4 py-3 text-sm font-medium border border-[#E2E8F0] rounded-xl text-[#0A1931] bg-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500 focus:bg-white transition-all cursor-pointer" />
                                </div>
                            </div>

                            {/* Right: Live Preview */}
                            <div className="col-span-2 p-6 bg-[#F8FAFC] flex flex-col">
                                <p className="text-[11px] font-extrabold text-[#64748B] uppercase tracking-widest mb-3">Canlı Önizleme</p>
                                <div className="bg-white rounded-[1.25rem] border border-[#E2E8F0] p-5 shadow-[0_4px_20px_rgb(0,0,0,0.03)] flex-1">
                                    <div className="flex items-center gap-3.5 mb-4">
                                        <div className="w-12 h-12 rounded-xl flex items-center justify-center shadow-sm border border-[#E2E8F0] bg-white relative overflow-hidden shrink-0">
                                            <div className="absolute inset-0 opacity-10" style={{ background: formColor }} />
                                            <FolderTree size={20} style={{ color: formColor }} />
                                        </div>
                                        <div className="min-w-0">
                                            <p className="font-black text-[#0A1931] text-xl tracking-tight truncate">{formName || "Grup Adı"}</p>
                                            <p className="text-xs font-medium text-[#64748B] mt-0.5 line-clamp-1">{formDesc || "Açıklama..."}</p>
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-2.5 flex-wrap">
                                        <span className="px-3.5 py-1.5 bg-white border border-[#E2E8F0] text-[#475569] text-[13px] rounded-xl font-bold shadow-sm flex items-center gap-2"><div className="w-2.5 h-2.5 rounded-full" style={{ background: formColor }} />0 öğrenci</span>
                                        <span className="px-3.5 py-1.5 bg-white border border-[#E2E8F0] text-[#475569] text-[13px] rounded-xl font-bold shadow-sm">0 ders</span>
                                        {formType && <span className="px-3.5 py-1.5 bg-indigo-50 text-indigo-700 text-[13px] rounded-xl font-bold border border-indigo-200 flex items-center gap-1.5">{getEducationIcon(formType, 16)} {formType}</span>}
                                    </div>
                                    {formParent && (
                                        <div className="mt-4 pt-3 border-t border-[#E2E8F0] text-[13px] font-bold text-[#64748B]">
                                            ↑ {groups.find(g => g.id === formParent)?.name ?? "Üst Grup"}
                                        </div>
                                    )}
                                </div>

                                {/* Quick Tips */}
                                <div className="mt-5 space-y-2">
                                    <p className="text-[11px] font-extrabold text-[#64748B] uppercase tracking-widest mb-3">İpuçları</p>
                                    <div className="text-[11px] text-[#475569] space-y-2.5 font-medium leading-relaxed">
                                        <p className="flex items-start gap-2"><span className="opacity-70">💡</span> Grup oluşturduktan sonra öğrenci ve ders ekleyebilirsiniz</p>
                                        <p className="flex items-start gap-2"><span className="opacity-70">🎨</span> Renk, ağaç görünümünde grubu hızlı tanımanızı sağlar</p>
                                        <p className="flex items-start gap-2"><span className="opacity-70">📂</span> Alt gruplarla hiyerarşik yapı oluşturabilirsiniz</p>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Footer */}
                        <div className="flex items-center justify-between px-7 py-5 border-t border-[#E2E8F0] bg-white shrink-0">
                            <p className="text-[11px] font-bold text-[#64748B]">{!formType ? <span className="text-red-500 bg-red-50 px-2 py-1 rounded-md">Eğitim modeli seçin</span> : !formName.trim() ? <span className="text-amber-600 bg-amber-50 px-2 py-1 rounded-md">Grup adı gerekli</span> : <span className="text-emerald-600 bg-emerald-50 px-2 py-1 rounded-md">✓ Form Hazır</span>}</p>
                            <div className="flex gap-3">
                                <button onClick={() => setFormOpen(false)} className="px-5 py-2.5 text-sm font-bold text-[#64748B] hover:text-[#0A1931] hover:bg-[#F8FAFC] rounded-xl transition-colors">İptal</button>
                                <button onClick={handleSave} disabled={!formName.trim() || !formType || formSaving}
                                    className="px-7 py-2.5 text-sm font-bold bg-[#0A1931] text-white rounded-xl hover:bg-indigo-600 disabled:bg-[#E2E8F0] disabled:text-[#A0AEC0] disabled:shadow-none shadow-[0_4px_14px_0_rgba(10,25,49,0.39)] hover:shadow-[0_6px_20px_rgba(79,70,229,0.23)] transition-all flex items-center justify-center gap-2">
                                    {formSaving && <Loader2 size={14} className="animate-spin" />}
                                    {editGroup ? "Kaydet" : "Oluştur"}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* ── Bulk Add Members Modal ── */}
            {addMemberOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/30 backdrop-blur-sm">
                    <div className="bg-white rounded-2xl shadow-2xl border border-[#E2E8F0] w-full max-w-md">
                        <div className="flex items-center justify-between px-6 py-4 border-b border-[#E2E8F0]">
                            <h3 className="text-base font-bold text-[#0A1931]">Öğrenci Ekle</h3>
                            <div className="flex items-center gap-2">
                                {bulkAddSelection.size > 0 && (
                                    <span className="text-xs font-bold text-[#1B3B6F] bg-blue-50 px-2 py-1 rounded-lg">{bulkAddSelection.size} seçili</span>
                                )}
                                <button onClick={() => { setAddMemberOpen(false); setAddMemberSearch(""); }}><X size={16} className="text-[#A0AEC0]" /></button>
                            </div>
                        </div>
                        <div className="p-4">
                            <div className="relative mb-3">
                                <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                                <input value={addMemberSearch} onChange={e => setAddMemberSearch(e.target.value)}
                                    placeholder="İsim veya e-posta ara..."
                                    className="w-full pl-8 pr-3 py-2 text-sm border border-[#E2E8F0] rounded-xl text-[#1B3B6F] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20" />
                            </div>
                            <div className="max-h-64 overflow-y-auto space-y-1">
                                {nonMembers.slice(0, 30).map(u => (
                                    <div key={u.id}
                                        className={`flex items-center gap-3 p-2 rounded-lg cursor-pointer transition-colors ${bulkAddSelection.has(u.id) ? "bg-blue-50 border border-blue-200" : "hover:bg-[#E2E8F0]/20 border border-transparent"}`}
                                        onClick={() => toggleBulkAdd(u.id)}>
                                        <input type="checkbox" checked={bulkAddSelection.has(u.id)} readOnly
                                            className="w-4 h-4 rounded border-[#E2E8F0] text-[#1B3B6F]" />
                                        <div className="w-8 h-8 rounded-full bg-[#0A1931] text-white flex items-center justify-center text-xs font-bold">
                                            {u.fullName.split(" ").map(n => n[0]).join("").slice(0, 2)}
                                        </div>
                                        <div className="flex-1 min-w-0">
                                            <p className="text-sm text-[#0A1931] truncate">{u.fullName}</p>
                                            <p className="text-xs text-[#A0AEC0] truncate">{u.email}</p>
                                        </div>
                                    </div>
                                ))}
                                {nonMembers.length === 0 && <p className="text-center text-sm text-[#A0AEC0] py-6">Eklenebilecek kullanıcı yok</p>}
                            </div>
                        </div>
                        <div className="px-6 py-4 border-t border-[#E2E8F0] bg-[#E2E8F0]/10 rounded-b-2xl flex justify-end gap-2">
                            <button onClick={() => { setAddMemberOpen(false); setAddMemberSearch(""); }}
                                className="px-4 py-2 text-xs font-bold text-[#A9A9A9]">İptal</button>
                            <button onClick={handleBulkAdd} disabled={bulkAddSelection.size === 0}
                                className="px-5 py-2 text-xs font-bold bg-[#0A1931] text-white rounded-xl hover:bg-[#1B3B6F] disabled:opacity-40">
                                {bulkAddSelection.size} Kişiyi Ekle
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* ── Copy Members Modal ── */}
            {copyOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/30 backdrop-blur-sm">
                    <div className="bg-white rounded-2xl shadow-2xl border border-[#E2E8F0] w-full max-w-sm p-6">
                        <div className="flex items-center justify-between mb-4">
                            <h3 className="text-base font-bold text-[#0A1931]">{selectedMembers.size} Öğrenciyi Aktar</h3>
                            <button onClick={() => setCopyOpen(false)}><X size={16} className="text-[#A0AEC0]" /></button>
                        </div>
                        <p className="text-xs text-[#A0AEC0] mb-3">Seçili öğrenciler bu grupta kalacak ve hedef gruba da kopyalanacak (aktarılacak).</p>
                        <select value={copyTargetGroup} onChange={e => setCopyTargetGroup(e.target.value)}
                            className="w-full px-3 py-2.5 text-sm border border-[#E2E8F0] rounded-xl text-[#1B3B6F] focus:outline-none mb-4">
                            <option value="">Hedef grup seçin</option>
                            {groups.filter(g => g.id !== selectedId).map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
                        </select>
                        <div className="flex gap-2">
                            <button onClick={() => setCopyOpen(false)} className="flex-1 py-2.5 text-sm font-bold text-[#A9A9A9] border border-[#E2E8F0] rounded-xl">İptal</button>
                            <button onClick={handleCopyMembers} disabled={!copyTargetGroup}
                                className="flex-1 py-2.5 text-sm font-bold bg-[#1B3B6F] text-white rounded-xl disabled:opacity-40">Aktar</button>
                        </div>
                    </div>
                </div>
            )}

            {/* ── Move Members Modal ── */}
            {moveOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/30 backdrop-blur-sm">
                    <div className="bg-white rounded-2xl shadow-2xl border border-[#E2E8F0] w-full max-w-sm p-6">
                        <div className="flex items-center justify-between mb-4">
                            <h3 className="text-base font-bold text-[#0A1931]">{selectedMembers.size} Öğrenciyi Taşı</h3>
                            <button onClick={() => setMoveOpen(false)}><X size={16} className="text-[#A0AEC0]" /></button>
                        </div>
                        <p className="text-xs text-[#A0AEC0] mb-3">Seçili öğrenciler bu gruptan çıkarılıp hedef gruba taşınacak.</p>
                        <select value={moveTargetGroup} onChange={e => setMoveTargetGroup(e.target.value)}
                            className="w-full px-3 py-2.5 text-sm border border-[#E2E8F0] rounded-xl text-[#1B3B6F] focus:outline-none mb-4">
                            <option value="">Hedef grup seçin</option>
                            {groups.filter(g => g.id !== selectedId).map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
                        </select>
                        <div className="flex gap-2">
                            <button onClick={() => setMoveOpen(false)} className="flex-1 py-2.5 text-sm font-bold text-[#A9A9A9] border border-[#E2E8F0] rounded-xl">İptal</button>
                            <button onClick={handleMoveMembers} disabled={!moveTargetGroup}
                                className="flex-1 py-2.5 text-sm font-bold bg-amber-500 text-white rounded-xl hover:bg-amber-600 disabled:opacity-40 flex items-center justify-center gap-2">
                                <ArrowRight size={14} /> Taşı
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* ── Assign Course Modal ── */}
            {assignCourseOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm">
                    <div className="bg-white rounded-[2rem] shadow-2xl w-full max-w-lg flex flex-col overflow-hidden animate-fade-in-up border border-[#E2E8F0]">
                        <div className="flex items-center justify-between p-6 border-b border-[#E2E8F0]/60 bg-[#E2E8F0]/15">
                            <h2 className="text-xl font-black text-[#0A1931]">Ders Ata</h2>
                            <button onClick={() => { setAssignCourseOpen(false); setAssignCourseSearch(""); }} className="p-2 text-[#A0AEC0] hover:text-[#0A1931] rounded-xl"><X size={20} /></button>
                        </div>
                        <div className="p-6">
                            <div className="relative mb-6">
                                <Search size={18} className="absolute left-4 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                                <input
                                    type="text"
                                    placeholder="Ders adı ara..."
                                    value={assignCourseSearch}
                                    onChange={(e) => setAssignCourseSearch(e.target.value)}
                                    className="w-full pl-12 pr-4 py-3 bg-[#E2E8F0]/20 border border-[#E2E8F0]/60 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500 transition-all font-medium"
                                />
                            </div>

                            <div className="space-y-2 max-h-[300px] overflow-y-auto pr-1">
                                {loadingCourses ? (
                                    <div className="flex flex-col items-center justify-center py-10">
                                        <Loader2 className="w-8 h-8 text-indigo-500 animate-spin mb-3" />
                                        <p className="text-sm font-medium text-[#64748B]">Dersler yükleniyor...</p>
                                    </div>
                                ) : (
                                    <>
                                        {allCourses
                                            .filter(c => !detail?.courses.some(dc => dc.courseId === c.id))
                                            .filter(c => !assignCourseSearch || c.title.toLowerCase().includes(assignCourseSearch.toLowerCase()))
                                            .map(c => {
                                                const isSelected = assignCourseSelection.has(c.id);
                                                return (
                                                    <div key={c.id} 
                                                        onClick={() => {
                                                            const newSel = new Set(assignCourseSelection);
                                                            if (isSelected) newSel.delete(c.id);
                                                            else newSel.add(c.id);
                                                            setAssignCourseSelection(newSel);
                                                        }}
                                                        className={`flex items-center gap-3 p-3 rounded-xl border cursor-pointer transition-all ${isSelected ? "border-indigo-500 bg-indigo-50/50" : "border-[#E2E8F0]/60 hover:border-indigo-200 hover:bg-indigo-50/30"}`}>
                                                        <input type="checkbox" checked={isSelected} readOnly className="w-4 h-4 rounded border-[#E2E8F0] text-indigo-600" />
                                                        <p className="text-sm font-bold text-[#0A1931] flex-1">{c.title}</p>
                                                    </div>
                                                );
                                        })}
                                        {allCourses.filter(c => !detail?.courses.some(dc => dc.courseId === c.id)).filter(c => !assignCourseSearch || c.title.toLowerCase().includes(assignCourseSearch.toLowerCase())).length === 0 && (
                                            <p className="text-center text-sm text-[#A0AEC0] py-4">Aramanıza uygun ders bulunamadı.</p>
                                        )}
                                    </>
                                )}
                            </div>
                        </div>
                        <div className="px-6 py-4 border-t border-[#E2E8F0]/60 bg-[#E2E8F0]/15 flex justify-end gap-2">
                            <button onClick={() => { setAssignCourseOpen(false); setAssignCourseSearch(""); setAssignCourseSelection(new Set()); }}
                                className="px-5 py-2.5 text-sm font-bold text-[#A9A9A9] border border-[#E2E8F0] rounded-xl hover:bg-white transition-colors">İptal</button>
                            <button onClick={handleAssignCourses} disabled={assignCourseSelection.size === 0}
                                className="px-5 py-2.5 text-sm font-bold bg-[#0A1931] text-white rounded-xl hover:bg-[#1B3B6F] disabled:opacity-40 transition-colors">
                                {assignCourseSelection.size > 0 ? `${assignCourseSelection.size} Dersi Ata` : "Ders Seçiniz"}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* ── Clone Group Modal ── */}
            {cloneGroupOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/30 backdrop-blur-sm">
                    <div className="bg-white rounded-2xl shadow-2xl border border-[#E2E8F0] w-full max-w-sm p-6">
                        <div className="flex items-center justify-between mb-4">
                            <h3 className="text-base font-bold text-[#0A1931]">Grubu Kopyala</h3>
                            <button onClick={() => setCloneGroupOpen(false)}><X size={16} className="text-[#A0AEC0]" /></button>
                        </div>
                        <div className="space-y-4">
                            <div>
                                <label className="text-xs font-bold text-[#1B3B6F] mb-1.5 block">Yeni Grup Adı</label>
                                <input value={cloneGroupName} onChange={e => setCloneGroupName(e.target.value)}
                                    className="w-full px-3 py-2.5 text-sm border border-[#E2E8F0] rounded-xl text-[#1B3B6F] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20" />
                            </div>
                            <div className="space-y-2">
                                <label className="flex items-center gap-2 cursor-pointer">
                                    <input type="checkbox" checked={cloneGroupMembers} onChange={e => setCloneGroupMembers(e.target.checked)} className="w-4 h-4 rounded border-[#E2E8F0] text-[#1B3B6F]" />
                                    <span className="text-sm font-medium text-[#0A1931]">Öğrencileri de kopyala</span>
                                </label>
                                <label className="flex items-center gap-2 cursor-pointer">
                                    <input type="checkbox" checked={cloneGroupCourses} onChange={e => setCloneGroupCourses(e.target.checked)} className="w-4 h-4 rounded border-[#E2E8F0] text-[#1B3B6F]" />
                                    <span className="text-sm font-medium text-[#0A1931]">Ders atamalarını kopyala</span>
                                </label>
                            </div>
                            <div className="flex gap-2 mt-5">
                                <button onClick={() => setCloneGroupOpen(false)} className="flex-1 py-2.5 text-sm font-bold text-[#A9A9A9] border border-[#E2E8F0] rounded-xl">İptal</button>
                                <button onClick={handleCloneGroup} disabled={!cloneGroupName.trim()}
                                    className="flex-1 py-2.5 text-sm font-bold bg-[#0A1931] text-white rounded-xl hover:bg-[#1B3B6F] disabled:opacity-40">
                                    Kopyala
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Delete Confirm */}
            <ConfirmDialog open={!!deleteTarget} title="Grubu Sil"
                message="Bu grubu silmek istediğinize emin misiniz? Alt gruplar da silinebilir."
                confirmText="Sil" onConfirm={handleDelete} onClose={() => setDeleteTarget(null)} variant="danger" />

            {/* Hard Delete Confirm */}
            <ConfirmDialog open={hardDeleteOpen} title="Kullanıcıları İmha Et"
                message={`DİKKAT: Seçili ${selectedMembers.size} kullanıcı SİSTEMDEN TAMAMEN SİLİNECEKTİR. Onaylıyor musunuz?`}
                confirmText="İmha Et" onConfirm={executeHardDeleteMembers} onClose={() => setHardDeleteOpen(false)} variant="danger" />

            {bulkRegisterOpen && (
                <BulkRegisterModal
                    token={token!}
                    tenantId={tenantId!}
                    groups={groups}
                    preselectedGroupId={detail?.id}
                    preselectedGroupName={detail?.name}
                    onClose={() => setBulkRegisterOpen(false)}
                    onSuccess={() => {
                        if (detail) {
                            groupsApi.get(token!, tenantId!, detail.id).then(setDetail).catch(() => {});
                        }
                    }}
                />
            )}
        </div>
    );
}

