using Microsoft.Extensions.Options;
using OrderApi.Config;
using OrderApi.Contracts;
using OrderApi.Diagnostics;
using OrderApi.Exceptions;
using System.Net;

namespace OrderApi.Services;

public interface IShippingGateway
{
    Task<ShippingQuoteResponse> GetQuoteAsync(CreateOrderRequest request, CancellationToken cancellationToken);
}

public sealed class ShippingGateway(
    HttpClient httpClient,
    IOptions<ShippingOptions> options) : IShippingGateway
{
    public async Task<ShippingQuoteResponse> GetQuoteAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds));

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token);

        var response = await httpClient.GetAsync(
            "/shipping/quote",
            linkedCts.Token);

       response.EnsureSuccessStatusCode();

        return new ShippingQuoteResponse(
            "DemoCarrier",
            9.99m,
            DateTimeOffset.UtcNow.AddMinutes(15));
    }
}
