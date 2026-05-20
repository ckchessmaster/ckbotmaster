using CkBotMaster.AuditBot.Configuration;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// Owns the <see cref="DiscordSocketClient"/> lifecycle: login, intent configuration,
/// event subscription, and graceful shutdown. Forwards audit log events to the queue
/// and dispatches messages to the appropriate handlers.
/// </summary>
public sealed class DiscordHostedService(
    DiscordSocketClient client,
    AuditLogQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<BotOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<DiscordHostedService> logger) : IHostedService
{
    private readonly BotOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        client.Log += LogAsync;
        client.Ready += OnReadyAsync;
        client.AuditLogCreated += OnAuditLogCreatedAsync;
        client.MessageReceived += OnMessageReceivedAsync;
        client.MessageDeleted += OnMessageDeletedAsync;
        client.MessagesBulkDeleted += OnMessagesBulkDeletedAsync;

        await client.LoginAsync(TokenType.Bot, _options.Token);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            client.Log -= LogAsync;
            client.Ready -= OnReadyAsync;
            client.AuditLogCreated -= OnAuditLogCreatedAsync;
            client.MessageReceived -= OnMessageReceivedAsync;
            client.MessageDeleted -= OnMessageDeletedAsync;
            client.MessagesBulkDeleted -= OnMessagesBulkDeletedAsync;

            await client.StopAsync();
            await client.LogoutAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while stopping Discord client.");
        }
    }

    private Task LogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information,
        };
        logger.Log(level, message.Exception, "[Discord:{Source}] {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }

    private async Task OnReadyAsync()
    {
        try
        {
            var guild = client.GetGuild(_options.GuildId);
            if (guild is null)
            {
                logger.LogCritical("Configured guild {GuildId} not found; the bot is not a member.", _options.GuildId);
                lifetime.StopApplication();
                return;
            }

            logger.LogInformation("Connected to guild {GuildName} ({GuildId}) as {BotUser}.", guild.Name, guild.Id, client.CurrentUser);

            using var scope = scopeFactory.CreateScope();
            var handlers = scope.ServiceProvider.GetServices<IOnReadyHandler>();
            foreach (var handler in handlers)
            {
                try
                {
                    await handler.OnReadyAsync(guild, lifetime.ApplicationStopping);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ready handler {Handler} failed.", handler.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Ready handler.");
        }
    }

    private async Task OnAuditLogCreatedAsync(SocketAuditLogEntry entry, SocketGuild guild)
    {
        if (guild.Id != _options.GuildId)
        {
            return;
        }

        try
        {
            await queue.EnqueueAsync(entry, fromCatchup: false, lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue audit entry {EntryId}.", entry.Id);
        }
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot || message.Author.Id == client.CurrentUser.Id)
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
            await handler.HandleAsync(message, lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle message {MessageId}.", message.Id);
        }
    }

    private async Task OnMessageDeletedAsync(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        if (channel.Id != _options.AuditChannelId)
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<AuditLogProcessor>();
            await processor.RestoreAsync(channel.Id, message.Id, lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restore deleted message {MessageId}.", message.Id);
        }
    }

    private async Task OnMessagesBulkDeletedAsync(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages, Cacheable<IMessageChannel, ulong> channel)
    {
        if (channel.Id != _options.AuditChannelId)
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<AuditLogProcessor>();
            foreach (var message in messages)
            {
                await processor.RestoreAsync(channel.Id, message.Id, lifetime.ApplicationStopping);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle bulk message deletion in channel {ChannelId}.", channel.Id);
        }
    }
}
