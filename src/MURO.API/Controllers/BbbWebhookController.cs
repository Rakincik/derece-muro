using Microsoft.AspNetCore.RateLimiting;
using MURO.API.Middleware;
using Microsoft.AspNetCore.Mvc;
using MURO.Application.Interfaces;
using MURO.Application.DTOs.Webhooks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MURO.API.Controllers;

/// <summary>
/// BigBlueButton Webhook: BBB sunucusu ders eventlarını buraya POST eder.
/// Güvenlik: HMAC-SHA256 checksum doğrulaması ile korunur.
/// </summary>
[EnableRateLimiting(RateLimitingConfig.GlobalPolicy)]
[ApiController]
[Route("api/v1/bbb/webhook")]
public class BbbWebhookController : ControllerBase
{
    private readonly ISessionAttendanceService _attendanceService;
    private readonly IWebhookHandlerService _webhookHandler;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BbbWebhookController> _logger;

    public BbbWebhookController(
        ISessionAttendanceService attendanceService,
        IWebhookHandlerService webhookHandler,
        IConfiguration configuration,
        ILogger<BbbWebhookController> logger)
    {
        _attendanceService = attendanceService;
        _webhookHandler = webhookHandler;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// BBB, ders eventlerini bu endpoint'e gönderir.
    /// Event Types: user-joined, user-left, recording-ready, meeting-ended
    /// </summary>
    [HttpPost("events")]
    public async Task<IActionResult> HandleEvent()
    {
        // ── 1. Checksum doğrulaması ──────────────────────────────────────────
        if (!await ValidateChecksumAsync())
        {
            _logger.LogWarning("BBB Webhook: Geçersiz checksum — istek reddedildi. IP: {IP}",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "Invalid checksum" });
        }

        // ── 2. Body'yi oku ve deserialize et ────────────────────────────────
        Request.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();

        BbbWebhookPayload? payload = null;
        try
        {
            var rawTrimmed = rawBody.TrimStart();
            if (rawTrimmed.StartsWith("["))
            {
                // Standart BBB 3.0 Webhook Payload (Array)
                var bbbArray = JsonDocument.Parse(rawBody).RootElement;
                payload = new BbbWebhookPayload();
                
                foreach (var item in bbbArray.EnumerateArray())
                {
                    if (item.TryGetProperty("event", out var eventStrProp))
                    {
                        var eventStr = eventStrProp.GetString();
                        if (!string.IsNullOrEmpty(eventStr))
                        {
                            JsonElement eventDoc;
                            if (eventStr.TrimStart().StartsWith("["))
                            {
                                var innerArr = JsonDocument.Parse(eventStr).RootElement;
                                if (innerArr.GetArrayLength() > 0)
                                    eventDoc = innerArr[0];
                                else
                                    continue;
                            }
                            else
                            {
                                eventDoc = JsonDocument.Parse(eventStr).RootElement;
                            }

                            if (eventDoc.TryGetProperty("data", out var dataNode) && dataNode.TryGetProperty("id", out var idNode))
                            {
                                var id = idNode.GetString();
                                if (id == "rap-publish-ended")
                                {
                                    string extMeetingId = "";
                                    string recordId = "";

                                    if (dataNode.TryGetProperty("attributes", out var attrNode))
                                    {
                                        if (attrNode.TryGetProperty("meeting", out var meetingNode) && meetingNode.TryGetProperty("externalMeetingId", out var extNode))
                                            extMeetingId = extNode.GetString() ?? "";

                                        if (attrNode.TryGetProperty("recordId", out var recIdNode))
                                            recordId = recIdNode.GetString() ?? "";
                                    }

                                    payload.Events.Add(new BbbEvent
                                    {
                                        EventType = "recording-ready",
                                        MeetingId = extMeetingId,
                                        RecordingUrl = _configuration["Bbb:Url"]?.Replace("/api", $"/playback/presentation/2.3/{recordId}") ?? $"https://canli.monopoluzem.com.tr/playback/presentation/2.3/{recordId}"
                                    });
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Eski Özel MURO Payload
                payload = JsonSerializer.Deserialize<BbbWebhookPayload>(rawBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("BBB Webhook: Geçersiz JSON body — {Error}", ex.Message);
            return BadRequest(new { error = "Invalid JSON" });
        }

        if (payload?.Events == null || payload.Events.Count == 0)
            return BadRequest(new { error = "No events" });

        // ── 3. Event işleme ──────────────────────────────────────────────────
        foreach (var evt in payload.Events)
        {
            _logger.LogInformation("BBB Event: {EventType} | Meeting: {MeetingId}", evt.EventType, evt.MeetingId);

            try
            {
                switch (evt.EventType)
                {
                    case "user-joined":
                        if (evt.SessionId != Guid.Empty && evt.UserId != Guid.Empty)
                            await _attendanceService.RecordJoinAsync(evt.TenantId, evt.SessionId, evt.UserId);
                        break;

                    case "user-left":
                        if (evt.SessionId != Guid.Empty && evt.UserId != Guid.Empty)
                            await _attendanceService.RecordLeaveAsync(evt.TenantId, evt.SessionId, evt.UserId);
                        break;

                    case "meeting-ended":
                        await _webhookHandler.HandleBbbMeetingEndedAsync(evt);
                        break;

                    case "recording-ready":
                        await _webhookHandler.HandleBbbRecordingReadyAsync(evt);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BBB event işlenirken hata: {EventType}", evt.EventType);
            }
        }

        return Ok(new { processed = payload.Events.Count });
    }

    // ── Checksum Doğrulama ────────────────────────────────────────────────────

    private async Task<bool> ValidateChecksumAsync()
    {
        var sharedSecret = _configuration["Bbb:WebhookSharedSecret"];

        if (string.IsNullOrWhiteSpace(sharedSecret))
        {
            _logger.LogWarning("BBB:WebhookSharedSecret yapılandırılmamış — checksum doğrulaması atlanıyor (geliştirme modu).");
            return true;
        }

        var checksum = Request.Query["checksum"].ToString();
        if (string.IsNullOrEmpty(checksum))
        {
            _logger.LogWarning("BBB Webhook: checksum query parametresi eksik.");
            return false;
        }

        Request.EnableBuffering();
        Request.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Seek(0, SeekOrigin.Begin);

        var endpointUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}";
        var dataToHash = endpointUrl + rawBody + sharedSecret;

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));
        var expectedChecksum = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var isValid = string.Equals(checksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);

        if (!isValid)
            _logger.LogWarning("BBB Webhook: Checksum eşleşmedi. Beklenen: {Expected}, Gelen: {Received}",
                expectedChecksum, checksum);

        return isValid;
    }
}
