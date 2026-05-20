using CkBotMaster.AuditBot.Services;
using Discord;
using Discord.WebSocket;
using NSubstitute;

namespace CkBotMaster.AuditBot.Tests;

public class AuditEntryDetailExtractorTests
{
    private readonly DiscordSocketClient _client;

    public AuditEntryDetailExtractorTests()
    {
        _client = Substitute.For<DiscordSocketClient>();
    }

    [Fact]
    public void Extract_NullData_ReturnsEmptyList()
    {
        var entry = Substitute.For<IAuditLogEntry>();
        entry.Data.Returns((IAuditLogData?)null);

        var details = AuditEntryDetailExtractor.Extract(entry, _client);

        Assert.Empty(details);
    }
}
