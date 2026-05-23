namespace OrderApi.Infrastructure.Persistence;

public sealed class BackgroundJobEntity
{
    public Guid Id { get; set; }

    public string JobType { get; set; } = default!;

    public string PayloadJson { get; set; } = default!;

    public string IdempotencyKey { get; set; } = default!;

    public BackgroundJobState State { get; set; }

    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? LastError { get; set; }
}

public enum BackgroundJobState
{
    Pending,
    Processing,
    Completed,
    Failed
}