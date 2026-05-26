using OrderApi.Domain;
using System.Text.Json;

namespace OrderApi.Services;

public sealed class OrderConfirmationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OrderConfirmationWorker> logger) : BackgroundService
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var jobStore = scope.ServiceProvider
                .GetRequiredService<IBackgroundJobStore>();

            var sender = scope.ServiceProvider
                .GetRequiredService<IOrderConfirmationSender>();

            var job = await jobStore.TryAcquireNextAsync(
                LockDuration,
                stoppingToken);

            if (job is null)
            {
                await Task.Delay(PollDelay, stoppingToken);
                continue;
            }

            try
            {
                if (job.JobType != "OrderConfirmation")
                {
                    await jobStore.MarkFailedAsync(
                        job.Id,
                        $"Unknown job type: {job.JobType}",
                        stoppingToken);

                    continue;
                }

                var payload = JsonSerializer.Deserialize<OrderConfirmationPayload>(
                    job.PayloadJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));

                if (payload is null)
                {
                    await jobStore.MarkFailedAsync(
                        job.Id,
                        "Invalid payload.",
                        stoppingToken);

                    continue;
                }

                await sender.SendAsync(
                    payload,
                    job.IdempotencyKey,
                    stoppingToken);

                await jobStore.CompleteAsync(job.Id, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Background job {JobId} failed on attempt {AttemptCount}.",
                    job.Id,
                    job.AttemptCount);

                if (job.AttemptCount >= MaxAttempts)
                {
                    await jobStore.MarkFailedAsync(
                        job.Id,
                        ex.Message,
                        stoppingToken);
                }
                else
                {
                    await jobStore.ReleaseForRetryAsync(
                        job.Id,
                        ex.Message,
                        stoppingToken);
                }
            }
        }
    }
}
