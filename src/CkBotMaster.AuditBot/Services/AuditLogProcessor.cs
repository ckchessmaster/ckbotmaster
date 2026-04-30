using CkBotMaster.AuditBot.Configuration;
using CkBotMaster.AuditBot.Data;
using CkBotMaster.AuditBot.Data.Entities;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// Mirrors a Discord audit log entry to the configured audit channel and, when applicable,
/// kicks off a reason prompt for the actor. Idempotent on <see cref="IAuditLogEntry.Id"/> so
/// it is safe to call from both the live gateway pipeline and the catch-up replayer.
/// </summary>
public sealed class AuditLogProcessor(
    DiscordSocketClient client,
    AuditDbContext db,
    AuditEmbedBuilder embedBuilder,
    IReasonPromptService promptService,
    IOptions<BotOptions> options,
    ILogger<AuditLogProcessor> logger)
{
    private readonly BotOptions _options = options.Value;

    public async Task ProcessAsync(IAuditLogEntry entry, bool fromCatchup, CancellationToken ct)
    {
        // Skip excluded event types entirely.
        if (IsExcluded(entry.Action))
        {
            logger.LogDebug("Audit entry {EntryId} action {Action} is excluded; skipping.", entry.Id, entry.Action);
            return;
        }

        // Idempotency: skip already-processed entries (covers gateway-redelivery and catch-up overlap).
        if (await db.AuditEntries.AnyAsync(e => e.DiscordEntryId == entry.Id, ct))
        {
            logger.LogDebug("Audit entry {EntryId} already processed; skipping.", entry.Id);
            return;
        }

        var channel = client.GetChannel(_options.AuditChannelId) as IMessageChannel;
        if (channel is null)
        {
            logger.LogError("Audit channel {ChannelId} not found or not a message channel.", _options.AuditChannelId);
            return;
        }

        var embed = embedBuilder.Build(entry, fromCatchup, client);
        var posted = await channel.SendMessageAsync(embed: embed, options: new RequestOptions { CancelToken = ct });

        var stored = new AuditEntry
        {
            DiscordEntryId = entry.Id,
            MessageId = posted.Id,
            ChannelId = channel.Id,
            ActionType = entry.Action.ToString(),
            ActorId = entry.User?.Id,
            CreatedAt = entry.CreatedAt,
            FromCatchup = fromCatchup,
            ReasonStatus = DetermineInitialStatus(entry),
            ReasonText = string.IsNullOrWhiteSpace(entry.Reason) ? null : entry.Reason,
        };
        db.AuditEntries.Add(stored);

        await UpdateLastSeenAsync(entry.Id, ct);
        await db.SaveChangesAsync(ct);

        if (stored.ReasonStatus == ReasonStatus.Pending)
        {
            try
            {
                await promptService.PromptAsync(entry, stored, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to issue reason prompt for audit entry {EntryId}.", entry.Id);
            }
        }
    }

    private ReasonStatus DetermineInitialStatus(IAuditLogEntry entry)
    {
        if (entry.User is null)
        {
            return ReasonStatus.NotRequired;
        }

        if (!IsMajor(entry.Action))
        {
            return ReasonStatus.NotRequired;
        }

        // Discord allows API callers to attach a reason at the time of the action;
        // if one is present we don't need to prompt.
        if (!string.IsNullOrWhiteSpace(entry.Reason))
        {
            return ReasonStatus.Provided;
        }

        return ReasonStatus.Pending;
    }

    private bool IsMajor(ActionType action)
    {
        var name = action.ToString();
        foreach (var configured in _options.MajorEventTypes)
        {
            if (string.Equals(configured, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsExcluded(ActionType action)
    {
        var name = action.ToString();
        foreach (var configured in _options.ExcludedEventTypes)
        {
            if (string.Equals(configured, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private async Task UpdateLastSeenAsync(ulong entryId, CancellationToken ct)
    {
        var row = await db.BotState.FirstOrDefaultAsync(s => s.Key == BotState.LastSeenAuditEntryIdKey, ct);
        var newValue = entryId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (row is null)
        {
            db.BotState.Add(new BotState { Key = BotState.LastSeenAuditEntryIdKey, Value = newValue });
            return;
        }

        // Track the maximum id we've seen.
        if (ulong.TryParse(row.Value, out var existing) && entryId <= existing)
        {
            return;
        }

        row.Value = newValue;
    }
}
