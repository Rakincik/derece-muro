namespace MURO.Application.Interfaces;

/// <summary>
/// Redis-backed job queue — arka plan işlerini güvenilir şekilde kuyruğa alır.
/// Başarısız işler retry counter ile tekrar kuyrığa eklenir.
/// </summary>
public interface IJobQueue
{
    /// <summary>İşi kuyruğa ekler.</summary>
    Task EnqueueAsync(string queueName, string payload);

    /// <summary>Kuyruktan bir iş alır (BRPOP). Boşsa null.</summary>
    Task<string?> DequeueAsync(string queueName, TimeSpan? timeout = null);

    /// <summary>Kuyruktaki iş sayısını döner.</summary>
    Task<long> GetQueueLengthAsync(string queueName);
}
