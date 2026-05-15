namespace MURO.Application.Interfaces;

/// <summary>
/// Redis INCR/DECR ile real-time counter'lar.
/// Online kullanıcı sayısı, aktif ders sayısı gibi metrikleri anlık tutar.
/// </summary>
public interface IRealtimeCounterService
{
    Task<long> IncrementAsync(string key, TimeSpan? expiry = null);
    Task<long> DecrementAsync(string key);
    Task<long> GetAsync(string key);
    Task ResetAsync(string key);
}
