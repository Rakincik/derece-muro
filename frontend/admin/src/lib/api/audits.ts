import { api, cachedApi, invalidateCache, invalidateCacheByPrefix, API_URL, PagedResult } from './core';
import { CourseListDto, SessionDto, CourseDetailDto, CourseMaterialDto, AuthResponse, UserDto, UserTenantDto, ExamListDto, ExamDetailDto, ExamAssignmentDto, ExamResultDto, ExamResultSummaryDto, ExamOverallSummaryDto, ScoreRangeDto, AssignmentListDto, StudentScorecardDto, CourseAttendanceDto, DashboardStatsDto, DeviceSessionDto, ScorecardSummaryDto, NotificationDto, AdminSentNotificationDto, GroupSummaryDto, SessionStartResult, RecordingDto, PlanDto, TransactionDto, MonthlyRevenueDto, PlanRevenueDto, AccountingSummaryDto, PaymentMethodBreakdownDto, CreateTransactionRequest, PodcastDto, GeneratePodcastRequest, GroupListDto, GroupMemberDto, GroupDetailDto, CalendarEventDto, CreateCalendarEventRequest, TicketDto, TicketReplyDto, AdminDashboardDto, PackageGroupDto, PackageDto, UserPackageDto, CreatePackageRequest, WebhookInfo, PagedUsersResult, CreateUserRequest, QuestionDto, CreateQuestionRequest, AuditLogDto, PagedAuditResult, AuditSummaryDto, TenantBrandingDto, SubmissionDto, AssignmentDetailDto, UserAuditSummaryDto, SuspiciousUserDto } from './types';

export const securityApi = {
    getEvents: (token: string, tenantId: string, params?: {
        page?: number; pageSize?: number; eventType?: string;
        userId?: string; from?: string; to?: string;
    }) => {
        const qs = new URLSearchParams();
        if (params?.page) qs.set('page', String(params.page));
        if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
        if (params?.eventType) qs.set('eventType', params.eventType);
        if (params?.userId) qs.set('userId', params.userId);
        if (params?.from) qs.set('from', params.from);
        if (params?.to) qs.set('to', params.to);
        return api<any>("/security/events?" + qs.toString(), { token, tenantId });
    }
};

export const auditApi = {
    getLogs: (token: string, tenantId: string, params?: {
        page?: number; pageSize?: number; action?: string;
        entityType?: string; search?: string; from?: string; to?: string;
    }) => {
        const qs = new URLSearchParams();
        if (params?.page) qs.set('page', String(params.page));
        if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
        if (params?.action) qs.set('action', params.action);
        if (params?.entityType) qs.set('entityType', params.entityType);
        if (params?.search) qs.set('search', params.search);
        if (params?.from) qs.set('from', params.from);
        if (params?.to) qs.set('to', params.to);
        return api<PagedAuditResult>(`/audit?${qs.toString()}`, { token, tenantId });
    },

    summary: (token: string, tenantId: string, params?: { from?: string; to?: string }) => {
        const qs = new URLSearchParams();
        if (params?.from) qs.set('from', params.from);
        if (params?.to) qs.set('to', params.to);
        return api<AuditSummaryDto>(`/audit/summary?${qs.toString()}`, { token, tenantId });
    },

    getUserAudits: (token: string, tenantId: string, params?: {
        page?: number; pageSize?: number; search?: string; sortBy?: string;
    }) => {
        const qs = new URLSearchParams();
        if (params?.page) qs.set('page', String(params.page));
        if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
        if (params?.search) qs.set('search', params.search);
        if (params?.sortBy) qs.set('sortBy', params.sortBy);
        return api<PagedResult<UserAuditSummaryDto>>(`/audit/users?${qs.toString()}`, { token, tenantId });
    },

    getSuspiciousUsers: (token: string, tenantId: string) => {
        return api<SuspiciousUserDto[]>(`/audit/suspicious`, { token, tenantId });
    }
};
