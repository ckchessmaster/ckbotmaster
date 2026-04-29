using CkBotMaster.AuditBot.Configuration;
using CkBotMaster.AuditBot.Data;
using CkBotMaster.AuditBot.Data.Entities;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// Issues a reason prompt to the actor of a major audit event. By default, tries DM first
/// and falls back to an @mention reply in the audit channel if DMs are blocked.
/// </summary>
public sealed class ReasonPromptService(
    DiscordSocketClient client,
    AuditDbContext db,
    IOptions<BotOptions> options,
    ILogger<ReasonPromptService> logger) : IReasonPromptService
{
    private const int CannotSendMessagesToUser = 50007;

    private readonly BotOptions _options = options.Value;

    public async Task PromptAsync(IAuditLogEntry entry, AuditEntry stored, CancellationToken ct)
    {
        if (entry.User is null)
        {
            return;
        }

        var actor = entry.User;
        var promptText = $"You performed `{entry.Action}` in the server. " +
                         "Please reply to this message with a brief reason for the change.";

        IUserMessage? promptMessage = null;
        PromptMode? deliveredMode = null;

        if (_options.PromptMode is PromptMode.DmThenMention or PromptMode.DmOnly)
        {
            promptMessage = await TrySendDmAsync(actor, promptText, ct);
            if (promptMessage is not null)
            {
                deliveredMode = PromptMode.DmOnly;
            }
            else if (_options.PromptMode == PromptMode.DmOnly)
            {
                logger.LogWarning("DM to {ActorId} failed and PromptMode=DmOnly; not falling back.", actor.Id);
                return;
            }
        }

        if (promptMessage is null)
        {
            promptMessage = await TrySendMentionAsync(actor, stored, ct);
            if (promptMessage is not null)
            {
                deliveredMode = PromptMode.MentionOnly;
            }
        }

        if (promptMessage is null || deliveredMode is null)
        {
            logger.LogError("Could not deliver reason prompt to {ActorId} for entry {EntryId}.", actor.Id, entry.Id);
            return;
        }

        var pending = new PendingReason
        {
            AuditEntryId = stored.DiscordEntryId,
            ActorId = actor.Id,
            PromptMode = deliveredMode.Value,
            PromptMessageId = promptMessage.Id,
            PromptChannelId = promptMessage.Channel.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(_options.ReasonTimeoutHours),
            IsOpen = true,
        };
        db.PendingReasons.Add(pending);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Issued {Mode} reason prompt for entry {EntryId} to {ActorId}.",
            deliveredMode, entry.Id, actor.Id);
    }

    private async Task<IUserMessage?> TrySendDmAsync(IUser actor, string text, CancellationToken ct)
    {
        try
        {
            var dm = await actor.CreateDMChannelAsync(new RequestOptions { CancelToken = ct });
            return await dm.SendMessageAsync(text, options: new RequestOptions { CancelToken = ct });
        }
        catch (HttpException ex) when (ex.DiscordCode is DiscordErrorCode.CannotSendMessageToUser
            || (int?)ex.DiscordCode == CannotSendMessagesToUser)
        {
            logger.LogInformation("DM to {ActorId} blocked; will fall back if allowed.", actor.Id);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unexpected error DMing {ActorId}.", actor.Id);
            return null;
        }
    }

    private async Task<IUserMessage?> TrySendMentionAsync(IUser actor, AuditEntry stored, CancellationToken ct)
    {
        if (client.GetChannel(_options.AuditChannelId) is not IMessageChannel channel)
        {
            return null;
        }

        try
        {
            var reference = new MessageReference(stored.MessageId, stored.ChannelId, failIfNotExists: false);
            return await channel.SendMessageAsync(
                $"<@{actor.Id}> please reply with a reason for this action.",
                messageReference: reference,
                allowedMentions: new AllowedMentions { UserIds = [actor.Id] },
                options: new RequestOptions { CancelToken = ct });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to post mention prompt for {ActorId}.", actor.Id);
            return null;
        }
    }
}
