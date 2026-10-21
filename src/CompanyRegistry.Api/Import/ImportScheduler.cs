using Microsoft.Extensions.Options;

namespace CompanyRegistry.Api.Import;

/// <summary>The cron line of this app: queues an import every Import:ScheduleInterval. Off when the interval is not set.</summary>
public sealed class ImportScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<ImportOptions> options,
    ILogger<ImportScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.ScheduleInterval is not { } interval)
        {
            logger.LogInformation("Scheduled import is off");
            return;
        }

        logger.LogInformation("Scheduled import every {Interval}", interval);
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var runs = scope.ServiceProvider.GetRequiredService<ImportRunService>();
            var result = await runs.QueueAsync("schedule", requestedBy: null, correlationId: Guid.NewGuid().ToString("N"), stoppingToken);
            if (result.AlreadyActive)
            {
                logger.LogInformation("Skipped scheduled import, run {ImportRunId} is still active", result.Run.Id);
            }
        }
    }
}
