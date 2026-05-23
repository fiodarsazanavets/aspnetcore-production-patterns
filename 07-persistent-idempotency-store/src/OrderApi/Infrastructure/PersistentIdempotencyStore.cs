using Microsoft.EntityFrameworkCore;
using OrderApi.Contracts;
using OrderApi.Infrastructure.Persistence;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OrderApi.Infrastructure;

public sealed class PersistentIdempotencyStore(
    IDbContextFactory<OrdersDbContext> dbContextFactory) : IIdempotencyStore
{
    public async Task<IdempotencyRecord?> TryBeginAsync(
        string key,
        object request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hash = ComputeHash(request);

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Key == key, cancellationToken);

        if (existing is not null)
        {
            return existing.RequestHash == hash
                ? existing.ToDomainRecord()
                : existing.ToDomainRecord() with { Response = null };
        }

        dbContext.IdempotencyRecords.Add(new IdempotencyRecordEntity
        {
            Key = key,
            RequestHash = hash,
            State = IdempotencyState.Processing,
            ResponseJson = null
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException)
        {
            // Another concurrent request inserted the same key.
            // Re-read and classify it the same way.
            var concurrent = await dbContext.IdempotencyRecords
                .AsNoTracking()
                .SingleAsync(x => x.Key == key, cancellationToken);

            return concurrent.RequestHash == hash
                ? concurrent.ToDomainRecord()
                : concurrent.ToDomainRecord() with { Response = null };
        }
    }

    public async Task CompleteAsync(
        string key,
        OrderResponse response,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var current = await dbContext.IdempotencyRecords
            .SingleAsync(x => x.Key == key, cancellationToken);

        current.State = IdempotencyState.Completed;
        current.ResponseJson = JsonSerializer.Serialize(response);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ComputeHash(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}