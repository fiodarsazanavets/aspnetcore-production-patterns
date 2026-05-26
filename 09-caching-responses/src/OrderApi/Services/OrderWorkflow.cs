using Microsoft.Extensions.Options;
using OrderApi.Config;
using OrderApi.Contracts;
using OrderApi.Domain;
using OrderApi.Infrastructure;
using Polly.CircuitBreaker;
using System.Collections;

namespace OrderApi.Services;

public enum OrderWorkflowStatus { Created, Replayed, Conflict }
public sealed record OrderWorkflowResult(OrderWorkflowStatus Status, OrderResponse? Order);

public sealed class OrderWorkflow(
    IOrderStore orders,
    IIdempotencyStore idempotency,
    IShippingGateway shippingGateway,
    IShippingFallbackProvider fallbackProvider,
    IBackgroundJobStore backgroundJobStore,
    IOptions<ShippingOptions> shippingOptions,
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

        ShippingQuoteResponse quote;

        try
        {
            quote = await shippingGateway.GetQuoteAsync(request, cancellationToken);
        }
        catch (BrokenCircuitException) when (shippingOptions.Value.UseFallbackQuote)
        {
            logger.LogWarning(
                "Shipping circuit is open. Using fallback shipping quote.");

            quote = fallbackProvider.CreateFallbackQuote(request);
        }
        catch (HttpRequestException) when (shippingOptions.Value.UseFallbackQuote)
        {
            logger.LogWarning("Shipping dependency failed. Using fallback quote.");
            quote = fallbackProvider.CreateFallbackQuote(request);
        }

        var response = new OrderResponse(
            Guid.NewGuid(),
            request.CustomerId,
            "Accepted",
            DateTimeOffset.UtcNow,
            request.Lines.Select(line => new OrderLineResponse(line.Sku, line.Quantity)).ToArray(),
            quote);

        await orders.AddAsync(response, cancellationToken);

        await backgroundJobStore.EnqueueAsync(
            "OrderConfirmation",
            new OrderConfirmationPayload(
                response.OrderId,
                response.CustomerId),
                $"order-confirmation:{response.OrderId}",
                cancellationToken);

        await idempotency.CompleteAsync(idempotencyKey, response, cancellationToken);

        return new OrderWorkflowResult(OrderWorkflowStatus.Created, response);
    }
}
