using System.Threading.Channels;

namespace Practice.Api.Import;

public class ImportQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>();

    public ValueTask EnqueueAsync(long runId, CancellationToken ct) =>
        _channel.Writer.WriteAsync(runId, ct);

    public IAsyncEnumerable<long> ReadAllAsync(CancellationToken ct) => 
        _channel.Reader.ReadAllAsync(ct);
}