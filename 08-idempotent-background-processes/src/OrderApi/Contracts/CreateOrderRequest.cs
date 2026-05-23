namespace OrderApi.Contracts;

public sealed record CreateOrderRequest(
    string CustomerId,
    IReadOnlyList<OrderLineRequest> Lines);
public sealed record OrderLineRequest(
    string Sku,
    int Quantity);
public sealed record OrderResponse(
    Guid OrderId,
    string CustomerId,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineResponse> Lines,
    ShippingQuoteResponse? ShippingQuote);
public sealed record OrderLineResponse(
    string Sku,
    int Quantity);
public sealed record ShippingQuoteResponse(
    string Carrier,
    decimal Amount,
    DateTimeOffset ExpiresAt,
    bool IsFallback = false);
public sealed record ProblemDetailsResponse(string Detail);
