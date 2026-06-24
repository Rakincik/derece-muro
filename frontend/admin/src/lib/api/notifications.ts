import { api, cachedApi, invalidateCache, invalidateCacheByPrefix, API_URL, PagedResult } from './core';
import { CourseListDto, SessionDto, CourseDetailDto, CourseMaterialDto, AuthResponse, UserDto, UserTenantDto, ExamListDto, ExamDetailDto, ExamAssignmentDto, ExamResultDto, ExamResultSummaryDto, ExamOverallSummaryDto, ScoreRangeDto, AssignmentListDto, StudentScorecardDto, CourseAttendanceDto, DashboardStatsDto, DeviceSessionDto, ScorecardSummaryDto, NotificationDto, AdminSentNotificationDto, GroupSummaryDto, SessionStartResult, RecordingDto, PlanDto, TransactionDto, MonthlyRevenueDto, PlanRevenueDto, AccountingSummaryDto, PaymentMethodBreakdownDto, CreateTransactionRequest, PodcastDto, GeneratePodcastRequest, GroupListDto, GroupMemberDto, GroupDetailDto, CalendarEventDto, CreateCalendarEventRequest, TicketDto, TicketReplyDto, AdminDashboardDto, PackageGroupDto, PackageDto, UserPackageDto, CreatePackageRequest, WebhookInfo, PagedUsersResult, CreateUserRequest, QuestionDto, CreateQuestionRequest, AuditLogDto, PagedAuditResult, AuditSummaryDto, TenantBrandingDto, SubmissionDto, AssignmentDetailDto } from './types';

export const notificationApi = {
    /** Admin: sent notification history */
    adminSent: (token: string, tenantId: string) =>
        api<AdminSentNotificationDto[]>(`/notifications/admin/sent`, { token, tenantId }),

    /** Hem admin hem student: bildirim listesi (PagedResult veya dizi) */
    list: (token: string, tenantId: string, page = 1, pageSize = 20) =>
        api<PagedResult<NotificationDto>>(`/notifications?page=${page}&pageSize=${pageSize}`, { token, tenantId }),

    /** Okunmamış bildirim sayısı */
    unreadCount: (token: string, tenantId: string) =>
        api<number>(`/notifications/unread-count`, { token, tenantId }),

    /** Bulk send: groupId veya sendToAll ile de çalışır */
    bulkSend: (
        token: string, tenantId: string,
        userIds: string[], title: string, body: string, type: string,
        scheduledAt?: string, groupId?: string, sendToAll?: boolean, courseId?: string
    ) =>
        api<number>(`/notifications/bulk`, {
            method: "POST", token, tenantId,
            body: JSON.stringify({ userIds, title, body, type, scheduledAt, groupId, sendToAll, courseId }),
        }),

    /** List groups for targeting */
    groups: async (token: string, tenantId: string): Promise<GroupSummaryDto[]> => {
        const res = await api<any>(`/groups?pageSize=1000`, { token, tenantId });
        return Array.isArray(res) ? res : res?.items ?? [];
    },

    /** All users for targeting */
    allUsers: async (token: string, tenantId: string): Promise<UserDto[]> => {
        const res = await api<PagedUsersResult | UserDto[]>(`/users?pageSize=10000`, { token, tenantId });
        return Array.isArray(res) ? res : (res as any).items ?? [];
    },

    /** Tek bildirimi okundu işaretle */
    markRead: (token: string, tenantId: string, id: string) =>
        api<void>(`/notifications/${id}/read`, { method: "PUT", token, tenantId }),

    /** Tüm bildirimleri okundu işaretle */
    markAllRead: (token: string, tenantId: string) =>
        api<void>(`/notifications/read-all`, { method: "PUT", token, tenantId }),
};

// SessionDto — see courseApi section above (lines ~127-141)
// SessionStartResult is unique to sessionApi



