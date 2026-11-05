using Practice2.Api.Data;

namespace Practice2.Api.Contracts;

public record ImportRunDto(
    long Id,
    string Status,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int Read,
    int Inserted,
    int Updated,
    int Unchanged,
    int Rejected,
    string? Error)
{
    public static ImportRunDto Fill(ImportRun r) => new(
        r.Id, r.Status.ToString(), r.QueuedAt, r.StartedAt, r.FinishedAt,
        r.Read, r.Inserted, r.Updated, r.Unchanged, r.Rejected, r.Error);
}