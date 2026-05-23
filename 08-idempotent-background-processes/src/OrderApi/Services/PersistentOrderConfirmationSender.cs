using Microsoft.EntityFrameworkCore;
using OrderApi.Domain;
using OrderApi.Infrastructure.Persistence;

namespace OrderApi.Services;

public sealed class PersistentOrderConfirmationSender(
    IDbContextFactory<OrdersDbContext> dbContextFactory,
    ILogger<PersistentOrderConfirmationSender> logger) : IOrderConfirmationSender
{
    public async Task SendAsync(
        OrderConfirmationPayload payload,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var alreadySent = await dbContext.OrderConfirmations
            .AnyAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

        if (alreadySent)
        {
            logger.LogInformation(
                "Order confirmation for {OrderId} was already sent.",
                payload.OrderId);

            return;
        }

        dbContext.OrderConfirmations.Add(new OrderConfirmationEntity
        {
            Id = Guid.NewGuid(),
            OrderId = payload.OrderId,
            IdempotencyKey = idempotencyKey,
            SentAt = DateTimeOffset.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Sent order confirmation for {OrderId}.",
                payload.OrderId);
        }
        catch (DbUpdateException)
        {
            logger.LogInformation(
                "Duplicate confirmation send was prevented for {OrderId}.",
                payload.OrderId);
        }
    }
}