namespace OrderApi.Config;

public class OrderCacheOptions
{
    public int OrderCacheSeconds { get; set; } = 60;
    public int SummaryCacheSeconds { get; set; } = 30;
}
