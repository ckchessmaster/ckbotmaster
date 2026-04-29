using CkBotMaster.AuditBot.Services;
using Discord;

namespace CkBotMaster.AuditBot.Tests;

public class AuditEmbedBuilderTests
{
    private static IAuditLogEntry MakeEntry(
        ulong id = 1234567890UL,
        ActionType action = ActionType.Ban,
        ulong userId = 42UL,
        string? reason = null)
        => FakeAuditLogEntryFactory.Create(id, action, FakeUser.Create(userId), reason, DateTimeOffset.UtcNow);

    [Fact]
    public void Build_PutsActionInTitle_AndIncludesFooterEntryId()
    {
        var embed = new AuditEmbedBuilder().Build(MakeEntry(), fromCatchup: false);

        Assert.Contains("banned", embed.Title);
        Assert.NotNull(embed.Footer);
        Assert.Contains("1234567890", embed.Footer!.Value.Text);
        Assert.DoesNotContain("catch-up", embed.Footer.Value.Text);
        Assert.Contains(embed.Fields, f => f.Name == "Actor" && f.Value.Contains("<@42>"));
    }

    [Fact]
    public void Build_FromCatchup_IncludesNoteInFooter()
    {
        var embed = new AuditEmbedBuilder().Build(MakeEntry(action: ActionType.Kick), fromCatchup: true);
        Assert.Contains("catch-up", embed.Footer!.Value.Text);
    }

    [Fact]
    public void Build_IncludesReasonField_WhenAuditLogReasonAlreadyPresent()
    {
        var embed = new AuditEmbedBuilder().Build(MakeEntry(reason: "raid prevention"), fromCatchup: false);
        var reasonField = Assert.Single(embed.Fields, f => f.Name == "Reason");
        Assert.Equal("raid prevention", reasonField.Value);
    }

    [Fact]
    public void WithReason_AddsField_WhenNonePresent()
    {
        var sut = new AuditEmbedBuilder();
        var initial = sut.Build(MakeEntry(action: ActionType.Kick), false);
        Assert.DoesNotContain(initial.Fields, f => f.Name == "Reason");

        var updated = sut.WithReason(initial, "spam");

        var field = Assert.Single(updated.Fields, f => f.Name == "Reason");
        Assert.Equal("spam", field.Value);
    }

    [Fact]
    public void WithReason_ReplacesExistingField()
    {
        var sut = new AuditEmbedBuilder();
        var initial = sut.Build(MakeEntry(reason: "old"), false);

        var updated = sut.WithReason(initial, "new reason");

        var fields = updated.Fields.Where(f => f.Name == "Reason").ToList();
        Assert.Single(fields);
        Assert.Equal("new reason", fields[0].Value);
    }

    [Fact]
    public void WithTimeoutReason_MarksAsNoReasonProvided()
    {
        var sut = new AuditEmbedBuilder();
        var initial = sut.Build(MakeEntry(), false);

        var updated = sut.WithTimeoutReason(initial);

        var reason = Assert.Single(updated.Fields, f => f.Name == "Reason").Value;
        Assert.Contains("No reason provided", reason);
    }
}
