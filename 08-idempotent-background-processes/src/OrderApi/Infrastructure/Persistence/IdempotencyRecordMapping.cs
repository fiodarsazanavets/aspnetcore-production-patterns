using OrderApi.Contracts;
using System.Text.Json;

namespace OrderApi.Infrastructure.Persistence;

public static class IdempotencyRecordMapping
{
    public static IdempotencyRecord ToDomainRecord(
        this IdempotencyRecordEntity entity)
    {
        var response = entity.ResponseJson is null
            ? null
            : JsonSerializer.Deserialize<OrderResponse>(entity.ResponseJson);

        return new IdempotencyRecord(
            entity.Key,
            entity.RequestHash,
            entity.State,
            response);
    }
}
