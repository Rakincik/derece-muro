namespace MURO.Application.Interfaces;

public interface ISmsSender
{
    /// <summary>SMS gönderir. SMS kanali devre dışıysa no-op döner.</summary>
    Task SendAsync(string phoneNumber, string message);
}
