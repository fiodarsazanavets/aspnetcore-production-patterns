using Microsoft.EntityFrameworkCore;
using OrderApi.Contracts;
using OrderApi.Infrastructure.Persistence;
using System.Text.Json;

namespace OrderApi.Infrastructure;

public class PersistentOrderStore(
    IDbContextFactory<OrdersDbContext> dbContextFactory,
    ILogger<PersistentOrderStore> logger) : IOrderStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task AddAsync(
        OrderResponse order,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.Orders.Add(new OrderEntity
        {
            Id = order.OrderId,
            CustomerId = order.CustomerId,
            ResponseJson = JsonSerializer.Serialize(order, JsonOptions),
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stored order {OrderId}.", order.OrderId);
    }

    public async Task<OrderResponse?> FindAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        return entity is null
            ? null
            : JsonSerializer.Deserialize<OrderResponse>(
                entity.ResponseJson,
                JsonOptions);
    }
}