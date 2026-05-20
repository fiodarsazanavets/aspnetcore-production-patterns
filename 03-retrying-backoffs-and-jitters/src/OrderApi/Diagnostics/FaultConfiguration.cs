namespace OrderApi.Diagnostics;

public sealed record FaultConfiguration(
    bool ShippingUnavailable = false,
    int ShippingDelayMs = 0,
    bool ForceReadinessFailure = false,
    int ShippingTransientFailuresBeforeSuccess = 0);

public interface IFaultSwitches
{
    FaultConfiguration Current { get; }
    void Configure(FaultConfiguration configuration);
    bool ShouldFailShippingTransiently();
}

public sealed class InMemoryFaultSwitches : IFaultSwitches
{
    private int _remainingTransientShippingFailures;

    private FaultConfiguration _current = new();
    public FaultConfiguration Current => _current;

    public void Configure(FaultConfiguration configuration)
    {
        _current = configuration;
        _remainingTransientShippingFailures = configuration.ShippingTransientFailuresBeforeSuccess;
    }

    public bool ShouldFailShippingTransiently()
    {
        while (true)
        {
            var current = _remainingTransientShippingFailures;

            if (current <= 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(
                    ref _remainingTransientShippingFailures,
                    current - 1,
                    current) == current)
            {
                return true;
            }
        }
    }
}
