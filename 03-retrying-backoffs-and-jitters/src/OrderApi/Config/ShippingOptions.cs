namespace OrderApi.Config;

public sealed class ShippingOptions
{
    public int TimeoutMilliseconds { get; set; } = 1500;
    public int MaxRetryAttempts { get; set; } = 3;
    public int BaseRetryDelayMilliseconds { get; set; } = 200;
    public int MaxJitterMilliseconds { get; set; } = 150;
}
