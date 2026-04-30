using Discord;
using Discord.WebSocket;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// Extracts human-readable detail strings from <see cref="IAuditLogEntry.Data"/>
/// for each known audit log action type.
/// </summary>
public static class AuditEntryDetailExtractor
{
    /// <summary>
    /// Returns one or more detail lines describing the audit log entry's data,
    /// or an empty list if the data type is unrecognized or has no useful detail.
    /// </summary>
    public static List<(string Label, string Value)> Extract(IAuditLogEntry entry, DiscordSocketClient client)
    {
        var details = new List<(string, string)>();
        var data = entry.Data;
        if (data is null)
        {
            return details;
        }

        switch (data)
        {
            case SocketBanAuditLogData ban:
                details.Add(("Banned user", UserMention(ban.Target.Id)));
                break;

            case SocketUnbanAuditLogData unban:
                details.Add(("Unbanned user", UserMention(unban.Target.Id)));
                break;

            case SocketKickAuditLogData kick:
                details.Add(("Kicked user", UserMention(kick.Target.Id)));
                break;

            case SocketMemberMoveAuditLogData move:
                details.Add(("Channel", ChannelMention(move.ChannelId)));
                details.Add(("Members moved", move.MemberCount.ToString()));
                break;

            case SocketMemberDisconnectAuditLogData disconnect:
                details.Add(("Members disconnected", disconnect.MemberCount.ToString()));
                break;

            case SocketMemberUpdateAuditLogData memberUpdate:
                details.Add(("Member", UserMention(memberUpdate.Target.Id)));
                AddMemberChanges(details, memberUpdate);
                break;

            case SocketMemberRoleAuditLogData roleChange:
                details.Add(("Member", UserMention(roleChange.Target.Id)));
                AddRoleChanges(details, roleChange);
                break;

            case SocketChannelCreateAuditLogData chCreate:
                details.Add(("Channel", $"{ChannelMention(chCreate.ChannelId)} (`{chCreate.ChannelName}`, {chCreate.ChannelType})"));
                break;

            case SocketChannelDeleteAuditLogData chDelete:
                details.Add(("Channel", $"`{chDelete.ChannelName}` ({chDelete.ChannelType})"));
                break;

            case SocketChannelUpdateAuditLogData chUpdate:
                details.Add(("Channel", ChannelMention(chUpdate.ChannelId)));
                AddChannelChanges(details, chUpdate);
                break;

            case SocketRoleCreateAuditLogData roleCreate:
                details.Add(("Role", RoleMention(roleCreate.RoleId)));
                if (roleCreate.Properties.Name is not null)
                {
                    details.Add(("Name", roleCreate.Properties.Name));
                }
                break;

            case SocketRoleDeleteAuditLogData roleDelete:
                details.Add(("Role", $"`{roleDelete.Properties.Name ?? roleDelete.RoleId.ToString()}`"));
                break;

            case SocketRoleUpdateAuditLogData roleUpdate:
                details.Add(("Role", RoleMention(roleUpdate.RoleId)));
                AddRolePropertyChanges(details, roleUpdate);
                break;

            case SocketOverwriteCreateAuditLogData owCreate:
                details.Add(("Channel", ChannelMention(owCreate.ChannelId)));
                details.Add(("Target", FormatOverwriteTarget(owCreate.Overwrite)));
                details.Add(("Allow", owCreate.Overwrite.Permissions.AllowValue.ToString()));
                details.Add(("Deny", owCreate.Overwrite.Permissions.DenyValue.ToString()));
                break;

            case SocketOverwriteUpdateAuditLogData owUpdate:
                details.Add(("Channel", ChannelMention(owUpdate.ChannelId)));
                details.Add(("Target", $"{owUpdate.OverwriteType} `{owUpdate.OverwriteTargetId}`"));
                if (owUpdate.OldPermissions.AllowValue != owUpdate.NewPermissions.AllowValue)
                {
                    details.Add(("Allow", $"`{owUpdate.OldPermissions.AllowValue}` → `{owUpdate.NewPermissions.AllowValue}`"));
                }
                if (owUpdate.OldPermissions.DenyValue != owUpdate.NewPermissions.DenyValue)
                {
                    details.Add(("Deny", $"`{owUpdate.OldPermissions.DenyValue}` → `{owUpdate.NewPermissions.DenyValue}`"));
                }
                break;

            case SocketOverwriteDeleteAuditLogData owDelete:
                details.Add(("Channel", ChannelMention(owDelete.ChannelId)));
                details.Add(("Target", FormatOverwriteTarget(owDelete.Overwrite)));
                break;

            case SocketMessageDeleteAuditLogData msgDelete:
                details.Add(("Channel", ChannelMention(msgDelete.ChannelId)));
                details.Add(("Messages deleted", msgDelete.MessageCount.ToString()));
                details.Add(("Author", UserMention(msgDelete.Target.Id)));
                break;

            case SocketMessageBulkDeleteAuditLogData bulkDelete:
                details.Add(("Messages deleted", bulkDelete.MessageCount.ToString()));
                details.Add(("Channel", ChannelMention(bulkDelete.ChannelId)));
                break;

            case SocketInviteCreateAuditLogData invCreate:
                details.Add(("Code", $"`{invCreate.Code}`"));
                details.Add(("Channel", ChannelMention(invCreate.ChannelId)));
                if (invCreate.MaxUses > 0)
                {
                    details.Add(("Max uses", invCreate.MaxUses.ToString()));
                }
                break;

            case SocketInviteDeleteAuditLogData invDelete:
                details.Add(("Code", $"`{invDelete.Code}`"));
                details.Add(("Channel", ChannelMention(invDelete.ChannelId)));
                break;

            case SocketWebhookCreateAuditLogData whCreate:
                details.Add(("Webhook", $"`{whCreate.Name}` in {ChannelMention(whCreate.ChannelId)}"));
                break;

            case SocketWebhookDeletedAuditLogData whDelete:
                details.Add(("Webhook", $"`{whDelete.Name}` in {ChannelMention(whDelete.ChannelId)}"));
                break;

            case SocketPruneAuditLogData prune:
                details.Add(("Members pruned", prune.MembersRemoved.ToString()));
                details.Add(("Prune days", prune.PruneDays.ToString()));
                break;

            case SocketBotAddAuditLogData botAdd:
                details.Add(("Bot", UserMention(botAdd.Target.Id)));
                break;

            case SocketMessagePinAuditLogData pin:
                details.Add(("Channel", ChannelMention(pin.ChannelId)));
                details.Add(("Message", pin.MessageId.ToString()));
                break;

            case SocketMessageUnpinAuditLogData unpin:
                details.Add(("Channel", ChannelMention(unpin.ChannelId)));
                details.Add(("Message", unpin.MessageId.ToString()));
                break;
        }

        return details;
    }

    private static void AddMemberChanges(
        List<(string, string)> details, SocketMemberUpdateAuditLogData data)
    {
        var before = data.Before;
        var after = data.After;

        if (before.Nickname != after.Nickname)
        {
            details.Add(("Nickname", $"`{before.Nickname ?? "(none)"}` → `{after.Nickname ?? "(none)"}`"));
        }
        if (before.Deaf != after.Deaf)
        {
            details.Add(("Server deafened", $"`{before.Deaf}` → `{after.Deaf}`"));
        }
        if (before.Mute != after.Mute)
        {
            details.Add(("Server muted", $"`{before.Mute}` → `{after.Mute}`"));
        }
        if (before.TimedOutUntil != after.TimedOutUntil)
        {
            details.Add(("Timed out until", $"`{FormatTimestamp(before.TimedOutUntil)}` → `{FormatTimestamp(after.TimedOutUntil)}`"));
        }
    }

    private static void AddRoleChanges(
        List<(string, string)> details, SocketMemberRoleAuditLogData data)
    {
        var added = data.Roles.Where(r => r.Added).Select(r => $"<@&{r.RoleId}>").ToList();
        var removed = data.Roles.Where(r => r.Removed).Select(r => $"<@&{r.RoleId}>").ToList();

        if (added.Count > 0)
        {
            details.Add(("Roles added", string.Join(", ", added)));
        }
        if (removed.Count > 0)
        {
            details.Add(("Roles removed", string.Join(", ", removed)));
        }
    }

    private static void AddChannelChanges(
        List<(string, string)> details, SocketChannelUpdateAuditLogData data)
    {
        var before = data.Before;
        var after = data.After;

        if (before.Name != after.Name)
        {
            details.Add(("Name", $"`{before.Name}` → `{after.Name}`"));
        }
        if (before.Topic != after.Topic)
        {
            details.Add(("Topic", $"`{Truncate(before.Topic ?? "(none)", 100)}` → `{Truncate(after.Topic ?? "(none)", 100)}`"));
        }
        if (before.IsNsfw != after.IsNsfw)
        {
            details.Add(("NSFW", $"`{before.IsNsfw}` → `{after.IsNsfw}`"));
        }
        if (before.SlowModeInterval != after.SlowModeInterval)
        {
            details.Add(("Slow mode", $"`{before.SlowModeInterval}s` → `{after.SlowModeInterval}s`"));
        }
        if (before.Bitrate != after.Bitrate)
        {
            details.Add(("Bitrate", $"`{before.Bitrate}` → `{after.Bitrate}`"));
        }
        if (before.UserLimit != after.UserLimit)
        {
            details.Add(("User limit", $"`{before.UserLimit}` → `{after.UserLimit}`"));
        }
    }

    private static void AddRolePropertyChanges(
        List<(string, string)> details, SocketRoleUpdateAuditLogData data)
    {
        var before = data.Before;
        var after = data.After;

        if (before.Name != after.Name)
        {
            details.Add(("Name", $"`{before.Name}` → `{after.Name}`"));
        }
        if (before.Colors != after.Colors)
        {
            details.Add(("Color", $"`{before.Colors}` → `{after.Colors}`"));
        }
        if (before.Hoist != after.Hoist)
        {
            details.Add(("Hoisted", $"`{before.Hoist}` → `{after.Hoist}`"));
        }
        if (before.Mentionable != after.Mentionable)
        {
            details.Add(("Mentionable", $"`{before.Mentionable}` → `{after.Mentionable}`"));
        }
        if (before.Permissions?.RawValue != after.Permissions?.RawValue)
        {
            details.Add(("Permissions changed", "yes"));
        }
    }

    private static string ChannelMention(ulong id) => $"<#{id}>";
    private static string RoleMention(ulong id) => $"<@&{id}>";
    private static string UserMention(ulong id) => $"<@{id}>";

    private static string FormatOverwriteTarget(Overwrite ow) =>
        ow.TargetType == PermissionTarget.Role
            ? $"Role <@&{ow.TargetId}>"
            : $"User <@{ow.TargetId}>";

    private static string FormatTimestamp(DateTimeOffset? ts) =>
        ts?.ToString("u") ?? "(none)";

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..(max - 1)] + "…";
}
