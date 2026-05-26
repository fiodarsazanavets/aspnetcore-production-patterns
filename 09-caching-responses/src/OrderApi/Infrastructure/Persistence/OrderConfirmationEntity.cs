namespace OrderApi.Infrastructure.Persistence;

public sealed class OrderConfirmationEntity
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string IdempotencyKey { get; set; } = default!;

    public DateTimeOffset SentAt { get; set; }
}