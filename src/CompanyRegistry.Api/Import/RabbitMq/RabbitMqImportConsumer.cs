using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CompanyRegistry.Api.Import.RabbitMq;

/// <summary>
/// The RabbitMQ version of ImportWorker: RabbitMQ pushes each import run id here, one at a time,
/// and the message is deleted only after the import has finished (manual ack).
/// </summary>
public sealed class RabbitMqImportConsumer(
    IOptions<QueueOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqImportConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        var factory = new ConnectionFactory { HostName = o.HostName };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(o.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        // Prefetch 1: RabbitMQ hands over the next message only after this one is acked.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, message) =>
        {
            try
            {
                var runId = long.Parse(Encoding.UTF8.GetString(message.Body.Span));
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider
                    .GetRequiredService<CompanyImporter>()
                    .RunAsync(runId, stoppingToken);
                if (stoppingToken.IsCancellationRequested)
                {
                    // The app is stopping and the import was cut short. No ack: RabbitMQ keeps the
                    // message and delivers it again on the next start.
                    return;
                }
                // done: RabbitMQ deletes it
                await channel.BasicAckAsync(message.DeliveryTag, multiple: false); 
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not process an import message; dropping it");
                await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: false); // don't retry forever
            }
        };

        // autoAck: false = we ack ourselves, after the work is done.
        await channel.BasicConsumeAsync(o.QueueName, autoAck: false, consumer, stoppingToken);

        // Keep the connection open until the app stops; RabbitMQ calls the handler above.
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}