namespace Practice.Api.Data;

public enum ImportStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
}

public class ImportRun
{
    public string? CorrelationId { get; set; }
    public long Id { get; set; }
    public ImportStatus Status { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int Read { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Rejected { get; set; }
    public string? Error { get; set; }
}