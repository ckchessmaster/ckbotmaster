# Gemini CLI Project Context: CkBotMaster

## Project Overview
CkBotMaster is a Discord bot designed to mirror a guild's audit logs to a dedicated channel and proactively capture reasons for "major" events (e.g., bans, kicks, permission changes) from the responsible actors.

**Main Technologies:**
- **Runtime:** .NET 10
- **Library:** [Discord.Net](https://github.com/discord-net/Discord.Net) for Discord API interaction.
- **Persistence:** EF Core with PostgreSQL.
- **Orchestration:** .NET Aspire for local development (database containers, orchestration).
- **Deployment:** Containerized (Docker) and Kubernetes (singleton Deployment).

**Architecture:**
- **CkBotMaster.AuditBot:** The core worker service (Hosted Service). It processes audit logs via a queue-based consumer pattern.
- **CkBotMaster.AppHost:** The .NET Aspire project used for local development orchestration.
- **CkBotMaster.ServiceDefaults:** Shared project for OpenTelemetry, health checks, and service discovery.

---

## Building and Running

### Prerequisites
- .NET 10 SDK
- Docker Desktop / Podman (for Aspire local Postgres)
- Discord Bot Token + Client ID

### Local Development (Aspire)
1. **Set Secrets:**
   ```powershell
   dotnet user-secrets set "Parameters:BotToken" "YOUR_TOKEN" --project src/CkBotMaster.AppHost
   dotnet user-secrets set "Parameters:GuildId" "GUILD_ID" --project src/CkBotMaster.AppHost
   dotnet user-secrets set "Parameters:AuditChannelId" "CHANNEL_ID" --project src/CkBotMaster.AppHost
   ```
2. **Run:**
   ```powershell
   dotnet run --project src/CkBotMaster.AppHost
   ```

### Running Tests
```powershell
dotnet test
```

### Building Container
```powershell
docker build -f deploy/Dockerfile -t ckbotmaster-auditbot:latest .
```

---

## Development Conventions

### Coding Standards
- **Nullable Reference Types:** Enabled project-wide.
- **Implicit Usings:** Enabled project-wide.
- **Strict Build:** `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are set to `true` in `Directory.Build.props`.
- **Dependency Injection:** Extensive use of Scoped and Singleton services via Microsoft.Extensions.DependencyInjection.

### Testing Practices
- **Framework:** xUnit.
- **Mocking:** [NSubstitute](https://nsubstitute.github.io/) is used for creating fakes/mocks (see `tests/CkBotMaster.AuditBot.Tests/Fakes.cs`).
- **Mandatory Coverage:** All new business logic or bug fixes **must** be accompanied by unit tests. If a service is difficult to test, consider refactoring it for better testability (e.g., using interfaces or wrappers for external dependencies like Discord.Net).
- **Mutation Testing:** When writing new tests, you **must** perform mutation testing. Manually introduce a breaking change into the business logic to verify that the corresponding test fails as expected. This confirms the test is providing high-signal validation.
- **Patterns:** Tests follow the AAA (Arrange, Act, Assert) pattern and focus on unit testing service logic (e.g., `AuditEmbedBuilderTests`).

### Architecture Patterns
- **Hosted Services:** Background logic (Discord connection, Queue consumption, Timeout monitoring) is implemented as `IHostedService` / `BackgroundService`.
- **Queueing:** `AuditLogQueue` acts as an in-memory buffer between the Discord Gateway events and the processing logic.
- **Idempotency & Restoration:** `AuditLogProcessor` checks `DiscordEntryId` in the database before processing to prevent duplicate posts. It also handles `RestoreAsync` to re-post audit logs if they are deleted from the audit channel, using the `MessageId` index for lookup.

### Key Files for Investigation
- `src/CkBotMaster.AuditBot/Services/AuditLogProcessor.cs`: Core business logic for handling audit entries and restoring deleted messages.
- `src/CkBotMaster.AuditBot/Configuration/BotOptions.cs`: Definition of all configurable bot parameters.
- `src/CkBotMaster.AuditBot/Data/AuditDbContext.cs`: EF Core context for audit entries and bot state.
- `src/CkBotMaster.AppHost/AppHost.cs`: Local orchestration and service wiring.
