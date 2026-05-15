using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var enabled = _config.GetValue<bool>("Notifications:Email:Enabled");
        if (!enabled)
        {
            _logger.LogDebug("E-posta kanalı devre dışı — gönderim atlandı. Konu: {Subject}", subject);
            return;
        }

        var host     = _config["Notifications:Email:SmtpHost"] ?? "smtp.gmail.com";
        var port     = _config.GetValue<int>("Notifications:Email:SmtpPort", 587);
        var username = _config["Notifications:Email:Username"] ?? "";
        var password = _config["Notifications:Email:Password"] ?? "";
        var fromName  = _config["Notifications:Email:FromName"] ?? "MURO";
        var fromEmail = _config["Notifications:Email:FromEmail"] ?? "noreply@muro.com";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            _logger.LogInformation("E-posta gönderildi: {To} | {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-posta gönderilemedi: {To} | {Subject}", toEmail, subject);
            // E-posta hatası ana akışı durdurmamalı
        }
    }
}
