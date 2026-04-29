namespace CkBotMaster.AuditBot.Configuration;

/// <summary>
/// How the bot prompts an actor for the reason behind a major audit event.
/// </summary>
public enum PromptMode
{
    /// <summary>Try DM first, fall back to an @mention reply in the audit channel if DMs fail.</summary>
    DmThenMention = 0,

    /// <summary>Always DM the actor; do not fall back.</summary>
    DmOnly = 1,

    /// <summary>Always @mention the actor in the audit channel.</summary>
    MentionOnly = 2,
}
