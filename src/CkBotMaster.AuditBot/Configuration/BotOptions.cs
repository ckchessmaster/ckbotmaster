using System.ComponentModel.DataAnnotations;
using Discord;

namespace CkBotMaster.AuditBot.Configuration;

/// <summary>
/// Bot configuration bound from the <c>Bot</c> section of configuration
/// (appsettings.json + environment variables). Discord credentials and
/// guild/channel ids are required; everything else has sensible defaults.
/// </summary>
public sealed class BotOptions
{
    public const string SectionName = "Bot";

    /// <summary>Discord bot token. Supplied via secret/env, never committed.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Token { get; set; } = string.Empty;

    /// <summary>Snowflake id of the guild to monitor.</summary>
    [Range(1, ulong.MaxValue)]
    public ulong GuildId { get; set; }

    /// <summary>Snowflake id of the channel where audit log entries are posted.</summary>
    [Range(1, ulong.MaxValue)]
    public ulong AuditChannelId { get; set; }

    /// <summary>How long an unanswered reason prompt remains open before being marked as timed-out.</summary>
    [Range(1, 24 * 30)]
    public int ReasonTimeoutHours { get; set; } = 24;

    /// <summary>Default prompt mode for major events.</summary>
    public PromptMode PromptMode { get; set; } = PromptMode.DmThenMention;

    /// <summary>
    /// Audit log action types that require a reason from the actor.
    /// Values must parse to <see cref="ActionType"/> members.
    /// </summary>
    public string[] MajorEventTypes { get; set; } =
    [
        nameof(ActionType.Ban),
        nameof(ActionType.Unban),
        nameof(ActionType.Kick),
        nameof(ActionType.RoleCreated),
        nameof(ActionType.RoleDeleted),
        nameof(ActionType.RoleUpdated),
        nameof(ActionType.ChannelDeleted),
        nameof(ActionType.OverwriteCreated),
        nameof(ActionType.OverwriteUpdated),
        nameof(ActionType.OverwriteDeleted),
        nameof(ActionType.MemberRoleUpdated),
    ];

    /// <summary>
    /// Audit log action types to completely ignore (not posted to the audit channel at all).
    /// Values must parse to <see cref="ActionType"/> members.
    /// </summary>
    public string[] ExcludedEventTypes { get; set; } =
    [
        nameof(ActionType.VoiceChannelStatusUpdated),
        nameof(ActionType.VoiceChannelStatusDeleted),
        nameof(ActionType.ThreadCreate)
    ];

    /// <summary>
    /// On startup, scan recent messages in the audit channel and delete
    /// any non-bot messages that are not associated with an open prompt.
    /// </summary>
    public bool CleanChannelOnStartup { get; set; } = true;
}
