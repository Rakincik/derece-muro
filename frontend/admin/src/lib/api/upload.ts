import { api, cachedApi, invalidateCache, invalidateCacheByPrefix, API_URL, PagedResult } from './core';
import { CourseListDto, SessionDto, CourseDetailDto, CourseMaterialDto, AuthResponse, UserDto, UserTenantDto, ExamListDto, ExamDetailDto, ExamAssignmentDto, ExamResultDto, ExamResultSummaryDto, ExamOverallSummaryDto, ScoreRangeDto, AssignmentListDto, StudentScorecardDto, CourseAttendanceDto, DashboardStatsDto, DeviceSessionDto, ScorecardSummaryDto, NotificationDto, AdminSentNotificationDto, GroupSummaryDto, SessionStartResult, RecordingDto, PlanDto, TransactionDto, MonthlyRevenueDto, PlanRevenueDto, AccountingSummaryDto, PaymentMethodBreakdownDto, CreateTransactionRequest, PodcastDto, GeneratePodcastRequest, GroupListDto, GroupMemberDto, GroupDetailDto, CalendarEventDto, CreateCalendarEventRequest, TicketDto, TicketReplyDto, AdminDashboardDto, PackageGroupDto, PackageDto, UserPackageDto, CreatePackageRequest, WebhookInfo, PagedUsersResult, CreateUserRequest, QuestionDto, CreateQuestionRequest, AuditLogDto, PagedAuditResult, AuditSummaryDto, TenantBrandingDto, SubmissionDto, AssignmentDetailDto } from './types';

export const uploadApi = {
    /** PDF veya dosya yüklemek için presigned URL al */
    getPresignedUrl: (token: string, tenantId: string, fileName: string, contentType: string) =>
        api<{ uploadUrl: string; publicUrl: string }>('/upload/presigned', {
            method: 'POST', token, tenantId,
            body: JSON.stringify({ fileName, contentType }),
        }),
    
    /** XMLHttpRequest ile ilerleme barını destekleyen direkt yükleme metodu */
    uploadMediaWithProgress: (uploadUrl: string, file: File, onProgress: (progress: number, etaSeconds?: number) => void): Promise<void> => {
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            xhr.open('PUT', uploadUrl, true);
            
            const startTime = Date.now();
            let lastProgressTime = 0;

            xhr.upload.onprogress = (event) => {
                if (event.lengthComputable) {
                    const percentComplete = Math.round((event.loaded / event.total) * 100);
                    
                    const now = Date.now();
                    // Sadece 100ms'de bir veya %100 olduğunda UI'ı güncelle (Hem akıcı bir bar hem de CPU optimizasyonu için)
                    if (now - lastProgressTime >= 100 || percentComplete === 100) {
                        lastProgressTime = now;
                        
                        let etaSeconds = 0;
                        const elapsedMs = now - startTime;
                        if (elapsedMs > 500 && event.loaded > 0) {
                            const bytesPerSec = event.loaded / (elapsedMs / 1000);
                            const remainingBytes = event.total - event.loaded;
                            etaSeconds = Math.max(0, Math.round(remainingBytes / bytesPerSec));
                        }
                        
                        onProgress(percentComplete, etaSeconds);
                    }
                }
            };
            
            xhr.onload = () => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    resolve();
                } else {
                    reject(new Error(`Upload failed with status ${xhr.status}`));
                }
            };
            
            xhr.onerror = () => reject(new Error("Network error during upload"));
            
            xhr.setRequestHeader("Content-Type", file.type);
            xhr.send(file);
        });
    }
};

// ── Package API ───────────────────────────────────────────────────────────────

