using OrderApi.Domain;

namespace OrderApi.Services;

public interface IOrderConfirmationSender
{
    Task SendAsync(
        OrderConfirmationPayload payload,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
