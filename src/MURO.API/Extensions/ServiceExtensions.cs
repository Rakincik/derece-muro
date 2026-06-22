using MURO.Application.Interfaces;
using MURO.Infrastructure.Services;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;

namespace MURO.API.Extensions;

/// <summary>
/// Program.cs'den ayrılmış DI servis kayıtları — okunabilirlik ve bakım kolaylığı için.
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Tüm business servislerini DI container'a kaydeder.
    /// </summary>
    public static IServiceCollection AddMuroBusinessServices(this IServiceCollection services)
    {
        // --- Core Services ---
        services.AddScoped<IAuthLoginService, AuthLoginService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IGroupAccessService, GroupAccessService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<ICourseSessionService, CourseSessionService>();
        services.AddScoped<ICourseMaterialService, CourseMaterialService>();
        services.AddScoped<ICourseEnrollmentService, CourseEnrollmentService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IExamAssignmentService, ExamAssignmentService>();
        services.AddScoped<IExamResultService, ExamResultService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<ISupportService, SupportService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMediaFolderService, MediaFolderService>();
        services.AddScoped<ICourseMediaService, CourseMediaService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ILiveMeetingService, LiveMeetingService>();
        services.AddScoped<IAccountingService, AccountingService>();
        
        services.AddScoped<IPodcastService, PodcastService>();
        services.AddScoped<ISessionAttendanceService, SessionAttendanceService>();
        services.AddScoped<IExcelService, ExcelService>();
        services.AddScoped<IVideoNoteService, VideoNoteService>();
        services.AddScoped<IVideoProgressService, VideoProgressService>();
        services.AddScoped<IPackageService, PackageService>();
        services.AddScoped<ISecurityService, SecurityService>();
        services.AddScoped<IRecordingService, RecordingService>();
        services.AddScoped<IWebhookHandlerService, WebhookHandlerService>();

        // --- VEP Control Plane ---
        services.AddScoped<ISystemHealthService, SystemHealthService>();

        // --- Admin Control Plane (Refactored from AdminService) ---

        services.AddScoped<IAdminSessionService, AdminSessionService>();
        services.AddScoped<IAdminAnalyticsService, AdminAnalyticsService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminHealthService, AdminHealthService>();
        services.AddScoped<IAdminBackupService, AdminBackupService>();
        services.AddScoped<IAdminConfigService, AdminConfigService>();
        services.AddScoped<IAdminJobsService, AdminJobsService>();
        services.AddScoped<IAdminSecurityService, AdminSecurityService>();

        // --- Background Job Queue (Channel<T> tabanlı fire-and-forget) ---
        services.AddSingleton<IBackgroundJobQueue, ChannelBackgroundJobQueue>();
        services.AddHostedService<BackgroundJobProcessor>();

        // --- Meeting Providers (BBB + Jitsi opsiyonel) ---
        services.AddScoped<BbbMeetingProvider>();
        services.AddScoped<JitsiMeetService>();
        services.AddScoped<MeetingProviderFactory>();

        return services;
    }

    /// <summary>
    /// Redis ve cache altyapısını yapılandırır.
    /// </summary>
    public static IServiceCollection AddMuroCaching(this IServiceCollection services, string redisConnection)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "muro:";
        });
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection));
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();
        services.AddSingleton<IRealtimeCounterService, RedisRealtimeCounterService>();
        services.AddSingleton<IJobQueue, RedisJobQueue>();

        return services;
    }

    /// <summary>
    /// BBB entegrasyonu — development'ta sertifika bypass, production'da strict.
    /// Polly ile resilient: Retry (3 deneme, exponential backoff) + Circuit Breaker (5 hata → 30s kapat).
    /// </summary>
    public static IServiceCollection AddMuroBbbIntegration(this IServiceCollection services, bool isDevelopment)
    {
        // ── Retry Policy: 3 deneme, exponential backoff (1s → 2s → 4s) + jitter ──
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError() // 5xx + 408 + network errors
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500)), // Jitter
                onRetry: (outcome, timespan, retryAttempt, _) =>
                {
                    var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("BbbResilience");
                    logger?.LogWarning(
                        "🔄 BBB Retry {Attempt}/3 — {StatusCode} — Bekleme: {Delay}ms",
                        retryAttempt,
                        outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.Message,
                        timespan.TotalMilliseconds);
                });

        // ── Circuit Breaker: 5 ardışık hata → 30 saniye sigortayı at ──
        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDelay) =>
                {
                    var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("BbbResilience");
                    logger?.LogError(
                        "🔴 BBB Circuit OPEN — {Reason} — {BreakDuration}s boyunca istekler reddedilecek",
                        outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.Message,
                        breakDelay.TotalSeconds);
                },
                onReset: () =>
                {
                    var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("BbbResilience");
                    logger?.LogInformation("🟢 BBB Circuit CLOSED — BBB bağlantısı tekrar sağlıklı");
                },
                onHalfOpen: () =>
                {
                    var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("BbbResilience");
                    logger?.LogWarning("🟡 BBB Circuit HALF-OPEN — Deneme isteği gönderiliyor...");
                });

        var builder = services.AddHttpClient<IBbbService, BbbService>();

        if (isDevelopment)
        {
            // ⚠️ Sadece development: self-signed sertifikaları kabul et
            builder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }

        builder.ConfigureHttpClient(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ── Polly policies: Retry → Circuit Breaker (sıralama önemli!) ──
        builder
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(circuitBreakerPolicy);

        return services;
    }
}
