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

    /// <summary>
    /// Attempts to restore a deleted audit log message by looking up its original
    /// audit entry in the database and re-posting it to the audit channel.
    /// </summary>
    public async Task RestoreAsync(ulong channelId, ulong messageId, CancellationToken ct)
    {
        if (channelId != _options.AuditChannelId)
        {
            return;
        }

        var stored = await db.AuditEntries.FirstOrDefaultAsync(e => e.MessageId == messageId, ct);
        if (stored is null)
        {
            return;
        }

        var guild = client.GetGuild(_options.GuildId);
        if (guild is null)
        {
            logger.LogWarning("Guild {GuildId} not found during restoration.", _options.GuildId);
            return;
        }

        // Fetch the audit log entry from Discord to rebuild the embed.
        // We use a small range around the ID to find it efficiently.
        var logs = await guild.GetAuditLogsAsync(limit: 10, beforeId: stored.DiscordEntryId + 1).FlattenAsync();
        var entry = logs.FirstOrDefault(l => l.Id == stored.DiscordEntryId);

        if (entry is null)
        {
            logger.LogWarning("Audit log entry {EntryId} not found in Discord for restoration.", stored.DiscordEntryId);
            return;
        }

        var channel = guild.GetChannel(_options.AuditChannelId) as IMessageChannel;
        if (channel is null)
        {
            logger.LogError("Audit channel {ChannelId} not found for restoration.", _options.AuditChannelId);
            return;
        }

        var embed = embedBuilder.Build(entry, stored.FromCatchup, client);

        // Re-apply any captured reason or timeout status.
        if (stored.ReasonStatus == ReasonStatus.Provided && !string.IsNullOrWhiteSpace(stored.ReasonText))
        {
            embed = embedBuilder.WithReason(embed, stored.ReasonText);
        }
        else if (stored.ReasonStatus == ReasonStatus.TimedOut)
        {
            embed = embedBuilder.WithTimeoutReason(embed);
        }

        var posted = await channel.SendMessageAsync(embed: embed, options: new RequestOptions { CancelToken = ct });

        // Update the database record with the new message ID.
        stored.MessageId = posted.Id;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Restored deleted audit message for entry {EntryId}. New MessageId: {MessageId}", 
            stored.DiscordEntryId, posted.Id);
    }
}
