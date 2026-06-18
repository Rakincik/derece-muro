const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5292/api/v1";

interface FetchOptions extends RequestInit {
    token?: string;
    tenantId?: string;
}

export async function api<T = unknown>(endpoint: string, options: FetchOptions = {}): Promise<T> {
    const { token, tenantId, headers: customHeaders, ...rest } = options;
    const headers: Record<string, string> = {
        "Content-Type": "application/json",
        ...(customHeaders as Record<string, string>),
    };
    if (token) headers["Authorization"] = `Bearer ${token}`;
    if (tenantId) headers["X-Tenant-Id"] = tenantId;

    const fetchOptions: RequestInit = {
        ...rest,
        headers,
        cache: "no-store", // Ensure we always get fresh data
    };
    
    const response = await fetch(`${API_URL}${endpoint}`, fetchOptions);
    if (!response.ok) {
        const body = await response.json().catch(() => ({}));
        // SESSION_KICKED: başka cihazdan giriş yapıldı — global event at
        if (response.status === 401 && body?.error === "SESSION_KICKED") {
            if (typeof window !== "undefined")
                window.dispatchEvent(new CustomEvent("session:kicked", { detail: body.message }));
        }
        throw new ApiError(body.error || `HTTP ${response.status}`, response.status);
    }
    if (response.status === 204) return undefined as T;
    return response.json();
}

export class ApiError extends Error {
    constructor(message: string, public statusCode: number) {
        super(message);
        this.name = "ApiError";
    }
}

// ── SWR Fetcher ──────────────────────────────────────────────────────────────
export const swrFetcher = async ([endpoint, token, tenantId]: [string, string?, string?]) => {
    return api(endpoint, { token, tenantId });
};

// ── API Cache Layer ──────────────────────────────────────────────────────────
const _cache = new Map<string, { data: unknown; expiry: number }>();
const _inflight = new Map<string, Promise<unknown>>();

/**
 * Cached API wrapper — deduplicates in-flight requests and caches results.
 * @param key   Unique cache key (e.g. "courses-list")
 * @param fetcher  The actual API call
 * @param ttlMs    Cache TTL in ms (default 5 min)
 */
export async function cachedApi<T>(key: string, fetcher: () => Promise<T>, ttlMs = 300_000): Promise<T> {
    // Return from cache if fresh
    const cached = _cache.get(key);
    if (cached && cached.expiry > Date.now()) return cached.data as T;

    // Deduplicate in-flight requests
    const existing = _inflight.get(key);
    if (existing) return existing as Promise<T>;

    const promise = fetcher().then(data => {
        _cache.set(key, { data, expiry: Date.now() + ttlMs });
        _inflight.delete(key);
        return data;
    }).catch(err => {
        _inflight.delete(key);
        throw err;
    });

    _inflight.set(key, promise);
    return promise;
}

/** Invalidate specific cache keys (supports prefix matching) */
export function invalidateCache(prefix: string) {
    for (const key of _cache.keys()) {
        if (key.startsWith(prefix)) _cache.delete(key);
    }
}

/** Clear entire cache */
export function clearCache() { _cache.clear(); }

// ── Types ────────────────────────────────────────────────────────────────────
export interface AuthResponse { token: string; refreshToken: string; expiresAt: string; user: UserDto; }
export interface UserDto { id: string; firstName: string; lastName: string; email: string; phone?: string; role: string; studentType?: string; demoExpiresAt?: string; isActive: boolean; createdAt: string; tenants: UserTenantDto[]; }
export interface UserTenantDto { tenantId: string; tenantName: string; tenantCode: string; role: string; status: string; }

export interface CourseDto { id: string; title: string; description: string | null; thumbnailUrl: string | null; isPublished: boolean; sessionCount: number; completionPercentage?: number; instructorId?: string | null; instructorName?: string | null; }
export interface SessionDto { id: string; title: string; description: string | null; order: number; videoUrl: string | null; durationMinutes: number | null; scheduledStart: string | null; scheduledEnd: string | null; bbbMeetingId: string | null; status: string; recordingEnabled: boolean; }
export interface UpcomingSessionDto extends SessionDto { courseId: string; courseTitle: string; }
export interface CourseMaterialDto { id: string; title: string; fileName: string; filePath: string; contentType: string; fileSize: number; createdAt: string; }

export interface VideoProgressDto { mediaAssetId: string; watchedSeconds: number; totalSeconds: number; lastPosition: number; completionPercentage: number; skipCount: number; replayCount: number; completedAt: string | null; }
export interface VideoNoteDto { id: string; mediaAssetId: string; timestampSeconds: number; timestampFormatted: string; text: string; createdAt: string; }

export interface StudentDashboardDto {
    totalWatchedMinutes: number;
    attendedSessionsThisMonth: number;
    totalSessionsThisMonth: number;
    attendanceRate: number;
    completedVideos: number;
    consecutiveDays: number;
    continueWatching: ResumeVideoDto[];
    weeklyActivity: WeeklyActivityDto[];
}
export interface WeeklyActivityDto { dayLabel: string; minutes: number; isToday: boolean; }
export interface ResumeVideoDto { mediaAssetId: string; title: string; thumbnailPath: string | null; lastPosition: number; totalSeconds: number; completionPercentage: number; }
export interface StudentDashboardSummaryDto {
    stats: StudentDashboardDto;
    courses: CourseDto[];
    upcomingSessions: UpcomingSessionDto[];
    unreadNotificationCount: number;
}

export interface MyAttendanceDto { sessionId: string; sessionTitle: string; courseTitle: string; joinedAt: string; leftAt: string | null; durationMinutes: number | null; }
export interface NotificationDto { id: string; title: string; body: string; type: string; isRead: boolean; createdAt: string; }

export interface MyAssignmentDto {
    id: string;
    title: string;
    description: string | null;
    courseName: string;
    dueDate: string;
    maxScore: number;
    fileUrl: string | null;
    submissionId: string | null;
    submissionFileUrl: string | null;
    submissionComment: string | null;
    score: number | null;
    feedback: string | null;
    submittedAt: string | null;
    status: "pending" | "submitted" | "graded" | "overdue";
}

export interface MyExamResultDto {
    examId: string;
    examTitle: string;
    examType: string;
    questionCount: number;
    correctCount: number;
    wrongCount: number;
    emptyCount: number;
    net: number;
    score: number;
    averageScore: number | null;
    rank: number | null;
    submittedAt: string;
    showResults: boolean;
    sectionResults?: Record<string, SectionResultDto>;
}

export interface SectionResultDto {
    name: string;
    correctCount: number;
    wrongCount: number;
    emptyCount: number;
    net: number;
}

export interface VideoNoteWithCourse extends VideoNoteDto {
    mediaAssetTitle?: string;
}

export interface QuestionDto {
    id: string;
    userId: string;
    userFullName: string;
    instructorId: string;
    instructorFullName: string;
    subject: string;
    body: string;
    imageUrl: string | null;
    audioUrl: string | null;
    courseId: string | null;
    courseTitle: string | null;
    answer: string | null;
    answeredAt: string | null;
    status: string; // "Pending" | "Answered"
    createdAt: string;
    note: string | null;
}

export interface ExamDetailDto {
    id: string;
    title: string;
    description: string | null;
    examType: string;
    questionCount: number;
    optionCount: number;
    durationMinutes: number | null;
    status: string;
    showResults: boolean;
    pdfUrl: string | null;
    solutionPdfUrl: string | null;
    answerKey: Record<number, string> | null;
    startDate: string | null;
    endDate: string | null;
    wrongPenaltyWeight: number;
    sectionsJson?: string;
    maxScore?: number;
    baseScore?: number;
    digitalQuestionsJson?: string;
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
    wrongPenaltyWeight: number;
    maxScore?: number;
    baseScore?: number;
}

// ── API functions ─────────────────────────────────────────────────────────────
export const getFileUrl = (path: string | null | undefined) => {
    if (!path) return "";
    
    let url = path;
    if (!path.startsWith("http") && !path.startsWith("blob:") && !path.startsWith("data:")) {
        let base = API_URL.replace('/api/v1', '');
        let cleanPath = path.startsWith('/') ? path : `/${path}`;
        if (cleanPath.startsWith('/uploads')) {
            cleanPath = `/api/v1${cleanPath}`;
        }
        url = `${base}${cleanPath}`;
    }
    
    // Ağ üzerinden (192.168.x.x vb) erişildiğinde localhost asset'lerinin kırılmasını önle
    if (typeof window !== 'undefined' && url.includes('localhost') && window.location.hostname !== 'localhost') {
        url = url.replace('localhost', window.location.hostname);
    }
    
    return url;
};

export const getDownloadUrl = (path: string | null | undefined, fileName?: string) => {
    if (!path) return "";
    let cleanPath = path.startsWith('/') ? path : `/${path}`;
    // Sadece /uploads içindeki dosyaları indirilebilir yapalım
    if (!cleanPath.startsWith('/uploads')) return getFileUrl(path);

    const encodedPath = btoa(cleanPath);
    let base = API_URL.replace('/api/v1', '');
    let url = `${base}/api/v1/files/download?path=${encodeURIComponent(encodedPath)}`;
    
    if (fileName) {
        url += `&name=${encodeURIComponent(fileName)}`;
    }
    
    // Ağ üzerinden (192.168.x.x vb) erişildiğinde localhost asset'lerinin kırılmasını önle
    if (typeof window !== 'undefined' && url.includes('localhost') && window.location.hostname !== 'localhost') {
        url = url.replace('localhost', window.location.hostname);
    }
    return url;
};

export const authApi = {
    login: (email: string, password: string) =>
        api<AuthResponse>("/auth/login", { method: "POST", body: JSON.stringify({ email, password }) }),
    me: (token: string) => api<UserDto>("/auth/me", { token }),
    refresh: (refreshToken: string) =>
        api<AuthResponse>("/auth/refresh", { method: "POST", body: JSON.stringify({ refreshToken }) }),
};

export function getVideoPlaybackDetails(url: string) {
    if (!url) return { url: "", type: "video" as const };
    
    // YouTube
    const ytRegex = /(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^"&?\/\s]{11})/;
    const ytMatch = url.match(ytRegex);
    if (ytMatch && ytMatch[1]) {
        return { url: `https://www.youtube.com/embed/${ytMatch[1]}`, type: "iframe" as const };
    }
    
    // Vimeo
    const vimeoRegex = /vimeo\.com\/(?:channels\/(?:\w+\/)?|groups\/(?:[^\/]*)\/videos\/|album\/(?:\d+)\/video\/|video\/|)(\d+)(?:$|\/|\?)/;
    const vimeoMatch = url.match(vimeoRegex);
    if (vimeoMatch && vimeoMatch[1]) {
        return { url: `https://player.vimeo.com/video/${vimeoMatch[1]}`, type: "iframe" as const };
    }
    
    // If it is already an iframe embed URL
    if (url.includes("youtube.com/embed/") || url.includes("player.vimeo.com/video/")) {
        return { url, type: "iframe" as const };
    }
    
    // Standard video
    const isHls = url.includes(".m3u8") || url.includes("/hls/");
    return { url, type: (isHls ? "video" : "iframe") as const };
}

export interface RecordingDto {
    id: string;
    sessionId: string;
    sessionTitle: string;
    courseTitle: string;
    playbackUrl: string | null;
    hlsPath: string | null;
    thumbnailPath: string | null;
    durationSeconds: number | null;
    status: string;
    scheduledStart: string | null;
    createdAt: string;
    type?: string;
    examId?: string;
    videoUrl?: string | null;
}

export interface CourseMediaDto {
    id: string;
    courseId: string;
    type: string;
    orderIndex: number;
    mediaAssetId?: string | null;
    mediaAsset?: MediaAssetDto | null;
    examId?: string | null;
    examTitle?: string | null;
    examType?: string | null;
    sessionId?: string | null;
    sessionTitle?: string | null;
    sessionStatus?: string | null;
    sessionScheduledStart?: string | null;
    createdAt: string;
}

export const courseApi = {
    getCourseMedias: async (token: string, tenantId: string, courseId: string): Promise<CourseMediaDto[]> => {
        return api<CourseMediaDto[]>(`/courses/${courseId}/media`, { token, tenantId });
    },
    // Fix #9: PagedResult<CourseDto> döndürüyor, Array değil
    list: (token: string, tenantId: string): Promise<CourseDto[]> =>
        cachedApi(`courses:${tenantId}`, async () => {
            const data = await api<{ items: CourseDto[] } | unknown>("/courses?pageSize=200", { token, tenantId });
            if (data && typeof data === "object" && "items" in (data as object))
                return (data as { items: CourseDto[] }).items ?? [];
            if (Array.isArray(data)) return data;
            return [];
        }),
    getSessions: async (token: string, tenantId: string, courseId: string): Promise<SessionDto[]> => {
        try {
            const data = await api<{ sessions?: SessionDto[] } | unknown>(`/courses/${courseId}`, { token, tenantId });
            if (data && typeof data === "object" && "sessions" in (data as object)) {
                return (data as { sessions: SessionDto[] }).sessions ?? [];
            }
            return [];
        } catch {
            return [];
        }
    },
    // Fix #2b: N+1 sorunu düzeltmesi — tek endpoint
    getUpcomingSessions: (token: string, tenantId: string): Promise<UpcomingSessionDto[]> =>
        cachedApi(`upcoming:${tenantId}`, async () => {
            const data = await api<UpcomingSessionDto[] | unknown>("/sessions/upcoming", { token, tenantId });
            return Array.isArray(data) ? data : [];
        }, 30_000), // 30s — needs freshness for live sessions
    joinSession: (token: string, tenantId: string, courseId: string, sessionId: string) =>
        api<{ sessionId: string; joinUrl: string; isModerator: boolean }>(
            `/courses/${courseId}/sessions/${sessionId}/join`,
            { method: "POST", token, tenantId }
        ),
    getMaterials: (token: string, tenantId: string, courseId: string) =>
        cachedApi(`courses-materials-${courseId}`, () => api<CourseMaterialDto[]>(`/courses/${courseId}/materials`, { token, tenantId }), 300_000),
};

export const sessionRecordingApi = {
    list: (token: string, tenantId: string): Promise<RecordingDto[]> =>
        cachedApi(`recordings:${tenantId}`, async () => {
            const data = await api<RecordingDto[] | unknown>("/recordings", { token, tenantId });
            return Array.isArray(data) ? data : [];
        }, 120_000), // 2 min
};


export const videoApi = {
    getProgress: (token: string, tenantId: string, mediaAssetId: string) =>
        api<VideoProgressDto>(`/videos/${mediaAssetId}/progress`, { token, tenantId }),
    updateProgress: (token: string, tenantId: string, mediaAssetId: string, data: { watchedSeconds: number; totalSeconds: number; lastPosition: number; skipCount?: number; replayCount?: number; markCompleted?: boolean }) =>
        api<VideoProgressDto>(`/videos/${mediaAssetId}/progress`, { method: "PUT", token, tenantId, body: JSON.stringify(data) }),
    getNotes: (token: string, tenantId: string, mediaAssetId: string) =>
        api<VideoNoteDto[]>(`/videos/${mediaAssetId}/notes`, { token, tenantId }),
    addNote: (token: string, tenantId: string, mediaAssetId: string, timestampSeconds: number, text: string) =>
        api<VideoNoteDto>(`/videos/${mediaAssetId}/notes`, { method: "POST", token, tenantId, body: JSON.stringify({ timestampSeconds, text }) }),
    deleteNote: (token: string, tenantId: string, noteId: string) =>
        api(`/videos/notes/${noteId}`, { method: "DELETE", token, tenantId }),
};

export const analyticsApi = {
    studentDashboard: (token: string, tenantId: string) =>
        cachedApi(`dashboard:${tenantId}`, () =>
            api<StudentDashboardDto>("/analytics/me/dashboard", { token, tenantId })
        , 120_000), // 2 min cache
    dashboardSummary: (token: string, tenantId: string) =>
        api<StudentDashboardSummaryDto>("/student/dashboard-summary", { token, tenantId })
};

export const attendanceApi = {
    myHistory: async (token: string, tenantId: string): Promise<MyAttendanceDto[]> => {
        const data = await api<MyAttendanceDto[] | unknown>("/attendance/my", { token, tenantId });
        return Array.isArray(data) ? data : [];
    },
};

export const notificationApi = {
    list: async (token: string, tenantId: string, page = 1, pageSize = 50): Promise<PagedResult<NotificationDto> | NotificationDto[]> =>
        api<PagedResult<NotificationDto>>(`/notifications?page=${page}&pageSize=${pageSize}`, { token, tenantId }),
    unreadCount: (token: string, tenantId: string) =>
        api<number>(`/notifications/unread-count`, { token, tenantId }),
    markRead: (token: string, tenantId: string, id: string) =>
        api(`/notifications/${id}/read`, { method: "PUT", token, tenantId }),
    markAllRead: (token: string, tenantId: string) =>
        api(`/notifications/read-all`, { method: "PUT", token, tenantId }),
    registerDeviceToken: (token: string, pushToken: string) =>
        api(`/device-sessions/push-token`, { method: "PUT", token, body: JSON.stringify({ token: pushToken }) }),
};

export const assignmentApi = {
    myAssignments: async (token: string, tenantId: string): Promise<MyAssignmentDto[]> => {
        const data = await api<MyAssignmentDto[] | unknown>("/assignments/my", { token, tenantId });
        return Array.isArray(data) ? data : [];
    },
    submit: (token: string, tenantId: string, assignmentId: string, data: { fileUrl?: string; comment?: string }) =>
        api(`/assignments/${assignmentId}/submit`, { method: "POST", token, tenantId, body: JSON.stringify(data) }),
};

export const examApi = {
    myResults: async (token: string, tenantId: string): Promise<MyExamResultDto[]> => {
        const data = await api<MyExamResultDto[] | unknown>("/exams/my-results", { token, tenantId });
        return Array.isArray(data) ? data : [];
    },
    list: async (token: string, tenantId: string, status?: string): Promise<ExamListDto[]> => {
        const q = status ? `?status=${status}` : "";
        const data = await api<{ items: ExamListDto[] } | ExamListDto[] | unknown>(`/exams${q}`, { token, tenantId });
        if (Array.isArray(data)) return data;
        if (data && typeof data === "object" && "items" in (data as object)) return (data as { items: ExamListDto[] }).items ?? [];
        return [];
    },
    getById: (token: string, tenantId: string, examId: string) =>
        api<ExamDetailDto>(`/exams/${examId}`, { token, tenantId }),
    getDigitalQuestions: async (token: string, tenantId: string, examId: string): Promise<string> => {
        const data = await api<string | unknown>(`/exams/${examId}/digital-questions`, { token, tenantId });
        return typeof data === "string" ? data : JSON.stringify(data);
    },
    saveDraft: (token: string, tenantId: string, examId: string, answers: Record<number, string>) =>
        api(`/exams/${examId}/draft`, {
            method: "POST", token, tenantId,
            body: JSON.stringify(answers),
        }),
    getDraft: async (token: string, tenantId: string, examId: string): Promise<Record<number, string>> => {
        const data = await api<Record<number, string> | unknown>(`/exams/${examId}/draft`, { token, tenantId });
        return data && typeof data === "object" && !Array.isArray(data) ? (data as Record<number, string>) : {};
    },
    submitAnswers: (token: string, tenantId: string, examId: string, answers: Record<number, string>, startedAt?: string) =>
        api<MyExamResultDto>(`/exams/${examId}/submit`, {
            method: "POST", token, tenantId,
            body: JSON.stringify({ answers, startedAt }),
        }),
};

export const questionApi = {
    list: async (token: string, tenantId: string): Promise<QuestionDto[]> => {
        const data = await api<{ items: QuestionDto[] } | QuestionDto[] | unknown>("/questions", { token, tenantId });
        if (Array.isArray(data)) return data;
        if (data && typeof data === "object" && "items" in (data as object)) return (data as { items: QuestionDto[] }).items ?? [];
        return [];
    },
    ask: (token: string, tenantId: string, request: {
        instructorId: string;
        subject: string;
        body: string;
        imageUrl?: string;
        audioUrl?: string;
        note?: string;
        courseId?: string;
    }) =>
        api<QuestionDto>("/questions", {
            method: "POST", token, tenantId,
            body: JSON.stringify(request),
        }),
    getById: (token: string, tenantId: string, id: string) =>
        api<QuestionDto>(`/questions/${id}`, { token, tenantId }),
    updateNote: (token: string, tenantId: string, id: string, note: string | null) =>
        api<QuestionDto>(`/questions/${id}/note`, {
            method: "PATCH", token, tenantId,
            body: JSON.stringify({ note }),
        }),
    deleteQuestion: (token: string, tenantId: string, id: string) =>
        api<void>(`/questions/${id}`, { method: 'DELETE', token, tenantId }),
};

export const userApi = {
    listInstructors: async (token: string, tenantId: string): Promise<UserDto[]> => {
        const data = await api<UserDto[] | unknown>(`/users?role=Instructor`, { token, tenantId });
        return Array.isArray(data) ? data : [];
    },
};

export const uploadApi = {
    getPresignedUrl: (token: string, tenantId: string, fileName: string, contentType: string) =>
        api<{ uploadUrl: string; publicUrl: string }>('/upload/presigned', {
            method: 'POST', token, tenantId,
            body: JSON.stringify({ fileName, contentType }),
        }),
};

// ── Podcast ───────────────────────────────────────────────────────────────────

export interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number; totalPages: number; }

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

// Fix #3: Duplike const kaldırıldı — artık API_URL kullanılıyor
export const podcastApi = {
    list: async (token: string, tenantId: string): Promise<PodcastDto[]> => {
        const data = await api<PagedResult<PodcastDto> | unknown>("/podcasts?pageSize=50", { token, tenantId });
        if (data && typeof data === "object" && "items" in (data as object))
            return (data as PagedResult<PodcastDto>).items ?? [];
        return [];
    },
    audioUrl: (id: string) => `${API_URL}/podcasts/${id}/audio`,
};

// Fix #2b: calendarApi artık N+1 HTTP isteği yapmıyor — tek endpoint
export const calendarApi = {
    upcoming: (token: string, tenantId: string): Promise<UpcomingSessionDto[]> =>
        courseApi.getUpcomingSessions(token, tenantId),
};

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
    courseTitle: string | null;
    color: string | null;
}

export const studentCalendarApi = {
    getEvents: async (token: string, tenantId: string, from?: string, to?: string): Promise<CalendarEventDto[]> => {
        const queryParams = [];
        if (from) queryParams.push(`from=${encodeURIComponent(from)}`);
        if (to) queryParams.push(`to=${encodeURIComponent(to)}`);
        const qs = queryParams.length ? `?${queryParams.join('&')}` : '';
        const data = await api<CalendarEventDto[] | unknown>(`/student/calendar${qs}`, { token, tenantId });
        return Array.isArray(data) ? data : [];
    }
};

// ── Media Asset (Videos) ─────────────────────────────────────────────────────────

export interface MediaAssetDto {
    id: string;
    title: string;
    description: string | null;
    sessionId: string | null;
    sessionTitle: string | null;
    courseId: string | null;
    courseTitle: string | null;
    hlsPath: string | null;
    thumbnailPath: string | null;
    durationSeconds: number | null;
    status: string; // Ready | Processing | Failed
    createdAt: string;
    progress?: VideoProgressDto | null; // izleme durumu
}

export const mediaApi = {
    /** Öğrencinin erişebildiği tüm video asset listesi */
    list: async (token: string, tenantId: string, params?: { courseId?: string; search?: string; page?: number; pageSize?: number }): Promise<MediaAssetDto[]> => {
        const qs = new URLSearchParams();
        if (params?.courseId) qs.set('courseId', params.courseId);
        if (params?.search) qs.set('search', params.search);
        if (params?.page) qs.set('page', String(params.page));
        if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
        const data = await api<{ items: MediaAssetDto[] } | MediaAssetDto[] | unknown>(`/media?${qs}`, { token, tenantId });
        if (Array.isArray(data)) return data;
        if (data && typeof data === 'object' && 'items' in (data as object)) return (data as { items: MediaAssetDto[] }).items ?? [];
        return [];
    },
    get: (token: string, tenantId: string, id: string) =>
        api<MediaAssetDto>(`/media/${id}`, { token, tenantId }),
    /** HLS stream URL'ini oluştur */
    hlsUrl: (hlsPath: string) => `${API_URL.replace('/api/v1', '')}${hlsPath}`,
};

// ── Support (Müşteri hizmetleri) ────────────────────────────────────────

export interface StudentTicketDto {
    id: string;
    subject: string;
    message: string;
    category: string;
    priority: string;
    status: string;
    createdAt: string;
    replies: { id: string; authorName: string; message: string; isAdmin: boolean; createdAt: string }[];
}

export const studentSupportApi = {
    list: async (token: string, tenantId: string): Promise<StudentTicketDto[]> => {
        const data = await api<{ items: StudentTicketDto[] } | StudentTicketDto[] | unknown>('/support?pageSize=50', { token, tenantId });
        if (Array.isArray(data)) return data;
        if (data && typeof data === 'object' && 'items' in (data as object)) return (data as { items: StudentTicketDto[] }).items ?? [];
        return [];
    },
    create: (token: string, tenantId: string, data: { subject: string; message: string; category: string; priority?: string }) =>
        api<StudentTicketDto>('/support/tickets', { method: 'POST', token, tenantId, body: JSON.stringify(data) }),
    reply: (token: string, tenantId: string, ticketId: string, message: string) =>
        api('/support/' + ticketId + '/replies', { method: 'POST', token, tenantId, body: JSON.stringify({ message }) }),
};

// ── Tenant Branding API ──────────────────────────────────────────────────────

export interface TenantBrandingDto {
    name: string;
    logoUrl: string | null;
    faviconUrl: string | null;
    primaryColor: string | null;
    accentColor: string | null;
    footerText: string | null;
}

export const tenantApi = {
    getBranding: (tenantId?: string) =>
        api<TenantBrandingDto>('/tenant/branding', { tenantId: tenantId ?? '' }),
};

