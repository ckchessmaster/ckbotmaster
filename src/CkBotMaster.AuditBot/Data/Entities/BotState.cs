namespace CkBotMaster.AuditBot.Data.Entities;

/// <summary>
/// Simple key/value table for bot state that doesn't warrant its own entity
/// (e.g. last seen audit log id for catch-up).
/// </summary>
public sealed class BotState
{
    public const string LastSeenAuditEntryIdKey = "LastSeenAuditEntryId";

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
