using Microsoft.Extensions.Options;
using OrderApi.Config;
using OrderApi.Contracts;
using OrderApi.Exceptions;

namespace OrderApi.Services;

public sealed class ManualRetryShippingGateway(
    IShippingGateway inner,
    IOptions<ShippingOptions> options,
    ILogger<ManualRetryShippingGateway> logger) : IShippingGateway
{
    public async Task<ShippingQuoteResponse> GetQuoteAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await inner.GetQuoteAsync(request, cancellationToken);
            }
            catch (TransientShippingException) when (attempt < settings.MaxRetryAttempts)
            {
                var delay = CalculateDelay(attempt, settings);

                logger.LogWarning(
                    "Shipping quote attempt {Attempt} failed transiently. Retrying in {DelayMilliseconds} ms.",
                    attempt,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static TimeSpan CalculateDelay(int attempt, ShippingOptions options)
    {
        var exponentialDelay =
            options.BaseRetryDelayMilliseconds * Math.Pow(2, attempt - 1);

        var jitter = Random.Shared.Next(0, options.MaxJitterMilliseconds + 1);

        return TimeSpan.FromMilliseconds(exponentialDelay + jitter);
    }
}
