import { api, cachedApi, invalidateCache, invalidateCacheByPrefix, API_URL, PagedResult } from './core';
import { CourseListDto, SessionDto, CourseDetailDto, CourseMaterialDto, AuthResponse, UserDto, UserTenantDto, ExamListDto, ExamDetailDto, ExamAssignmentDto, ExamResultDto, ExamResultSummaryDto, ExamOverallSummaryDto, ScoreRangeDto, AssignmentListDto, StudentScorecardDto, CourseAttendanceDto, DashboardStatsDto, DeviceSessionDto, ScorecardSummaryDto, NotificationDto, AdminSentNotificationDto, GroupSummaryDto, SessionStartResult, RecordingDto, PlanDto, TransactionDto, MonthlyRevenueDto, PlanRevenueDto, AccountingSummaryDto, PaymentMethodBreakdownDto, CreateTransactionRequest, PodcastDto, GeneratePodcastRequest, GroupListDto, GroupMemberDto, GroupDetailDto, CalendarEventDto, CreateCalendarEventRequest, TicketDto, TicketReplyDto, AdminDashboardDto, PackageGroupDto, PackageDto, UserPackageDto, CreatePackageRequest, WebhookInfo, PagedUsersResult, CreateUserRequest, QuestionDto, CreateQuestionRequest, AuditLogDto, PagedAuditResult, AuditSummaryDto, TenantBrandingDto, SubmissionDto, AssignmentDetailDto, CourseStudentListDto } from './types';

export const courseApi = {
    list: (token: string, tenantId: string, params?: { page?: number; pageSize?: number; search?: string; instructorId?: string }) => {
        const qs = new URLSearchParams();
        if (params?.page) qs.set("page", String(params.page));
        if (params?.pageSize) qs.set("pageSize", String(params.pageSize));
        if (params?.search) qs.set("search", params.search);
        if (params?.instructorId) qs.set("instructorId", params.instructorId);
        return api<PagedResult<CourseListDto>>(`/courses?${qs}`, { token, tenantId });
    },

    get: (token: string, tenantId: string, courseId: string) =>
        api<CourseDetailDto>(`/courses/${courseId}`, { token, tenantId }),

    create: async (token: string, tenantId: string, data: { title: string; description?: string; courseType?: string; order?: number; instructorId?: string }) => {
        const result = await api<CourseListDto>("/courses", { method: "POST", token, tenantId, body: JSON.stringify(data) });
        invalidateCache("courses:");
        return result;
    },

    uploadCover: async (token: string, tenantId: string, courseId: string, file: File): Promise<{ url: string }> => {
        const formData = new FormData();
        formData.append("file", file);
        const response = await fetch(`${API_URL}/courses/${courseId}/cover`, {
            method: "POST",
            headers: { "Authorization": `Bearer ${token}`, "X-Tenant-Id": tenantId },
            body: formData,
        });
        if (!response.ok) throw new Error("Kapak görseli yüklenemedi");
        return response.json();
    },

    createSession: (token: string, tenantId: string, courseId: string, data: {
        title: string; description?: string; scheduledStart?: string; scheduledEnd?: string;
        durationMinutes?: number; recordingEnabled?: boolean; order?: number; isFree?: boolean;
    }) =>
        api<SessionDto>(`/courses/${courseId}/sessions`, {
            method: "POST", token, tenantId, body: JSON.stringify({ ...data, isFree: data.isFree ?? false, recordingEnabled: data.recordingEnabled ?? true }),
        }),

    deleteSession: async (token: string, tenantId: string, courseId: string, sessionId: string) => {
        const result = await api<void>(`/courses/${courseId}/sessions/${sessionId}`, { method: "DELETE", token, tenantId });
        invalidateCache("courses:");
        return result;
    },

    startSession: (token: string, tenantId: string, courseId: string, sessionId: string) =>
        api<{ sessionId: string; meetingId: string; joinUrl: string; status: string }>(
            `/courses/${courseId}/sessions/${sessionId}/start`,
            { method: "POST", token, tenantId }
        ),

    endSession: (token: string, tenantId: string, courseId: string, sessionId: string) =>
        api<{ message: string }>(
            `/courses/${courseId}/sessions/${sessionId}/end`,
            { method: "POST", token, tenantId }
        ),

    joinSession: (token: string, tenantId: string, courseId: string, sessionId: string) =>
        api<{ sessionId: string; joinUrl: string; isModerator: boolean }>(
            `/courses/${courseId}/sessions/${sessionId}/join`,
            { method: "POST", token, tenantId }
        ),

    update: async (token: string, tenantId: string, courseId: string, data: {
        title?: string; description?: string; thumbnailUrl?: string; courseType?: string; isPublished?: boolean; order?: number; instructorId?: string | null; instructorIds?: string[];
    }) => {
        const result = await api<CourseListDto>(`/courses/${courseId}`, { method: "PUT", token, tenantId, body: JSON.stringify(data) });
        invalidateCache("courses:");
        return result;
    },

    delete: async (token: string, tenantId: string, courseId: string) => {
        const result = await api<void>(`/courses/${courseId}`, { method: "DELETE", token, tenantId });
        invalidateCache("courses:");
        return result;
    },

    updateSession: (token: string, tenantId: string, courseId: string, sessionId: string, data: {
        title?: string; description?: string; scheduledStart?: string; scheduledEnd?: string;
        durationMinutes?: number; recordingEnabled?: boolean; order?: number; isFree?: boolean;
        videoUrl?: string;
    }) =>
        api<SessionDto>(`/courses/${courseId}/sessions/${sessionId}`, { method: "PUT", token, tenantId, body: JSON.stringify(data) }),

    // Materials
    listMaterials: (token: string, tenantId: string, courseId: string) =>
        api<CourseMaterialDto[]>(`/courses/${courseId}/materials`, { token, tenantId }),

    uploadMaterial: async (token: string, tenantId: string, courseId: string, file: File, title?: string): Promise<CourseMaterialDto> => {
        const formData = new FormData();
        formData.append("file", file);
        if (title) formData.append("title", title);
        const res = await fetch(`${API_URL}/courses/${courseId}/materials`, {
            method: "POST",
            headers: { Authorization: `Bearer ${token}`, "X-Tenant-Id": tenantId },
            body: formData,
        });
        if (!res.ok) throw new Error(await res.text());
        return res.json();
    },

    deleteMaterial: (token: string, tenantId: string, courseId: string, materialId: string) =>
        api<void>(`/courses/${courseId}/materials/${materialId}`, { method: "DELETE", token, tenantId }),
    addVodToCourse: (token: string, tenantId: string, courseId: string, title: string, filePath: string, durationSeconds?: number) =>
        api<SessionDto>(`/courses/${courseId}/vod`, {
            method: "POST", token, tenantId, body: JSON.stringify({ title, filePath, durationSeconds })
        }),

    // Direct Students
    getStudents: (token: string, tenantId: string, courseId: string) =>
        api<CourseStudentListDto[]>(`/courses/${courseId}/students`, { token, tenantId }),
        
    updateStudentExpiration: (token: string, tenantId: string, courseId: string, userId: string, expiresAt: string | null) =>
        api(`/courses/${courseId}/students/${userId}/expires-at`, { token, tenantId, method: 'PUT', body: JSON.stringify({ expiresAt }) }),

    assignStudent: (token: string, tenantId: string, courseId: string, userId: string) =>
        api<{ message: string }>(`/courses/${courseId}/students`, {
            method: "POST", token, tenantId, body: JSON.stringify({ userId })
        }),
    
    removeStudent: (token: string, tenantId: string, courseId: string, userId: string) =>
        api<void>(`/courses/${courseId}/students/${userId}`, {
            method: "DELETE", token, tenantId
        })
};








// ── Exam Types ──













// ── Exam API ──

