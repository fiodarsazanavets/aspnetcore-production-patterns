using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OrderApi.Contracts;

namespace OrderApi.Infrastructure;

public enum IdempotencyState { Processing, Completed }

public sealed record IdempotencyRecord(string Key, string RequestHash, IdempotencyState State, OrderResponse? Response);

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> TryBeginAsync(string key, object request, CancellationToken cancellationToken);
    Task CompleteAsync(string key, OrderResponse response, CancellationToken cancellationToken);
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, IdempotencyRecord> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    public Task<IdempotencyRecord?> TryBeginAsync(string key, object request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hash = ComputeHash(request);
        lock (_lock)
        {
            if (_records.TryGetValue(key, out var existing))
            {
                return Task.FromResult<IdempotencyRecord?>(existing.RequestHash == hash ? existing : existing with { Response = null });
            }

            _records[key] = new IdempotencyRecord(key, hash, IdempotencyState.Processing, null);
            return Task.FromResult<IdempotencyRecord?>(null);
        }
    }

    public Task CompleteAsync(string key, OrderResponse response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            var current = _records[key];
            _records[key] = current with { State = IdempotencyState.Completed, Response = response };
        }
        return Task.CompletedTask;
    }

    private static string ComputeHash(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
