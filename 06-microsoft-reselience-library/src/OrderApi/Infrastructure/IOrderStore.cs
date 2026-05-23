using OrderApi.Contracts;

namespace OrderApi.Infrastructure;

public interface IOrderStore
{
    Task AddAsync(OrderResponse order, CancellationToken cancellationToken);
    Task<OrderResponse?> FindAsync(Guid orderId, CancellationToken cancellationToken);
}

public sealed class InMemoryOrderStore : IOrderStore
{
    private readonly Dictionary<Guid, OrderResponse> _orders = new();
    private readonly Lock _lock = new();

    public Task AddAsync(OrderResponse order, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _orders[order.OrderId] = order;
        }
        return Task.CompletedTask;
    }

    public Task<OrderResponse?> FindAsync(Guid orderId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _orders.TryGetValue(orderId, out var order);
            return Task.FromResult(order);
        }
    }
}
