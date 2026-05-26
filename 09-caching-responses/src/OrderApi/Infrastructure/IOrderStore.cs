using OrderApi.Contracts;

namespace OrderApi.Infrastructure;

public interface IOrderStore
{
    Task AddAsync(OrderResponse order, CancellationToken cancellationToken);
    Task<OrderResponse?> FindAsync(Guid orderId, CancellationToken cancellationToken);
}