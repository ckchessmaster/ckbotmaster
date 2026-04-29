using CkBotMaster.AuditBot.Data.Entities;
using Discord;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// Issues reason prompts to actors of major audit events and records them as <see cref="PendingReason"/> rows.
/// </summary>
public interface IReasonPromptService
{
    Task PromptAsync(IAuditLogEntry entry, AuditEntry stored, CancellationToken ct);
}
