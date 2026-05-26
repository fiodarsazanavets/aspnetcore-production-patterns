using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using OrderApi.Config;
using OrderApi.Contracts;
using OrderApi.Infrastructure;

namespace OrderApi.Services;

public sealed class CachedOrderReader(
    IOrderStore orderStore,
    HybridCache cache,
    IOptions<OrderCacheOptions> options,
    LastKnownOrderSnapshot snapshot,
    ILogger<CachedOrderReader> logger)
{
    public async Task<OrderResponse?> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await cache.GetOrCreateAsync(
                $"order:{orderId}",
                async token =>
                {
                    logger.LogInformation(
                        "Cache miss for order {OrderId}. Loading from store.",
                        orderId);

                    var loaded = await orderStore.FindAsync(orderId, token);

                    if (loaded is not null)
                    {
                        snapshot.Remember(loaded);
                    }

                    return loaded;
                },
                new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromSeconds(
                        options.Value.OrderCacheSeconds)
                },
                cancellationToken: cancellationToken);

            if (order is not null)
            {
                snapshot.Remember(order);
            }

            return order;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "Failed to load order {OrderId}. Trying last-known snapshot.",
                orderId);

            return snapshot.TryGet(orderId, out var fallback)
                ? fallback
                : null;
        }
    }

    public async Task InvalidateAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(
            $"order:{orderId}",
            cancellationToken);

        await cache.RemoveAsync(
            $"order-summary:{orderId}",
            cancellationToken);
    }
}
