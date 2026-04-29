using Discord.WebSocket;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// Routes incoming non-bot messages: reason replies in DM, reason replies / cleanup in the audit channel.
/// </summary>
public interface IMessageDispatcher
{
    Task HandleAsync(SocketMessage message, CancellationToken ct);
}
