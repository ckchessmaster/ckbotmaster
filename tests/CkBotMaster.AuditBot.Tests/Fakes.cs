using Discord;
using NSubstitute;

namespace CkBotMaster.AuditBot.Tests;

internal static class FakeUser
{
    public static IUser Create(ulong id, string username = "tester")
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(id);
        user.Username.Returns(username);
        return user;
    }
}

internal static class FakeAuditLogEntryFactory
{
    public static IAuditLogEntry Create(
        ulong id,
        ActionType action,
        IUser? user,
        string? reason,
        DateTimeOffset createdAt)
    {
        var entry = Substitute.For<IAuditLogEntry>();
        entry.Id.Returns(id);
        entry.Action.Returns(action);
        entry.User.Returns(user);
        entry.Reason.Returns(reason);
        entry.CreatedAt.Returns(createdAt);
        return entry;
    }
}
