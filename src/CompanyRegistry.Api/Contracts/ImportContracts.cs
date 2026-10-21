using CompanyRegistry.Api.Data;

namespace CompanyRegistry.Api.Contracts;

public sealed record ImportRunDto(
    long Id,
    string Status,
    string Trigger,
    string? RequestedBy,
    string? CorrelationId,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int RowsRead,
    int RowsInserted,
    int RowsUpdated,
    int RowsUnchanged,
    int RowsRejected,
    string? Error)
{
    public static ImportRunDto From(ImportRun r) => new(
        r.Id, r.Status.ToString(), r.Trigger, r.RequestedBy, r.CorrelationId,
        r.QueuedAt, r.StartedAt, r.FinishedAt,
        r.RowsRead, r.RowsInserted, r.RowsUpdated, r.RowsUnchanged, r.RowsRejected, r.Error);
}
