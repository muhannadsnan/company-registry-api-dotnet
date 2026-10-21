using CompanyRegistry.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CompanyRegistry.Api.Import;

public sealed record QueueResult(ImportRun Run, bool AlreadyActive);

/// <summary>Creates an import_runs row and puts it on the queue. Only one import may be queued or running at a time.</summary>
public sealed class ImportRunService(AppDbContext db, ImportQueue queue)
{
    public async Task<QueueResult> QueueAsync(string trigger, string? requestedBy, string? correlationId, CancellationToken ct)
    {
        var active = await db.ImportRuns
            .Where(r => r.Status == ImportStatus.Queued || r.Status == ImportStatus.Running)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (active is not null)
        {
            return new QueueResult(active, AlreadyActive: true);
        }

        var run = new ImportRun
        {
            Status = ImportStatus.Queued,
            Trigger = trigger,
            RequestedBy = requestedBy,
            CorrelationId = correlationId,
            QueuedAt = DateTimeOffset.UtcNow,
        };
        db.ImportRuns.Add(run);
        await db.SaveChangesAsync(ct);

        await queue.EnqueueAsync(run.Id, ct);
        return new QueueResult(run, AlreadyActive: false);
    }
}
