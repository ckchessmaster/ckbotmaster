using Discord.WebSocket;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// Hook executed when the Discord client signals <c>Ready</c>. Each implementation
/// runs in its own DI scope. Called once per <c>Ready</c> event (Discord may emit
/// this more than once across reconnects, so handlers must be idempotent).
/// </summary>
public interface IOnReadyHandler
{
    Task OnReadyAsync(SocketGuild guild, CancellationToken ct);
}
