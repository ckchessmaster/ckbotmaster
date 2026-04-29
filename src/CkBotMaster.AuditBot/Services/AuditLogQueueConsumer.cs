using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CkBotMaster.AuditBot.Services;

/// <summary>
/// Drains the <see cref="AuditLogQueue"/>, creating a DI scope per item so the
/// scoped <c>AuditDbContext</c> can be resolved cleanly.
/// </summary>
public sealed class AuditLogQueueConsumer(
    AuditLogQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditLogQueueConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<AuditLogProcessor>();
                await processor.ProcessAsync(item.Entry, item.FromCatchup, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process audit entry {EntryId}.", item.Entry.Id);
            }
        }
    }
}
