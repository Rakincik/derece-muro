import { api, cachedApi, invalidateCache, invalidateCacheByPrefix, API_URL, PagedResult } from './core';
import { CourseListDto, SessionDto, CourseDetailDto, CourseMaterialDto, AuthResponse, UserDto, UserTenantDto, ExamListDto, ExamDetailDto, ExamAssignmentDto, ExamResultDto, ExamResultSummaryDto, ExamOverallSummaryDto, ScoreRangeDto, AssignmentListDto, StudentScorecardDto, CourseAttendanceDto, DashboardStatsDto, DeviceSessionDto, ScorecardSummaryDto, NotificationDto, AdminSentNotificationDto, GroupSummaryDto, SessionStartResult, RecordingDto, PlanDto, TransactionDto, MonthlyRevenueDto, PlanRevenueDto, AccountingSummaryDto, PaymentMethodBreakdownDto, CreateTransactionRequest, PodcastDto, GeneratePodcastRequest, GroupListDto, GroupMemberDto, GroupDetailDto, CalendarEventDto, CreateCalendarEventRequest, TicketDto, TicketReplyDto, AdminDashboardDto, PackageGroupDto, PackageDto, UserPackageDto, CreatePackageRequest, WebhookInfo, PagedUsersResult, CreateUserRequest, QuestionDto, CreateQuestionRequest, AuditLogDto, PagedAuditResult, AuditSummaryDto, TenantBrandingDto, SubmissionDto, AssignmentDetailDto } from './types';

export const analyticsApi = {
    dashboard: (token: string, tenantId: string, params?: { from?: string; to?: string }) => {
        const qs = new URLSearchParams();
        if (params?.from) qs.set('from', params.from);
        if (params?.to) qs.set('to', params.to);
        return api<AdminDashboardDto>(`/analytics/admin-dashboard?${qs}`, { token, tenantId });
    },
    export: (token: string, tenantId: string, format: 'pdf' | 'excel', from?: string, to?: string) => {
        const qs = new URLSearchParams({ format });
        if (from) qs.set('from', from);
        if (to) qs.set('to', to);
        return api<Blob>(`/analytics/export?${qs}`, { token, tenantId });
    },
};

// ── Upload API ────────────────────────────────────────────────────────────────
