using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services.Aircraft;

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
    private const uint OperationalRequestId    = 2;
    private const uint AircraftStateRequestId  = 3;    // for RequestSystemState("AircraftLoaded")
    private const uint AtcModelRequestId          = 4;    // for ATC MODEL string SimVar (once per aircraft load)
    private const uint GpsDestinationRequestId    = 5;    // for GPS DESTINATION AIRPORT IDENT (polled each frame)
    private const uint FlightCriticalDefinitionId = 11;
    private const uint OperationalDefinitionId    = 12;
    private const uint AtcModelDefinitionId       = 13;   // separate from numeric struct definitions
    private const uint GpsDestinationDefinitionId = 14;   // separate string SimVar definition
    private const uint UserObjectId = 0;

    private readonly nint _nativeLibraryHandle;
    private readonly SimConnectHostOptions _options;
    private readonly NativeSimConnectExports _exports;
    private readonly ConcurrentQueue<SimConnectRawTelemetryFrame> _frames = new();

    private nint _simConnectHandle;
    private LatestSimConnectState _latestState = new();
    private AircraftProfile _activeProfile = AircraftProfile.Default;
    private string? _detectedAircraftTitle;   // file-based detection, used until SimVar arrives
    private string? _atcModelSimVar;           // ATC MODEL SimVar — overrides file-based value when available
    private string? _gpsDestinationIdent;      // GPS DESTINATION AIRPORT IDENT — destination from active GPS plan
    private readonly MobiFlightBridge _mobiFlightBridge = new();
    private bool _disposed;

    public NativeSimConnectBridge(nint nativeLibraryHandle, SimConnectHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _nativeLibraryHandle = nativeLibraryHandle;
        _options = options;
        _exports = NativeSimConnectExports.Load(nativeLibraryHandle);

        OpenConnection();
        RegisterDefinitions();
        RequestAircraftState();
        _mobiFlightBridge.Initialize(_simConnectHandle, _exports);
    }

    public bool IsConnected => !_disposed && _simConnectHandle != nint.Zero;

    public async Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var started = DateTimeOffset.UtcNow;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Drain ALL available SimConnect messages in one pass.
            // SIMCONNECT_PERIOD_SIM_FRAME queues 30–60 messages per second; if we only
            // dequeue one message per poll the queue grows without bound and the data
            // shown becomes increasingly stale (effectively "frozen" after ~30 seconds).
            DrainAllAvailableMessages();

            // Discard intermediate frames — keep only the most recent snapshot so the
            // UI always reflects the current sim state, not data from seconds ago.
            SimConnectRawTelemetryFrame? latest = null;
            while (_frames.TryDequeue(out var f))
            {
                latest = f;
            }

            if (latest is not null)
            {
                return latest;
            }

            await Task.Delay(15, cancellationToken).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow - started < _options.FrameReadTimeout);

        return null;
    }

    private void DrainAllAvailableMessages()
    {
        while (true)
        {
            var result = _exports.GetNextDispatch(_simConnectHandle, out var dispatchPointer, out _);
            if (IsNoDispatchAvailable(result))
            {
                return;
            }

            ThrowIfFailed(result, "SimConnect_GetNextDispatch");

            if (dispatchPointer == nint.Zero)
            {
                return;
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
                    // Non-fatal: log and continue. A single bad SimVar name causes
                    // SIMCONNECT_EXCEPTION_UNIMPLEMENTED (13) or DATA_ERROR (7).
                    System.Diagnostics.Debug.WriteLine(
                        $"[SimConnect] SIMCONNECT_RECV_EXCEPTION code={exception.ExceptionCode} sendId={exception.SendId} index={exception.Index}");
                    break;
                case SimConnectRecvId.Quit:
                    throw new InvalidOperationException("Microsoft Flight Simulator closed the SimConnect session.");
                case SimConnectRecvId.SimObjectData:
                    HandleSimObjectData(dispatchPointer);
                    break;
                case SimConnectRecvId.ClientData:
                    _mobiFlightBridge.HandleClientData(dispatchPointer);
                    break;
                case SimConnectRecvId.SystemState:
                    HandleSystemState(dispatchPointer);
                    break;
            }
        }
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

        // Register the ATC MODEL string SimVar definition.
        // String SimVars must be in their own definition — they cannot share a
        // definition with Float64 SimVars because the data layout is incompatible.
        var result = _exports.AddToDataDefinition(
            _simConnectHandle,
            AtcModelDefinitionId,
            "ATC MODEL",
            null,  // string SimVars have no unit
            SimConnectDataType.String256,
            0.0f,
            0);
        if (result < 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SimConnect] AddToDataDefinition(ATC MODEL) failed: 0x{result:X8}");
        }

        // Register the GPS DESTINATION AIRPORT IDENT string SimVar.
        // Returns the ICAO of the active GPS flight plan destination (e.g. "KLAX").
        // Used to auto-detect arrival airport for runway resolution on free flights.
        result = _exports.AddToDataDefinition(
            _simConnectHandle,
            GpsDestinationDefinitionId,
            "GPS DESTINATION AIRPORT IDENT",
            null,
            SimConnectDataType.String256,
            0.0f,
            0);
        if (result < 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SimConnect] AddToDataDefinition(GPS DESTINATION AIRPORT IDENT) failed: 0x{result:X8}");
        }
    }

    private void RequestAircraftState()
    {
        // Ask SimConnect which aircraft is currently loaded.
        // The response arrives as SIMCONNECT_RECV_SYSTEM_STATE (RecvId 15) with
        // szString = path to the aircraft .air / .cfg file, e.g.:
        //   "Community\fenix-a319\SimObjects\Airplanes\fenix_a319\fenix_a319.air"
        // We match this path against AircraftProfileCatalog to select the right
        // variable-mapping profile for lights etc.
        var result = _exports.RequestSystemState(_simConnectHandle, AircraftStateRequestId, "AircraftLoaded");
        if (result < 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SimConnect] RequestSystemState(AircraftLoaded) failed: 0x{result:X8} — defaulting to standard SimVar profile.");
        }
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
            case AtcModelRequestId:
                // ATC MODEL is a String256 SimVar — the payload starts immediately after
                // the SimConnectRecvSimObjectData header (no intermediate struct needed).
                var atcModel = Marshal.PtrToStructure<AtcModelSnapshot>(payloadPointer);
                var rawAtcModel = atcModel.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(rawAtcModel))
                {
                    // MSFS 2024 returns an internal model key instead of a plain ICAO code, e.g.:
                    //   "ATCCOM.AC_MODEL A319.0.text"  →  "A319"
                    //   "ATCCOM.AC_MODEL B738.0.text"  →  "B738"
                    // MSFS 2020 returns the clean ICAO code directly ("A319", "B738", …).
                    // ParseAtcModelValue handles both formats.
                    _atcModelSimVar = ParseAtcModelValue(rawAtcModel);
                    System.Diagnostics.Debug.WriteLine($"[SimConnect] ATC MODEL raw     : {rawAtcModel}");
                    System.Diagnostics.Debug.WriteLine($"[SimConnect] ATC MODEL parsed  : {_atcModelSimVar}");
                }
                break;
            case GpsDestinationRequestId:
                var gpsDest = Marshal.PtrToStructure<AtcModelSnapshot>(payloadPointer);
                var rawGpsDest = gpsDest.Value?.Trim();
                // Accept any non-empty string that looks like an ICAO ident (3–4 chars, starts with letter).
                if (!string.IsNullOrWhiteSpace(rawGpsDest) && rawGpsDest.Length is >= 3 and <= 5
                    && char.IsLetter(rawGpsDest[0]))
                {
                    _gpsDestinationIdent = rawGpsDest.ToUpperInvariant();
                }
                else
                {
                    _gpsDestinationIdent = null;
                }
                break;
        }
    }

    private void HandleSystemState(nint dispatchPointer)
    {
        var state = Marshal.PtrToStructure<SimConnectRecvSystemState>(dispatchPointer);

        if (state.RequestId != AircraftStateRequestId)
        {
            return;
        }

        var aircraftPath = state.StringValue ?? string.Empty;
        _activeProfile = AircraftProfileCatalog.MatchOrDefault(aircraftPath);

        // Detection priority (most → least accurate):
        //   1. FLTSIM section match — parse aircraft.cfg to find the [FLTSIM.n] section
        //                             whose "sim =" value matches the loaded .air filename;
        //                             read that section's atc_model. This is the only
        //                             approach that distinguishes variants inside a combined
        //                             package (e.g. Fenix A32X with A319/A320/A321 in one
        //                             folder sharing a single aircraft.cfg).
        //   2. .air filename heuristic — "FNX_A319.air" encodes the variant in the name,
        //                             so a simple pattern scan works for many addons even
        //                             without FLTSIM match (e.g. if aircraft.cfg has no
        //                             per-variant atc_model).
        //   3. Profile IcaoType   — hardcoded for known addons, but catch-all profiles
        //                           (e.g. generic "fnx" → A320) can be wrong for variants;
        //                           used only after the two variant-specific checks above.
        //   4. atc_model in cfg   — [GENERAL] section value; shared across all variants
        //                           in combined packages, used as a broad fallback.
        //   5. ParseAircraftTitle — community package folder name (last resort).
        _detectedAircraftTitle = ReadVariantIcaoFromAircraftCfg(aircraftPath)
            ?? ExtractIcaoFromAircraftPath(aircraftPath)
            ?? _activeProfile.IcaoType
            ?? ReadTitleFromAircraftCfg(aircraftPath)
            ?? ParseAircraftTitle(aircraftPath);

        System.Diagnostics.Debug.WriteLine($"[SimConnect] Aircraft loaded: {aircraftPath}");
        System.Diagnostics.Debug.WriteLine($"[SimConnect] Detected title : {_detectedAircraftTitle}");
        System.Diagnostics.Debug.WriteLine($"[SimConnect] Active profile : {_activeProfile.Name}" +
            (_activeProfile.RequiresLvarBridge ? " (LVAR bridge required)" : string.Empty));

        // Clear any stale ATC MODEL value from the previously loaded aircraft, then
        // request a fresh read. The response arrives asynchronously as SimObjectData
        // and will override _detectedAircraftTitle in EnqueueFrame once received.
        _atcModelSimVar = null;
        var atcResult = _exports.RequestDataOnSimObject(
            _simConnectHandle,
            AtcModelRequestId,
            AtcModelDefinitionId,
            UserObjectId,
            SimConnectPeriod.Once,
            0, 0, 0, 0);
        if (atcResult < 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SimConnect] RequestDataOnSimObject(ATC MODEL) failed: 0x{atcResult:X8}");
        }

        // Poll GPS destination once per second. The destination can change when the pilot
        // modifies the flight plan mid-flight, so use Period.Second rather than Once.
        var gpsResult = _exports.RequestDataOnSimObject(
            _simConnectHandle,
            GpsDestinationRequestId,
            GpsDestinationDefinitionId,
            UserObjectId,
            SimConnectPeriod.Second,
            0, 0, 0, 0);
        if (gpsResult < 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SimConnect] RequestDataOnSimObject(GPS DESTINATION) failed: 0x{gpsResult:X8}");
        }

        // Subscribe to profile LVARs via MobiFlight (deferred if bridge not yet Active).
        _mobiFlightBridge.SubscribeToProfile(_activeProfile);
    }

    private void UpdateFlightCritical(FlightCriticalSnapshot snapshot)
    {
        // Primary: SIM ON GROUND SimVar — now reliable because we request it as Float64.
        // The previous Int32 mixed-type struct had alignment issues that made it always read 0.
        // Fallback: AGL + VS heuristic for any MSFS build where the SimVar is still broken:
        //   AGL < 30 ft AND VS > -500 fpm distinguishes ground from approach:
        //   • Taxiing / landing roll: AGL 3-15 ft, VS ≈ 0          → true
        //   • ILS approach at 30 ft: VS ≈ -700 fpm                 → false
        //   • Post-touchdown: AGL → 0, VS recovers above -500       → true
        //   • Cruise / climb: AGL is thousands of feet              → false
        var onGroundSimVar = snapshot.OnGround >= 0.5;
        var onGroundHeuristic = snapshot.AltitudeAglFeet < 30.0 && snapshot.VerticalSpeedFpm > -500.0;
        var onGround = onGroundSimVar || onGroundHeuristic ? 1 : 0;

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
            OnGround = onGround,
            CrashFlag = snapshot.CrashFlag,
            VelocityWorldYFps = snapshot.VelocityWorldYFps,
        };

        EnqueueFrame();
    }

    private void UpdateOperational(OperationalSnapshot snapshot)
    {
        // Primary source: individual LIGHT_ bool SimVars (one per channel).
        // Supplementary source: LIGHT STATES bitmask OR'd in per-bit.
        //
        // History:
        //   • The old "use bitmask if all individual vars are zero" fallback was removed because
        //     LIGHT STATES was always 0x0000 on MSFS 2024 XGP (the SimVar was registered as Int32
        //     in a mixed Int32/Float64 struct, which caused struct alignment padding to corrupt it).
        //   • With the all-Float64 struct fix, LIGHT STATES is now requested as Float64 and returns
        //     the correct integer bitmask cast to a double (e.g. 0x000A → 10.0). It is now safe to
        //     OR the bitmask bits in: if either source says a light is on, it is on.
        //   • This matters for LIGHT TAXI: some MSFS 2024 builds return 0 for the individual
        //     LIGHT TAXI SimVar even when the taxi light is on. The bitmask bit 0x0008 fills the gap.
        //
        // All snapshot fields are double (uniform Float64 struct).
        // Cast to int only for the diagnostic Raw fields.
        var rawBeacon  = snapshot.BeaconLightOn;
        var rawTaxi    = snapshot.TaxiLightsOn;
        var rawLanding = snapshot.LandingLightsOn;
        var rawStrobe  = snapshot.StrobesOn;
        var lightStates = snapshot.LightStates;
        var bitmaskInt  = (int)lightStates;

        // OR individual SimVar with corresponding bitmask bit — whichever source is authoritative wins.
        var beacon  = rawBeacon  >= 0.5 || SimConnectLightStateDecoder.IsBeaconOn(bitmaskInt)  ? 1.0 : 0.0;
        var taxi    = rawTaxi    >= 0.5 || SimConnectLightStateDecoder.IsTaxiOn(bitmaskInt)    ? 1.0 : 0.0;
        var landing = rawLanding >= 0.5 || SimConnectLightStateDecoder.IsLandingOn(bitmaskInt) ? 1.0 : 0.0;
        var strobe  = rawStrobe  >= 0.5 || SimConnectLightStateDecoder.IsStrobeOn(bitmaskInt)  ? 1.0 : 0.0;

        // When the MobiFlight WASM bridge is active and the aircraft profile maps a light
        // channel to an LVAR, the LVAR value is authoritative — it replaces the SimVar read.
        // This covers aircraft like Fenix A319/A320 and Aerosoft CRJ where the standard
        // LIGHT TAXI / LIGHT LANDING SimVars are unconnected.
        if (_mobiFlightBridge.IsActive)
        {
            beacon  = ResolveLvar(_activeProfile.BeaconLight,  beacon);
            taxi    = ResolveLvar(_activeProfile.TaxiLight,    taxi);
            landing = ResolveLvar(_activeProfile.LandingLight, landing);
            strobe  = ResolveLvar(_activeProfile.StrobeLight,  strobe);
        }

        var usedIndividual = true;

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
            BeaconLightOn  = beacon,
            TaxiLightsOn   = taxi,
            LandingLightsOn = landing,
            StrobesOn      = strobe,
            LightStatesRaw = bitmaskInt,
            LightBeaconRaw = (int)rawBeacon,
            LightTaxiRaw   = (int)rawTaxi,
            LightLandingRaw = (int)rawLanding,
            LightStrobeRaw = (int)rawStrobe,
            LightSourceIsIndividual = usedIndividual,
            StallWarning = snapshot.StallWarning,
            GpwsAlert = snapshot.GpwsAlert,
            OverspeedWarning = snapshot.OverspeedWarning,
            Nav1GlideslopeErrorDegrees = snapshot.Nav1GlideslopeErrorDegrees,
            Nav1RadialErrorDegrees = snapshot.Nav1RadialErrorDegrees,
        };

        EnqueueFrame();
    }

    /// <summary>
    /// Extracts a clean ICAO type code from the raw <c>ATC MODEL</c> SimVar value.
    ///
    /// MSFS 2020 returns the code directly ("A319", "B738").
    /// MSFS 2024 returns an internal model key: "ATCCOM.AC_MODEL A319.0.text".
    /// Both formats are handled:
    /// <list type="bullet">
    ///   <item>If "AC_MODEL " is present, the token immediately after it is extracted
    ///         and everything from its first dot onward is stripped.</item>
    ///   <item>Otherwise the value is returned as-is (already a plain ICAO code).</item>
    /// </list>
    /// </summary>
    internal static string ParseAtcModelValue(string raw)
    {
        // Look for the "AC_MODEL " marker (case-insensitive).
        const string marker = "AC_MODEL ";
        var idx = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var typeStart = idx + marker.Length;
            // The type code ends at the first dot, e.g. "A319.0.text" → "A319".
            var dotIdx = raw.IndexOf('.', typeStart);
            return dotIdx > typeStart
                ? raw[typeStart..dotIdx]
                : raw[typeStart..];
        }

        // Already a clean ICAO code — return as-is.
        return raw;
    }

    /// <summary>
    /// Reads the ICAO type code for the specific variant that is currently loaded by
    /// matching the loaded <c>.air</c> filename against the <c>sim =</c> key in each
    /// <c>[FLTSIM.n]</c> section of the aircraft's <c>aircraft.cfg</c>, then returning
    /// <c>atc_model</c> from that section.
    ///
    /// This is the most accurate approach for combined packages (e.g. Fenix A32X) where
    /// A319/A320/A321 live in a single folder. MSFS loads a variant-specific <c>.air</c>
    /// file (e.g. <c>FNX_A319.air</c>) even when the package is combined, and each
    /// <c>[FLTSIM.n]</c> section carries its own <c>atc_model</c>.
    ///
    /// Returns <c>null</c> when:
    /// – the path does not end in <c>.air</c> (e.g. MSFS reported a <c>.cfg</c> path),
    /// – no matching section is found, or
    /// – the matching section has no <c>atc_model</c> key.
    /// </summary>
    private static string? ReadVariantIcaoFromAircraftCfg(string aircraftPath)
    {
        if (!aircraftPath.EndsWith(".air", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            // The filename without extension is exactly the value of "sim =" in the
            // matching [FLTSIM.n] section (e.g. "FNX_A319" for FNX_A319.air).
            var simName = Path.GetFileNameWithoutExtension(aircraftPath);
            if (string.IsNullOrEmpty(simName)) return null;

            var dir = Path.GetDirectoryName(aircraftPath);
            if (string.IsNullOrEmpty(dir)) return null;

            var cfgPath = Path.Combine(dir, "aircraft.cfg");
            if (!File.Exists(cfgPath)) return null;

            // Buffer the current section's sim= and atc_model= values.
            // When we move to the next section we can check if the buffered section matched.
            string? sectionSim = null;
            string? sectionAtcModel = null;
            var inFltsim = false;
            var lineCount = 0;

            foreach (var rawLine in File.ReadLines(cfgPath))
            {
                // Safety cap — aircraft.cfg files can be large in big livery packs.
                if (++lineCount > 4000) break;

                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == ';') continue;

                if (line[0] == '[')
                {
                    // Leaving a FLTSIM section: check if it was the target.
                    if (inFltsim
                        && sectionSim?.Equals(simName, StringComparison.OrdinalIgnoreCase) == true
                        && sectionAtcModel is not null)
                    {
                        return sectionAtcModel;
                    }

                    // Reset for the new section.
                    sectionSim      = null;
                    sectionAtcModel = null;
                    inFltsim = line.StartsWith("[FLTSIM.", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inFltsim) continue;

                var eq = line.IndexOf('=');
                if (eq < 0) continue;

                var key   = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (key.Equals("sim", StringComparison.OrdinalIgnoreCase))
                    sectionSim = value;
                else if (key.Equals("atc_model", StringComparison.OrdinalIgnoreCase))
                    sectionAtcModel = value;
            }

            // Handle a matching section at end of file (no trailing section header).
            if (inFltsim
                && sectionSim?.Equals(simName, StringComparison.OrdinalIgnoreCase) == true
                && sectionAtcModel is not null)
            {
                return sectionAtcModel;
            }

            return null;
        }
        catch
        {
            return null; // File locked, path invalid, etc.
        }
    }

    /// <summary>
    /// Extracts an ICAO type code from the aircraft .air filename and its immediate
    /// parent folder name. MSFS always loads the variant-specific .air file, so the
    /// filename is a reliable variant indicator even when the package bundles multiple
    /// variants in one folder with a shared aircraft.cfg.
    ///
    /// Examples:
    ///   "...FNX_32X\fnx_a319.air"  → "A319"
    ///   "...FNX_32X\fnx_a320.air"  → "A320"
    ///   "...pmdg-737-800\b738.air" → "B738"
    /// </summary>
    private static string? ExtractIcaoFromAircraftPath(string aircraftPath)
    {
        if (string.IsNullOrWhiteSpace(aircraftPath))
            return null;

        // Build a short candidate string from just the filename and its parent folder.
        // Searching only these two segments avoids false matches from package root names.
        var fileName     = Path.GetFileNameWithoutExtension(aircraftPath) ?? string.Empty;
        var parentFolder = Path.GetFileName(Path.GetDirectoryName(aircraftPath)) ?? string.Empty;
        var candidates   = $"{fileName} {parentFolder}";

        // Ordered most-specific → least-specific so "a319" beats "a31x" style substrings.
        // Each tuple: (search token, ICAO designator to return).
        ReadOnlySpan<(string Token, string Icao)> patterns =
        [
            // Airbus narrow-body family
            ("a318", "A318"), ("a319", "A319"), ("a320", "A320"), ("a321", "A321"),
            // Airbus wide-body
            ("a220", "A220"), ("a310", "A310"),
            ("a330", "A330"), ("a340", "A340"), ("a350", "A350"), ("a380", "A380"),
            // Boeing narrow-body
            ("b737", "B737"), ("b738", "B738"), ("b739", "B739"),
            ("b757", "B752"),
            // Boeing wide-body
            ("b767", "B763"), ("b777", "B77W"), ("b787", "B789"),
            ("b747", "B744"), ("b748", "B748"),
            // Regional jets
            ("crj7", "CRJ7"), ("crj9", "CRJ9"), ("crj2", "CRJ2"),
            ("e170", "E170"), ("e175", "E175"), ("e190", "E190"), ("e195", "E195"),
            // Turboprops
            ("q400", "DH8D"), ("atr72", "AT75"), ("atr42", "AT43"),
        ];

        foreach (var (token, icao) in patterns)
        {
            if (candidates.Contains(token, StringComparison.OrdinalIgnoreCase))
                return icao;
        }

        return null;
    }

    /// <summary>
    /// Reads the human-readable aircraft title directly from the aircraft.cfg file
    /// that MSFS reports in the AircraftLoaded system state.
    ///
    /// Looks for <c>title =</c> under the <c>[GENERAL]</c> section first, then falls
    /// back to the first <c>[FLTSIM.x]</c> variant title. Returns null if the file
    /// cannot be found or read (e.g. path is a .air file, not a .cfg).
    /// </summary>
    /// <summary>
    /// Reads the best available aircraft identifier from the aircraft.cfg file.
    ///
    /// Priority order:
    ///   1. atc_model in [GENERAL] — ICAO type designator (e.g. "A320", "B738").
    ///      This is the cleanest source: it is the standard ICAO code set by the
    ///      aircraft developer and works regardless of livery package folder names.
    ///   2. title in [GENERAL] — developer-level aircraft title.
    ///   3. title in [FLTSIM.0] — first livery title (often verbose, last resort).
    /// </summary>
    private static string? ReadTitleFromAircraftCfg(string aircraftPath)
    {
        try
        {
            // Build the .cfg path: use as-is if it already points to a .cfg,
            // otherwise look for aircraft.cfg in the same directory.
            string cfgPath;
            if (aircraftPath.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
            {
                cfgPath = aircraftPath;
            }
            else
            {
                var dir = Path.GetDirectoryName(aircraftPath);
                if (string.IsNullOrEmpty(dir)) return null;
                cfgPath = Path.Combine(dir, "aircraft.cfg");
            }

            if (!File.Exists(cfgPath)) return null;

            string? generalAtcModel = null;
            string? generalTitle = null;
            string? firstFltsimTitle = null;
            var inGeneral = false;
            var inFirstFltsim = false;
            var fltsimSeen = false;

            // Read only up to 300 lines — the GENERAL section is always near the top.
            var lineCount = 0;
            foreach (var rawLine in File.ReadLines(cfgPath))
            {
                if (++lineCount > 300) break;

                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == ';') continue;

                if (line[0] == '[')
                {
                    // Once we leave [GENERAL] and already have atc_model we're done.
                    if (inGeneral && generalAtcModel is not null) break;

                    inGeneral     = line.Equals("[GENERAL]",  StringComparison.OrdinalIgnoreCase);
                    inFirstFltsim = !fltsimSeen &&
                                    line.StartsWith("[FLTSIM.", StringComparison.OrdinalIgnoreCase);
                    if (inFirstFltsim) fltsimSeen = true;
                    continue;
                }

                var eq = line.IndexOf('=');
                if (eq < 0) continue;
                var value = line[(eq + 1)..].Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (inGeneral)
                {
                    // atc_model is the ICAO type code — highest priority.
                    if (line.StartsWith("atc_model", StringComparison.OrdinalIgnoreCase))
                    {
                        generalAtcModel = value;
                    }
                    else if (generalTitle is null &&
                             line.StartsWith("title", StringComparison.OrdinalIgnoreCase))
                    {
                        generalTitle = value;
                    }
                }
                else if (inFirstFltsim && firstFltsimTitle is null &&
                         line.StartsWith("title", StringComparison.OrdinalIgnoreCase))
                {
                    firstFltsimTitle = value;
                }

                // Stop as soon as we have everything we need.
                if (generalAtcModel is not null && firstFltsimTitle is not null) break;
            }

            return generalAtcModel ?? generalTitle ?? firstFltsimTitle;
        }
        catch
        {
            return null; // File locked, path invalid, etc. — fall through to path parsing.
        }
    }

    /// <summary>
    ///   "Community\fenix-a319\Aircraft\A319\fenix_a319.air"              → "fenix-a319"
    ///   "Official\OneStore\asobo-aircraft-a320neo\...\aircraft.cfg"      → "asobo-aircraft-a320neo"
    ///   "Official\Base\asobo-aircraft-a320neo\...\config\aircraft.cfg"   → "asobo-aircraft-a320neo"
    ///   "SimObjects\Airplanes\Asobo_B787_10\B787_10.air"                 → "Asobo_B787_10"
    /// </summary>
    private static string? ParseAircraftTitle(string aircraftPath)
    {
        if (string.IsNullOrWhiteSpace(aircraftPath))
            return null;

        var parts = aircraftPath
            .Replace('/', '\\')
            .Split('\\', StringSplitOptions.RemoveEmptyEntries);

        // ── Primary scan: package folder structure ─────────────────────────────
        // MSFS path layouts:
        //   ...\Packages\Community\<package>\...          → parts[i+1] after "Community"
        //   ...\Packages\Official\<channel>\<package>\... → parts[i+2] after "Official"
        //     where <channel> is OneStore | Steam | Base | Marketplace | etc.
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (string.Equals(parts[i], "Community", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];

            if (string.Equals(parts[i], "Official", StringComparison.OrdinalIgnoreCase)
                && i + 2 < parts.Length)
                return parts[i + 2];
        }

        // ── Secondary scan: SimObjects vehicle container ───────────────────────
        // Relative paths that skip the package root (e.g. "SimObjects\Airplanes\Asobo_B787_10\...").
        // The aircraft folder name sits immediately after the vehicle-type directory.
        var vehicleContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Airplanes", "Helicopters", "Rotorcraft", "Boats", "GroundVehicles" };

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (vehicleContainers.Contains(parts[i]))
                return parts[i + 1];
        }

        // ── Final fallback: walk from the end, skip files and generic folders ──
        var genericFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "config", "data", "sounds", "texture", "model", "panel", "effects", "SimObjects" };

        for (var i = parts.Length - 1; i >= 0; i--)
        {
            var seg = parts[i];
            if (seg.Contains('.')) continue;           // skip files (.air, .cfg, …)
            if (genericFolders.Contains(seg)) continue; // skip generic sub-folders
            return seg;
        }

        return null;
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
            LightStatesRaw = _latestState.LightStatesRaw,
            LightBeaconRaw = _latestState.LightBeaconRaw,
            LightTaxiRaw = _latestState.LightTaxiRaw,
            LightLandingRaw = _latestState.LightLandingRaw,
            LightStrobeRaw = _latestState.LightStrobeRaw,
            LightSourceIsIndividual = _latestState.LightSourceIsIndividual,
            StallWarning = _latestState.StallWarning,
            GpwsAlert = _latestState.GpwsAlert,
            OverspeedWarning = _latestState.OverspeedWarning,
            Engine1Running = _latestState.Engine1Running,
            Engine2Running = _latestState.Engine2Running,
            Engine3Running = _latestState.Engine3Running,
            Engine4Running = _latestState.Engine4Running,
            VelocityWorldYFps   = _latestState.VelocityWorldYFps,
            ActiveProfileName   = _activeProfile.Name,
            LvarBridgeRequired  = _activeProfile.RequiresLvarBridge,
            LvarBridgeConnected = _mobiFlightBridge.IsActive,
            // ATC MODEL SimVar is the authoritative source (same one vPilot / Volanta use).
            // Fall back to the file-based detection until the SimVar response arrives.
            AircraftTitle            = _atcModelSimVar ?? _detectedAircraftTitle,
            Nav1GlideslopeErrorDegrees = _latestState.Nav1GlideslopeErrorDegrees,
            Nav1RadialErrorDegrees   = _latestState.Nav1RadialErrorDegrees,
            GpsDestinationIdent      = _gpsDestinationIdent,
        });
    }

    /// <summary>
    /// Returns the LVAR-sourced value when the mapping points to an LVAR and the bridge
    /// is active; otherwise returns the existing SimVar-derived value unchanged.
    /// </summary>
    private double ResolveLvar(LightVariableMapping mapping, double simVarValue)
    {
        if (mapping.Source != LightVariableSource.LVar || string.IsNullOrEmpty(mapping.LvarName))
        {
            return simVarValue;
        }

        var lvarFloat = _mobiFlightBridge.GetValue(mapping.LvarName);
        return lvarFloat >= (float)mapping.OnThreshold ? 1.0 : 0.0;
    }

    internal static SimConnectDataType NormalizeValueType(SimConnectVariableDefinition definition) =>
        // Always request Float64 regardless of logical type.
        // Mixed Int32/Float64 definitions cause struct alignment problems on the native
        // SimConnect DLL — some MSFS builds pad Int32 fields to 8 bytes in the data packet,
        // shifting all subsequent fields and causing bool SimVars to read wrong (always 0).
        // Float64 structs are uniform in size and have no alignment ambiguity.
        // Values ≤ 2^53 (covers all bool/int fields we use) are exactly representable.
        SimConnectDataType.Float64;

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

    // All fields are double — uniform 8-byte layout, no mixed-type alignment issues.
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
        public readonly double ParkingBrakePosition;  // bool SimVar → 0.0 or 1.0
        public readonly double OnGround;              // SIM ON GROUND → 0.0 or 1.0
        public readonly double CrashFlag;             // bool SimVar → 0.0 or 1.0
        // Physics-engine vertical velocity (ft/s, negative = descending, no barometric lag).
        // Must stay last to match velocity_world_y appended at the end of FlightCriticalVariables.
        public readonly double VelocityWorldYFps;
    }

    // All fields are double — uniform 8-byte layout, no mixed-type alignment issues.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct OperationalSnapshot
    {
        public readonly double HeadingMagneticDegrees;
        public readonly double HeadingTrueDegrees;
        public readonly double TrueAirspeedKnots;
        public readonly double Mach;
        public readonly double GForce;
        public readonly double FlapsHandleIndex;  // int SimVar → exact double (values 0-8)
        public readonly double GearPosition;
        public readonly double Engine1Running;    // bool SimVar → 0.0 or 1.0
        public readonly double Engine2Running;
        public readonly double Engine3Running;
        public readonly double Engine4Running;
        public readonly double BeaconLightOn;
        public readonly double TaxiLightsOn;
        public readonly double LandingLightsOn;
        public readonly double StrobesOn;
        public readonly double LightStates;       // int bitmask → exact double (32-bit mask, ≤ 2^53)
        public readonly double StallWarning;
        public readonly double GpwsAlert;
        public readonly double OverspeedWarning;
        // NAV1 approach instruments — appended last to preserve existing layout.
        public readonly double Nav1GlideslopeErrorDegrees;   // NAV GLIDE SLOPE ERROR:1
        public readonly double Nav1RadialErrorDegrees;        // NAV RADIAL ERROR:1
    }

    // SIMCONNECT_DATATYPE_STRING256 payload — 256 ANSI characters, null-terminated.
    // Used to read string SimVars such as ATC MODEL and TITLE.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private readonly struct AtcModelSnapshot
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public readonly string Value;
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

    // SIMCONNECT_RECV_SYSTEM_STATE — returned by SimConnect_RequestSystemState.
    // szString is MAX_PATH (260) chars; contains the aircraft .air file path when
    // state = "AircraftLoaded", e.g. "Community\fenix-a319\...\fenix_a319.air".
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private readonly struct SimConnectRecvSystemState
    {
        public readonly uint Size;
        public readonly uint Version;
        public readonly uint RecvId;
        public readonly uint RequestId;
        public readonly int  IValue;
        public readonly float FValue;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public readonly string StringValue;
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
        public int LightStatesRaw { get; init; }
        public int LightBeaconRaw { get; init; }
        public int LightTaxiRaw { get; init; }
        public int LightLandingRaw { get; init; }
        public int LightStrobeRaw { get; init; }
        public bool LightSourceIsIndividual { get; init; }
        public double StallWarning { get; init; }
        public double GpwsAlert { get; init; }
        public double OverspeedWarning { get; init; }
        public double Engine1Running { get; init; }
        public double Engine2Running { get; init; }
        public double Engine3Running { get; init; }
        public double Engine4Running { get; init; }
        public double VelocityWorldYFps { get; init; }
        public double Nav1GlideslopeErrorDegrees { get; init; }
        public double Nav1RadialErrorDegrees { get; init; }
    }
}

internal sealed class NativeSimConnectExports
{
    private NativeSimConnectExports(
        OpenDelegate open,
        CloseDelegate close,
        AddToDataDefinitionDelegate addToDataDefinition,
        RequestDataOnSimObjectDelegate requestDataOnSimObject,
        RequestSystemStateDelegate requestSystemState,
        MapClientDataNameToIdDelegate mapClientDataNameToId,
        AddToClientDataDefinitionDelegate addToClientDataDefinition,
        RequestClientDataDelegate requestClientData,
        SetClientDataDelegate setClientData,
        GetNextDispatchDelegate getNextDispatch)
    {
        Open = open;
        Close = close;
        AddToDataDefinition = addToDataDefinition;
        RequestDataOnSimObject = requestDataOnSimObject;
        RequestSystemState = requestSystemState;
        MapClientDataNameToId = mapClientDataNameToId;
        AddToClientDataDefinition = addToClientDataDefinition;
        RequestClientData = requestClientData;
        SetClientData = setClientData;
        GetNextDispatch = getNextDispatch;
    }

    public OpenDelegate Open { get; }
    public CloseDelegate Close { get; }
    public AddToDataDefinitionDelegate AddToDataDefinition { get; }
    public RequestDataOnSimObjectDelegate RequestDataOnSimObject { get; }
    public RequestSystemStateDelegate RequestSystemState { get; }
    public MapClientDataNameToIdDelegate MapClientDataNameToId { get; }
    public AddToClientDataDefinitionDelegate AddToClientDataDefinition { get; }
    public RequestClientDataDelegate RequestClientData { get; }
    public SetClientDataDelegate SetClientData { get; }
    public GetNextDispatchDelegate GetNextDispatch { get; }

    public static NativeSimConnectExports Load(nint libraryHandle) =>
        new(
            GetExport<OpenDelegate>(libraryHandle, "SimConnect_Open"),
            GetExport<CloseDelegate>(libraryHandle, "SimConnect_Close"),
            GetExport<AddToDataDefinitionDelegate>(libraryHandle, "SimConnect_AddToDataDefinition"),
            GetExport<RequestDataOnSimObjectDelegate>(libraryHandle, "SimConnect_RequestDataOnSimObject"),
            GetExport<RequestSystemStateDelegate>(libraryHandle, "SimConnect_RequestSystemState"),
            GetExport<MapClientDataNameToIdDelegate>(libraryHandle, "SimConnect_MapClientDataNameToID"),
            GetExport<AddToClientDataDefinitionDelegate>(libraryHandle, "SimConnect_AddToClientDataDefinition"),
            GetExport<RequestClientDataDelegate>(libraryHandle, "SimConnect_RequestClientData"),
            GetExport<SetClientDataDelegate>(libraryHandle, "SimConnect_SetClientData"),
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

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate int RequestSystemStateDelegate(
        nint simConnectHandle,
        uint requestId,
        string stateName);

    // ── MobiFlight WASM / client-data P/Invoke ───────────────────────────────────

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public delegate int MapClientDataNameToIdDelegate(
        nint simConnectHandle,
        string clientDataName,
        uint clientDataId);

    /// <summary>
    /// dwSizeOrType: positive = byte count; 4 = 4-byte raw block (float32 slot).
    /// Special SDK type constants (-1 to -6) are also accepted but use 4 for floats.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int AddToClientDataDefinitionDelegate(
        nint simConnectHandle,
        uint defineId,
        uint offset,
        uint sizeOrType,
        float epsilon,
        uint datumId);

    /// <summary>period: 2 = ON_SET. dwFlags: 1 = CHANGED.</summary>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int RequestClientDataDelegate(
        nint simConnectHandle,
        uint clientDataId,
        uint requestId,
        uint defineId,
        uint period,
        uint dwFlags,
        uint dwOrigin,
        uint dwInterval,
        uint dwLimit);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int SetClientDataDelegate(
        nint simConnectHandle,
        uint clientDataId,
        uint defineId,
        uint dwFlags,
        uint dwReserved,
        uint cbUnitSize,
        nint pDataSet);

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
    ClientData    = 14,
    SystemState   = 15,
}
