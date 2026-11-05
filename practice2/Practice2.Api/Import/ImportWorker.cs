using Microsoft.EntityFrameworkCore;
using Practice2.Api.Data;

namespace Practice2.Api.Import;

public class ImportWorker(
    ImportQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MarkUnfinishedRunsAbandonedAsync(stoppingToken);   // 8d

        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            // The worker lives as long as the app, but a DbContext should be short-lived:
            // a fresh scope per RUN gives a fresh DbContext and importer.
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); // this function gives "fresh" one
            var importer = scope.ServiceProvider.GetRequiredService<CompanyImporter>(); // this function gives "fresh" one

            var run = await db.ImportRuns.FindAsync([runId], stoppingToken);
            if (run is null)
                continue;

            run.Status = ImportStatus.Running;
            run.StartedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(stoppingToken);
            logger.LogInformation("Import run {RunId} started", runId);

            ImportSummary? summary = null;
            string? error = null;
            try
            { // this RunSync() has a Clear() after every page, which forces use of Attach() later
                summary = await importer.RunAsync(stoppingToken); 
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Import run {RunId} failed", runId);
                error = ex.Message;
            }

            // The importer clears the change tracker after every page, which also
            // forgets `run`. Attach it again before recording how the run ended.
            db.ChangeTracker.Clear();
            db.ImportRuns.Attach(run); // to the DbContext change tracker in this scope. 
            // or we could re-fetch it from the db by run.Id instead

            run.Status = error is null ? ImportStatus.Succeeded : ImportStatus.Failed;
            run.Error = error;
            run.Read = summary?.Read ?? 0;
            run.Inserted = summary?.Inserted ?? 0;
            run.Updated = summary?.Updated ?? 0;
            run.Unchanged = summary?.Unchanged ?? 0;
            run.Rejected = summary?.Rejected ?? 0;
            run.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);

            logger.LogInformation("Import run {RunId} finished: {Status}", runId, run.Status);
        }
    }


    // The queue lives in memory, so a restart empties it. The RUNs that were Queued or Running
    // when the app stopped will never finish: mark them, so they don't conflict with new imports.
    private async Task MarkUnfinishedRunsAbandonedAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var count = await db.ImportRuns
            .Where(r => r.Status == ImportStatus.Queued || r.Status == ImportStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ImportStatus.Abandoned)
                .SetProperty(r => r.FinishedAt, DateTimeOffset.UtcNow), ct);

        if (count > 0)
            logger.LogWarning("Marked {Count} unfinished import run(s) as Abandoned", count);
    }

}
