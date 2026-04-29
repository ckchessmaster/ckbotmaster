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
/// Routes incoming non-bot messages: captures reasons from DM or @mention prompts, and
/// keeps the audit channel clean by deleting unrelated user messages.
/// </summary>
public sealed class MessageDispatcher(
    DiscordSocketClient client,
    AuditDbContext db,
    AuditEmbedBuilder embedBuilder,
    IOptions<BotOptions> options,
    ILogger<MessageDispatcher> logger) : IMessageDispatcher
{
    private readonly BotOptions _options = options.Value;

    public async Task HandleAsync(SocketMessage message, CancellationToken ct)
    {
        if (message.Channel is IDMChannel)
        {
            await HandleDmAsync(message, ct);
            return;
        }

        if (message.Channel.Id == _options.AuditChannelId)
        {
            await HandleAuditChannelAsync(message, ct);
        }
    }

    private async Task HandleDmAsync(SocketMessage message, CancellationToken ct)
    {
        var pending = await db.PendingReasons
            .Include(p => p.AuditEntry)
            .FirstOrDefaultAsync(p => p.IsOpen
                && p.ActorId == message.Author.Id
                && p.PromptMode == PromptMode.DmOnly, ct);

        if (pending is null)
        {
            return;
        }

        await CaptureReasonAsync(pending, message, ct);

        try
        {
            await message.Channel.SendMessageAsync(
                "Thanks — your reason has been recorded.",
                options: new RequestOptions { CancelToken = ct });
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not send DM acknowledgement to {ActorId}.", message.Author.Id);
        }
    }

    private async Task HandleAuditChannelAsync(SocketMessage message, CancellationToken ct)
    {
        var pending = await db.PendingReasons
            .Include(p => p.AuditEntry)
            .FirstOrDefaultAsync(p => p.IsOpen
                && p.ActorId == message.Author.Id
                && p.PromptMode == PromptMode.MentionOnly, ct);

        if (pending is not null)
        {
            await CaptureReasonAsync(pending, message, ct);
            await TryEditPromptAsync(pending, "Reason captured.", ct);
        }

        // Always delete the user's message — channel is bot-only.
        try
        {
            await message.DeleteAsync(new RequestOptions { CancelToken = ct });
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not delete message {MessageId} from audit channel.", message.Id);
        }
    }

    private async Task CaptureReasonAsync(PendingReason pending, SocketMessage message, CancellationToken ct)
    {
        var reason = message.Content.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            reason = "*(empty)*";
        }

        var stored = pending.AuditEntry!;
        await UpdateAuditEmbedAsync(stored, reason, ct);

        stored.ReasonStatus = ReasonStatus.Provided;
        stored.ReasonText = reason;
        pending.IsOpen = false;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Captured reason for audit entry {EntryId} from {ActorId} via {Mode}.",
            stored.DiscordEntryId, pending.ActorId, pending.PromptMode);
    }

    private async Task UpdateAuditEmbedAsync(AuditEntry stored, string reason, CancellationToken ct)
    {
        if (client.GetChannel(stored.ChannelId) is not IMessageChannel channel)
        {
            return;
        }

        var existing = await channel.GetMessageAsync(stored.MessageId,
            options: new RequestOptions { CancelToken = ct });
        if (existing is not IUserMessage userMessage || userMessage.Embeds.Count == 0)
        {
            return;
        }

        var updated = embedBuilder.WithReason(userMessage.Embeds.First(), reason);
        await userMessage.ModifyAsync(props => props.Embed = updated,
            new RequestOptions { CancelToken = ct });
    }

    private async Task TryEditPromptAsync(PendingReason pending, string newText, CancellationToken ct)
    {
        if (client.GetChannel(pending.PromptChannelId) is not IMessageChannel channel)
        {
            return;
        }

        try
        {
            var msg = await channel.GetMessageAsync(pending.PromptMessageId,
                options: new RequestOptions { CancelToken = ct });
            if (msg is IUserMessage prompt)
            {
                await prompt.ModifyAsync(p => p.Content = newText,
                    new RequestOptions { CancelToken = ct });
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not edit prompt {PromptMessageId}.", pending.PromptMessageId);
        }
    }
}
