using OrderApi.Contracts;
using OrderApi.Infrastructure;

namespace OrderApi.Services;

public enum OrderWorkflowStatus { Created, Replayed, Conflict }
public sealed record OrderWorkflowResult(OrderWorkflowStatus Status, OrderResponse? Order);

public sealed class OrderWorkflow(
    IOrderStore orders,
    IIdempotencyStore idempotency,
    IShippingGateway shippingGateway,
    ILogger<OrderWorkflow> logger)
{
    public async Task<OrderWorkflowResult> CreateOrderAsync(string idempotencyKey, CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var existing = await idempotency.TryBeginAsync(idempotencyKey, request, cancellationToken);

        if (existing is { State: IdempotencyState.Completed, Response: not null })
        {
            logger.LogInformation("Replayed completed operation for idempotency key {IdempotencyKey}.", idempotencyKey);
            return new OrderWorkflowResult(OrderWorkflowStatus.Replayed, existing.Response);
        }

        if (existing is not null)
        {
            return new OrderWorkflowResult(OrderWorkflowStatus.Conflict, null);
        }

        var quote = await shippingGateway.GetQuoteAsync(request, cancellationToken);
        var response = new OrderResponse(
            Guid.NewGuid(),
            request.CustomerId,
            "Accepted",
            DateTimeOffset.UtcNow,
            request.Lines.Select(line => new OrderLineResponse(line.Sku, line.Quantity)).ToArray(),
            quote);

        await orders.AddAsync(response, cancellationToken);
        await idempotency.CompleteAsync(idempotencyKey, response, cancellationToken);

        return new OrderWorkflowResult(OrderWorkflowStatus.Created, response);
    }
}
