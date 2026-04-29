var builder = DistributedApplication.CreateBuilder(args);

// Secrets/config — populate via user-secrets:
//   dotnet user-secrets set "Parameters:BotToken" "..." --project src/CkBotMaster.AppHost
//   dotnet user-secrets set "Parameters:GuildId" "123" --project src/CkBotMaster.AppHost
//   dotnet user-secrets set "Parameters:AuditChannelId" "456" --project src/CkBotMaster.AppHost
var botToken = builder.AddParameter("BotToken", secret: true);
var guildId = builder.AddParameter("GuildId");
var auditChannelId = builder.AddParameter("AuditChannelId");

// Containerized Postgres with persistent data volume + pgAdmin for inspection.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("ckbotmaster-pgdata")
    .WithPgAdmin();

var auditDb = postgres.AddDatabase("auditdb");

builder.AddProject<Projects.CkBotMaster_AuditBot>("auditbot")
    .WithReference(auditDb)
    .WaitFor(auditDb)
    .WithEnvironment("Bot__Token", botToken)
    .WithEnvironment("Bot__GuildId", guildId)
    .WithEnvironment("Bot__AuditChannelId", auditChannelId);

builder.Build().Run();
