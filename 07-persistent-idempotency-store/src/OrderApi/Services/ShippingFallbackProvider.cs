using OrderApi.Contracts;

namespace OrderApi.Services;

public sealed class ShippingFallbackProvider : IShippingFallbackProvider
{
    public ShippingQuoteResponse CreateFallbackQuote(CreateOrderRequest request)
    {
        var itemCount = request.Lines.Sum(line => line.Quantity);

        return new ShippingQuoteResponse(
            Carrier: "FallbackCarrier",
            Amount: 7.99m + itemCount,
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(3),
            IsFallback: true);
    }
}
