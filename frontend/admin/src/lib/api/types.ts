export interface CourseListDto {
    id: string;
    title: string;
    description: string | null;
    thumbnailUrl: string | null;
    courseType: string;
    isPublished: boolean;
    sessionCount: number;
    groupCount: number;
    order: number;
    createdAt: string;
    instructorId?: string | null;
    instructorName?: string | null;
}

export interface SessionDto {
    id: string;
    title: string;
    description: string | null;
    order: number;
    videoUrl: string | null;
    durationMinutes: number | null;
    isFree: boolean;
    scheduledStart: string | null;
    scheduledEnd: string | null;
    recordingEnabled: boolean;
    status: string; // Scheduled | Live | Ended
    bbbMeetingId: string | null;
    createdAt: string;
}

export interface CourseDetailDto {
    id: string;
    title: string;
    description: string | null;
    thumbnailUrl: string | null;
    courseType: string;
    isPublished: boolean;
    order: number;
    createdAt: string;
    sessions: SessionDto[];
    groups: { groupId: string; groupName: string; mode: string }[];
    instructorId?: string | null;
    instructorName?: string | null;
}

export interface CourseMaterialDto {
    id: string; title: string; fileName: string; filePath: string;
    contentType: string; fileSize: number; createdAt: string;
}

export interface AuthResponse {
    token: string;
    refreshToken: string;
    expiresAt: string;
    user: UserDto;
}

export interface UserDto {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    username: string;
    phone: string | null;
    role: string;
    studentType: string | null;
    demoExpiresAt: string | null;
    isActive: boolean;
    createdAt: string;
    tenants: UserTenantDto[];
    lastLoginAt: string | null;
    groupNames: string[];
    password?: string;
}

export interface UserTenantDto {
    tenantId: string;
    tenantName: string;
    tenantCode: string;
    role: string;
    status: string;
    features?: string | null;
}

export interface ExamListDto {
    id: string;
    title: string;
    description: string | null;
    examType: string;
    questionCount: number;
    optionCount: number;
    durationMinutes: number | null;
    status: string;
    startDate: string | null;
    endDate: string | null;
    assignmentCount: number;
    resultCount: number;
    averageScore: number | null;
    createdAt: string;
    maxScore: number;
    virtualParticipantCount: number;
    assignments?: ExamAssignmentDto[];
}

export interface ExamDetailDto extends ExamListDto {
    showResults: boolean;
    pdfUrl: string | null;
    solutionPdfUrl: string | null;
    answerKey: Record<number, string> | null;
    assignments: ExamAssignmentDto[];
    resultSummary: ExamResultSummaryDto | null;
    digitalQuestionsJson?: string | null;
    sectionsJson?: string | null;
}

export interface ExamAssignmentDto {
    id: string;
    targetType: string;
    targetId: string;
    targetName: string;
    startsAt: string | null;
    endsAt: string | null;
    assignedAt: string;
}

export interface ExamResultDto {
    id: string;
    userId: string;
    userFullName: string;
    correctCount: number;
    wrongCount: number;
    emptyCount: number;
    net: number;
    score: number;
    submittedAt: string;
    startedAt?: string;
    durationSeconds?: number;
    sectionResults?: Record<string, SectionResultDto>;
    answers?: Record<number, string>;
}

export interface SectionResultDto {
    name: string;
    correctCount: number;
    wrongCount: number;
    emptyCount: number;
    net: number;
}

export interface ExamResultSummaryDto {
    totalParticipants: number;
    averageScore: number;
    averageNet: number;
    highestScore: number;
    lowestScore: number;
    results: ExamResultDto[];
    scoreDistribution?: { range: string; count: number }[];
    answerKey?: Record<number, string>;
}

export interface ExamOverallSummaryDto {
    examWithResultsCount: number;
    totalParticipants: number;
    overallAverageScore: number;
    examTypeCount: number;
}

export interface ScoreRangeDto {
    range: string; count: number;
}

export interface AssignmentListDto {
    id: string;
    title: string;
    description?: string | null;
    courseId: string;
    courseName?: string;
    dueDate: string;
    maxScore: number;
    fileUrl?: string | null;
    submissionCount: number;
    gradedCount: number;
    averageScore: number | null;
    createdAt?: string;
}

export interface StudentScorecardDto {
    userId: string;
    fullName: string;
    email: string;
    attendedSessions: number;
    totalSessions: number;
    attendanceRate: number;
    completedVideos: number;
    totalVideos: number;
    videoCompletionRate: number;
    totalWatchedMinutes: number;
    submittedAssignments: number;
    avgExamScore: number;
}

export interface CourseAttendanceDto {
    courseId: string;
    courseTitle: string;
    totalSessions: number;
    totalEnrolled: number;
    avgAttendanceRate: number;
    sessions: {
        sessionId: string;
        sessionTitle: string;
        scheduledStart: string | null;
        totalAttendees: number;
        attendanceRate: number;
        presentStudentIds: string[];
    }[];
}

export interface DashboardStatsDto {
    totalUsers: number;
    activeStudents: number;
    demoStudents: number;
    totalCourses: number;
    publishedCourses: number;
    totalExams: number;
    totalAssignments: number;
    totalGroups: number;
    pendingTickets: number;
}

export interface DeviceSessionDto {
    id: string;
    userId: string;
    userFullName: string;
    deviceInfo: string;
    ipAddress: string | null;
    loginAt: string;
    logoutAt: string | null;
    isActive: boolean;
}

export interface ScorecardSummaryDto {
    totalStudents: number;
    avgAttendanceRate: number;
    avgVideoCompletionRate: number;
    avgExamScore: number;
    avgAttendedSessions: number;
    avgTotalSessions: number;
    avgCompletedVideos: number;
    avgTotalVideos: number;
    avgTotalWatchedMinutes: number;
    avgSubmittedAssignments: number;
}

export interface StudentExamHistoryDto {
    examId: string;
    title: string;
    score: number;
    net: number;
    takenAt: string;
}

export interface StudentAssignmentHistoryDto {
    assignmentId: string;
    title: string;
    status: string;
    grade: number | null;
    submittedAt: string;
}

export interface StudentAcademicHistoryDto {
    exams: StudentExamHistoryDto[];
    assignments: StudentAssignmentHistoryDto[];
}

export interface NotificationDto {
    id: string;
    title: string;
    body: string;
    type: string | null;
    isRead: boolean;
    channel: string;
    createdAt: string;
}

export interface AdminSentNotificationDto {
    id: string;
    title: string;
    body: string;
    type: string | null;
    createdAt: string;
    recipientCount: number;
}

export interface GroupSummaryDto {
    id: string;
    name: string;
    memberCount: number;
}

export interface SessionStartResult {
    sessionId: string;
    bbbMeetingId: string;
    moderatorJoinUrl: string;
    status: string;
}

export interface RecordingDto {
    id: string;
    sessionId: string;
    sessionTitle: string;
    courseTitle: string;
    scheduledStart: string | null;
    status: string;
    playbackUrl: string | null;  // BBB presentation player URL
    hlsPath: string | null;      // /hls/{assetId}/master.m3u8
    thumbnailPath: string | null;
    durationSeconds: number | null;
    createdAt: string;
    mediaAssetId?: string;
}

export interface PlanDto {
    id: string;
    name: string;
    description: string | null;
    price: number;
    currency: string;
    billingCycle: string;
    maxStudents: number | null;
    isActive: boolean;
    transactionCount: number;
}

export interface TransactionDto {
    id: string;
    type: string;           // sale | refund | expense
    amount: number;
    description: string | null;
    status: string;         // pending | paid | failed | refunded
    paymentMethod: string | null;
    invoiceNo: string | null;
    userId: string | null;
    userName: string | null;
    planId: string | null;
    planName: string | null;
    transactionDate: string;
    createdAt: string;
}

export interface MonthlyRevenueDto {
    year: number; month: number; monthLabel: string;
    revenue: number; refunds: number; expenses: number;
}

export interface PlanRevenueDto {
    planName: string; revenue: number; saleCount: number; color: string;
}

export interface AccountingSummaryDto {
    totalRevenue: number;
    totalRefunds: number;
    totalExpenses: number;
    netRevenue: number;
    totalTransactions: number;
    paidCount: number;
    pendingCount: number;
    failedCount: number;
    pendingTotal: number;
    last3MonthAvgRevenue: number;
    monthlyRevenue: MonthlyRevenueDto[];
    planBreakdown: PlanRevenueDto[];
    paymentMethodBreakdown: PaymentMethodBreakdownDto[];
}

export interface PaymentMethodBreakdownDto {
    method: string; label: string; count: number; amount: number; percentage: number;
}

export interface CreateTransactionRequest {
    type: string; amount: number; description?: string; status: string;
    paymentMethod?: string; invoiceNo?: string;
    userId?: string; planId?: string; transactionDate?: string;
    userName?: string;
}

export interface PodcastDto {
    id: string;
    courseId: string | null;
    courseTitle: string | null;
    title: string;
    textContent: string | null;
    generatedScript: string | null;
    audioFilePath: string | null;
    durationSeconds: number | null;
    status: "Processing" | "Ready" | "Failed";
    createdAt: string;
}

export interface GeneratePodcastRequest {
    title: string;
    courseId?: string;
    rawText: string;
    voice: string;
}

export interface GroupListDto {
    id: string;
    name: string;
    description: string | null;
    parentGroupId: string | null;
    parentGroupName: string | null;
    color: string | null;
    educationType: string | null;
    memberCount: number;
    courseCount: number;
    expirationDate: string | null;
    createdAt: string;
}

export interface GroupMemberDto {
    userId: string;
    userFullName: string;
    email: string;
    role: string;
    addedAt: string;
}

export interface GroupDetailDto extends GroupListDto {
    members: GroupMemberDto[];
    courses: { courseId: string; courseTitle: string; mode: string }[];
    children: GroupListDto[];
    educationType: string | null;
}

export interface CalendarEventDto {
    id: string;
    title: string;
    description: string | null;
    eventType: string;
    startDate: string;
    endDate: string;
    groupId: string | null;
    groupName: string | null;
    courseId: string | null;
    courseName: string | null;
    color: string | null;
    createdAt: string;
}

export interface CreateCalendarEventRequest {
    title: string;
    description?: string;
    eventType: string;
    startDate: string;
    endDate: string;
    groupId?: string;
    courseId?: string;
    color?: string;
}

export interface TicketDto {
    id: string;
    userId: string;
    userFullName: string;
    subject: string;
    body: string;
    category: string;   // Teknik | İçerik | Ödeme | Diğer
    priority: string;   // Düşük | Normal | Yüksek | Acil
    status: string;     // Açık | Yanıtlandı | Çözüldü
    createdAt: string;
    messages: TicketReplyDto[];
}

export interface TicketReplyDto {
    id: string;
    senderId: string;
    senderName: string;
    body: string;
    isAdmin?: boolean;
    createdAt: string;
}

export interface AdminDashboardDto {
    totalStudents: number;
    activeStudents: number;
    totalCourses: number;
    totalVideosWatched: number;
    totalExams: number;
    avgExamScore: number;
    totalRevenue: number;
    weeklyActivity: { day: string; sessions: number; videoMinutes: number }[];
    topCourses: { courseId: string; title: string; studentCount: number; avgAttendance: number }[];
    topStudents: { userId: string; fullName: string; score: number; rank: number }[];
    examScoreDistribution: { range: string; count: number }[];
    hourlyActivity: { hour: number; count: number }[];
}

export interface PackageGroupDto { id: string; groupId: string; groupName: string; contentMode: string; }
export interface PackageDto {
    id: string; tenantId: string; name: string; description?: string;
    price: number; durationDays: number; isActive: boolean; createdAt: string;
    groups: PackageGroupDto[]; activeUserCount: number;
}

export interface UserPackageDto {
    id: string; userId: string; packageId: string; packageName: string;
    orderId?: string; activatedAt: string; expiresAt?: string;
    status: 'active' | 'expired' | 'cancelled'; source: string;
}

export interface CreatePackageRequest {
    name: string; description?: string; price: number; durationDays: number;
    groups: { groupId: string; contentMode: string }[];
}

export interface WebhookInfo {
    purchaseUrl: string; cancelUrl: string; secretHint: string;
    signatureHeader: string; signatureAlgo: string;
}

export interface PagedUsersResult {
    items: UserDto[]; totalCount: number; page: number; pageSize: number; totalPages: number;
}

export interface CreateUserRequest {
    firstName: string; lastName: string; email: string; username?: string;
    password: string; role: string; studentType?: string; phone?: string;
}

export interface QuestionDto {
    id: string; userId: string; userFullName: string;
    instructorId: string; instructorFullName: string;
    subject: string; body: string; imageUrl: string | null; audioUrl: string | null;
    courseId: string | null; courseTitle: string | null;
    answer: string | null; answeredAt: string | null;
    status: string; createdAt: string; note: string | null;
}

export interface CreateQuestionRequest {
    instructorId: string; subject: string; body: string;
    imageUrl?: string; audioUrl?: string; note?: string; courseId?: string;
}

export interface AuditLogDto {
    id: string; userId: string | null; userName: string | null;
    action: string; entityType: string; entityId: string | null;
    entityName: string | null; details: string | null;
    ipAddress: string | null; createdAt: string;
}

export interface PagedAuditResult {
    items: AuditLogDto[]; totalCount: number; page: number; pageSize: number; totalPages: number;
}

export interface AuditSummaryDto {
    totalCount: number;
    createCount: number;
    updateCount: number;
    deleteCount: number;
    nightActivityCount: number;
    topEntities: Record<string, number>;
}

export interface UserAuditSummaryDto {
    userId: string | null;
    userName: string | null;
    actionCount: number;
    lastActionAt: string;
}

export interface SuspiciousUserDto {
    userId: string | null;
    userName: string | null;
    alertType: string;
    eventCount: number;
    lastEventAt: string;
}

export interface TenantBrandingDto {
    name: string;
    logoUrl: string | null;
    faviconUrl: string | null;
    primaryColor: string | null;
    accentColor: string | null;
    footerText: string | null;
}

export interface SubmissionDto {
    id: string;
    studentId: string;
    userFullName?: string;
    submittedAt: string;
    fileUrl?: string;
    comment?: string;
    score: number | null;
    feedback?: string;
}

export interface AssignmentDetailDto extends AssignmentListDto {
    submissions: SubmissionDto[];
}

// --- Media Library Types ---
export interface MediaAssetDto {
    id: string;
    title: string;
    filePath?: string;
    hlsPath?: string;
    thumbnailPath?: string;
    durationSeconds?: number;
    status: string;
    courseId?: string;
    courseName?: string;
    folderId?: string;
    tags?: string;
    createdAt: string;
}

export interface MediaFolderDto {
    id: string;
    name: string;
    parentFolderId?: string;
    createdAt: string;
    subFolderCount: number;
    mediaAssetCount: number;
}

export interface CourseMediaDto {
    id: string;
    courseId: string;
    mediaAssetId?: string;
    orderIndex: number;
    mediaAsset?: MediaAssetDto;
    examId?: string;
    examTitle?: string;
    sessionId?: string;
    sessionTitle?: string;
    type: string;
}

export interface CourseStudentListDto {
    userId: string;
    firstName: string;
    lastName: string;
    email: string;
    assignedAt: string;
    expiresAt?: string | null;
}
