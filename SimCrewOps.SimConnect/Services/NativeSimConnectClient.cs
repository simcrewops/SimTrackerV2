using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public sealed class NativeSimConnectClient : ISimConnectClient, ISimConnectClientDiagnostics
{
    private readonly NativeSimConnectLibraryLocator _libraryLocator;
    private readonly INativeSimConnectBridgeFactory _bridgeFactory;
    private INativeSimConnectBridge? _bridge;

    public NativeSimConnectClient()
        : this(new NativeSimConnectLibraryLocator(), new NativeSimConnectBridgeFactory())
    {
    }

    internal NativeSimConnectClient(
        NativeSimConnectLibraryLocator libraryLocator,
        INativeSimConnectBridgeFactory bridgeFactory)
    {
        ArgumentNullException.ThrowIfNull(libraryLocator);
        ArgumentNullException.ThrowIfNull(bridgeFactory);

        _libraryLocator = libraryLocator;
        _bridgeFactory = bridgeFactory;
    }

    public bool IsConnected => _bridge?.IsConnected == true;
    public string DiagnosticsClientName => "Native SimConnect";

    public async Task OpenAsync(SimConnectHostOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Native SimConnect is only supported on Windows.");
        }

        if (_bridge?.IsConnected == true)
        {
            return;
        }

        var nativeLibraryHandle = _libraryLocator.LoadNativeLibrary(options);
        try
        {
            _bridge = await _bridgeFactory.CreateAsync(nativeLibraryHandle, options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            NativeLibrary.Free(nativeLibraryHandle);
            throw;
        }
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

public sealed class AdaptiveSimConnectClient : ISimConnectClient, ISimConnectClientDiagnostics
{
    private readonly ISimConnectClient _primaryClient;
    private readonly ISimConnectClient _fallbackClient;
    private ISimConnectClient? _activeClient;

    public AdaptiveSimConnectClient()
        : this(new NativeSimConnectClient(), new ManagedSimConnectClient())
    {
    }

    internal AdaptiveSimConnectClient(ISimConnectClient primaryClient, ISimConnectClient fallbackClient)
    {
        ArgumentNullException.ThrowIfNull(primaryClient);
        ArgumentNullException.ThrowIfNull(fallbackClient);

        _primaryClient = primaryClient;
        _fallbackClient = fallbackClient;
    }

    public bool IsConnected => _activeClient?.IsConnected == true;
    public string DiagnosticsClientName =>
        _activeClient is ISimConnectClientDiagnostics diagnostics
            ? diagnostics.DiagnosticsClientName
            : _activeClient?.GetType().Name ?? "Adaptive SimConnect (native first)";

    public async Task OpenAsync(SimConnectHostOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_activeClient?.IsConnected == true)
        {
            return;
        }

        Exception? primaryFailure = null;

        try
        {
            await _primaryClient.OpenAsync(options, cancellationToken).ConfigureAwait(false);
            _activeClient = _primaryClient;
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            primaryFailure = ex;
        }

        try
        {
            await _fallbackClient.OpenAsync(options, cancellationToken).ConfigureAwait(false);
            _activeClient = _fallbackClient;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Unable to open SimConnect via native or managed client. Native failure: {primaryFailure?.Message ?? "unknown"}. Managed fallback failure: {ex.Message}",
                ex);
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_primaryClient.IsConnected)
            {
                await _primaryClient.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (_fallbackClient.IsConnected)
            {
                await _fallbackClient.CloseAsync(cancellationToken).ConfigureAwait(false);
            }

            _activeClient = null;
        }
    }

    public Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default)
    {
        if (_activeClient is null)
        {
            throw new InvalidOperationException("SimConnect client is not open.");
        }

        return _activeClient.ReadNextFrameAsync(cancellationToken);
    }
}

internal sealed class NativeSimConnectBridgeFactory : INativeSimConnectBridgeFactory
{
    public Task<INativeSimConnectBridge> CreateAsync(
        nint nativeLibraryHandle,
        SimConnectHostOptions options,
        CancellationToken cancellationToken = default)
    {
        INativeSimConnectBridge bridge = new NativeSimConnectBridge(nativeLibraryHandle, options);
        return Task.FromResult(bridge);
    }
}

internal sealed class NativeSimConnectBridge : INativeSimConnectBridge
{
    private const uint FlightCriticalRequestId = 1;
    private const uint OperationalRequestId = 2;
    private const uint FlightCriticalDefinitionId = 11;
    private const uint OperationalDefinitionId = 12;
    private const uint UserObjectId = 0;

    private readonly nint _nativeLibraryHandle;
    private readonly SimConnectHostOptions _options;
    private readonly NativeSimConnectExports _exports;
    private readonly ConcurrentQueue<SimConnectRawTelemetryFrame> _frames = new();

    private nint _simConnectHandle;
    private LatestSimConnectState _latestState = new();
    private bool _disposed;

    public NativeSimConnectBridge(nint nativeLibraryHandle, SimConnectHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _nativeLibraryHandle = nativeLibraryHandle;
        _options = options;
        _exports = NativeSimConnectExports.Load(nativeLibraryHandle);

        OpenConnection();
        RegisterDefinitions();
    }

    public bool IsConnected => !_disposed && _simConnectHandle != nint.Zero;

    public async Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_frames.TryDequeue(out var bufferedFrame))
        {
            return bufferedFrame;
        }

        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < _options.FrameReadTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frame = TryDispatchNextMessage();
            if (frame is not null)
            {
                return frame;
            }

            await Task.Delay(15, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        _disposed = true;

        try
        {
            if (_simConnectHandle != nint.Zero)
            {
                ThrowIfFailed(_exports.Close(_simConnectHandle), "SimConnect_Close");
                _simConnectHandle = nint.Zero;
            }
        }
        finally
        {
            if (_nativeLibraryHandle != nint.Zero)
            {
                NativeLibrary.Free(_nativeLibraryHandle);
            }
        }

        return Task.CompletedTask;
    }

    private void OpenConnection()
    {
        var result = _exports.Open(
            out _simConnectHandle,
            _options.ClientName,
            nint.Zero,
            0,
            nint.Zero,
            0);

        ThrowIfFailed(result, "SimConnect_Open");
    }

    private void RegisterDefinitions()
    {
        RegisterDefinition(
            FlightCriticalDefinitionId,
            FlightCriticalRequestId,
            SimConnectDefinitionCatalog.FlightCriticalVariables,
            SimConnectPeriod.SimFrame);

        RegisterDefinition(
            OperationalDefinitionId,
            OperationalRequestId,
            SimConnectDefinitionCatalog.ScoringAndOperationalVariables,
            SimConnectPeriod.Second);
    }

    private void RegisterDefinition(
        uint definitionId,
        uint requestId,
        IReadOnlyList<SimConnectVariableDefinition> variables,
        SimConnectPeriod period)
    {
        for (var index = 0; index < variables.Count; index++)
        {
            var variable = variables[index];
            ThrowIfFailed(
                _exports.AddToDataDefinition(
                    _simConnectHandle,
                    definitionId,
                    variable.SimVarName,
                    NormalizeUnit(variable.Unit),
                    NormalizeValueType(variable),
                    0.0f,
                    (uint)index),
                $"SimConnect_AddToDataDefinition({variable.SimVarName})");
        }

        ThrowIfFailed(
            _exports.RequestDataOnSimObject(
                _simConnectHandle,
                requestId,
                definitionId,
                UserObjectId,
                period,
                0,
                0,
                0,
                0),
            $"SimConnect_RequestDataOnSimObject({requestId})");
    }

    private SimConnectRawTelemetryFrame? TryDispatchNextMessage()
    {
        var result = _exports.GetNextDispatch(_simConnectHandle, out var dispatchPointer, out _);
        if (IsNoDispatchAvailable(result))
        {
            return _frames.TryDequeue(out var emptyFrame) ? emptyFrame : null;
        }

        ThrowIfFailed(result, "SimConnect_GetNextDispatch");

        if (dispatchPointer == nint.Zero)
        {
            return _frames.TryDequeue(out var emptyFrame) ? emptyFrame : null;
        }

        var header = Marshal.PtrToStructure<SimConnectRecv>(dispatchPointer);
        switch ((SimConnectRecvId)header.RecvId)
        {
            case SimConnectRecvId.Null:
                break;
            case SimConnectRecvId.Open:
                System.Diagnostics.Debug.WriteLine("[SimConnect] SIMCONNECT_RECV_OPEN received — handshake complete, SimVars registered.");
                break;
            case SimConnectRecvId.Exception:
                var exception = Marshal.PtrToStructure<SimConnectRecvException>(dispatchPointer);
                // Non-fatal: log the exception and continue. A single bad SimVar name causes
                // SIMCONNECT_EXCEPTION_UNIMPLEMENTED (13) or DATA_ERROR (7); throwing here would
                // kill the entire session and suppress all data from the remaining valid vars.
                System.Diagnostics.Debug.WriteLine(
                    $"[SimConnect] SIMCONNECT_RECV_EXCEPTION code={exception.ExceptionCode} sendId={exception.SendId} index={exception.Index}");
                break;
            case SimConnectRecvId.Quit:
                throw new InvalidOperationException("Microsoft Flight Simulator closed the SimConnect session.");
            case SimConnectRecvId.SimObjectData:
                HandleSimObjectData(dispatchPointer);
                break;
        }

        return _frames.TryDequeue(out var frame) ? frame : null;
    }

    private void HandleSimObjectData(nint dispatchPointer)
    {
        var data = Marshal.PtrToStructure<SimConnectRecvSimObjectData>(dispatchPointer);
        var payloadPointer = nint.Add(dispatchPointer, Marshal.SizeOf<SimConnectRecvSimObjectData>());

        switch (data.RequestId)
        {
            case FlightCriticalRequestId:
                UpdateFlightCritical(Marshal.PtrToStructure<FlightCriticalSnapshot>(payloadPointer));
                break;
            case OperationalRequestId:
                UpdateOperational(Marshal.PtrToStructure<OperationalSnapshot>(payloadPointer));
                break;
        }
    }

    private void UpdateFlightCritical(FlightCriticalSnapshot snapshot)
    {
        _latestState = _latestState with
        {
            HasFlightCritical = true,
            Latitude = snapshot.Latitude,
            Longitude = snapshot.Longitude,
            AltitudeAglFeet = snapshot.AltitudeAglFeet,
            AltitudeFeet = snapshot.AltitudeFeet,
            IndicatedAltitudeFeet = snapshot.IndicatedAltitudeFeet,
            IndicatedAirspeedKnots = snapshot.IndicatedAirspeedKnots,
            GroundSpeedKnots = snapshot.GroundSpeedKnots,
            VerticalSpeedFpm = snapshot.VerticalSpeedFpm,
            BankAngleDegrees = snapshot.BankAngleDegrees,
            PitchAngleDegrees = snapshot.PitchAngleDegrees,
            ParkingBrakePosition = snapshot.ParkingBrakePosition,
            OnGround = snapshot.OnGround,
            CrashFlag = snapshot.CrashFlag,
        };

        EnqueueFrame();
    }

    private void UpdateOperational(OperationalSnapshot snapshot)
    {
        var lightStates = snapshot.LightStates;

        _latestState = _latestState with
        {
            HasOperational = true,
            HeadingMagneticDegrees = snapshot.HeadingMagneticDegrees,
            HeadingTrueDegrees = snapshot.HeadingTrueDegrees,
            TrueAirspeedKnots = snapshot.TrueAirspeedKnots,
            Mach = snapshot.Mach,
            GForce = snapshot.GForce,
            FlapsHandleIndex = snapshot.FlapsHandleIndex,
            GearPosition = snapshot.GearPosition,
            Engine1Running = snapshot.Engine1Running,
            Engine2Running = snapshot.Engine2Running,
            Engine3Running = snapshot.Engine3Running,
            Engine4Running = snapshot.Engine4Running,
            BeaconLightOn = SimConnectLightStateDecoder.IsBeaconOn(lightStates) ? 1 : 0,
            TaxiLightsOn = SimConnectLightStateDecoder.IsTaxiOn(lightStates) ? 1 : 0,
            LandingLightsOn = SimConnectLightStateDecoder.IsLandingOn(lightStates) ? 1 : 0,
            StrobesOn = SimConnectLightStateDecoder.IsStrobeOn(lightStates) ? 1 : 0,
            StallWarning = snapshot.StallWarning,
            GpwsAlert = snapshot.GpwsAlert,
            OverspeedWarning = snapshot.OverspeedWarning,
        };

        EnqueueFrame();
    }

    private void EnqueueFrame()
    {
        if (!_latestState.HasFlightCritical)
        {
            return;
        }

        _frames.Enqueue(new SimConnectRawTelemetryFrame
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            HasFlightCriticalData = _latestState.HasFlightCritical,
            HasOperationalData = _latestState.HasOperational,
            Latitude = _latestState.Latitude,
            Longitude = _latestState.Longitude,
            AltitudeAglFeet = _latestState.AltitudeAglFeet,
            AltitudeFeet = _latestState.AltitudeFeet,
            IndicatedAltitudeFeet = _latestState.IndicatedAltitudeFeet,
            IndicatedAirspeedKnots = _latestState.IndicatedAirspeedKnots,
            TrueAirspeedKnots = _latestState.TrueAirspeedKnots,
            Mach = _latestState.Mach,
            GroundSpeedKnots = _latestState.GroundSpeedKnots,
            VerticalSpeedFpm = _latestState.VerticalSpeedFpm,
            BankAngleDegrees = _latestState.BankAngleDegrees,
            PitchAngleDegrees = _latestState.PitchAngleDegrees,
            HeadingMagneticDegrees = _latestState.HeadingMagneticDegrees,
            HeadingTrueDegrees = _latestState.HeadingTrueDegrees,
            GForce = _latestState.GForce,
            ParkingBrakePosition = _latestState.ParkingBrakePosition,
            OnGround = _latestState.OnGround,
            CrashFlag = _latestState.CrashFlag,
            FlapsHandleIndex = _latestState.FlapsHandleIndex,
            GearPosition = _latestState.GearPosition,
            BeaconLightOn = _latestState.BeaconLightOn,
            TaxiLightsOn = _latestState.TaxiLightsOn,
            LandingLightsOn = _latestState.LandingLightsOn,
            StrobesOn = _latestState.StrobesOn,
            StallWarning = _latestState.StallWarning,
            GpwsAlert = _latestState.GpwsAlert,
            OverspeedWarning = _latestState.OverspeedWarning,
            Engine1Running = _latestState.Engine1Running,
            Engine2Running = _latestState.Engine2Running,
            Engine3Running = _latestState.Engine3Running,
            Engine4Running = _latestState.Engine4Running,
        });
    }

    internal static SimConnectDataType NormalizeValueType(SimConnectVariableDefinition definition) =>
        definition.ValueType switch
        {
            SimConnectValueType.Int32 => SimConnectDataType.Int32,
            _ when string.Equals(definition.Unit, "bool", StringComparison.OrdinalIgnoreCase) => SimConnectDataType.Int32,
            _ => SimConnectDataType.Float64,
        };

    private static string? NormalizeUnit(string? unit) => unit switch
    {
        "bool" => "Bool",
        "number" => "Number",
        "percent" => "Percent Over 100",
        "gforce" => "GForce",
        "mach" => "Mach",
        _ => unit,
    };

    private static void ThrowIfFailed(int hresult, string operation)
    {
        if (hresult >= 0)
        {
            return;
        }

        throw new InvalidOperationException($"{operation} failed with HRESULT 0x{hresult:X8}.", Marshal.GetExceptionForHR(hresult));
    }

    internal static bool IsNoDispatchAvailable(int hresult) => hresult == unchecked((int)0x80004005);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NativeSimConnectBridge));
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct FlightCriticalSnapshot
    {
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly double AltitudeAglFeet;
        public readonly double AltitudeFeet;
        public readonly double IndicatedAltitudeFeet;
        public readonly double IndicatedAirspeedKnots;
        public readonly double GroundSpeedKnots;
        public readonly double VerticalSpeedFpm;
        public readonly double BankAngleDegrees;
        public readonly double PitchAngleDegrees;
        public readonly int ParkingBrakePosition;
        public readonly int OnGround;
        public readonly int CrashFlag;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct OperationalSnapshot
    {
        public readonly double HeadingMagneticDegrees;
        public readonly double HeadingTrueDegrees;
        public readonly double TrueAirspeedKnots;
        public readonly double Mach;
        public readonly double GForce;
        public readonly int FlapsHandleIndex;
        public readonly double GearPosition;
        public readonly int Engine1Running;
        public readonly int Engine2Running;
        public readonly int Engine3Running;
        public readonly int Engine4Running;
        public readonly int BeaconLightOn;
        public readonly int TaxiLightsOn;
        public readonly int LandingLightsOn;
        public readonly int StrobesOn;
        public readonly int LightStates;
        public readonly int StallWarning;
        public readonly int GpwsAlert;
        public readonly int OverspeedWarning;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct SimConnectRecv
    {
        public readonly uint Size;
        public readonly uint Version;
        public readonly uint RecvId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct SimConnectRecvException
    {
        public readonly uint Size;
        public readonly uint Version;
        public readonly uint RecvId;
        public readonly uint ExceptionCode;
        public readonly uint SendId;
        public readonly uint Index;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct SimConnectRecvSimObjectData
    {
        public readonly uint Size;
        public readonly uint Version;
        public readonly uint RecvId;
        public readonly uint RequestId;
        public readonly uint ObjectId;
        public readonly uint DefineId;
        public readonly uint Flags;
        public readonly uint EntryNumber;
        public readonly uint OutOf;
        public readonly uint DefineCount;
    }

    private sealed record LatestSimConnectState
    {
        public bool HasFlightCritical { get; init; }
        public bool HasOperational { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double AltitudeAglFeet { get; init; }
        public double AltitudeFeet { get; init; }
        public double IndicatedAltitudeFeet { get; init; }
        public double IndicatedAirspeedKnots { get; init; }
        public double TrueAirspeedKnots { get; init; }
        public double Mach { get; init; }
        public double GroundSpeedKnots { get; init; }
        public double VerticalSpeedFpm { get; init; }
        public double BankAngleDegrees { get; init; }
        public double PitchAngleDegrees { get; init; }
        public double HeadingMagneticDegrees { get; init; }
        public double HeadingTrueDegrees { get; init; }
        public double GForce { get; init; }
        public double ParkingBrakePosition { get; init; }
        public double OnGround { get; init; }
        public double CrashFlag { get; init; }
        public double FlapsHandleIndex { get; init; }
        public double GearPosition { get; init; }
        public double BeaconLightOn { get; init; }
        public double TaxiLightsOn { get; init; }
        public double LandingLightsOn { get; init; }
        public double StrobesOn { get; init; }
        public double StallWarning { get; init; }
        public double GpwsAlert { get; init; }
        public double OverspeedWarning { get; init; }
        public double Engine1Running { get; init; }
        public double Engine2Running { get; init; }
        public double Engine3Running { get; init; }
        public double Engine4Running { get; init; }
    }
}

internal sealed class NativeSimConnectExports
{
    private NativeSimConnectExports(
        OpenDelegate open,
        CloseDelegate close,
        AddToDataDefinitionDelegate addToDataDefinition,
        RequestDataOnSimObjectDelegate requestDataOnSimObject,
        GetNextDispatchDelegate getNextDispatch)
    {
        Open = open;
        Close = close;
        AddToDataDefinition = addToDataDefinition;
        RequestDataOnSimObject = requestDataOnSimObject;
        GetNextDispatch = getNextDispatch;
    }

    public OpenDelegate Open { get; }
    public CloseDelegate Close { get; }
    public AddToDataDefinitionDelegate AddToDataDefinition { get; }
    public RequestDataOnSimObjectDelegate RequestDataOnSimObject { get; }
    public GetNextDispatchDelegate GetNextDispatch { get; }

    public static NativeSimConnectExports Load(nint libraryHandle) =>
        new(
            GetExport<OpenDelegate>(libraryHandle, "SimConnect_Open"),
            GetExport<CloseDelegate>(libraryHandle, "SimConnect_Close"),
            GetExport<AddToDataDefinitionDelegate>(libraryHandle, "SimConnect_AddToDataDefinition"),
            GetExport<RequestDataOnSimObjectDelegate>(libraryHandle, "SimConnect_RequestDataOnSimObject"),
            GetExport<GetNextDispatchDelegate>(libraryHandle, "SimConnect_GetNextDispatch"));

    private static TDelegate GetExport<TDelegate>(nint libraryHandle, string exportName)
        where TDelegate : Delegate
    {
        var export = NativeLibrary.GetExport(libraryHandle, exportName);
        return Marshal.GetDelegateForFunctionPointer<TDelegate>(export);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate int OpenDelegate(
        out nint simConnectHandle,
        string clientName,
        nint windowHandle,
        uint userEventWin32,
        nint eventHandle,
        uint configIndex);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int CloseDelegate(nint simConnectHandle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate int AddToDataDefinitionDelegate(
        nint simConnectHandle,
        uint defineId,
        string datumName,
        string? unitsName,
        SimConnectDataType datumType,
        float epsilon,
        uint datumId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int RequestDataOnSimObjectDelegate(
        nint simConnectHandle,
        uint requestId,
        uint defineId,
        uint objectId,
        SimConnectPeriod period,
        uint flags,
        uint origin,
        uint interval,
        uint limit);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int GetNextDispatchDelegate(
        nint simConnectHandle,
        out nint data,
        out uint dataSize);
}

internal enum SimConnectDataType : uint
{
    Invalid = 0,
    Int32 = 1,
    Int64 = 2,
    Float32 = 3,
    Float64 = 4,
    String8 = 5,
    String32 = 6,
    String64 = 7,
    String128 = 8,
    String256 = 9,
    String260 = 10,
    StringV = 11,
    InitPosition = 12,
    MarkerState = 13,
    Waypoint = 14,
    LatLonAlt = 15,
    Xyz = 16,
    Max = 17,
}

internal enum SimConnectPeriod : uint
{
    Never = 0,
    Once = 1,
    VisualFrame = 2,
    SimFrame = 3,
    Second = 4,
}

internal enum SimConnectRecvId : uint
{
    Null = 0,
    Exception = 1,
    Open = 2,
    Quit = 3,
    Event = 4,
    EventObjectAddRemove = 5,
    EventFilename = 6,
    EventFrame = 7,
    SimObjectData = 8,
}
