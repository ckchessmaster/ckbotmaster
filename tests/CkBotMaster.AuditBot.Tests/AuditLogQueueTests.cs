using CkBotMaster.AuditBot.Services;
using Discord;
using NSubstitute;

namespace CkBotMaster.AuditBot.Tests;

public class AuditLogQueueTests
{
    [Fact]
    public async Task EnqueueAndRead_WorksCorrectly()
    {
        // Arrange
        var sut = new AuditLogQueue();
        var entry = Substitute.For<IAuditLogEntry>();
        entry.Id.Returns(123UL);
        var cts = new CancellationTokenSource(1000);

        // Act
        await sut.EnqueueAsync(entry, fromCatchup: true, cts.Token);
        
        var reader = sut.ReadAllAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        var hasItem = await reader.MoveNextAsync();

        // Assert
        Assert.True(hasItem);
        Assert.Equal(123UL, reader.Current.Entry.Id);
        Assert.True(reader.Current.FromCatchup);
    }
}
