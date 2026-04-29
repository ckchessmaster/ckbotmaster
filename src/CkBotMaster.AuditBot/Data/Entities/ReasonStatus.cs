namespace CkBotMaster.AuditBot.Data.Entities;

/// <summary>
/// Status lifecycle of a reason prompt for a major audit event.
/// </summary>
public enum ReasonStatus
{
    /// <summary>Event is not classified as major; no reason expected.</summary>
    NotRequired = 0,

    /// <summary>Reason has been requested, awaiting the actor's response.</summary>
    Pending = 1,

    /// <summary>Actor provided a reason which has been recorded.</summary>
    Provided = 2,

    /// <summary>Prompt expired before a reason was provided.</summary>
    TimedOut = 3,
}
