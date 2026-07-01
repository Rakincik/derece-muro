import { api, cachedApi, invalidateCache, invalidateCacheByPrefix, API_URL, PagedResult } from './core';
import { CourseListDto, SessionDto, CourseDetailDto, CourseMaterialDto, AuthResponse, UserDto, UserTenantDto, ExamListDto, ExamDetailDto, ExamAssignmentDto, ExamResultDto, ExamResultSummaryDto, ExamOverallSummaryDto, ScoreRangeDto, AssignmentListDto, StudentScorecardDto, StudentAcademicHistoryDto, CourseAttendanceDto, DashboardStatsDto, DeviceSessionDto, ScorecardSummaryDto, NotificationDto, AdminSentNotificationDto, GroupSummaryDto, SessionStartResult, RecordingDto, PlanDto, TransactionDto, MonthlyRevenueDto, PlanRevenueDto, AccountingSummaryDto, PaymentMethodBreakdownDto, CreateTransactionRequest, PodcastDto, GeneratePodcastRequest, GroupListDto, GroupMemberDto, GroupDetailDto, CalendarEventDto, CreateCalendarEventRequest, TicketDto, TicketReplyDto, AdminDashboardDto, PackageGroupDto, PackageDto, UserPackageDto, CreatePackageRequest, WebhookInfo, PagedUsersResult, CreateUserRequest, QuestionDto, CreateQuestionRequest, AuditLogDto, PagedAuditResult, AuditSummaryDto, TenantBrandingDto, SubmissionDto, AssignmentDetailDto } from './types';

export const analyticsAdminApi = {
    /** Admin: Dashboard istatistikleri (hızlı birim sayımları) */
    stats: (token: string, tenantId: string) =>
        cachedApi(`admin-stats:${tenantId}`, () => api<DashboardStatsDto>(`/analytics/dashboard`, { token, tenantId }), 120_000),

    /** Admin: Aktif cihaz oturumları */
    activeSessions: async (token: string, tenantId: string): Promise<DeviceSessionDto[]> => {
        const data = await api<DeviceSessionDto[] | unknown>(`/analytics/sessions`, { token, tenantId });
        return Array.isArray(data) ? data : [];
    },

    /** Admin: Geçmiş oturumlar */
    recentSessions: async (token: string, tenantId: string, days = 7): Promise<DeviceSessionDto[]> => {
        const data = await api<DeviceSessionDto[] | unknown>(`/analytics/sessions/recent?days=${days}`, { token, tenantId });
        return Array.isArray(data) ? data : [];
    },

    studentScorecard: (token: string, tenantId: string, userId: string) =>
        api<StudentScorecardDto>(`/analytics/students/${userId}/scorecard`, { token, tenantId }),

    studentScorecardsList: (token: string, tenantId: string) =>
        api<StudentScorecardDto[]>(`/analytics/students/scorecards`, { token, tenantId }),

    studentAcademicHistory: (token: string, tenantId: string, userId: string) =>
        api<StudentAcademicHistoryDto>(`/analytics/students/${userId}/academic-history`, { token, tenantId }),

    courseAttendance: (token: string, tenantId: string, courseId: string) =>
        api<CourseAttendanceDto>(`/analytics/courses/${courseId}/attendance`, { token, tenantId }),

    scorecardSummary: (token: string, tenantId: string) =>
        api<ScorecardSummaryDto>(`/analytics/students/summary`, { token, tenantId }),
};
