namespace OrderApi.Diagnostics;

public sealed record FaultConfiguration(
    bool ShippingUnavailable = false,
    int ShippingDelayMs = 0,
    bool ForceReadinessFailure = false);

public interface IFaultSwitches
{
    FaultConfiguration Current { get; }
    void Configure(FaultConfiguration configuration);
}

public sealed class InMemoryFaultSwitches : IFaultSwitches
{
    private FaultConfiguration _current = new();
    public FaultConfiguration Current => _current;
    public void Configure(FaultConfiguration configuration) => _current = configuration;
}
