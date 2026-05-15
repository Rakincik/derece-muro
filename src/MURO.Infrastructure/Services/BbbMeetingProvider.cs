using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Services;

/// <summary>
/// BBB'yi IMeetingProvider olarak wrap eder.
/// Mevcut BbbService'e delege eder — sıfır kod tekrarı.
/// </summary>
public class BbbMeetingProvider : IMeetingProvider
{
    private readonly IBbbService _bbb;
    private readonly IConfiguration _config;

    public string ProviderName => "bbb";

    public BbbMeetingProvider(IBbbService bbb, IConfiguration config)
    {
        _bbb = bbb;
        _config = config;
    }

    public async Task<MeetingCreateResult> CreateMeetingAsync(CreateMeetingRequest request)
    {
        var bbbOptions = new BbbMeetingOptions(
            MeetingId: request.MeetingId,
            MeetingName: request.MeetingName,
            AttendeePw: _config["Bbb:DefaultAttendeePw"] ?? "attendee123",
            ModeratorPw: _config["Bbb:DefaultModeratorPw"] ?? "moderator123",
            RecordingEnabled: request.RecordingEnabled,
            WelcomeMessage: request.WelcomeMessage,
            DurationMinutes: request.DurationMinutes,
            LogoutURL: request.LogoutUrl,
            TenantId: request.TenantId
        );

        var meetingId = await _bbb.CreateMeetingAsync(bbbOptions);
        return new MeetingCreateResult(meetingId, "bbb");
    }

    public async Task<string> GetJoinUrlAsync(JoinMeetingRequest request)
    {
        var bbbOptions = new BbbJoinOptions(
            MeetingId: request.MeetingId,
            FullName: request.FullName,
            Password: request.IsModerator
                ? (_config["Bbb:DefaultModeratorPw"] ?? "moderator123")
                : (_config["Bbb:DefaultAttendeePw"] ?? "attendee123"),
            UserId: request.UserId,
            IsModerator: request.IsModerator
        );

        return await _bbb.GetJoinUrlAsync(bbbOptions);
    }

    public async Task<bool> EndMeetingAsync(string meetingId)
        => await _bbb.ForceEndMeetingAsync(meetingId);

    public async Task<bool> IsMeetingRunningAsync(string meetingId)
        => await _bbb.IsMeetingRunningAsync(meetingId);
}
