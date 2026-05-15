using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Services;

/// <summary>
/// VEP Control Plane'e HMAC-SHA256 imzalı webhook bildirimleri gönderir.
/// Konfigürasyon: appsettings.json → Vep:WebhookUrl, Vep:WebhookSecret
/// </summary>
public class VepWebhookService : IVepWebhookService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<VepWebhookService> _logger;

    public VepWebhookService(
        HttpClient http,
        IConfiguration config,
        ILogger<VepWebhookService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public Task<bool> IsConfiguredAsync()
    {
        var url = _config["Vep:WebhookUrl"];
        return Task.FromResult(!string.IsNullOrEmpty(url));
    }

    public async Task NotifyAsync(VepWebhookEvent evt)
    {
        var url = _config["Vep:WebhookUrl"];
        if (string.IsNullOrEmpty(url))
        {
            _logger.LogDebug("VEP webhook URL yapılandırılmamış — atlanıyor: {EventType}", evt.EventType);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // HMAC-SHA256 imza
            var secret = _config["Vep:WebhookSecret"] ?? "";
            if (!string.IsNullOrEmpty(secret))
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
                var signature = Convert.ToHexString(hash).ToLowerInvariant();
                request.Headers.Add("X-Muro-Signature", signature);
            }

            request.Headers.Add("X-Muro-Event", evt.EventType);

            var response = await _http.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("VEP webhook gönderildi: {EventType} → {StatusCode}",
                    evt.EventType, (int)response.StatusCode);
            }
            else
            {
                _logger.LogWarning("VEP webhook başarısız: {EventType} → {StatusCode} {Reason}",
                    evt.EventType, (int)response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            // Webhook hatası MURO'yu durdurumamalı — fire-and-forget mantığı
            _logger.LogWarning(ex, "VEP webhook gönderilemedi: {EventType}", evt.EventType);
        }
    }
}
