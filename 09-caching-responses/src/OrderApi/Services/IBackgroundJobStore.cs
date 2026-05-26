using OrderApi.Infrastructure.Persistence;

namespace OrderApi.Services;

public interface IBackgroundJobStore
{
    Task EnqueueAsync(
        string jobType,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<BackgroundJobEntity?> TryAcquireNextAsync(
        TimeSpan lockDuration,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        Guid jobId,
        CancellationToken cancellationToken);

    Task ReleaseForRetryAsync(
        Guid jobId,
        string error,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid jobId,
        string error,
        CancellationToken cancellationToken);
}
