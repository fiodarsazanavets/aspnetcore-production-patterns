namespace OrderApi.Infrastructure.Persistence;

public class IdempotencyRecordEntity
{
    public string Key { get; set; } = default!;
    public string RequestHash { get; set; } = default!;
    public IdempotencyState State { get; set; }
    public string? ResponseJson { get; set; }
}
