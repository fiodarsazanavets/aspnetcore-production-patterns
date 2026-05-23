using OrderApi.Contracts;

namespace OrderApi.Services;

public interface IShippingFallbackProvider
{
    ShippingQuoteResponse CreateFallbackQuote(CreateOrderRequest request);
}
