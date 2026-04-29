using System.Globalization;
using CkBotMaster.AuditBot.Data;
using CkBotMaster.AuditBot.Data.Entities;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// On startup, replays any audit log entries that were created since the last
/// persisted <see cref="BotState.LastSeenAuditEntryIdKey"/>, capped at the last 24 hours.
/// Pages backwards through Discord's audit log (which returns newest-first) and
/// feeds entries through the queue in chronological order.
/// </summary>
public sealed class CatchupService(
    AuditDbContext db,
    AuditLogQueue queue,
    ILogger<CatchupService> logger) : IOnReadyHandler
{
    private const int PageSize = 100;
    private const int MaxPages = 50; // hard ceiling: 5,000 entries
    private static readonly TimeSpan CatchupWindow = TimeSpan.FromHours(24);

    public async Task OnReadyAsync(SocketGuild guild, CancellationToken ct)
    {
        var lastSeenRow = await db.BotState
            .FirstOrDefaultAsync(s => s.Key == BotState.LastSeenAuditEntryIdKey, ct);

        ulong lastSeenId = 0;
        if (lastSeenRow is not null && ulong.TryParse(lastSeenRow.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            lastSeenId = parsed;
        }

        if (lastSeenId == 0)
        {
            logger.LogInformation("No prior audit log state; recording current head and skipping catch-up.");
            await RecordHeadAsync(guild, ct);
            return;
        }

        var cutoff = DateTimeOffset.UtcNow - CatchupWindow;
        var collected = new List<IAuditLogEntry>();
        ulong? beforeId = null;

        try
        {
            for (var page = 0; page < MaxPages; page++)
            {
                var batch = await FetchPageAsync(guild, beforeId, ct);
                if (batch.Count == 0)
                {
                    break;
                }

                var stop = false;
                foreach (var entry in batch)
                {
                    if (entry.Id <= lastSeenId || entry.CreatedAt < cutoff)
                    {
                        stop = true;
                        break;
                    }
                    collected.Add(entry);
                }

                if (stop)
                {
                    break;
                }

                beforeId = batch[^1].Id;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching audit log during catch-up.");
            return;
        }

        if (collected.Count == 0)
        {
            logger.LogInformation(
                "Audit log catch-up: nothing new since {LastSeen} within the last {Hours}h window.",
                lastSeenId, CatchupWindow.TotalHours);
            return;
        }

        // Discord returns newest-first; replay chronologically.
        collected.Reverse();
        logger.LogInformation(
            "Audit log catch-up: replaying {Count} entries since {LastSeen} (capped to last {Hours}h).",
            collected.Count, lastSeenId, CatchupWindow.TotalHours);

        foreach (var entry in collected)
        {
            await queue.EnqueueAsync(entry, fromCatchup: true, ct);
        }
    }

    private static async Task<IReadOnlyList<IAuditLogEntry>> FetchPageAsync(
        SocketGuild guild, ulong? beforeId, CancellationToken ct)
    {
        var options = new RequestOptions { CancelToken = ct };
        var paged = beforeId is null
            ? guild.GetAuditLogsAsync(PageSize, options: options)
            : guild.GetAuditLogsAsync(PageSize, beforeId: beforeId.Value, options: options);

        var result = new List<IAuditLogEntry>();
        await foreach (var batch in paged.WithCancellation(ct))
        {
            result.AddRange(batch);
        }
        return result;
    }

    private async Task RecordHeadAsync(SocketGuild guild, CancellationToken ct)
    {
        try
        {
            var page = await FetchPageAsync(guild, beforeId: null, ct);
            if (page.Count == 0)
            {
                return;
            }

            var head = page[0].Id;
            var existing = await db.BotState.FirstOrDefaultAsync(s => s.Key == BotState.LastSeenAuditEntryIdKey, ct);
            var value = head.ToString(CultureInfo.InvariantCulture);

            if (existing is null)
            {
                db.BotState.Add(new BotState { Key = BotState.LastSeenAuditEntryIdKey, Value = value });
            }
            else
            {
                existing.Value = value;
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not record initial audit log head.");
        }
    }
}
