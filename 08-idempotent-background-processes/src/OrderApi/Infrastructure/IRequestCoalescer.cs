using System.Collections.Concurrent;

namespace OrderApi.Infrastructure;

public interface IRequestCoalescer
{
    Task<T> RunOnceAsync<T>(string key, Func<Task<T>> work);
}

public sealed class InMemoryRequestCoalescer : IRequestCoalescer
{
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inFlight = new();

    public async Task<T> RunOnceAsync<T>(string key, Func<Task<T>> work)
    {
        var lazy = _inFlight.GetOrAdd(key, _ => new Lazy<Task<object?>>(async () => await work()));
        try
        {
            var result = await lazy.Value;
            return (T)result!;
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }
}
