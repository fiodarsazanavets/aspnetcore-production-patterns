namespace OrderApi.Infrastructure.Persistence;

public sealed class OrderEntity
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = default!;
    public string ResponseJson { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}