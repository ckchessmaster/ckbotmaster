using System.Threading.Channels;
using Discord;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// In-process queue for audit log entries that need processing. Decouples the gateway
/// event handler (which must return quickly) from the database/embed work.
/// </summary>
public sealed class AuditLogQueue
{
    private readonly Channel<QueueItem> _channel = Channel.CreateUnbounded<QueueItem>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(IAuditLogEntry entry, bool fromCatchup, CancellationToken ct)
        => _channel.Writer.WriteAsync(new QueueItem(entry, fromCatchup), ct);

    public IAsyncEnumerable<QueueItem> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);

    public readonly record struct QueueItem(IAuditLogEntry Entry, bool FromCatchup);
}
