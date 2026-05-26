using Microsoft.Extensions.Caching.Hybrid;
using OrderApi.Domain;
using OrderApi.Infrastructure;

namespace OrderApi.Services;

public sealed class OrderSummaryService(
    IOrderStore orderStore,
    HybridCache cache,
    ILogger<OrderSummaryService> logger)
{
    public async Task<OrderSummaryResponse?> GetSummaryAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            $"order-summary:{orderId}",
            async token =>
            {
                logger.LogInformation(
                    "Computing expensive summary for order {OrderId}.",
                    orderId);

                var order = await orderStore.FindAsync(orderId, token);

                if (order is null)
                {
                    return null;
                }

                await Task.Delay(750, token);

                return new OrderSummaryResponse(
                    order.OrderId,
                    order.CustomerId,
                    order.Lines.Count,
                    order.Lines.Sum(x => x.Quantity),
                    order.ShippingQuote!.Amount,
                    FromExpensiveComputation: true);
            },
            cancellationToken: cancellationToken);
    }
}