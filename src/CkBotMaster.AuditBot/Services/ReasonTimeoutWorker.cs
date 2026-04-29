using CkBotMaster.AuditBot.Data;
using CkBotMaster.AuditBot.Data.Entities;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// Periodically expires open <see cref="PendingReason"/> rows that have passed their
/// <see cref="PendingReason.ExpiresAt"/>, marking the audit embed as "no reason provided".
/// </summary>
public sealed class ReasonTimeoutWorker(
    IServiceScopeFactory scopeFactory,
    DiscordSocketClient client,
    ILogger<ReasonTimeoutWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var embedBuilder = scope.ServiceProvider.GetRequiredService<AuditEmbedBuilder>();

            var now = DateTimeOffset.UtcNow;
            var expired = await db.PendingReasons
                .Include(p => p.AuditEntry)
                .Where(p => p.IsOpen && p.ExpiresAt <= now)
                .ToListAsync(ct);

            foreach (var pending in expired)
            {
                if (pending.AuditEntry is null)
                {
                    pending.IsOpen = false;
                    continue;
                }

                await TimeOutAsync(pending, embedBuilder, ct);
            }

            if (expired.Count > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Timed out {Count} pending reason(s).", expired.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during reason timeout sweep.");
        }
    }

    private async Task TimeOutAsync(PendingReason pending, AuditEmbedBuilder embedBuilder, CancellationToken ct)
    {
        var stored = pending.AuditEntry!;
        try
        {
            if (client.GetChannel(stored.ChannelId) is IMessageChannel channel)
            {
                var msg = await channel.GetMessageAsync(stored.MessageId,
                    options: new RequestOptions { CancelToken = ct });
                if (msg is IUserMessage user && user.Embeds.Count > 0)
                {
                    var updated = embedBuilder.WithTimeoutReason(user.Embeds.First());
                    await user.ModifyAsync(p => p.Embed = updated, new RequestOptions { CancelToken = ct });
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not edit embed for timed-out audit entry {EntryId}.", stored.DiscordEntryId);
        }

        stored.ReasonStatus = ReasonStatus.TimedOut;
        pending.IsOpen = false;
    }
}
