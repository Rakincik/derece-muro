using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using MURO.Application.Interfaces;
using MURO.Infrastructure.Services;
using Xunit;

namespace MURO.Tests.Unit;

/// <summary>
/// BbbService unit testleri — HTTP isteklerini mock'layarak BBB API davranışını doğrular.
/// Checksum hesaplama, XML parsing, hata yönetimi.
/// </summary>
public class BbbServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly BbbService _service;
    private readonly IConfiguration _config;

    public BbbServiceTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpHandlerMock.Object);

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bbb:Url"] = "https://bbb.test.com/bigbluebutton/api",
                ["Bbb:Secret"] = "test-secret-key-12345",
                ["Bbb:Defaults:MaxParticipants"] = "200",
                ["Bbb:Defaults:GuestPolicy"] = "ASK_MODERATOR",
            })
            .Build();

        _service = new BbbService(httpClient, _config, Mock.Of<ILogger<BbbService>>());
    }

    // ── 1. CreateMeeting — Başarılı ─────────────────────────────────────────
    [Fact]
    public async Task CreateMeeting_Success_ShouldReturnMeetingId()
    {
        MockBbbResponse("<response><returncode>SUCCESS</returncode><meetingID>test_123</meetingID></response>");

        var result = await _service.CreateMeetingAsync(new BbbMeetingOptions(
            MeetingId: "test_123", MeetingName: "Test Ders",
            AttendeePw: "ap", ModeratorPw: "mp",
            RecordingEnabled: true));

        result.Should().Be("test_123");
    }

    // ── 2. CreateMeeting — BBB hata dönerse ─────────────────────────────────
    [Fact]
    public async Task CreateMeeting_Failure_ShouldThrow()
    {
        MockBbbResponse("<response><returncode>FAILED</returncode><message>duplicateWarning</message></response>");

        var act = () => _service.CreateMeetingAsync(new BbbMeetingOptions(
            MeetingId: "test_123", MeetingName: "Test",
            AttendeePw: "ap", ModeratorPw: "mp",
            RecordingEnabled: false));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*duplicateWarning*");
    }

    // ── 3. CreateMeeting — BBB'ye ulaşılamıyor ─────────────────────────────
    [Fact]
    public async Task CreateMeeting_NetworkError_ShouldThrowWithMessage()
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var act = () => _service.CreateMeetingAsync(new BbbMeetingOptions(
            MeetingId: "test", MeetingName: "Test",
            AttendeePw: "ap", ModeratorPw: "mp",
            RecordingEnabled: false));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ulaşılamıyor*");
    }

    // ── 4. GetJoinUrl — URL'de checksum olmalı ──────────────────────────────
    [Fact]
    public async Task GetJoinUrl_ShouldContainChecksum()
    {
        var url = await _service.GetJoinUrlAsync(new BbbJoinOptions(
            MeetingId: "test_123", FullName: "Ahmet Yılmaz",
            Password: "mp", UserId: Guid.NewGuid(), IsModerator: true));

        url.Should().Contain("checksum=");
        url.Should().Contain("fullName=Ahmet");
        url.Should().Contain("meetingID=test_123");
    }

    // ── 5. GetJoinUrl — Moderatör vs Student ────────────────────────────────
    [Fact]
    public async Task GetJoinUrl_Moderator_ShouldHaveJoinViaHtml5()
    {
        var modUrl = await _service.GetJoinUrlAsync(new BbbJoinOptions(
            MeetingId: "m1", FullName: "Hoca",
            Password: "mp", UserId: Guid.NewGuid(), IsModerator: true));

        var studentUrl = await _service.GetJoinUrlAsync(new BbbJoinOptions(
            MeetingId: "m1", FullName: "Öğrenci",
            Password: "ap", UserId: Guid.NewGuid(), IsModerator: false));

        modUrl.Should().Contain("joinViaHtml5=true");
        studentUrl.Should().NotContain("joinViaHtml5");
    }

    // ── 6. EndMeeting — Başarılı ────────────────────────────────────────────
    [Fact]
    public async Task EndMeeting_Success_ShouldReturnTrue()
    {
        MockBbbResponse("<response><returncode>SUCCESS</returncode></response>");

        var result = await _service.EndMeetingAsync("test_123", "mp");

        result.Should().BeTrue();
    }

    // ── 7. EndMeeting — Başarısız ───────────────────────────────────────────
    [Fact]
    public async Task EndMeeting_Failure_ShouldReturnFalse()
    {
        MockBbbResponse("<response><returncode>FAILED</returncode><message>notFound</message></response>");

        var result = await _service.EndMeetingAsync("nonexistent", "mp");

        result.Should().BeFalse();
    }

    // ── 8. EndMeeting — Network Error ───────────────────────────────────────
    [Fact]
    public async Task EndMeeting_NetworkError_ShouldReturnFalse()
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Timeout"));

        var result = await _service.EndMeetingAsync("test", "mp");

        result.Should().BeFalse();
    }

    // ── 9. GetRecordings — Kayıt var ────────────────────────────────────────
    [Fact]
    public async Task GetRecordings_WithRecordings_ShouldParseCorrectly()
    {
        MockBbbResponse(@"
            <response>
                <returncode>SUCCESS</returncode>
                <recordings>
                    <recording>
                        <recordID>rec-001</recordID>
                        <meetingID>test_123</meetingID>
                        <playback><format><url>https://bbb.test.com/playback/rec-001</url></format></playback>
                        <duration>3600000</duration>
                        <startTime>1700000000000</startTime>
                        <state>published</state>
                    </recording>
                </recordings>
            </response>");

        var recordings = await _service.GetRecordingsAsync("test_123");

        recordings.Should().HaveCount(1);
        recordings[0].RecordingId.Should().Be("rec-001");
        recordings[0].PlaybackUrl.Should().Contain("playback/rec-001");
        recordings[0].DurationSeconds.Should().Be(3600); // 3600000ms = 3600s
        recordings[0].Status.Should().Be("published");
    }

    // ── 10. GetRecordings — Kayıt yok ───────────────────────────────────────
    [Fact]
    public async Task GetRecordings_NoRecordings_ShouldReturnEmpty()
    {
        MockBbbResponse("<response><returncode>SUCCESS</returncode><recordings></recordings></response>");

        var recordings = await _service.GetRecordingsAsync("test_123");

        recordings.Should().BeEmpty();
    }

    // ── 11. GetRecordings — Hata durumu ─────────────────────────────────────
    [Fact]
    public async Task GetRecordings_Error_ShouldReturnEmpty()
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Down"));

        var recordings = await _service.GetRecordingsAsync("test_123");

        recordings.Should().BeEmpty();
    }

    // ── 12. IsMeetingRunning — true ─────────────────────────────────────────
    [Fact]
    public async Task IsMeetingRunning_True_ShouldReturnTrue()
    {
        MockBbbResponse("<response><returncode>SUCCESS</returncode><running>true</running></response>");

        var result = await _service.IsMeetingRunningAsync("test_123");

        result.Should().BeTrue();
    }

    // ── 13. IsMeetingRunning — false ────────────────────────────────────────
    [Fact]
    public async Task IsMeetingRunning_False_ShouldReturnFalse()
    {
        MockBbbResponse("<response><returncode>SUCCESS</returncode><running>false</running></response>");

        var result = await _service.IsMeetingRunningAsync("test_123");

        result.Should().BeFalse();
    }

    // ── 14. Config eksik → exception ────────────────────────────────────────
    [Fact]
    public async Task CreateMeeting_MissingConfig_ShouldThrow()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        var svc = new BbbService(new HttpClient(), emptyConfig, Mock.Of<ILogger<BbbService>>());

        var act = () => svc.CreateMeetingAsync(new BbbMeetingOptions(
            "id", "name", "ap", "mp", false));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*yapılandırılmamış*");
    }

    // ── Helper ──────────────────────────────────────────────────────────────
    private void MockBbbResponse(string xml)
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, System.Text.Encoding.UTF8, "application/xml")
            });
    }
}
