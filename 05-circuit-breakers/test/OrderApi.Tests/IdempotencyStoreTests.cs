using OrderApi.Contracts;
using OrderApi.Infrastructure;
using Xunit;

namespace OrderApi.Tests;

public sealed class IdempotencyStoreTests
{
    [Fact]
    public async Task TryBeginAsync_ReturnsCompletedResponse_WhenSameOperationIsReplayed()
    {
        var store = new InMemoryIdempotencyStore();
        var request = new CreateOrderRequest("customer-1", [new OrderLineRequest("sku-1", 1)]);
        var response = new OrderResponse(Guid.NewGuid(), "customer-1", "Accepted", DateTimeOffset.UtcNow, [new OrderLineResponse("sku-1", 1)], null);

        var first = await store.TryBeginAsync("key-1", request, CancellationToken.None);
        await store.CompleteAsync("key-1", response, CancellationToken.None);
        var replay = await store.TryBeginAsync("key-1", request, CancellationToken.None);

        Assert.Null(first);
        Assert.NotNull(replay);
        Assert.Equal(IdempotencyState.Completed, replay.State);
        Assert.Equal(response.OrderId, replay.Response?.OrderId);
    }
}
