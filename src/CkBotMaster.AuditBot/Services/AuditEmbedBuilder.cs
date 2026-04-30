using Discord;
using Discord.WebSocket;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// Builds a Discord <see cref="Embed"/> for an audit log entry.
/// </summary>
public sealed class AuditEmbedBuilder
{
    private const string NoReasonProvidedText = "*No reason provided (timed out)*";

    /// <summary>
    /// Builds the initial embed posted when the bot first sees an audit entry.
    /// </summary>
    public Embed Build(IAuditLogEntry entry, bool fromCatchup, DiscordSocketClient? client = null)
    {
        var actorMention = entry.User is null ? "*unknown*" : $"<@{entry.User.Id}>";

        var builder = new EmbedBuilder()
            .WithTitle(FormatActionTitle(entry.Action))
            .WithColor(GetColor(entry.Action))
            .WithTimestamp(entry.CreatedAt)
            .AddField("Actor", actorMention, inline: true)
            .AddField("Action", entry.Action.ToString(), inline: true)
            .WithFooter($"Audit entry {entry.Id}{(fromCatchup ? " · captured during catch-up" : string.Empty)}");

        // Add detail fields from the audit log data object.
        if (client is not null)
        {
            var details = AuditEntryDetailExtractor.Extract(entry, client);
            foreach (var (label, value) in details)
            {
                builder.AddField(label, Truncate(value, 1024), inline: true);
            }
        }

        if (!string.IsNullOrWhiteSpace(entry.Reason))
        {
            builder.AddField("Reason", Truncate(entry.Reason, 1000));
        }

        return builder.Build();
    }

    /// <summary>
    /// Returns a copy of <paramref name="original"/> with the reason field set/replaced.
    /// </summary>
    public Embed WithReason(IEmbed original, string reason)
    {
        var builder = original.ToEmbedBuilder();
        var existing = builder.Fields.FindIndex(f => f.Name == "Reason");
        var value = Truncate(reason, 1000);
        if (existing >= 0)
        {
            builder.Fields[existing].Value = value;
        }
        else
        {
            builder.AddField("Reason", value);
        }
        return builder.Build();
    }

    /// <summary>
    /// Marks the embed as timed out (reason not provided in time).
    /// </summary>
    public Embed WithTimeoutReason(IEmbed original) => WithReason(original, NoReasonProvidedText);

    private static string FormatActionTitle(ActionType action) => action switch
    {
        ActionType.Ban => "🔨 Member banned",
        ActionType.Unban => "🕊️ Member unbanned",
        ActionType.Kick => "👢 Member kicked",
        ActionType.RoleCreated => "➕ Role created",
        ActionType.RoleDeleted => "➖ Role deleted",
        ActionType.RoleUpdated => "✏️ Role updated",
        ActionType.ChannelCreated => "➕ Channel created",
        ActionType.ChannelDeleted => "➖ Channel deleted",
        ActionType.ChannelUpdated => "✏️ Channel updated",
        ActionType.OverwriteCreated => "🔐 Permission overwrite created",
        ActionType.OverwriteUpdated => "🔐 Permission overwrite updated",
        ActionType.OverwriteDeleted => "🔓 Permission overwrite removed",
        ActionType.MemberRoleUpdated => "🛡️ Member roles changed",
        ActionType.MessageDeleted => "🗑️ Message deleted",
        ActionType.MessageBulkDeleted => "🗑️ Messages bulk-deleted",
        _ => $"📝 {action}",
    };

    private static Color GetColor(ActionType action) => action switch
    {
        ActionType.Ban or ActionType.Kick or ActionType.RoleDeleted or ActionType.ChannelDeleted
            or ActionType.OverwriteDeleted or ActionType.MessageBulkDeleted => Color.Red,
        ActionType.Unban or ActionType.RoleCreated or ActionType.ChannelCreated
            or ActionType.OverwriteCreated => Color.Green,
        ActionType.RoleUpdated or ActionType.ChannelUpdated or ActionType.OverwriteUpdated
            or ActionType.MemberRoleUpdated => Color.Orange,
        _ => Color.LightGrey,
    };

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..(max - 1)] + "…";
}
