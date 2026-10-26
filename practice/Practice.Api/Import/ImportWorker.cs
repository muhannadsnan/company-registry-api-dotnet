using Practice.Api.Data;

namespace Practice.Api.Import;

public class ImportWorker(
    ImportQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            // The worker lives as long as the app, but a DbContext should be short-lived:
            // a fresh scope per run gives a fresh DbContext and importer.
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var importer = scope.ServiceProvider.GetRequiredService<CompanyImporter>();

            var run = await db.ImportRuns.FindAsync([runId], stoppingToken);
            if (run is null)
            {
                continue;
            }

            using var logScope = logger.BeginScope(
                "ImportRun {RunId}, CorrelationId {CorrelationId}", run.Id, run.CorrelationId);

            run.Status = ImportStatus.Running;
            run.StartedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(stoppingToken);
            logger.LogInformation("Import run {RunId} started", runId);

            ImportSummary? summary = null;
            string? error = null;
            try
            {
                summary = await importer.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Import run {RunId} failed", runId);
                error = ex.Message;
            }

            // The importer clears EF's change tracker after every page, which also
            // forgets `run`. Attach it again before recording how the run ended.
            db.ChangeTracker.Clear();
            db.ImportRuns.Attach(run);

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
}
