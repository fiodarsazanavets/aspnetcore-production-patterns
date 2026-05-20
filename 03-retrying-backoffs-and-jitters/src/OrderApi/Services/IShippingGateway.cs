using Microsoft.Extensions.Options;
using OrderApi.Config;
using OrderApi.Contracts;
using OrderApi.Diagnostics;
using OrderApi.Exceptions;

namespace OrderApi.Services;

public interface IShippingGateway
{
    Task<ShippingQuoteResponse> GetQuoteAsync(CreateOrderRequest request, CancellationToken cancellationToken);
}

public sealed class ShippingGateway(
    HttpClient httpClient,
    IFaultSwitches faults,
    IOptions<ShippingOptions> options,
    ILogger<ShippingGateway> logger) : IShippingGateway
{
    public async Task<ShippingQuoteResponse> GetQuoteAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var current = faults.Current;
        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds));

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        var boundedToken = linkedCts.Token;

        try
        {
            if (current.ShippingDelayMs > 0)
            {
                await Task.Delay(current.ShippingDelayMs, boundedToken);
            }

            if (faults.ShouldFailShippingTransiently())
            {
                logger.LogWarning("Injected transient shipping dependency failure.");
                throw new TransientShippingException("Injected transient shipping dependency failure.");
            }

            if (current.ShippingUnavailable)
            {
                logger.LogWarning("Shipping dependency fault was injected.");
                throw new HttpRequestException("Injected shipping dependency failure.");
            }

            _ = httpClient;
            var itemCount = request.Lines.Sum(line => line.Quantity);   

            return new ShippingQuoteResponse(
                "DemoCarrier",
                4.99m + itemCount,
                DateTimeOffset.UtcNow.AddMinutes(15));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Shipping quote timed out after {TimeoutMilliseconds} ms.",
                options.Value.TimeoutMilliseconds);

            throw new TimeoutException(
                $"Shipping quote timed out after {options.Value.TimeoutMilliseconds} ms.");
        }
    }
}
