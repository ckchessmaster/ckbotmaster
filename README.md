# ckbotmaster

A Discord bot that mirrors a guild's audit log to a dedicated channel, asks actors of major events for a reason (DM first, falling back to an @mention), and edits the audit-log post once a reason is captured. Built on .NET 10 + Discord.Net + EF Core (PostgreSQL), orchestrated locally with .NET Aspire, and packaged for Kubernetes.

## Features

- Mirrors every audit log entry into a dedicated audit channel.
- For configured "major" events (bans, kicks, role/channel/permission changes), DMs the actor for a reason; falls back to an @mention reply in the audit channel if DMs are blocked.
- Edits the original audit message with the captured reason. Marks unanswered prompts as "No reason provided" after a configurable timeout (default 24h).
- Keeps the audit channel clean — deletes any non-bot message.
- Catches up missed audit entries on startup using the last seen entry id; safe across restarts and offline periods.
- Postgres-backed state via EF Core; .NET Aspire spins up the database container locally with one command.
- Containerized; ships with Kubernetes manifests (singleton Deployment, ConfigMap, Secret template).

## Solution layout

| Project | Purpose |
| --- | --- |
| `src/CkBotMaster.AppHost` | .NET Aspire AppHost — local dev orchestration only (Postgres + bot). Not deployed. |
| `src/CkBotMaster.ServiceDefaults` | Aspire shared defaults (OpenTelemetry, health checks, resilience). |
| `src/CkBotMaster.AuditBot` | The worker service. This is the only project packaged for production. |
| `tests/CkBotMaster.AuditBot.Tests` | xUnit tests. |
| `deploy/Dockerfile` | Multi-stage build for the worker. |
| `deploy/k8s/` | Kubernetes manifests. |

## Prerequisites

- .NET 10 SDK
- Docker Desktop (or Podman) — required by Aspire to run the local Postgres container
- A Discord application + bot token with the privileged intents `Server Members Intent` and `Message Content Intent` enabled in the Developer Portal
- Bot must have the **View Audit Log**, **Send Messages**, **Manage Messages**, **Read Message History**, and **Embed Links** permissions in the audit channel

## Configuration

Configuration is bound from the `Bot` section of `appsettings.json` plus environment variables (env vars use the `Bot__` prefix with double-underscore separators).

| Key | Description | Default |
| --- | --- | --- |
| `Bot:Token` | Discord bot token (secret) | _required_ |
| `Bot:GuildId` | Snowflake of the guild to monitor | _required_ |
| `Bot:AuditChannelId` | Snowflake of the audit channel | _required_ |
| `Bot:ReasonTimeoutHours` | Hours before an unanswered prompt times out | `24` |
| `Bot:PromptMode` | `DmThenMention`, `DmOnly`, or `MentionOnly` | `DmThenMention` |
| `Bot:MajorEventTypes` | List of `ActionType` names that trigger a reason prompt | sensible defaults (Ban, Kick, role/channel/perm changes…) |
| `Bot:ExcludedEventTypes` | List of `ActionType` names to completely ignore (not posted) | VoiceChannelStatusUpdated/Deleted, Thread*, Stage* |
| `Bot:CleanChannelOnStartup` | Sweep the audit channel for stale non-bot messages on startup | `true` |
| `ConnectionStrings:auditdb` | PostgreSQL connection string | _injected by Aspire locally; secret in K8s_ |

## Run locally with Aspire

Set the bot secrets on the AppHost (these are stored via `dotnet user-secrets`, never committed):

```powershell
dotnet user-secrets set "Parameters:BotToken" "YOUR_DISCORD_TOKEN" --project src/CkBotMaster.AppHost
dotnet user-secrets set "Parameters:GuildId" "123456789012345678" --project src/CkBotMaster.AppHost
dotnet user-secrets set "Parameters:AuditChannelId" "234567890123456789" --project src/CkBotMaster.AppHost
```

Then start the whole stack:

```powershell
dotnet run --project src/CkBotMaster.AppHost
```

Or, in VS Code, press <kbd>F5</kbd> and choose **Run AppHost (Aspire, recommended)** — the workspace ships with `.vscode/launch.json` and `.vscode/tasks.json` configured for both the Aspire AppHost and a direct AuditBot launch.

The Aspire dashboard URL is printed in the console. From there you can watch logs for the bot, hit pgAdmin to inspect the database, etc. Postgres data is persisted to a named Docker volume (`ckbotmaster-pgdata`), so catch-up still works across restarts.

## Run tests

```powershell
dotnet test
```

## Build the container

```powershell
docker build -f deploy/Dockerfile -t ghcr.io/your-org/ckbotmaster-auditbot:latest .
```

## Deploy to Kubernetes

1. Provision a PostgreSQL database (managed service or in-cluster — outside the scope of this repo).
2. Copy `deploy/k8s/secret.example.yaml` to `secret.yaml`, fill in the bot token, guild/channel ids, and Postgres connection string. Do **not** commit it.
3. Apply:

```powershell
kubectl apply -f deploy/k8s/deployment.yaml
kubectl apply -f secret.yaml
```

The Deployment is intentionally limited to **one replica** — running multiple replicas would cause duplicate audit messages.

## How the catch-up works

On every `Ready`, the bot reads `bot_state.LastSeenAuditEntryId`, pages backwards through the guild's audit log, and replays any entries newer than that id — capped at the **last 24 hours**. Anything older than the cutoff is skipped (the bot will resume live coverage from now). Replayed embeds are tagged with "captured during catch-up" in the footer, and reason prompts are still issued (the actor can answer at any time within the timeout window).

If there is no prior state (first run), the current head of the audit log is recorded as the baseline and no historical entries are replayed.