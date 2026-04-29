using CkBotMaster.AuditBot.Configuration;

namespace CkBotMaster.AuditBot.Data.Entities;

/// <summary>
/// Tracks an outstanding reason prompt the bot has issued to an actor for a major audit event.
/// Closed when the actor replies (status -> Provided) or the timeout elapses (status -> TimedOut).
/// </summary>
public sealed class PendingReason
{
    public long Id { get; set; }

    /// <summary>FK to the audit entry that triggered this prompt.</summary>
    public ulong AuditEntryId { get; set; }
    public AuditEntry? AuditEntry { get; set; }

    /// <summary>Snowflake id of the actor expected to respond.</summary>
    public ulong ActorId { get; set; }

    /// <summary>How the prompt was delivered.</summary>
    public PromptMode PromptMode { get; set; }

    /// <summary>Snowflake id of the prompt message (DM or audit-channel reply).</summary>
    public ulong PromptMessageId { get; set; }

    /// <summary>Snowflake id of the channel the prompt was posted in (DM channel or audit channel).</summary>
    public ulong PromptChannelId { get; set; }

    /// <summary>UTC time the prompt was issued.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC time after which this prompt is considered timed-out.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Open prompts are <c>true</c>; closed (provided or timed-out) prompts are <c>false</c>.</summary>
    public bool IsOpen { get; set; } = true;
}
