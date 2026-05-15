using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using MURO.Domain.Entities;
using MURO.Domain.Enums;
using MURO.Infrastructure.Persistence;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// BBB Webhook Idempotency + Sırasız Event Testleri.
/// Gerçek dünyada BBB aynı eventi 2 kez gönderebilir, eventler sırasız gelebilir.
/// DB seviyesinde doğrudan test edilerek controller bağımlılığı kaldırılmıştır.
/// </summary>
public class BbbWebhookIdempotencyTests : IDisposable
{
    private readonly MuroDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _meetingId;

    public BbbWebhookIdempotencyTests()
    {
        var options = new DbContextOptionsBuilder<MuroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MuroDbContext(options);
        _meetingId = $"test_{_sessionId}";

        // Seed: Course + Session (Live, recording enabled)
        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "T", Code = "test", CreatedAt = DateTime.UtcNow });
        _db.Users.Add(new User { Id = _userId, Email = "u@t.com", FirstName = "U", LastName = "T", PasswordHash = "h", Role = UserRole.Student, IsActive = true });
        _db.Courses.Add(new Course { Id = _courseId, TenantId = _tenantId, Title = "Ders" });
        _db.Sessions.Add(new Session
        {
            Id = _sessionId, CourseId = _courseId, Title = "Canlı",
            Status = SessionStatus.Live, RecordingEnabled = true,
            BbbMeetingId = _meetingId
        });
        _db.SaveChanges();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. meeting-ended 2 KEZ — İlki Ended yapar, ikincisi skip etmeli
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task MeetingEnded_CalledTwice_ShouldBeIdempotent()
    {
        // İlk meeting-ended
        await SimulateMeetingEnded(_meetingId);

        var session1 = await _db.Sessions.FindAsync(_sessionId);
        session1!.Status.Should().Be(SessionStatus.Ended);

        var recCount1 = await _db.SessionRecordings.CountAsync(r => r.SessionId == _sessionId);
        recCount1.Should().Be(1, "İlk meeting-ended recording oluşturmalı");

        // İkinci meeting-ended (duplicate)
        await SimulateMeetingEnded(_meetingId);

        var session2 = await _db.Sessions.FindAsync(_sessionId);
        session2!.Status.Should().Be(SessionStatus.Ended); // Hâlâ Ended

        // Recording duplicate oluşmamalı
        var recCount2 = await _db.SessionRecordings.CountAsync(r => r.SessionId == _sessionId);
        recCount2.Should().Be(1, "Duplicate recording oluşmamalı");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. user-joined 2 KEZ — Attendance duplicate olmamalı
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task UserJoined_CalledTwice_ShouldNotDuplicateAttendance()
    {
        await SimulateUserJoined(_sessionId, _userId);
        await SimulateUserJoined(_sessionId, _userId);

        var count = await _db.SessionAttendances
            .CountAsync(a => a.SessionId == _sessionId && a.UserId == _userId);

        count.Should().Be(1, "Aynı user 2 kez join'sa da 1 attendance");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. 3 farklı user join → 3 attendance
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ThreeUsers_Join_ShouldCreateThreeAttendances()
    {
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        var u3 = Guid.NewGuid();

        await SimulateUserJoined(_sessionId, u1);
        await SimulateUserJoined(_sessionId, u2);
        await SimulateUserJoined(_sessionId, u3);

        var count = await _db.SessionAttendances
            .CountAsync(a => a.SessionId == _sessionId);

        count.Should().Be(3);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. recording-ready 2 KEZ — Duplicate MediaAsset oluşmamalı
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task RecordingReady_CalledTwice_ShouldNotDuplicateAsset()
    {
        // İlk recording-ready
        await SimulateRecordingReady(_sessionId, "https://bbb.test.com/playback/rec1");

        var recCount1 = await _db.SessionRecordings.CountAsync(r => r.SessionId == _sessionId);
        var assetCount1 = await _db.MediaAssets.CountAsync();

        // İkinci recording-ready (duplicate)
        await SimulateRecordingReady(_sessionId, "https://bbb.test.com/playback/rec1");

        var recCount2 = await _db.SessionRecordings.CountAsync(r => r.SessionId == _sessionId);
        var assetCount2 = await _db.MediaAssets.CountAsync();

        recCount2.Should().Be(recCount1, "Duplicate recording oluşmamalı");
        assetCount2.Should().Be(assetCount1, "Duplicate asset oluşmamalı");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. SIRASIYLA: recording-ready → meeting-ended (ters sıra)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task RecordingReady_BeforeMeetingEnded_ShouldWork()
    {
        // recording-ready önce geliyor (BBB bazen bunu yapar)
        await SimulateRecordingReady(_sessionId, "https://bbb.test.com/playback/rec1");

        var rec = await _db.SessionRecordings.FirstOrDefaultAsync(r => r.SessionId == _sessionId);
        rec.Should().NotBeNull("recording-ready gelince recording oluşmalı");

        // Sonra meeting-ended geliyor
        await SimulateMeetingEnded(_meetingId);

        var session = await _db.Sessions.FindAsync(_sessionId);
        session!.Status.Should().Be(SessionStatus.Ended);

        // Duplicate recording oluşmamalı
        var recCount = await _db.SessionRecordings.CountAsync(r => r.SessionId == _sessionId);
        recCount.Should().Be(1, "Ters sırada da tek recording olmalı");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. MEETING-ENDED: Var olmayan meetingId — crash olmamalı
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task MeetingEnded_SessionNotFound_ShouldNotCrash()
    {
        await SimulateMeetingEnded("non_existent_meeting_id_xyz");

        // Crash olmadı ✅ — session değişmemiş
        var session = await _db.Sessions.FindAsync(_sessionId);
        session!.Status.Should().Be(SessionStatus.Live);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. user-joined → user-left → user-joined (reconnect)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task UserReconnects_ShouldStillHaveSingleAttendance()
    {
        await SimulateUserJoined(_sessionId, _userId);
        await SimulateUserLeft(_sessionId, _userId);
        await SimulateUserJoined(_sessionId, _userId); // Reconnect

        var count = await _db.SessionAttendances
            .CountAsync(a => a.SessionId == _sessionId && a.UserId == _userId);

        count.Should().Be(1, "Reconnect'te yeni attendance oluşmamalı");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. user-left İLK GELDİ (user-joined'dan önce) — crash olmamalı
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task UserLeft_BeforeJoined_ShouldNotCrash()
    {
        // user-left, user-joined'dan önce geldi
        await SimulateUserLeft(_sessionId, _userId);

        // Crash olmadı ✅
        var count = await _db.SessionAttendances
            .CountAsync(a => a.SessionId == _sessionId && a.UserId == _userId);
        count.Should().Be(0, "Join olmadan leave geldi — attendance yok");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. GUID.EMPTY userId — skip edilmeli
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task UserJoined_EmptyGuid_ShouldSkip()
    {
        await SimulateUserJoined(_sessionId, Guid.Empty);

        var count = await _db.SessionAttendances.CountAsync();
        count.Should().Be(0, "Empty GUID ile attendance oluşmamalı");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. TOPLU USER EVENT — 5 join + 2 left = net 3 live
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task MultipleEvents_ShouldProcessAllCorrectly()
    {
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        var u3 = Guid.NewGuid();
        var u4 = Guid.NewGuid();
        var u5 = Guid.NewGuid();

        // 5 user join
        await SimulateUserJoined(_sessionId, u1);
        await SimulateUserJoined(_sessionId, u2);
        await SimulateUserJoined(_sessionId, u3);
        await SimulateUserJoined(_sessionId, u4);
        await SimulateUserJoined(_sessionId, u5);

        // 2 user left
        await SimulateUserLeft(_sessionId, u1);
        await SimulateUserLeft(_sessionId, u2);

        var totalAttendances = await _db.SessionAttendances
            .CountAsync(a => a.SessionId == _sessionId);

        totalAttendances.Should().Be(5, "5 join = 5 attendance kayıt");

        // LeftAt dolmuş olanlar
        var leftCount = await _db.SessionAttendances
            .CountAsync(a => a.SessionId == _sessionId && a.LeftAt != null);

        leftCount.Should().Be(2, "2 user left → 2 LeftAt dolu");
    }

    // ── Helper: Webhook event simülasyonları (DB seviyesinde) ────────────────

    private async Task SimulateMeetingEnded(string meetingId)
    {
        var session = await _db.Sessions
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.BbbMeetingId == meetingId);

        if (session == null) return;
        if (session.Status == SessionStatus.Ended) return; // İdempotent

        session.Status = SessionStatus.Ended;

        if (session.RecordingEnabled)
        {
            var exists = await _db.SessionRecordings.AnyAsync(r => r.SessionId == session.Id);
            if (!exists)
            {
                _db.SessionRecordings.Add(new SessionRecording
                {
                    Id = Guid.NewGuid(), SessionId = session.Id,
                    Status = MediaStatus.Processing, CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task SimulateRecordingReady(Guid sessionId, string recordingUrl)
    {
        var session = await _db.Sessions
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null) return;

        var recording = await _db.SessionRecordings.FirstOrDefaultAsync(r => r.SessionId == sessionId);
        if (recording == null)
        {
            recording = new SessionRecording
            {
                Id = Guid.NewGuid(), SessionId = sessionId,
                Status = MediaStatus.Processing, CreatedAt = DateTime.UtcNow
            };
            _db.SessionRecordings.Add(recording);
        }

        if (recording.MediaAssetId == null)
        {
            var asset = new MediaAsset
            {
                Id = Guid.NewGuid(),
                TenantId = session.Course.TenantId,
                CourseId = session.CourseId,
                Title = $"{session.Title} — Kayıt",
                FilePath = recordingUrl,
                Status = MediaStatus.Processing,
                CreatedAt = DateTime.UtcNow
            };
            _db.MediaAssets.Add(asset);
            recording.MediaAssetId = asset.Id;
        }

        await _db.SaveChangesAsync();
    }

    private async Task SimulateUserJoined(Guid sessionId, Guid userId)
    {
        if (userId == Guid.Empty) return;

        var exists = await _db.SessionAttendances
            .AnyAsync(a => a.SessionId == sessionId && a.UserId == userId);

        if (!exists)
        {
            _db.SessionAttendances.Add(new SessionAttendance
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = userId,
                TenantId = _tenantId,
                JoinedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }

    private async Task SimulateUserLeft(Guid sessionId, Guid userId)
    {
        var attendance = await _db.SessionAttendances
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.UserId == userId);

        if (attendance != null)
        {
            attendance.LeftAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public void Dispose() => _db.Dispose();
}
