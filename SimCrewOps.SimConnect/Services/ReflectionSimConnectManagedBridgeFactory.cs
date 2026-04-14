using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

internal sealed class ReflectionSimConnectManagedBridgeFactory : ISimConnectManagedBridgeFactory
{
    public Task<ISimConnectManagedBridge> CreateAsync(Assembly managedAssembly, SimConnectHostOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(managedAssembly);
        ArgumentNullException.ThrowIfNull(options);

        ISimConnectManagedBridge bridge = new ReflectionSimConnectManagedBridge(managedAssembly, options);
        return Task.FromResult(bridge);
    }
}

internal sealed class ReflectionSimConnectManagedBridge : ISimConnectManagedBridge
{
    private const uint FlightCriticalRequestId = 1;
    private const uint OperationalRequestId = 2;
    private const uint FlightCriticalDefinitionId = 11;
    private const uint OperationalDefinitionId = 12;

    private readonly SimConnectHostOptions _options;
    private readonly ConcurrentQueue<SimConnectRawTelemetryFrame> _frames = new();
    private readonly ConcurrentQueue<Exception> _pendingErrors = new();
    private readonly AutoResetEvent _messageSignal = new(false);
    private readonly List<Delegate> _eventHandlers = [];

    private readonly object _simConnect;
    private readonly MethodInfo _receiveMessageMethod;
    private readonly uint _userObjectId;

    private LatestSimConnectState _latestState = new();
    private bool _disposed;

    public ReflectionSimConnectManagedBridge(Assembly managedAssembly, SimConnectHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(managedAssembly);
        ArgumentNullException.ThrowIfNull(options);

        _options = options;

        var simConnectType = managedAssembly.GetType("Microsoft.FlightSimulator.SimConnect.SimConnect")
            ?? throw new InvalidOperationException("Managed SimConnect type Microsoft.FlightSimulator.SimConnect.SimConnect was not found.");

        var constructor = simConnectType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(IsManagedSimConnectConstructor)
            ?? throw new InvalidOperationException("Managed SimConnect constructor was not found.");

        _simConnect = constructor.Invoke([options.ClientName, IntPtr.Zero, 0u, _messageSignal, 0u]);
        _receiveMessageMethod = simConnectType.GetMethod("ReceiveMessage", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Managed SimConnect ReceiveMessage method was not found.");
        _userObjectId = GetUnsignedStaticValue(simConnectType, "SIMCONNECT_OBJECT_ID_USER");

        WireEvent(simConnectType, "OnRecvSimobjectData", HandleSimObjectData);
        WireEvent(simConnectType, "OnRecvException", HandleSimConnectException);
        WireEvent(simConnectType, "OnRecvQuit", HandleSimConnectQuit);

        RegisterDefinitions(managedAssembly, simConnectType);
    }

    public bool IsConnected => !_disposed;

    public Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_pendingErrors.TryDequeue(out var pendingError))
        {
            throw pendingError;
        }

        if (_frames.TryDequeue(out var bufferedFrame))
        {
            return Task.FromResult<SimConnectRawTelemetryFrame?>(bufferedFrame);
        }

        var waitHandles = new WaitHandle[] { cancellationToken.WaitHandle, _messageSignal };
        var signaledIndex = WaitHandle.WaitAny(
            waitHandles,
            (int)Math.Clamp(_options.FrameReadTimeout.TotalMilliseconds, 0, int.MaxValue));

        if (signaledIndex == 0)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (signaledIndex == WaitHandle.WaitTimeout)
        {
            return Task.FromResult<SimConnectRawTelemetryFrame?>(null);
        }

        DrainMessages();

        if (_pendingErrors.TryDequeue(out pendingError))
        {
            throw pendingError;
        }

        _frames.TryDequeue(out var frame);
        return Task.FromResult(frame);
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        _disposed = true;
        _frames.Clear();
        _pendingErrors.Clear();

        if (_simConnect is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _messageSignal.Dispose();
        return Task.CompletedTask;
    }

    private void RegisterDefinitions(Assembly managedAssembly, Type simConnectType)
    {
        var dataTypeEnum = managedAssembly.GetType("Microsoft.FlightSimulator.SimConnect.SIMCONNECT_DATATYPE")
            ?? throw new InvalidOperationException("Managed SimConnect datatype enum was not found.");
        var periodEnum = managedAssembly.GetType("Microsoft.FlightSimulator.SimConnect.SIMCONNECT_PERIOD")
            ?? throw new InvalidOperationException("Managed SimConnect period enum was not found.");
        var requestFlagEnum = managedAssembly.GetType("Microsoft.FlightSimulator.SimConnect.SIMCONNECT_DATA_REQUEST_FLAG")
            ?? throw new InvalidOperationException("Managed SimConnect request flag enum was not found.");

        var addToDataDefinitionMethod = simConnectType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name == "AddToDataDefinition" && method.GetParameters().Length == 6)
            ?? throw new InvalidOperationException("Managed SimConnect AddToDataDefinition method was not found.");
        var registerDataDefineStructMethod = simConnectType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method => method.Name == "RegisterDataDefineStruct" && method.IsGenericMethodDefinition)
            ?? throw new InvalidOperationException("Managed SimConnect RegisterDataDefineStruct method was not found.");
        var requestDataOnSimObjectMethod = simConnectType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == "RequestDataOnSimObject")
            .OrderByDescending(method => method.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Managed SimConnect RequestDataOnSimObject method was not found.");

        RegisterDefinition(
            addToDataDefinitionMethod,
            registerDataDefineStructMethod,
            requestDataOnSimObjectMethod,
            dataTypeEnum,
            periodEnum,
            requestFlagEnum,
            FlightCriticalDefinitionId,
            FlightCriticalRequestId,
            SimConnectDefinitionCatalog.FlightCriticalVariables,
            typeof(FlightCriticalSnapshot),
            "SIM_FRAME");

        RegisterDefinition(
            addToDataDefinitionMethod,
            registerDataDefineStructMethod,
            requestDataOnSimObjectMethod,
            dataTypeEnum,
            periodEnum,
            requestFlagEnum,
            OperationalDefinitionId,
            OperationalRequestId,
            SimConnectDefinitionCatalog.ScoringAndOperationalVariables,
            typeof(OperationalSnapshot),
            "SECOND");
    }

    private void RegisterDefinition(
        MethodInfo addToDataDefinitionMethod,
        MethodInfo registerDataDefineStructMethod,
        MethodInfo requestDataOnSimObjectMethod,
        Type dataTypeEnum,
        Type periodEnum,
        Type requestFlagEnum,
        uint definitionId,
        uint requestId,
        IReadOnlyList<SimConnectVariableDefinition> variables,
        Type structType,
        string periodName)
    {
        for (var index = 0; index < variables.Count; index++)
        {
            var variable = variables[index];
            addToDataDefinitionMethod.Invoke(_simConnect,
            [
                definitionId,
                variable.SimVarName,
                NormalizeUnit(variable.Unit),
                Enum.Parse(dataTypeEnum, NormalizeValueType(variable).ToString(), ignoreCase: true),
                0.0f,
                (uint)index,
            ]);
        }

        registerDataDefineStructMethod.MakeGenericMethod(structType).Invoke(_simConnect, [definitionId]);

        var requestArguments = requestDataOnSimObjectMethod.GetParameters().Length switch
        {
            8 => new object[]
            {
                requestId,
                definitionId,
                _userObjectId,
                Enum.Parse(periodEnum, periodName, ignoreCase: true),
                Enum.ToObject(requestFlagEnum, 0),
                0u,
                0u,
                0u,
            },
            5 => new object[]
            {
                requestId,
                definitionId,
                _userObjectId,
                Enum.Parse(periodEnum, periodName, ignoreCase: true),
                Enum.ToObject(requestFlagEnum, 0),
            },
            _ => throw new InvalidOperationException("Managed SimConnect RequestDataOnSimObject overload is not supported."),
        };

        requestDataOnSimObjectMethod.Invoke(_simConnect, requestArguments);
    }

    private static bool IsManagedSimConnectConstructor(ConstructorInfo constructor)
    {
        var parameters = constructor.GetParameters();
        return parameters.Length == 5
            && parameters[0].ParameterType == typeof(string)
            && parameters[1].ParameterType == typeof(IntPtr)
            && parameters[2].ParameterType == typeof(uint)
            && typeof(WaitHandle).IsAssignableFrom(parameters[3].ParameterType)
            && parameters[4].ParameterType == typeof(uint);
    }

    private void WireEvent(Type simConnectType, string eventName, Action<object?> handler)
    {
        var eventInfo = simConnectType.GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);
        if (eventInfo is null)
        {
            return;
        }

        var delegateType = eventInfo.EventHandlerType
            ?? throw new InvalidOperationException($"Managed SimConnect event {eventName} did not expose a delegate type.");
        var eventDelegate = CreateEventDelegate(delegateType, handler);
        eventInfo.AddEventHandler(_simConnect, eventDelegate);
        _eventHandlers.Add(eventDelegate);
    }

    private static Delegate CreateEventDelegate(Type delegateType, Action<object?> handler)
    {
        var invokeMethod = delegateType.GetMethod("Invoke")
            ?? throw new InvalidOperationException($"Delegate type {delegateType.Name} does not expose an Invoke method.");
        var parameters = invokeMethod.GetParameters();
        if (parameters.Length != 2)
        {
            throw new InvalidOperationException($"Delegate type {delegateType.Name} must have exactly two parameters.");
        }

        var senderParameter = Expression.Parameter(parameters[0].ParameterType, "sender");
        var dataParameter = Expression.Parameter(parameters[1].ParameterType, "data");
        var handlerTarget = handler.Target is null
            ? null
            : Expression.Constant(handler.Target);
        var body = handler.Method.IsStatic
            ? Expression.Call(handler.Method, Expression.Convert(dataParameter, typeof(object)))
            : Expression.Call(handlerTarget!, handler.Method, Expression.Convert(dataParameter, typeof(object)));

        return Expression.Lambda(delegateType, body, senderParameter, dataParameter).Compile();
    }

    private void HandleSimObjectData(object? data)
    {
        try
        {
            if (data is null)
            {
                return;
            }

            var requestId = Convert.ToUInt32(GetInstanceValue(data, "dwRequestID"));
            var payload = GetPayload(data);

            switch (requestId)
            {
                case FlightCriticalRequestId:
                    UpdateFlightCritical((FlightCriticalSnapshot)payload);
                    break;
                case OperationalRequestId:
                    UpdateOperational((OperationalSnapshot)payload);
                    break;
                default:
                    return;
            }
        }
        catch (Exception ex)
        {
            _pendingErrors.Enqueue(ex);
        }
    }

    private void HandleSimConnectException(object? data)
    {
        var exceptionCode = data is null ? "unknown" : GetInstanceValue(data, "dwException")?.ToString() ?? "unknown";
        _pendingErrors.Enqueue(new InvalidOperationException($"SimConnect exception received: {exceptionCode}."));
    }

    private void HandleSimConnectQuit(object? data)
    {
        _pendingErrors.Enqueue(new InvalidOperationException("Microsoft Flight Simulator closed the SimConnect session."));
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
            TrueAirspeedKnots = snapshot.TrueAirspeedKnots,
            HeadingTrueDegrees = snapshot.HeadingTrueDegrees,
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

    private void DrainMessages()
    {
        while (true)
        {
            try
            {
                _receiveMessageMethod.Invoke(_simConnect, null);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is COMException)
            {
                break;
            }

            if (!_messageSignal.WaitOne(0))
            {
                break;
            }
        }
    }

    private static object GetPayload(object data)
    {
        var value = GetInstanceValue(data, "dwData")
            ?? throw new InvalidOperationException("Managed SimConnect event payload did not contain dwData.");

        return value switch
        {
            Array array when array.Length > 0 => array.GetValue(0)
                ?? throw new InvalidOperationException("Managed SimConnect dwData[0] was null."),
            _ => throw new InvalidOperationException("Managed SimConnect dwData did not contain an array payload."),
        };
    }

    private static object? GetInstanceValue(object instance, string memberName)
    {
        var instanceType = instance.GetType();
        var property = instanceType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property is not null)
        {
            return property.GetValue(instance);
        }

        var field = instanceType.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
        return field?.GetValue(instance);
    }

    private static uint GetUnsignedStaticValue(Type ownerType, string memberName)
    {
        var property = ownerType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
        if (property?.GetValue(null) is not null)
        {
            return Convert.ToUInt32(property.GetValue(null));
        }

        var field = ownerType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
        if (field?.GetValue(null) is not null)
        {
            return Convert.ToUInt32(field.GetValue(null));
        }

        throw new InvalidOperationException($"Managed SimConnect static member {memberName} was not found.");
    }

    private static SimConnectValueType NormalizeValueType(SimConnectVariableDefinition definition)
    {
        if (definition.ValueType == SimConnectValueType.Int32)
        {
            return SimConnectValueType.Int32;
        }

        return definition.Unit switch
        {
            "bool" => SimConnectValueType.Int32,
            _ => definition.ValueType,
        };
    }

    private static string? NormalizeUnit(string? unit) => unit switch
    {
        "bool" => "Bool",
        "number" => "Number",
        "percent" => "Percent Over 100",
        "gforce" => "GForce",
        _ => unit,
    };

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReflectionSimConnectManagedBridge));
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
