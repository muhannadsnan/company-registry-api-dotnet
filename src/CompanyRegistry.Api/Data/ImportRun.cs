namespace CompanyRegistry.Api.Data;

public enum ImportStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    Abandoned,
}

/// <summary>One row per import: when it ran, who asked for it, and what happened to the rows.</summary>
public class ImportRun
{
    public long Id { get; set; }
    public ImportStatus Status { get; set; }
    public required string Trigger { get; set; }
    public string? RequestedBy { get; set; }
    public string? CorrelationId { get; set; }

    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public int RowsUnchanged { get; set; }
    public int RowsRejected { get; set; }
    public string? Error { get; set; }

    public bool IsActive => Status is ImportStatus.Queued or ImportStatus.Running;
}
