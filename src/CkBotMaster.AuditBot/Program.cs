using CkBotMaster.AuditBot.Configuration;
using CkBotMaster.AuditBot.Data;
using CkBotMaster.AuditBot.Services;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Bind + validate Bot options.
builder.Services
    .AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Aspire-injected Postgres. Connection string name "auditdb" matches AppHost.
builder.AddNpgsqlDbContext<AuditDbContext>("auditdb");

// Discord client (singleton).
builder.Services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
        | GatewayIntents.GuildMembers
        | GatewayIntents.GuildBans
        | GatewayIntents.GuildMessages
        | GatewayIntents.MessageContent
        | GatewayIntents.DirectMessages,
    LogLevel = LogSeverity.Info,
    AlwaysDownloadUsers = false,
    MessageCacheSize = 0,
}));

// Audit pipeline.
builder.Services.AddSingleton<AuditLogQueue>();
builder.Services.AddSingleton<AuditEmbedBuilder>();
builder.Services.AddScoped<AuditLogProcessor>();
builder.Services.AddScoped<IReasonPromptService, ReasonPromptService>();
builder.Services.AddScoped<IMessageDispatcher, MessageDispatcher>();

// Ready handlers run once when the Discord client connects.
builder.Services.AddScoped<IOnReadyHandler, CatchupService>();
builder.Services.AddScoped<IOnReadyHandler, ChannelCleaner>();

// Hosted services. Order matters: queue consumer must be running before Discord client emits events.
builder.Services.AddHostedService<AuditLogQueueConsumer>();
builder.Services.AddHostedService<ReasonTimeoutWorker>();
builder.Services.AddHostedService<DiscordHostedService>();

var app = builder.Build();

app.UseRequestTimeouts();
app.UseOutputCache();
app.MapDefaultEndpoints();

// Apply pending migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await db.Database.MigrateAsync();
}

// Force options validation now so misconfiguration fails fast before Discord login.
_ = app.Services.GetRequiredService<IOptions<BotOptions>>().Value;

await app.RunAsync();
