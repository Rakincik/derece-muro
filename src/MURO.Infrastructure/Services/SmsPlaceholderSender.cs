using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Services;

/// <summary>
/// SMS gönderici. Netgsm / İletimMerkezi uyumlu HTTP GET formatı kullanır.
/// Enabled=false ise hiçbir şey yapmaz. Provider eklenince sadece config değişir.
/// </summary>
public class SmsPlaceholderSender : ISmsSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmsPlaceholderSender> _logger;

    public SmsPlaceholderSender(IConfiguration config, ILogger<SmsPlaceholderSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string phoneNumber, string message)
    {
        var enabled = _config.GetValue<bool>("Notifications:Sms:Enabled");
        if (!enabled)
        {
            _logger.LogDebug("SMS kanalı devre dışı — gönderim atlandı. Hedef: {Phone}", phoneNumber);
            return;
        }

        var apiUrl   = _config["Notifications:Sms:ApiUrl"] ?? "";
        var username = _config["Notifications:Sms:Username"] ?? "";
        var password = _config["Notifications:Sms:Password"] ?? "";
        var header   = _config["Notifications:Sms:Header"] ?? "MURO";

        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            _logger.LogWarning("SMS API URL yapılandırılmamış. Gönderim atlandı.");
            return;
        }

        try
        {
            var url = $"{apiUrl}?usercode={Uri.EscapeDataString(username)}" +
                      $"&password={Uri.EscapeDataString(password)}" +
                      $"&gsmno={Uri.EscapeDataString(phoneNumber)}" +
                      $"&message={Uri.EscapeDataString(message)}" +
                      $"&msgheader={Uri.EscapeDataString(header)}";

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var response = await client.GetAsync(url);
            _logger.LogInformation("SMS gönderildi: {Phone}, HTTP {Status}", phoneNumber, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMS gönderilemedi: {Phone}", phoneNumber);
        }
    }
}
