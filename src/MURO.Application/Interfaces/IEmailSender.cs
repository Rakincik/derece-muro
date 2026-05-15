namespace MURO.Application.Interfaces;

public interface IEmailSender
{
    /// <summary>HTML gövdeli e-posta gönderir. E-posta kanali devre dışıysa no-op döner.</summary>
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
}
