namespace CkBotMaster.AuditBot.Data.Entities;

/// <summary>
/// One row per audit log entry that has been mirrored to the audit channel.
/// </summary>
public sealed class AuditEntry
{
    /// <summary>Discord audit log entry id (snowflake). Primary key.</summary>
    public ulong DiscordEntryId { get; set; }

    /// <summary>Snowflake id of the message posted in the audit channel.</summary>
    public ulong MessageId { get; set; }

    /// <summary>Snowflake id of the channel the embed was posted in.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>Discord <see cref="Discord.ActionType"/> value as string for forward compatibility.</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>Snowflake id of the actor who performed the action; null if unknown (rare system events).</summary>
    public ulong? ActorId { get; set; }

    /// <summary>UTC timestamp of the audit entry.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Whether this entry was processed during catch-up rather than live.</summary>
    public bool FromCatchup { get; set; }

    /// <summary>Status of the reason workflow for this entry.</summary>
    public ReasonStatus ReasonStatus { get; set; }

    /// <summary>Captured reason text, when <see cref="ReasonStatus"/> is <see cref="ReasonStatus.Provided"/>.</summary>
    public string? ReasonText { get; set; }
}
