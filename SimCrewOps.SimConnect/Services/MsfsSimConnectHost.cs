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
        var simulatorProcess = _simulatorProcessDetector.FindRunningSimulator(_options.SimulatorProcessNames);
        if (simulatorProcess is null)
        {
            _status = _status with
            {
                ConnectionState = SimConnectConnectionState.WaitingForSimulatorProcess,
                SimulatorProcess = null,
                LastErrorMessage = null,
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
            };

            await _simConnectClient.OpenAsync(_options, cancellationToken).ConfigureAwait(false);

            _status = _status with
            {
                ConnectionState = SimConnectConnectionState.Connected,
                SimulatorProcess = simulatorProcess,
                ConnectedUtc = DateTimeOffset.UtcNow,
                LastErrorMessage = null,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _status = _status with
            {
                ConnectionState = SimConnectConnectionState.Faulted,
                SimulatorProcess = simulatorProcess,
                LastErrorMessage = ex.Message,
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
        if (!_simConnectClient.IsConnected)
        {
            var status = await ConnectAsync(cancellationToken).ConfigureAwait(false);
            if (status.ConnectionState != SimConnectConnectionState.Connected)
            {
                return new SimConnectPollResult
                {
                    Status = status,
                };
            }
        }

        try
        {
            var rawFrame = await _simConnectClient.ReadNextFrameAsync(cancellationToken).ConfigureAwait(false);
            if (rawFrame is null)
            {
                return new SimConnectPollResult
                {
                    Status = _status,
                };
            }

            var telemetryFrame = _telemetryMapper.Map(rawFrame);
            _status = _status with
            {
                ConnectionState = SimConnectConnectionState.Connected,
                LastTelemetryUtc = rawFrame.TimestampUtc,
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
                LastErrorMessage = ex.Message,
            };

            return new SimConnectPollResult
            {
                Status = _status,
            };
        }
    }
}
