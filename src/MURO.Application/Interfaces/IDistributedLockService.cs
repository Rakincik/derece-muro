namespace MURO.Application.Interfaces;

/// <summary>
/// Redis-backed distributed lock — race condition koruması.
/// CourseService.StartSessionAsync gibi eş zamanlı çağrılarda kullanılır.
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Lock almayı dener. Başarılıysa IAsyncDisposable dönerken,
    /// lock alınamazsa null döner.
    /// </summary>
    Task<IAsyncDisposable?> TryAcquireAsync(string resource, TimeSpan expiry);
}
