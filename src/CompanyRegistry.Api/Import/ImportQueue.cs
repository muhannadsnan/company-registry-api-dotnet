using System.Threading.Channels;

namespace CompanyRegistry.Api.Import;

/// <summary>
/// In-process queue between whoever asks for an import (the API, the scheduler) and the worker that runs it.
/// Registered as a singleton, so producers and the consumer share the same channel.
/// </summary>
public sealed class ImportQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>();

    public ValueTask EnqueueAsync(long importRunId, CancellationToken ct) => _channel.Writer.WriteAsync(importRunId, ct);

    public IAsyncEnumerable<long> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
