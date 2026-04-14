using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public sealed class ManagedSimConnectClient : ISimConnectClient, ISimConnectClientDiagnostics
{
    private readonly SimConnectAssemblyLocator _assemblyLocator;
    private readonly ISimConnectManagedBridgeFactory _bridgeFactory;
    private ISimConnectManagedBridge? _bridge;

    public ManagedSimConnectClient()
        : this(new SimConnectAssemblyLocator(), new ReflectionSimConnectManagedBridgeFactory())
    {
    }

    internal ManagedSimConnectClient(
        SimConnectAssemblyLocator assemblyLocator,
        ISimConnectManagedBridgeFactory bridgeFactory)
    {
        ArgumentNullException.ThrowIfNull(assemblyLocator);
        ArgumentNullException.ThrowIfNull(bridgeFactory);

        _assemblyLocator = assemblyLocator;
        _bridgeFactory = bridgeFactory;
    }

    public bool IsConnected => _bridge?.IsConnected == true;
    public string DiagnosticsClientName => "Managed SimConnect";

    public async Task OpenAsync(SimConnectHostOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Managed SimConnect is only supported on Windows.");
        }

        if (_bridge?.IsConnected == true)
        {
            return;
        }

        var managedAssembly = _assemblyLocator.LoadManagedAssembly(options);
        _bridge = await _bridgeFactory.CreateAsync(managedAssembly, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_bridge is null)
        {
            return;
        }

        try
        {
            await _bridge.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _bridge = null;
        }
    }

    public Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default)
    {
        if (_bridge is null)
        {
            throw new InvalidOperationException("SimConnect client is not open.");
        }

        return _bridge.ReadNextFrameAsync(cancellationToken);
    }
}
