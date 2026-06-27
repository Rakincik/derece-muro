using Microsoft.EntityFrameworkCore;
using MURO.Application.DTOs.Auth;
using MURO.Application.DTOs.Notifications;
using MURO.Application.DTOs.Webhooks;
using MURO.Application.Interfaces;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace MURO.Infrastructure.Services;

public class WebhookHandlerService : IWebhookHandlerService
{
    private readonly MuroDbContext _context;
    private readonly IPackageService _packages;
    private readonly IAuthLoginService _auth;
    private readonly INotificationService _notificationService;
    private readonly IBbbService _bbbService;
    private readonly ILogger<WebhookHandlerService> _logger;

    public WebhookHandlerService(
        MuroDbContext context,
        IPackageService packages,
        IAuthLoginService auth,
        INotificationService notificationService,
        IBbbService bbbService,
        ILogger<WebhookHandlerService> logger)
    {
        _context = context;
        _packages = packages;
        _auth = auth;
        _notificationService = notificationService;
        _bbbService = bbbService;
        _logger = logger;
    }

    public async Task<PurchaseWebhookResponse> HandlePurchaseAsync(PurchaseWebhookRequest request)
    {
        if (!string.IsNullOrEmpty(request.OrderId))
        {
            var duplicate = await _context.UserPackages
                .AnyAsync(up => up.OrderId == request.OrderId);
            if (duplicate)
                return new PurchaseWebhookResponse(true, null, null, null, "Sipariş zaten işlendi.", true);
        }

        var package = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == request.PackageId && p.IsActive);
        if (package is null)
            throw new KeyNotFoundException("Paket bulunamadı veya aktif değil.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.UserEmail);

        if (user is null)
        {
            var tempPassword = GenerateTempPassword();
            var registerRequest = new RegisterRequest(
                request.UserFirstName, request.UserLastName,
                request.UserEmail, tempPassword,
                request.UserPhone);

            await _auth.RegisterAsync(registerRequest);
            user = await _context.Users
                .FirstAsync(u => u.Email == request.UserEmail);
        }

        var userPackage = await _packages.ActivateUserPackageAsync(
            userId: user.Id,
            packageId: package.Id,
            orderId: request.OrderId,
            source: "webhook");

        return new PurchaseWebhookResponse(true, user.Id, userPackage.Id, userPackage.ExpiresAt, "Paket başarıyla aktive edildi.");
    }

    public async Task<CancelWebhookResponse> HandleCancelAsync(CancelWebhookRequest request)
    {
        var userPackage = await _context.UserPackages
            .FirstOrDefaultAsync(up => up.OrderId == request.OrderId);

        if (userPackage is null)
            throw new KeyNotFoundException("Sipariş bulunamadı.");

        await _packages.CancelPackageAsync(userPackage.Id);

        return new CancelWebhookResponse(true, "Paket iptal edildi.");
    }

    public async Task HandleBbbMeetingEndedAsync(BbbEvent evt)
    {
        if (string.IsNullOrEmpty(evt.MeetingId)) return;

        var session = await _context.Sessions
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.BbbMeetingId == evt.MeetingId);

        if (session == null)
        {
            _logger.LogWarning("meeting-ended: Session bulunamadı. MeetingId: {MeetingId}", evt.MeetingId);
            return;
        }

        if (session.Status == SessionStatus.Ended)
        {
            _logger.LogInformation("meeting-ended: Session zaten Ended durumunda. SessionId: {SessionId}", session.Id);
            return;
        }

        session.Status = SessionStatus.Ended;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Session sonlandırıldı ✓ SessionId: {SessionId} | MeetingId: {MeetingId}",
            session.Id, evt.MeetingId);

        var existingRecording = await _context.SessionRecordings
            .AnyAsync(r => r.SessionId == session.Id);

        if (!existingRecording && session.RecordingEnabled)
        {
            var recording = new SessionRecording
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Status = MediaStatus.Processing,
                CreatedAt = DateTime.UtcNow
            };
            _context.SessionRecordings.Add(recording);
            await _context.SaveChangesAsync();

            _logger.LogInformation("SessionRecording oluşturuldu (Processing) → SessionId: {SessionId}", session.Id);
        }

        try
        {
            var enrolledUserIds = await _context.CourseGroups
                .Where(cg => cg.CourseId == session.CourseId)
                .Join(_context.GroupMembers, cg => cg.GroupId, gm => gm.GroupId, (cg, gm) => gm.UserId)
                .Distinct()
                .ToListAsync();

            if (enrolledUserIds.Any())
            {
                await _notificationService.BulkSendAsync(new BulkNotificationRequest(
                    enrolledUserIds,
                    "📚 Ders Sona Erdi",
                    $"\"{session.Title}\" dersi sona erdi." + (session.RecordingEnabled ? " Kayıt kısa süre içinde hazır olacak." : ""),
                    $"SessionEnded:{session.CourseId}"
                ));

                _logger.LogInformation("meeting-ended bildirimi {Count} öğrenciye gönderildi. SessionId: {SessionId}",
                    enrolledUserIds.Count, session.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "meeting-ended bildirim gönderilemedi. SessionId: {SessionId}", session.Id);
        }
    }

    public async Task HandleBbbRecordingReadyAsync(BbbEvent evt)
    {
        if (evt.SessionId == Guid.Empty && string.IsNullOrEmpty(evt.MeetingId)) 
            return;

        var sessionQuery = _context.Sessions
            .Include(s => s.Course)
            .Include(s => s.Recording)
            .AsQueryable();

        var session = evt.SessionId != Guid.Empty
            ? await sessionQuery.FirstOrDefaultAsync(s => s.Id == evt.SessionId)
            : await sessionQuery.FirstOrDefaultAsync(s => s.BbbMeetingId == evt.MeetingId);

        if (session != null && evt.SessionId == Guid.Empty)
        {
            evt.SessionId = session.Id;
        }

        if (session == null)
        {
            _logger.LogWarning("recording-ready: Session bulunamadı. SessionId: {SessionId}", evt.SessionId);
            return;
        }

        var recording = session.Recording;
        if (recording == null)
        {
            recording = new SessionRecording
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Status = MediaStatus.Processing,
                CreatedAt = DateTime.UtcNow
            };
            _context.SessionRecordings.Add(recording);
        }

        if (!string.IsNullOrEmpty(evt.RecordingUrl))
        {
            int? durationSeconds = null;
            if (!string.IsNullOrEmpty(session.BbbMeetingId))
            {
                try
                {
                    var recordings = await _bbbService.GetRecordingsAsync(session.BbbMeetingId);
                    var rec = recordings.FirstOrDefault(r => r.PlaybackUrl == evt.RecordingUrl || r.Status == "published");
                    if (rec != null) durationSeconds = rec.DurationSeconds;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Webhook: BBB'den kayit suresi cekilemedi. SessionId: {SessionId}", session.Id);
                }
            }

            var asset = recording.MediaAsset;
            if (asset == null)
            {
                asset = new MediaAsset
                {
                    Id = Guid.NewGuid(),
                    CourseId = session.CourseId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MediaAssets.Add(asset);
                recording.MediaAssetId = asset.Id;
            }

            asset.Title = $"{evt.SessionTitle ?? session.Title} — Kayıt";
            asset.FilePath = evt.RecordingUrl;
            asset.HlsPath = null;
            asset.DurationSeconds = durationSeconds;
            asset.Status = MediaStatus.Ready;

            recording.Status = MediaStatus.Ready;

            var existsInMedia = await _context.CourseMedias.AnyAsync(cm => cm.SessionId == session.Id);
            if (!existsInMedia)
            {
                var hasManualSort = await _context.CourseMedias
                    .AnyAsync(cm => cm.CourseId == session.CourseId && cm.OrderIndex >= 0);

                int orderIndex = -1;
                if (hasManualSort)
                {
                    var courseMediaMaxOrder = await _context.CourseMedias
                        .Where(cm => cm.CourseId == session.CourseId)
                        .MaxAsync(cm => (int?)cm.OrderIndex) ?? -1;
                    orderIndex = courseMediaMaxOrder + 1;
                }

                _context.CourseMedias.Add(new CourseMedia
                {
                    CourseId = session.CourseId,
                    SessionId = session.Id,
                    OrderIndex = orderIndex
                });
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("recording-ready işlendi ✓ SessionId: {SessionId} | RecordingUrl: {Url}",
            evt.SessionId, evt.RecordingUrl);

        if (evt.EnrolledUserIds?.Any() == true)
        {
            await _notificationService.BulkSendAsync( new BulkNotificationRequest(
                evt.EnrolledUserIds,
                "📹 Ders Kaydı Hazır",
                $"\"{evt.SessionTitle ?? session.Title}\" dersinin kaydı izlemeye hazır.",
                $"RecordingReady:{session.CourseId}"
            ));

            _logger.LogInformation("Recording-ready bildirimi {Count} öğrenciye gönderildi. Session: {SessionId}",
                evt.EnrolledUserIds.Count, evt.SessionId);
        }
    }

    private static string GenerateTempPassword()
    {
        var chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#";
        var random = new Random();
        return new string(Enumerable.Range(0, 10).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
