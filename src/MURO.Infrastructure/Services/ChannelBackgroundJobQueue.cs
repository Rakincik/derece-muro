using System.Threading.Channels;
using MURO.Application.Interfaces;

namespace MURO.Infrastructure.Services;

/// <summary>
/// Channel&lt;T&gt; tabanlı in-process background job queue.
/// Bounded capacity ile backpressure sağlar — kuyruk dolunca yeni job bekler.
/// </summary>
public sealed class ChannelBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<BackgroundJob> _channel;

    public ChannelBackgroundJobQueue(int capacity = 500)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait, // Kuyruk dolunca bekle (backpressure)
            SingleReader = false,                   // Birden fazla consumer destekle
            SingleWriter = false,                   // Birden fazla producer destekle
        };
        _channel = Channel.CreateBounded<BackgroundJob>(options);
    }

    public ValueTask EnqueueAsync(BackgroundJob job)
        => _channel.Writer.WriteAsync(job);

    public ValueTask<BackgroundJob> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);
}
