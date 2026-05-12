using OrderApi.Contracts;
using OrderApi.Diagnostics;

namespace OrderApi.Services;

public interface IShippingGateway
{
    Task<ShippingQuoteResponse> GetQuoteAsync(CreateOrderRequest request, CancellationToken cancellationToken);
}

public sealed class ShippingGateway(HttpClient httpClient, IFaultSwitches faults, ILogger<ShippingGateway> logger) : IShippingGateway
{
    public async Task<ShippingQuoteResponse> GetQuoteAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var current = faults.Current;
        if (current.ShippingDelayMs > 0)
        {
            await Task.Delay(current.ShippingDelayMs, cancellationToken);
        }

        if (current.ShippingUnavailable)
        {
            logger.LogWarning("Shipping dependency fault was injected.");
            throw new HttpRequestException("Injected shipping dependency failure.");
        }

        // The base project uses a local deterministic response so clips can evolve this into
        // real HttpClient resilience, fallbacks, circuit breakers, and fault injection.
        _ = httpClient;
        var itemCount = request.Lines.Sum(line => line.Quantity);
        return new ShippingQuoteResponse("DemoCarrier", 4.99m + itemCount, DateTimeOffset.UtcNow.AddMinutes(15));
    }
}
