using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CompanyRegistry.Api.Import.RabbitMq;

/// <summary>
/// Publishes import run ids to a durable RabbitMQ queue, so a queued import survives an app restart.
/// Opens a connection per message: imports are rare, so simple wins over fast here.
/// </summary>
public sealed class RabbitMqImportQueue(IOptions<QueueOptions> options) : IImportQueue
{
    public async ValueTask EnqueueAsync(long importRunId, CancellationToken ct)
    {
        var o = options.Value;
        var factory = new ConnectionFactory { HostName = o.HostName };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        // Idempotent: creates the queue if it is missing, does nothing if it exists with the same settings.
        await channel.QueueDeclareAsync(o.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

        var body = Encoding.UTF8.GetBytes(importRunId.ToString());
        var properties = new BasicProperties { Persistent = true }; // stored on disk, survives a broker restart 
        await channel.BasicPublishAsync(exchange: "", routingKey: o.QueueName, mandatory: false,
            basicProperties: properties, body: body, cancellationToken: ct);
    }
}