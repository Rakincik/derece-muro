"use client";

import { useState, useMemo, useEffect, useCallback } from "react";
import {
    FolderTree, Plus, Users, Edit3, Trash2, X, Search,
    BookOpen, Settings, ChevronDown, ChevronRight, Loader2,
    RefreshCw, UserPlus, UserMinus, ArrowRight, Check,
    AlertTriangle, BarChart3, Calendar
} from "lucide-react";
import { useToast } from "@/components/toast";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { useAuth } from "@/contexts/AuthContext";
import { groupsApi, courseApi, notificationApi, type GroupListDto, type GroupDetailDto, type CourseListDto } from "@/lib/api";

type DetailTab = "members" | "courses" | "settings";

const COLOR_PRESETS = ["#6366f1", "#8b5cf6", "#3b82f6", "#ec4899", "#10b981", "#f59e0b", "#14b8a6", "#ef4444"];

// ── Tree Item ────────────────────────────────────────────────────────────────
function GroupTreeItem({
    group, selected, expanded, hasChildren, onSelect, onToggle, onEdit, onDelete,
}: {
    group: GroupListDto; selected: boolean; expanded: boolean; hasChildren: boolean;
    onSelect: () => void; onToggle: () => void; onEdit: () => void; onDelete: () => void;
}) {
    const isEmpty = group.memberCount === 0;
    return (
        <div
            className={`flex items-center gap-2 px-3 py-2 rounded-xl cursor-pointer group transition-all ${selected ? "bg-[#E2E8F0]/30 border border-[#E2E8F0]" : "hover:bg-[#E2E8F0]/20 border border-transparent"}`}
            onClick={onSelect}
        >
            <button onClick={e => { e.stopPropagation(); onToggle(); }} className="shrink-0 w-4 text-[#A0AEC0]">
                {hasChildren ? (expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />) : <span className="w-4" />}
            </button>
            <div className="w-3 h-3 rounded-full shrink-0" style={{ background: group.color ?? "#94a3b8" }} />
            <div className="flex-1 min-w-0">
                <p className={`text-sm font-medium truncate ${selected ? "text-[#0A1931]" : "text-[#1B3B6F]"}`}>{group.name}</p>
                <p className="text-[10px] text-[#A0AEC0]">{group.memberCount} üye · {group.courseCount} ders{group.educationType ? ` · ${group.educationType}` : ""}</p>
            </div>
            {isEmpty && <span className="text-amber-400" title="Boş grup"><AlertTriangle size={12} /></span>}
            <div className="hidden group-hover:flex items-center gap-1">
                <button onClick={e => { e.stopPropagation(); onEdit(); }}
                    className="p-1.5 text-[#A0AEC0] hover:text-[#1B3B6F] hover:bg-[#E2E8F0]/30 rounded-lg transition-colors">
                    <Edit3 size={12} />
                </button>
                <button onClick={e => { e.stopPropagation(); onDelete(); }}
                    className="p-1.5 text-[#A0AEC0] hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors">
                    <Trash2 size={12} />
                </button>
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
    const [formSaving, setFormSaving] = useState(false);

    // Member management
    const [addMemberOpen, setAddMemberOpen] = useState(false);
    const [addMemberSearch, setAddMemberSearch] = useState("");
    const [allUsers, setAllUsers] = useState<{ id: string; fullName: string; email: string }[]>([]);
    const [selectedMembers, setSelectedMembers] = useState<Set<string>>(new Set());
    const [bulkAddSelection, setBulkAddSelection] = useState<Set<string>>(new Set());

    // Move members
    const [moveOpen, setMoveOpen] = useState(false);
    const [moveTargetGroup, setMoveTargetGroup] = useState("");

    // Course assignment
    const [allCourses, setAllCourses] = useState<{ id: string; title: string }[]>([]);
    const [assignCourseOpen, setAssignCourseOpen] = useState(false);
    const [assignCourseId, setAssignCourseId] = useState("");
    const [assignMode, setAssignMode] = useState("Both");

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

    // Load courses for assignment
    useEffect(() => {
        if (!assignCourseOpen || !token || !tenantId || allCourses.length > 0) return;
        courseApi.list(token, tenantId, { pageSize: 200 }).then(res => {
            setAllCourses(res.items.map((c: CourseListDto) => ({ id: c.id, title: c.title })));
        }).catch(() => { });
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
        setEditGroup(null); setFormName(""); setFormDesc(""); setFormColor(COLOR_PRESETS[0]); setFormParent(""); setFormType("");
        setFormOpen(true);
    };
    const openEdit = (g: GroupListDto) => {
        setEditGroup(g); setFormName(g.name); setFormDesc(g.description ?? ""); setFormColor(g.color ?? COLOR_PRESETS[0]); setFormParent(g.parentGroupId ?? ""); setFormType(g.educationType ?? "");
        setFormOpen(true);
    };

    const handleSave = async () => {
        if (!token || !tenantId || !formName.trim() || !formType) return;
        setFormSaving(true);
        try {
            const data = { name: formName.trim(), description: formDesc || undefined, color: formColor, parentGroupId: formParent || undefined, educationType: formType || undefined };
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
        } catch { toastError("Hata", "Silinemedi."); }
        finally { setDeleteTarget(null); }
    };

    const handleRemoveMember = async (userId: string) => {
        if (!selectedId || !token || !tenantId) return;
        try {
            await groupsApi.removeMember(token, tenantId, selectedId, userId);
            setDetail(prev => prev ? { ...prev, members: prev.members.filter(m => m.userId !== userId), memberCount: prev.memberCount - 1 } : prev);
            success("Çıkarıldı", "Üye gruptan çıkarıldı.");
        } catch { toastError("Hata", "Üye çıkarılamadı."); }
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
            success("Üye Eklendi", `${bulkAddSelection.size} üye gruba eklendi.${childCount > 0 ? ` (${childCount} alt gruba da otomatik eklendi)` : ''}`);
            await loadGroups();
        } catch { toastError("Hata", "Üye eklenemedi."); }
    };

    // Move members
    const handleMoveMembers = async () => {
        if (!selectedId || !token || !tenantId || !moveTargetGroup || selectedMembers.size === 0) return;
        try {
            await groupsApi.moveMembers(token, tenantId, selectedId, moveTargetGroup, Array.from(selectedMembers));
            setMoveOpen(false); setSelectedMembers(new Set());
            const d = await groupsApi.get(token, tenantId, selectedId);
            setDetail(d);
            success("Taşındı", `${selectedMembers.size} üye taşındı.`);
            loadGroups();
        } catch { toastError("Hata", "Taşıma başarısız."); }
    };

    // Assign course
    const handleAssignCourse = async () => {
        if (!selectedId || !token || !tenantId || !assignCourseId) return;
        try {
            await groupsApi.assignCourse(token, tenantId, selectedId, assignCourseId, assignMode);
            setAssignCourseOpen(false); setAssignCourseId("");
            const d = await groupsApi.get(token, tenantId, selectedId);
            setDetail(d);
            success("Atandı", "Ders gruba atandı.");
            loadGroups();
        } catch { toastError("Hata", "Ders atanamadı."); }
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
                        onToggle={() => toggle(g.id)} onEdit={() => openEdit(g)} onDelete={() => setDeleteTarget(g.id)} />
                    {isExpanded && children.length > 0 && renderTree(children, depth + 1)}
                </div>
            );
        });

    return (
        <div className="space-y-5">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-[#0A1931] flex items-center gap-2">
                        <FolderTree size={22} className="text-[#1B3B6F]" /> Gruplar
                    </h1>
                    <p className="text-sm text-[#A9A9A9] mt-0.5">Öğrenci gruplarını yönetin</p>
                </div>
                <div className="flex items-center gap-2">
                    <button onClick={loadGroups} className="p-2 rounded-xl bg-white border border-[#E2E8F0] hover:bg-[#E2E8F0]/20 text-[#A9A9A9]"><RefreshCw size={14} /></button>
                    <button onClick={openCreate} className="px-4 py-2 bg-[#0A1931] text-white text-sm font-bold rounded-xl hover:bg-[#1B3B6F] transition-colors flex items-center gap-2 shadow-lg shadow-black/10">
                        <Plus size={15} /> Yeni Grup
                    </button>
                </div>
            </div>

            {/* Stats */}
            <div className="grid grid-cols-4 gap-4">
                {[
                    { label: "Toplam Grup", value: groups.length, icon: FolderTree, color: "text-[#1B3B6F]", bg: "bg-[#E2E8F0]/15" },
                    { label: "Toplam Üye", value: totalMembers, icon: Users, color: "text-blue-600", bg: "bg-blue-50" },
                    { label: "Ders Ataması", value: totalCourses, icon: BookOpen, color: "text-emerald-600", bg: "bg-emerald-50" },
                    { label: "Boş Grup", value: emptyGroups, icon: AlertTriangle, color: emptyGroups > 0 ? "text-amber-600" : "text-emerald-600", bg: emptyGroups > 0 ? "bg-amber-50" : "bg-emerald-50" },
                ].map(s => (
                    <div key={s.label} className="bg-white rounded-2xl border border-[#E2E8F0] p-4 flex items-center gap-3">
                        <div className={`w-10 h-10 rounded-xl ${s.bg} flex items-center justify-center`}>
                            <s.icon size={18} className={s.color} />
                        </div>
                        <div>
                            <p className="text-xl font-bold text-[#0A1931]">{s.value}</p>
                            <p className="text-[10px] font-bold uppercase tracking-widest text-[#A0AEC0]">{s.label}</p>
                        </div>
                    </div>
                ))}
            </div>

            {/* Empty Groups Alert */}
            {emptyGroups > 0 && (
                <div className="flex items-center gap-3 p-3 bg-amber-50 border border-amber-200 rounded-xl">
                    <AlertTriangle size={16} className="text-amber-500 flex-shrink-0" />
                    <p className="text-xs text-amber-700 font-medium">
                        <strong>{emptyGroups} grup</strong> henüz üyesi olmayan boş grup. Ağaçta ⚠️ ile işaretlendi.
                    </p>
                </div>
            )}

            <div className="grid grid-cols-12 gap-5" style={{ height: "calc(100vh - 340px)" }}>
                {/* Tree */}
                <div className="col-span-4 bg-white rounded-2xl border border-[#E2E8F0] flex flex-col overflow-hidden">
                    <div className="p-3 border-b border-[#E2E8F0]">
                        <div className="relative">
                            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#A0AEC0]" />
                            <input value={treeSearch} onChange={e => setTreeSearch(e.target.value)} placeholder="Grup ara..."
                                className="w-full pl-8 pr-3 py-2 text-sm bg-[#E2E8F0]/20 border border-[#E2E8F0] rounded-xl text-[#1B3B6F] placeholder-[#A9A9A9] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20" />
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
                    <div className="p-3 border-t border-[#E2E8F0] text-xs text-[#A0AEC0] text-center">
                        {groups.length} grup • {groups.filter(g => g.parentGroupId !== null).length} alt grup
                    </div>
                </div>

                {/* Detail */}
                <div className="col-span-8 bg-white rounded-2xl border border-[#E2E8F0] flex flex-col overflow-hidden">
                    {!selectedId ? (
                        <div className="flex-1 flex items-center justify-center text-[#A0AEC0]">
                            <div className="text-center">
                                <FolderTree size={48} className="opacity-20 mx-auto mb-3" />
                                <p className="text-lg font-medium">Bir grup seçin</p>
                            </div>
                        </div>
                    ) : detailLoading ? (
                        <div className="flex-1 flex items-center justify-center"><Loader2 size={32} className="text-[#A0AEC0] animate-spin" /></div>
                    ) : detail ? (
                        <>
                            {/* Detail Header */}
                            <div className="p-5 border-b border-[#E2E8F0]">
                                <div className="flex items-center justify-between">
                                    <div className="flex items-center gap-3">
                                        <div className="w-10 h-10 rounded-xl flex items-center justify-center"
                                            style={{ background: `${detail.color ?? "#6366f1"}20` }}>
                                            <FolderTree size={18} style={{ color: detail.color ?? "#6366f1" }} />
                                        </div>
                                        <div>
                                            <h2 className="font-bold text-[#0A1931]">{detail.name}</h2>
                                            <p className="text-xs text-[#A0AEC0]">{detail.description ?? "Açıklama yok"}</p>
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <span className="px-2 py-1 bg-[#E2E8F0]/30 text-[#1B3B6F] text-xs rounded-lg font-bold">{detail.memberCount} üye</span>
                                        <span className="px-2 py-1 bg-[#E2E8F0]/30 text-[#1B3B6F] text-xs rounded-lg font-bold">{detail.courseCount} ders</span>
                                        {detail.educationType && <span className="px-2 py-1 bg-indigo-50 text-indigo-600 text-xs rounded-lg font-bold border border-indigo-200">{detail.educationType}</span>}
                                        {detail.parentGroupName && <span className="px-2 py-1 bg-[#E2E8F0]/30 text-[#A0AEC0] text-xs rounded-lg">↑ {detail.parentGroupName}</span>}
                                        <button onClick={() => openEdit(detail as unknown as GroupListDto)}
                                            className="p-2 hover:bg-[#E2E8F0]/30 rounded-lg text-[#A0AEC0] hover:text-[#1B3B6F]"><Edit3 size={14} /></button>
                                    </div>
                                </div>
                                {/* Tabs */}
                                <div className="flex gap-1 mt-4">
                                    {([["members", "Üyeler", Users], ["courses", "Dersler", BookOpen], ["settings", "Ayarlar", Settings]] as const).map(([key, label, Icon]) => (
                                        <button key={key} onClick={() => setActiveTab(key)}
                                            className={`px-3 py-1.5 text-xs font-bold rounded-lg flex items-center gap-1.5 transition-colors ${activeTab === key ? "bg-[#0A1931] text-white" : "text-[#A9A9A9] hover:bg-[#E2E8F0]/30"}`}>
                                            <Icon size={12} />{label}
                                        </button>
                                    ))}
                                </div>
                            </div>

                            {/* Tab Content */}
                            <div className="flex-1 overflow-y-auto p-5">
                                {/* ── Members Tab ── */}
                                {activeTab === "members" && (
                                    <>
                                        <div className="flex items-center justify-between mb-4">
                                            <p className="text-sm font-bold text-[#1B3B6F]">{detail.members.length} Üye</p>
                                            <div className="flex items-center gap-2">
                                                {selectedMembers.size > 0 && (
                                                    <button onClick={() => setMoveOpen(true)}
                                                        className="px-3 py-1.5 text-xs bg-amber-50 text-amber-700 border border-amber-200 rounded-lg flex items-center gap-1.5 hover:bg-amber-100 font-bold">
                                                        <ArrowRight size={12} /> {selectedMembers.size} Kişiyi Taşı
                                                    </button>
                                                )}
                                                <button onClick={() => { setBulkAddSelection(new Set()); setAddMemberOpen(true); }}
                                                    className="px-3 py-1.5 text-xs bg-[#0A1931] text-white rounded-lg flex items-center gap-1.5 hover:bg-[#1B3B6F] font-bold">
                                                    <UserPlus size={12} /> Üye Ekle
                                                </button>
                                            </div>
                                        </div>
                                        <div className="space-y-1.5">
                                            {detail.members.map(m => (
                                                <div key={m.userId} className={`flex items-center gap-3 p-3 rounded-xl group transition-colors ${selectedMembers.has(m.userId) ? "bg-blue-50 border border-blue-200" : "bg-[#E2E8F0]/15 hover:bg-[#E2E8F0]/30 border border-transparent"}`}>
                                                    <input type="checkbox" checked={selectedMembers.has(m.userId)}
                                                        onChange={() => toggleMember(m.userId)}
                                                        className="w-4 h-4 rounded border-[#E2E8F0] text-[#1B3B6F] focus:ring-[#1B3B6F]/20" />
                                                    <div className="w-8 h-8 rounded-full bg-[#0A1931] text-white flex items-center justify-center text-xs font-bold">
                                                        {m.userFullName.split(" ").map(n => n[0]).join("").slice(0, 2)}
                                                    </div>
                                                    <div className="flex-1 min-w-0">
                                                        <p className="text-sm font-medium text-[#0A1931]">{m.userFullName}</p>
                                                        <p className="text-xs text-[#A0AEC0]">{m.email}</p>
                                                    </div>
                                                    <span className="text-[10px] text-[#A0AEC0] hidden sm:block">
                                                        <Calendar size={10} className="inline mr-1" />
                                                        {new Date(m.addedAt).toLocaleDateString("tr-TR", { day: "numeric", month: "short", year: "numeric" })}
                                                    </span>
                                                    <span className="px-2 py-0.5 bg-white border border-[#E2E8F0] text-[#A9A9A9] text-[10px] rounded-lg font-bold">{m.role}</span>
                                                    <button onClick={() => handleRemoveMember(m.userId)}
                                                        className="p-1.5 text-[#A0AEC0] hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors opacity-0 group-hover:opacity-100">
                                                        <UserMinus size={13} />
                                                    </button>
                                                </div>
                                            ))}
                                            {detail.members.length === 0 && (
                                                <div className="text-center py-10 text-[#A0AEC0]">
                                                    <Users size={32} className="mx-auto opacity-20 mb-2" />
                                                    <p className="text-sm">Henüz üye yok</p>
                                                    <button onClick={() => { setBulkAddSelection(new Set()); setAddMemberOpen(true); }}
                                                        className="mt-3 px-4 py-2 text-xs font-bold text-[#1B3B6F] bg-[#E2E8F0]/30 rounded-lg hover:bg-[#E2E8F0]/50">+ Üye Ekle</button>
                                                </div>
                                            )}
                                        </div>
                                    </>
                                )}

                                {/* ── Courses Tab ── */}
                                {activeTab === "courses" && (
                                    <>
                                        <div className="flex items-center justify-between mb-4">
                                            <p className="text-sm font-bold text-[#1B3B6F]">{detail.courses.length} Ders</p>
                                            <button onClick={() => { setAssignCourseId(""); setAssignCourseOpen(true); }}
                                                className="px-3 py-1.5 text-xs bg-[#0A1931] text-white rounded-lg flex items-center gap-1.5 hover:bg-[#1B3B6F] font-bold">
                                                <Plus size={12} /> Ders Ata
                                            </button>
                                        </div>
                                        <div className="space-y-2">
                                            {detail.courses.map(c => {
                                                const modeLabel = c.mode === "Both" ? "Hibrit" : c.mode;
                                                return (
                                                <div key={c.courseId} className="flex items-center gap-3 p-3 rounded-xl bg-[#E2E8F0]/15 hover:bg-[#E2E8F0]/30 transition-colors group">
                                                    <div className="w-9 h-9 rounded-xl bg-emerald-50 flex items-center justify-center">
                                                        <BookOpen size={15} className="text-emerald-600" />
                                                    </div>
                                                    <div className="flex-1 min-w-0">
                                                        <p className="text-sm font-medium text-[#0A1931] truncate">{c.courseTitle}</p>
                                                        <p className="text-[10px] text-[#A0AEC0]">{modeLabel}</p>
                                                    </div>
                                                    <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${c.mode === "Online" ? "bg-blue-50 text-blue-600 border border-blue-200" : c.mode === "Offline" ? "bg-amber-50 text-amber-600 border border-amber-200" : "bg-emerald-50 text-emerald-600 border border-emerald-200"}`}>
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
                                                    <button onClick={() => { setAssignCourseId(""); setAssignCourseOpen(true); }}
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

            {/* ── Premium Group Form Modal ── */}
            {formOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm" onClick={() => setFormOpen(false)}>
                    <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl overflow-hidden" onClick={e => e.stopPropagation()}>
                        {/* Gradient Header */}
                        <div className="relative overflow-hidden px-6 py-5" style={{ background: `linear-gradient(135deg, ${formColor}15, ${formColor}05)` }}>
                            <div className="absolute -right-8 -top-8 w-32 h-32 rounded-full opacity-[0.08]" style={{ background: formColor }} />
                            <div className="flex items-center justify-between relative z-10">
                                <div className="flex items-center gap-3">
                                    <div className="w-11 h-11 rounded-xl flex items-center justify-center shadow-sm" style={{ background: `${formColor}20` }}>
                                        <FolderTree size={20} style={{ color: formColor }} />
                                    </div>
                                    <div>
                                        <h3 className="text-lg font-bold text-[#0A1931]">{editGroup ? "Grubu Düzenle" : "Yeni Grup Oluştur"}</h3>
                                        <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest">Öğrenci Grubu Yönetimi</p>
                                    </div>
                                </div>
                                <button onClick={() => setFormOpen(false)} className="p-2 rounded-xl bg-white/80 text-[#A0AEC0] hover:text-[#0A1931] shadow-sm"><X size={16} /></button>
                            </div>
                        </div>

                        <div className="grid grid-cols-5 gap-0">
                            {/* Left: Form */}
                            <div className="col-span-3 p-6 space-y-4 border-r border-[#E2E8F0]">
                                {/* Group Type Selector */}
                                <div>
                                    <label className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Eğitim Modeli <span className="text-red-500">*</span></label>
                                    <div className="grid grid-cols-6 gap-1.5">
                                        {[
                                            { emoji: "🎥", label: "Canlı" },
                                            { emoji: "📖", label: "Offline" },
                                            { emoji: "🏕️", label: "Kamp" },
                                            { emoji: "📝", label: "Sınav" },
                                            { emoji: "🔄", label: "Hibrit" },
                                            { emoji: "🎯", label: "Demo" },
                                        ].map(t => (
                                            <button key={t.label} type="button" onClick={() => setFormType(t.label)}
                                                className={`flex flex-col items-center gap-1 py-2 rounded-xl border transition-all text-center ${formType === t.label ? "border-[#1B3B6F] bg-[#0A1931]/5 ring-2 ring-[#1B3B6F]/20" : "border-[#E2E8F0] hover:border-[#A0AEC0] hover:bg-[#E2E8F0]/15"}`}>
                                                <span className="text-lg">{t.emoji}</span>
                                                <span className={`text-[9px] font-bold ${formType === t.label ? "text-[#0A1931]" : "text-[#1B3B6F]"}`}>{t.label}</span>
                                            </button>
                                        ))}
                                    </div>
                                </div>

                                {/* Group Name */}
                                <div>
                                    <label className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest block mb-1.5">Grup Adı <span className="text-red-500">*</span></label>
                                    <input value={formName} onChange={e => setFormName(e.target.value)} placeholder="Örn: TYT-A Sınıfı, Matematik Çalışma Grubu"
                                        className="w-full px-4 py-2.5 text-sm font-medium border border-[#E2E8F0] rounded-xl text-[#0A1931] bg-[#E2E8F0]/10 focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:bg-white placeholder:text-[#A0AEC0]" />
                                </div>

                                {/* Description */}
                                <div>
                                    <label className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest block mb-1.5">Açıklama</label>
                                    <textarea value={formDesc} onChange={e => setFormDesc(e.target.value)} rows={2} placeholder="Grup hakkında kısa bir açıklama..."
                                        className="w-full px-4 py-2.5 text-sm border border-[#E2E8F0] rounded-xl text-[#0A1931] bg-[#E2E8F0]/10 focus:outline-none focus:ring-2 focus:ring-[#1B3B6F]/20 focus:bg-white resize-none placeholder:text-[#A0AEC0]" />
                                </div>

                                {/* Parent Group */}
                                <div>
                                    <label className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest block mb-1.5">Üst Grup</label>
                                    <select value={formParent} onChange={e => setFormParent(e.target.value)}
                                        className="w-full px-4 py-2.5 text-sm border border-[#E2E8F0] rounded-xl text-[#0A1931] bg-[#E2E8F0]/10 focus:outline-none">
                                        <option value="">— Kök Grup (Bağımsız) —</option>
                                        {groups.filter(g => !editGroup || g.id !== editGroup.id).map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
                                    </select>
                                </div>

                                {/* Color Picker */}
                                <div>
                                    <label className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest block mb-2">Grup Rengi</label>
                                    <div className="flex items-center gap-3">
                                        <div className="flex gap-1.5">
                                            {COLOR_PRESETS.map(c => (
                                                <div key={c} onClick={() => setFormColor(c)}
                                                    className={`w-7 h-7 rounded-full cursor-pointer hover:scale-110 transition-all ${formColor === c ? "ring-2 ring-offset-2 ring-[#0A1931] scale-110" : "opacity-70 hover:opacity-100"}`}
                                                    style={{ background: c }} />
                                            ))}
                                        </div>
                                        <div className="flex items-center gap-1.5 ml-2">
                                            <div className="w-7 h-7 rounded-full border-2 border-dashed border-[#E2E8F0]" style={{ background: formColor }} />
                                            <input value={formColor} onChange={e => setFormColor(e.target.value)} maxLength={7}
                                                className="w-20 px-2 py-1 text-xs font-mono border border-[#E2E8F0] rounded-lg text-[#1B3B6F] bg-[#E2E8F0]/10 focus:outline-none" />
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Right: Live Preview */}
                            <div className="col-span-2 p-6 bg-[#F8FAFC] flex flex-col">
                                <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest mb-3">Canlı Önizleme</p>
                                <div className="bg-white rounded-2xl border border-[#E2E8F0] p-4 shadow-sm flex-1">
                                    <div className="flex items-start gap-3 mb-3">
                                        <div className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0"
                                            style={{ background: `${formColor}20` }}>
                                            <FolderTree size={18} style={{ color: formColor }} />
                                        </div>
                                        <div className="min-w-0">
                                            <p className="font-bold text-[#0A1931] text-sm truncate">{formName || "Grup Adı"}</p>
                                            <p className="text-[10px] text-[#A0AEC0] mt-0.5 line-clamp-2">{formDesc || "Açıklama..."}</p>
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-2 mt-3 pt-3 border-t border-[#E2E8F0]">
                                        <div className="w-3 h-3 rounded-full" style={{ background: formColor }} />
                                        <span className="text-[10px] text-[#A0AEC0]">0 üye</span>
                                        <span className="text-[10px] text-[#A0AEC0]">·</span>
                                        <span className="text-[10px] text-[#A0AEC0]">0 ders</span>
                                    </div>
                                    {formParent && (
                                        <div className="mt-2 text-[9px] text-[#A0AEC0]">
                                            ↑ {groups.find(g => g.id === formParent)?.name ?? "Üst Grup"}
                                        </div>
                                    )}
                                </div>

                                {/* Quick Tips */}
                                <div className="mt-4 space-y-2">
                                    <p className="text-[10px] font-bold text-[#A0AEC0] uppercase tracking-widest">İpuçları</p>
                                    <div className="text-xs text-[#718096] space-y-2 leading-relaxed">
                                        <p className="flex items-start gap-1.5">💡 Grup oluşturduktan sonra üye ve ders ekleyebilirsiniz</p>
                                        <p className="flex items-start gap-1.5">🎨 Renk, ağaç görünümünde grubu hızlı tanımanızı sağlar</p>
                                        <p className="flex items-start gap-1.5">📂 Alt gruplarla hiyerarşik yapı oluşturabilirsiniz</p>
                                        <p className="flex items-start gap-1.5">👥 Üst gruba eklenen öğrenciler alt gruplara da otomatik eklenir</p>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Footer */}
                        <div className="flex items-center justify-between px-6 py-4 border-t border-[#E2E8F0] bg-[#E2E8F0]/10">
                            <p className="text-[10px] text-[#A0AEC0]">{!formType ? <span className="text-red-400">Eğitim modeli seçin</span> : !formName.trim() ? "Grup adı gerekli" : "✓ Hazır"}</p>
                            <div className="flex gap-2">
                                <button onClick={() => setFormOpen(false)} className="px-5 py-2.5 text-sm font-bold text-[#A9A9A9] hover:text-[#0A1931]">İptal</button>
                                <button onClick={handleSave} disabled={!formName.trim() || !formType || formSaving}
                                    className="px-6 py-2.5 text-sm font-bold bg-[#0A1931] text-white rounded-xl hover:bg-[#1B3B6F] disabled:opacity-40 shadow-lg shadow-black/10 flex items-center justify-center gap-2">
                                    {formSaving && <Loader2 size={14} className="animate-spin" />}
                                    {editGroup ? "💾 Kaydet" : "✨ Oluştur"}
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
                            <h3 className="text-base font-bold text-[#0A1931]">Üye Ekle</h3>
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

            {/* ── Move Members Modal ── */}
            {moveOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/30 backdrop-blur-sm">
                    <div className="bg-white rounded-2xl shadow-2xl border border-[#E2E8F0] w-full max-w-sm p-6">
                        <div className="flex items-center justify-between mb-4">
                            <h3 className="text-base font-bold text-[#0A1931]">{selectedMembers.size} Üyeyi Taşı</h3>
                            <button onClick={() => setMoveOpen(false)}><X size={16} className="text-[#A0AEC0]" /></button>
                        </div>
                        <p className="text-xs text-[#A0AEC0] mb-3">Seçili üyeler bu gruptan çıkarılıp hedef gruba taşınacak.</p>
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
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/30 backdrop-blur-sm">
                    <div className="bg-white rounded-2xl shadow-2xl border border-[#E2E8F0] w-full max-w-sm p-6">
                        <div className="flex items-center justify-between mb-4">
                            <h3 className="text-base font-bold text-[#0A1931]">Ders Ata</h3>
                            <button onClick={() => setAssignCourseOpen(false)}><X size={16} className="text-[#A0AEC0]" /></button>
                        </div>
                        <div className="space-y-3">
                            <div>
                                <label className="text-xs font-bold text-[#1B3B6F] mb-1.5 block">Ders Seçin</label>
                                <select value={assignCourseId} onChange={e => setAssignCourseId(e.target.value)}
                                    className="w-full px-3 py-2.5 text-sm border border-[#E2E8F0] rounded-xl text-[#1B3B6F] focus:outline-none">
                                    <option value="">— Ders seçin —</option>
                                    {allCourses.filter(c => !detail?.courses.some(dc => dc.courseId === c.id)).map(c => (
                                        <option key={c.id} value={c.id}>{c.title}</option>
                                    ))}
                                </select>
                            </div>
                            <div className="flex gap-2 mt-5">
                                <button onClick={() => setAssignCourseOpen(false)} className="flex-1 py-2.5 text-sm font-bold text-[#A9A9A9] border border-[#E2E8F0] rounded-xl">İptal</button>
                                <button onClick={handleAssignCourse} disabled={!assignCourseId}
                                    className="flex-1 py-2.5 text-sm font-bold bg-[#0A1931] text-white rounded-xl hover:bg-[#1B3B6F] disabled:opacity-40">
                                    Ata
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
        </div>
    );
}
