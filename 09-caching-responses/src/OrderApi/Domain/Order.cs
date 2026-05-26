namespace OrderApi.Domain;

public sealed record Order(
    Guid OrderId,
    string CustomerId,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLine> Lines,
    ShippingQuote? ShippingQuote);
public sealed record OrderLine(
    string Sku,
    int Quantity);
public sealed record ShippingQuote(
    string Carrier,
    decimal Amount,
    DateTimeOffset ExpiresAt);

public sealed record OrderConfirmationPayload(
    Guid OrderId,
    string CustomerId);

public sealed record OrderSummaryResponse(
    Guid OrderId,
    string CustomerId,
    int LineCount,
    int TotalQuantity,
    decimal ShippingPrice,
    bool FromExpensiveComputation);