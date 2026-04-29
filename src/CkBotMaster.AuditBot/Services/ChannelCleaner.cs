using CkBotMaster.AuditBot.Configuration;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// On startup, scans recent messages in the audit channel and deletes any non-bot
/// messages. This catches anything posted while the bot was offline.
/// </summary>
public sealed class ChannelCleaner(
    DiscordSocketClient client,
    IOptions<BotOptions> options,
    ILogger<ChannelCleaner> logger) : IOnReadyHandler
{
    private const int ScanLimit = 100;
    private readonly BotOptions _options = options.Value;

    public async Task OnReadyAsync(SocketGuild guild, CancellationToken ct)
    {
        if (!_options.CleanChannelOnStartup)
        {
            return;
        }

        if (client.GetChannel(_options.AuditChannelId) is not IMessageChannel channel)
        {
            logger.LogWarning("Audit channel {ChannelId} not available for cleanup.", _options.AuditChannelId);
            return;
        }

        var deleted = 0;
        try
        {
            await foreach (var page in channel.GetMessagesAsync(ScanLimit,
                options: new RequestOptions { CancelToken = ct }).WithCancellation(ct))
            {
                foreach (var msg in page)
                {
                    if (msg.Author.Id == client.CurrentUser.Id || msg.Author.IsBot)
                    {
                        continue;
                    }

                    try
                    {
                        await msg.DeleteAsync(new RequestOptions { CancelToken = ct });
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to delete message {MessageId} during cleanup.", msg.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during channel cleanup.");
        }

        if (deleted > 0)
        {
            logger.LogInformation("Cleaned {Count} stale message(s) from audit channel.", deleted);
        }
    }
}
