using CompanyRegistry.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CompanyRegistry.Api.Import;

/// <summary>
/// Long-running background service that takes import runs off the queue and runs them one at a time.
/// It is a singleton, so it creates a fresh DI scope (and with it a fresh DbContext) for every run.
/// </summary>
public sealed class ImportWorker(
    ImportQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await AbandonInterruptedRunsAsync(stoppingToken);

        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<CompanyImporter>();
            await importer.RunAsync(runId, stoppingToken);
        }
    }

    /// <summary>The queue lives in memory, so runs that were queued or running when the process stopped will never finish.</summary>
    private async Task AbandonInterruptedRunsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var abandoned = await db.ImportRuns
            .Where(r => r.Status == ImportStatus.Queued || r.Status == ImportStatus.Running)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.Status, ImportStatus.Abandoned)
                .SetProperty(r => r.FinishedAt, DateTimeOffset.UtcNow)
                .SetProperty(r => r.Error, "Interrupted by an application restart"), ct);

        if (abandoned > 0)
        {
            logger.LogWarning("Marked {Count} interrupted import runs as abandoned", abandoned);
        }
    }
}
