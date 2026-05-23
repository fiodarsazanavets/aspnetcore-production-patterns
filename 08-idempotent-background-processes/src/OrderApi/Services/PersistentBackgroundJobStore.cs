using Microsoft.EntityFrameworkCore;
using OrderApi.Infrastructure.Persistence;
using System.Text.Json;

namespace OrderApi.Services;

public sealed class PersistentBackgroundJobStore(
    IDbContextFactory<OrdersDbContext> dbContextFactory,
    ILogger<PersistentBackgroundJobStore> logger) : IBackgroundJobStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task EnqueueAsync(
        string jobType,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.BackgroundJobs.Add(new BackgroundJobEntity
        {
            Id = Guid.NewGuid(),
            JobType = jobType,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            IdempotencyKey = idempotencyKey,
            State = BackgroundJobState.Pending,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            logger.LogInformation(
                "Background job with idempotency key {IdempotencyKey} already exists.",
                idempotencyKey);
        }
    }

    public async Task<BackgroundJobEntity?> TryAcquireNextAsync(
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var pendingJob = await dbContext.BackgroundJobs
            .Where(x => x.State == BackgroundJobState.Pending)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var expiredProcessingJob = await dbContext.BackgroundJobs
            .Where(x =>
                x.State == BackgroundJobState.Processing &&
                x.LockedUntil != null &&
                x.LockedUntil < now)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var job = pendingJob ?? expiredProcessingJob;

        if (job is null)
        {
            return null;
        }

        job.State = BackgroundJobState.Processing;
        job.AttemptCount++;
        job.LockedUntil = now.Add(lockDuration);
        job.LastError = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return job;
    }

    public async Task CompleteAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var job = await dbContext.BackgroundJobs
            .SingleAsync(x => x.Id == jobId, cancellationToken);

        job.State = BackgroundJobState.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.LockedUntil = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseForRetryAsync(
        Guid jobId,
        string error,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var job = await dbContext.BackgroundJobs
            .SingleAsync(x => x.Id == jobId, cancellationToken);

        job.State = BackgroundJobState.Pending;
        job.LockedUntil = null;
        job.LastError = error;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid jobId,
        string error,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var job = await dbContext.BackgroundJobs
            .SingleAsync(x => x.Id == jobId, cancellationToken);

        job.State = BackgroundJobState.Failed;
        job.LockedUntil = null;
        job.LastError = error;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
