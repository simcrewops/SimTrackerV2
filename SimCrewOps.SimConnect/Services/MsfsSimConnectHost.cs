using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public sealed class MsfsSimConnectHost
{
    private readonly ISimulatorProcessDetector _simulatorProcessDetector;
    private readonly ISimConnectClient _simConnectClient;
    private readonly SimConnectTelemetryMapper _telemetryMapper;
    private readonly SimConnectHostOptions _options;

    private SimConnectHostStatus _status = new();

    public MsfsSimConnectHost(
        ISimulatorProcessDetector simulatorProcessDetector,
        ISimConnectClient simConnectClient,
        SimConnectTelemetryMapper? telemetryMapper = null,
        SimConnectHostOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(simulatorProcessDetector);
        ArgumentNullException.ThrowIfNull(simConnectClient);

        _simulatorProcessDetector = simulatorProcessDetector;
        _simConnectClient = simConnectClient;
        _telemetryMapper = telemetryMapper ?? new SimConnectTelemetryMapper();
        _options = options ?? new SimConnectHostOptions();
    }

    public SimConnectHostStatus Status => _status;

    public async Task<SimConnectHostStatus> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var clientPath = ResolveClientPath();
        var simulatorProcess = _simulatorProcessDetector.FindRunningSimulator(_options.SimulatorProcessNames);
        if (simulatorProcess is null)
        {
            _status = _status with
            {
                ConnectionState = SimConnectConnectionState.WaitingForSimulatorProcess,
                SimulatorProcess = null,
                LastErrorMessage = null,
                ClientPath = clientPath,
            };

            return _status;
        }

        try
        {
            _status = _status with
            {
                ConnectionState = SimConnectConnectionState.Connecting,
                SimulatorProcess = simulatorProcess,
                LastErrorMessage = null,
                ClientPath = clientPath,
            };

            await _simConnectClient.OpenAsync(_options, cancellationToken).ConfigureAwait(false);

            _status = _status with
            {
                ConnectionState = SimConnectConnectionState.Connected,
                SimulatorProcess = simulatorProcess,
                ConnectedUtc = DateTimeOffset.UtcNow,
                LastErrorMessage = null,
                ClientPath = ResolveClientPath(),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _status = _status with
            {
                ConnectionState = SimConnectConnectionState.Faulted,
                SimulatorProcess = simulatorProcess,
                LastErrorMessage = ex.Message,
                ClientPath = clientPath,
            };
        }

        return _status;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _simConnectClient.CloseAsync(cancellationToken).ConfigureAwait(false);
        _status = _status with
        {
            ConnectionState = SimConnectConnectionState.Disconnected,
        };
    }

    public async Task<SimConnectPollResult> PollAsync(CancellationToken cancellationToken = default)
    {
        var pollCount = _status.PollCount + 1;

        if (!_simConnectClient.IsConnected)
        {
            var status = await ConnectAsync(cancellationToken).ConfigureAwait(false);
            if (status.ConnectionState != SimConnectConnectionState.Connected)
            {
                _status = status with { PollCount = pollCount };
                return new SimConnectPollResult
                {
                    Status = _status,
                };
            }
        }

        try
        {
            var rawFrame = await _simConnectClient.ReadNextFrameAsync(cancellationToken).ConfigureAwait(false);
            if (rawFrame is null)
            {
                _status = _status with
                {
                    PollCount = pollCount,
                    NullPollCount = _status.NullPollCount + 1,
                    ClientPath = ResolveClientPath(),
                };

                return new SimConnectPollResult
                {
                    Status = _status,
                };
            }

            var telemetryFrame = _telemetryMapper.Map(rawFrame);
            _status = _status with
            {
                ConnectionState = SimConnectConnectionState.Connected,
                PollCount = pollCount,
                RawFrameCount = _status.RawFrameCount + 1,
                TelemetryFrameCount = _status.TelemetryFrameCount + 1,
                LastRawFrameUtc = rawFrame.TimestampUtc,
                LastTelemetryUtc = rawFrame.TimestampUtc,
                HasReceivedFlightCriticalData = _status.HasReceivedFlightCriticalData || rawFrame.HasFlightCriticalData,
                HasReceivedOperationalData = _status.HasReceivedOperationalData || rawFrame.HasOperationalData,
                ClientPath = ResolveClientPath(),
                LastErrorMessage = null,
            };

            return new SimConnectPollResult
            {
                Status = _status,
                RawFrame = rawFrame,
                TelemetryFrame = telemetryFrame,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _simConnectClient.CloseAsync(cancellationToken).ConfigureAwait(false);
            _status = _status with
            {
                ConnectionState = SimConnectConnectionState.Faulted,
                PollCount = pollCount,
                LastErrorMessage = ex.Message,
                ClientPath = ResolveClientPath(),
            };

            return new SimConnectPollResult
            {
                Status = _status,
            };
        }
    }

    private string ResolveClientPath() =>
        _simConnectClient is ISimConnectClientDiagnostics diagnostics
            ? diagnostics.DiagnosticsClientName
            : _simConnectClient.GetType().Name;
}
