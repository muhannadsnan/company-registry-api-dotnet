namespace CompanyRegistry.Api.Import;

/// <summary>Where a queued import run id goes. In memory or RabbitMQ, chosen by Queue:Provider.</summary>
public interface IImportQueue
{
    ValueTask EnqueueAsync(long importRunId, CancellationToken ct);
}