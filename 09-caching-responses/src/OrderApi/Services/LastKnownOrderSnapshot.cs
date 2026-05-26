using OrderApi.Contracts;

namespace OrderApi.Services;

public sealed class LastKnownOrderSnapshot
{
    private readonly Dictionary<Guid, OrderResponse> _orders = new();

    public void Remember(OrderResponse order)
    {
        _orders[order.OrderId] = order;
    }

    public bool TryGet(Guid orderId, out OrderResponse? order)
    {
        return _orders.TryGetValue(orderId, out order);
    }
}