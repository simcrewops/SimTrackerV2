using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Providers;
using SimCrewOps.SimConnect.Models;


namespace SimCrewOps.SimConnect.Services;

public sealed class SimConnectFacilityRunwayProvider : IRunwayDataProvider
{
    private const double FeetPerMeter = 3.28084;
    private readonly ISimConnectFacilityDataSource _facilityDataSource;

    public SimConnectFacilityRunwayProvider()
        : this(new ManagedSimConnectFacilityDataSource())
    {
    }

    /// <summary>
    /// Creates a provider that routes all facility requests through the supplied live
    /// SimConnect client, avoiding the per-lookup connection that races with the main session.
    /// </summary>
    public static SimConnectFacilityRunwayProvider ForLiveConnection(ManagedSimConnectClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new SimConnectFacilityRunwayProvider(new LiveConnectionFacilityDataSource(client));
    }

    internal SimConnectFacilityRunwayProvider(ISimConnectFacilityDataSource facilityDataSource)
    {
        ArgumentNullException.ThrowIfNull(facilityDataSource);
        _facilityDataSource = facilityDataSource;
    }

    public async Task<AirportRunwayCatalog?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default)
    {
        var normalizedIcao = NormalizeAirportIcao(airportIcao);
        var snapshot = await _facilityDataSource.GetRunwaysAsync(normalizedIcao, cancellationToken).ConfigureAwait(false);
        if (snapshot?.Runways.Count is not > 0)
        {
            return null;
        }

        // Filter out individual runways missing threshold data rather than rejecting the
        // entire catalog. Some MSFS airports return complete data for the active runways
        // but null for closed or construction runways — discarding the whole airport was
        // causing all runway resolution to fail silently.
        var mappedRunways = snapshot.Runways
            .Where(runway => runway.HasPrimaryThresholdData && runway.HasSecondaryThresholdData)
            .SelectMany(MapRunwayEnds)
            .OrderBy(runway => runway.RunwayIdentifier, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return mappedRunways.Length == 0
            ? null
            : new AirportRunwayCatalog
            {
                AirportIcao = snapshot.AirportIcao,
                DataSource = RunwayDataSource.SimConnectFacilityApi,
                Runways = mappedRunways,
            };
    }

    private static IEnumerable<RunwayEnd> MapRunwayEnds(SimConnectFacilityRunway runway)
    {
        if (!IsFinite(runway.CenterLatitude)
            || !IsFinite(runway.CenterLongitude)
            || !IsFinite(runway.HeadingTrueDegrees)
            || runway.LengthFeet <= 0)
        {
            yield break;
        }

        var normalizedHeading = NormalizeHeading(runway.HeadingTrueDegrees);
        var halfLengthFeet = runway.LengthFeet / 2.0;
        var primaryIdent = BuildRunwayIdentifier(runway.PrimaryNumber, runway.PrimaryDesignator);
        if (!string.IsNullOrWhiteSpace(primaryIdent))
        {
            var primaryDistance = Math.Max(0, halfLengthFeet - Math.Max(0, runway.PrimaryThresholdLengthFeet));
            var primaryThreshold = DestinationPoint(
                runway.CenterLatitude,
                runway.CenterLongitude,
                normalizedHeading + 180,
                primaryDistance);

            yield return new RunwayEnd
            {
                AirportIcao = runway.AirportIcao,
                RunwayIdentifier = primaryIdent,
                TrueHeadingDegrees = NormalizeHeading(normalizedHeading),
                LengthFeet = runway.LengthFeet,
                ThresholdLatitude = primaryThreshold.Latitude,
                ThresholdLongitude = primaryThreshold.Longitude,
                DisplacedThresholdFeet = Math.Max(0, runway.PrimaryThresholdLengthFeet),
                DataSource = RunwayDataSource.SimConnectFacilityApi,
            };
        }

        var secondaryIdent = BuildRunwayIdentifier(runway.SecondaryNumber, runway.SecondaryDesignator);
        if (!string.IsNullOrWhiteSpace(secondaryIdent))
        {
            var secondaryDistance = Math.Max(0, halfLengthFeet - Math.Max(0, runway.SecondaryThresholdLengthFeet));
            var secondaryThreshold = DestinationPoint(
                runway.CenterLatitude,
                runway.CenterLongitude,
                normalizedHeading,
                secondaryDistance);

            yield return new RunwayEnd
            {
                AirportIcao = runway.AirportIcao,
                RunwayIdentifier = secondaryIdent,
                TrueHeadingDegrees = NormalizeHeading(normalizedHeading + 180),
                LengthFeet = runway.LengthFeet,
                ThresholdLatitude = secondaryThreshold.Latitude,
                ThresholdLongitude = secondaryThreshold.Longitude,
                DisplacedThresholdFeet = Math.Max(0, runway.SecondaryThresholdLengthFeet),
                DataSource = RunwayDataSource.SimConnectFacilityApi,
            };
        }
    }

    internal static string? BuildRunwayIdentifier(int number, int designator)
    {
        var numberPart = number switch
        {
            >= 1 and <= 36 => number.ToString("00"),
            37 => "N",
            38 => "NE",
            39 => "E",
            40 => "SE",
            41 => "S",
            42 => "SW",
            43 => "W",
            44 => "NW",
            _ => null,
        };

        if (numberPart is null)
        {
            return null;
        }

        var suffix = designator switch
        {
            0 => string.Empty,
            1 => "L",
            2 => "R",
            3 => "C",
            4 => "W",
            5 => "A",
            6 => "B",
            _ => string.Empty,
        };

        return $"{numberPart}{suffix}";
    }

    internal static string NormalizeAirportIcao(string airportIcao) =>
        airportIcao.Trim().ToUpperInvariant();

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static double NormalizeHeading(double headingDegrees)
    {
        var normalized = headingDegrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static (double Latitude, double Longitude) DestinationPoint(
        double startLatitude,
        double startLongitude,
        double headingDegrees,
        double distanceFeet)
    {
        const double EarthRadiusFeet = 20_925_524.9;
        var headingRad = headingDegrees * Math.PI / 180.0;
        var distanceRad = distanceFeet / EarthRadiusFeet;
        var startLatRad = startLatitude * Math.PI / 180.0;
        var startLonRad = startLongitude * Math.PI / 180.0;

        var destLatRad = Math.Asin(
            Math.Sin(startLatRad) * Math.Cos(distanceRad) +
            Math.Cos(startLatRad) * Math.Sin(distanceRad) * Math.Cos(headingRad));

        var destLonRad = startLonRad + Math.Atan2(
            Math.Sin(headingRad) * Math.Sin(distanceRad) * Math.Cos(startLatRad),
            Math.Cos(distanceRad) - Math.Sin(startLatRad) * Math.Sin(destLatRad));

        return (destLatRad * 180.0 / Math.PI, destLonRad * 180.0 / Math.PI);
    }

    internal interface ISimConnectFacilityDataSource
    {
        Task<SimConnectAirportFacilitySnapshot?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default);
    }

    // SimConnectAirportFacilitySnapshot and SimConnectFacilityRunway are defined in
    // SimCrewOps.SimConnect.Models.SimConnectFacilityModels — shared with the bridge.

    /// <summary>
    /// Routes facility requests through the existing live SimConnect connection to avoid
    /// the race condition caused by opening a second connection per lookup.
    /// Per-session cache ensures each airport is only requested once.
    /// </summary>
    internal sealed class LiveConnectionFacilityDataSource : ISimConnectFacilityDataSource
    {
        private readonly ManagedSimConnectClient _client;
        private readonly ConcurrentDictionary<string, Task<SimConnectAirportFacilitySnapshot?>> _cache
            = new(StringComparer.OrdinalIgnoreCase);

        public LiveConnectionFacilityDataSource(ManagedSimConnectClient client)
        {
            _client = client;
        }

        public Task<SimConnectAirportFacilitySnapshot?> GetRunwaysAsync(
            string airportIcao,
            CancellationToken cancellationToken = default)
        {
            return _cache.GetOrAdd(
                airportIcao,
                icao => _client.RequestFacilityDataAsync(icao, cancellationToken));
        }
    }

    private sealed class ManagedSimConnectFacilityDataSource : ISimConnectFacilityDataSource
    {
        private readonly SimConnectAssemblyLocator _assemblyLocator;
        private readonly SimConnectHostOptions _options;

        public ManagedSimConnectFacilityDataSource(
            SimConnectAssemblyLocator? assemblyLocator = null,
            SimConnectHostOptions? options = null)
        {
            _assemblyLocator = assemblyLocator ?? new SimConnectAssemblyLocator();
            _options = options ?? new SimConnectHostOptions
            {
                ClientName = "SimCrewOps Tracker Runways",
            };
        }

        public async Task<SimConnectAirportFacilitySnapshot?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                var managedAssembly = _assemblyLocator.LoadManagedAssembly(_options);
                using var requestSession = new ReflectionFacilityRequestSession(managedAssembly, _options);
                return await requestSession.GetRunwaysAsync(airportIcao, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Trace.TraceWarning(
                    "SimConnect facility runway lookup failed for {0}: {1}",
                    airportIcao,
                    ex.Message);
                return null;
            }
        }
    }

    private sealed class ReflectionFacilityRequestSession : IDisposable
    {
        private const uint FacilityDefinitionId = 401;
        private const uint FacilityRequestId = 402;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        private readonly object _simConnect;
        private readonly MethodInfo _receiveMessageMethod;
        private readonly MethodInfo _requestFacilityDataMethod;
        private readonly AutoResetEvent _messageSignal = new(false);
        private readonly ConcurrentQueue<Exception> _pendingErrors = new();
        private readonly List<Delegate> _eventHandlers = [];
        private readonly Dictionary<uint, PendingRunwayNode> _runwaysByUniqueId = [];
        private bool _disposed;
        private string? _activeAirportIcao;
        private TaskCompletionSource<SimConnectAirportFacilitySnapshot?>? _completion;

        public ReflectionFacilityRequestSession(Assembly managedAssembly, SimConnectHostOptions options)
        {
            ArgumentNullException.ThrowIfNull(managedAssembly);
            ArgumentNullException.ThrowIfNull(options);

            var simConnectType = managedAssembly.GetType("Microsoft.FlightSimulator.SimConnect.SimConnect")
                ?? throw new InvalidOperationException("Managed SimConnect type Microsoft.FlightSimulator.SimConnect.SimConnect was not found.");

            var constructor = simConnectType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(IsManagedSimConnectConstructor)
                ?? throw new InvalidOperationException("Managed SimConnect constructor was not found.");

            _simConnect = constructor.Invoke([options.ClientName, IntPtr.Zero, 0u, _messageSignal, 0u]);
            _receiveMessageMethod = simConnectType.GetMethod("ReceiveMessage", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Managed SimConnect ReceiveMessage method was not found.");
            _requestFacilityDataMethod = simConnectType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == "RequestFacilityData")
                .OrderByDescending(method => method.GetParameters().Length)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Managed SimConnect RequestFacilityData method was not found.");

            var addToFacilityDefinitionMethod = simConnectType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(method => method.Name == "AddToFacilityDefinition" && method.GetParameters().Length == 2)
                ?? throw new InvalidOperationException("Managed SimConnect AddToFacilityDefinition method was not found.");

            WireEvent(simConnectType, "OnRecvFacilityData", HandleFacilityData);
            WireEvent(simConnectType, "OnRecvFacilityDataEnd", HandleFacilityDataEnd);
            WireEvent(simConnectType, "OnRecvException", HandleSimConnectException);
            WireEvent(simConnectType, "OnRecvQuit", HandleSimConnectQuit);

            RegisterFacilityDefinition(addToFacilityDefinitionMethod);
        }

        public async Task<SimConnectAirportFacilitySnapshot?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            _runwaysByUniqueId.Clear();
            _pendingErrors.Clear();
            _activeAirportIcao = NormalizeAirportIcao(airportIcao);
            _completion = new TaskCompletionSource<SimConnectAirportFacilitySnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);

            InvokeRequestFacilityData(_activeAirportIcao);
            await WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
            return await _completion.Task.ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_simConnect is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _messageSignal.Dispose();
        }

        private void RegisterFacilityDefinition(MethodInfo addToFacilityDefinitionMethod)
        {
            foreach (var fieldName in FacilityDefinitionFields)
            {
                addToFacilityDefinitionMethod.Invoke(_simConnect, [FacilityDefinitionId, fieldName]);
            }
        }

        private void InvokeRequestFacilityData(string airportIcao)
        {
            var parameters = _requestFacilityDataMethod.GetParameters();
            object?[] arguments = parameters.Length switch
            {
                4 => [FacilityDefinitionId, FacilityRequestId, airportIcao, string.Empty],
                3 => [FacilityDefinitionId, FacilityRequestId, airportIcao],
                _ => throw new InvalidOperationException("Managed SimConnect RequestFacilityData overload is not supported."),
            };

            _requestFacilityDataMethod.Invoke(_simConnect, arguments);
        }

        private async Task WaitForCompletionAsync(CancellationToken cancellationToken)
        {
            var started = DateTimeOffset.UtcNow;

            while (_completion is not null && !_completion.Task.IsCompleted)
            {
                if (_pendingErrors.TryDequeue(out var pendingError))
                {
                    throw pendingError;
                }

                var elapsed = DateTimeOffset.UtcNow - started;
                var remaining = RequestTimeout - elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException("Timed out waiting for SimConnect facility runway data.");
                }

                var signaledIndex = WaitHandle.WaitAny(
                    [cancellationToken.WaitHandle, _messageSignal],
                    (int)Math.Clamp(remaining.TotalMilliseconds, 1, int.MaxValue));

                if (signaledIndex == 0)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (signaledIndex == WaitHandle.WaitTimeout)
                {
                    continue;
                }

                DrainMessages();
            }

            await Task.CompletedTask.ConfigureAwait(false);
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

        private void HandleFacilityData(object? data)
        {
            try
            {
                if (data is null || GetUnsignedInstanceValue(data, "UserRequestId", "dwRequestID", "RequestId") != FacilityRequestId)
                {
                    return;
                }

                var uniqueRequestId = GetUnsignedInstanceValue(data, "UniqueRequestId", "dwUniqueRequestId");
                var parentUniqueRequestId = GetUnsignedInstanceValue(data, "ParentUniqueRequestId", "dwParentUniqueRequestId");
                var facilityType = GetEnumName(GetInstanceValue(data, "Type", "dwType"));
                var payload = GetFacilityPayload(data);

                if (facilityType.Contains("RUNWAY", StringComparison.OrdinalIgnoreCase))
                {
                    var runway = ParseRunwayPayload(payload);
                    if (runway is null)
                    {
                        return;
                    }

                    _runwaysByUniqueId[uniqueRequestId] = new PendingRunwayNode { Runway = runway };
                    return;
                }

                if (facilityType.Contains("PAVEMENT", StringComparison.OrdinalIgnoreCase)
                    && _runwaysByUniqueId.TryGetValue(parentUniqueRequestId, out var runwayNode))
                {
                    var pavement = ParsePavementPayload(payload);
                    runwayNode.AddThresholdPayload(pavement);
                }
            }
            catch (Exception ex)
            {
                _pendingErrors.Enqueue(ex);
            }
        }

        private void HandleFacilityDataEnd(object? data)
        {
            try
            {
                if (data is null || GetUnsignedInstanceValue(data, "UserRequestId", "dwRequestID", "RequestId") != FacilityRequestId)
                {
                    return;
                }

                _completion?.TrySetResult(BuildSnapshot());
            }
            catch (Exception ex)
            {
                _pendingErrors.Enqueue(ex);
            }
        }

        private void HandleSimConnectException(object? data)
        {
            var exceptionCode = data is null ? "unknown" : GetInstanceValue(data, "dwException", "Exception")?.ToString() ?? "unknown";
            _pendingErrors.Enqueue(new InvalidOperationException($"SimConnect facility exception received: {exceptionCode}."));
        }

        private void HandleSimConnectQuit(object? data)
        {
            _pendingErrors.Enqueue(new InvalidOperationException("Microsoft Flight Simulator closed the SimConnect facility session."));
        }

        private SimConnectAirportFacilitySnapshot? BuildSnapshot()
        {
            if (string.IsNullOrWhiteSpace(_activeAirportIcao))
            {
                return null;
            }

            var runways = _runwaysByUniqueId.Values
                .Select(node => node.ToFacilityRunway(_activeAirportIcao!))
                .Where(runway => runway is not null)
                .Cast<SimConnectFacilityRunway>()
                .OrderBy(runway => runway.PrimaryNumber)
                .ThenBy(runway => runway.PrimaryDesignator)
                .ToArray();

            return runways.Length == 0
                ? null
                : new SimConnectAirportFacilitySnapshot
                {
                    AirportIcao = _activeAirportIcao!,
                    Runways = runways,
                };
        }

        private static FacilityRunwayPayload? ParseRunwayPayload(object payload)
        {
            if (payload is IntPtr pointer && pointer != IntPtr.Zero)
            {
                return Marshal.PtrToStructure<FacilityRunwayPayload>(pointer);
            }

            if (payload is byte[] bytes)
            {
                var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try
                {
                    return Marshal.PtrToStructure<FacilityRunwayPayload>(handle.AddrOfPinnedObject());
                }
                finally
                {
                    handle.Free();
                }
            }

            return new FacilityRunwayPayload
            {
                Latitude = GetDouble(payload, "Latitude", "LATITUDE"),
                Longitude = GetDouble(payload, "Longitude", "LONGITUDE"),
                Heading = (float)GetDouble(payload, "Heading", "HEADING"),
                Length = (float)GetDouble(payload, "Length", "LENGTH"),
                PrimaryNumber = GetInt32(payload, "PrimaryNumber", "PRIMARY_NUMBER"),
                PrimaryDesignator = GetInt32(payload, "PrimaryDesignator", "PRIMARY_DESIGNATOR"),
                SecondaryNumber = GetInt32(payload, "SecondaryNumber", "SECONDARY_NUMBER"),
                SecondaryDesignator = GetInt32(payload, "SecondaryDesignator", "SECONDARY_DESIGNATOR"),
            };
        }

        private static FacilityPavementPayload? ParsePavementPayload(object payload)
        {
            if (payload is IntPtr pointer && pointer != IntPtr.Zero)
            {
                return Marshal.PtrToStructure<FacilityPavementPayload>(pointer);
            }

            if (payload is byte[] bytes)
            {
                var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try
                {
                    return Marshal.PtrToStructure<FacilityPavementPayload>(handle.AddrOfPinnedObject());
                }
                finally
                {
                    handle.Free();
                }
            }

            var enable = GetNullableInt32(payload, "Enable", "ENABLE");
            var length = GetNullableDouble(payload, "Length", "LENGTH");
            if (length is null && enable is null)
            {
                return null;
            }

            return new FacilityPavementPayload
            {
                Length = (float)(length ?? 0),
                Enable = enable ?? 0,
            };
        }

        private static object GetFacilityPayload(object data)
        {
            var value = GetInstanceValue(data, "Data", "dwData");
            if (value is null)
            {
                return data;
            }

            return value switch
            {
                Array array when array.Length > 0 => array.GetValue(0) ?? data,
                _ => value,
            };
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
            var handlerTarget = handler.Target is null ? null : Expression.Constant(handler.Target);
            var body = handler.Method.IsStatic
                ? Expression.Call(handler.Method, Expression.Convert(dataParameter, typeof(object)))
                : Expression.Call(handlerTarget!, handler.Method, Expression.Convert(dataParameter, typeof(object)));

            return Expression.Lambda(delegateType, body, senderParameter, dataParameter).Compile();
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

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ReflectionFacilityRequestSession));
            }
        }

        private static object? GetInstanceValue(object instance, params string[] memberNames)
        {
            var instanceType = instance.GetType();
            foreach (var memberName in memberNames)
            {
                var property = instanceType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property is not null)
                {
                    return property.GetValue(instance);
                }

                var field = instanceType.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field is not null)
                {
                    return field.GetValue(instance);
                }
            }

            var normalizedNames = memberNames.Select(NormalizeMemberName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var property in instanceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (normalizedNames.Contains(NormalizeMemberName(property.Name)))
                {
                    return property.GetValue(instance);
                }
            }

            foreach (var field in instanceType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (normalizedNames.Contains(NormalizeMemberName(field.Name)))
                {
                    return field.GetValue(instance);
                }
            }

            return null;
        }

        private static string NormalizeMemberName(string value) =>
            value.Replace("_", string.Empty, StringComparison.Ordinal).Trim();

        private static uint GetUnsignedInstanceValue(object instance, params string[] memberNames)
        {
            var value = GetInstanceValue(instance, memberNames);
            return value is null ? 0u : Convert.ToUInt32(value);
        }

        private static string GetEnumName(object? value) => value?.ToString() ?? string.Empty;

        private static double GetDouble(object instance, params string[] memberNames) =>
            GetNullableDouble(instance, memberNames) ?? 0;

        private static double? GetNullableDouble(object instance, params string[] memberNames)
        {
            var value = GetInstanceValue(instance, memberNames);
            if (value is null)
            {
                return null;
            }

            return value switch
            {
                float single => single,
                double dbl => dbl,
                _ => Convert.ToDouble(value),
            };
        }

        private static int GetInt32(object instance, params string[] memberNames) =>
            GetNullableInt32(instance, memberNames) ?? 0;

        private static int? GetNullableInt32(object instance, params string[] memberNames)
        {
            var value = GetInstanceValue(instance, memberNames);
            return value is null ? null : Convert.ToInt32(value);
        }

        private sealed class PendingRunwayNode
        {
            private int _thresholdPayloadCount;

            public FacilityRunwayPayload? Runway { get; set; }
            public FacilityPavementPayload? PrimaryThreshold { get; private set; }
            public FacilityPavementPayload? SecondaryThreshold { get; private set; }

            public void AddThresholdPayload(FacilityPavementPayload? payload)
            {
                if (payload is null)
                {
                    return;
                }

                if (_thresholdPayloadCount == 0)
                {
                    PrimaryThreshold = payload;
                }
                else if (_thresholdPayloadCount == 1)
                {
                    SecondaryThreshold = payload;
                }

                _thresholdPayloadCount++;
            }

            public SimConnectFacilityRunway? ToFacilityRunway(string airportIcao)
            {
                if (Runway is null)
                {
                    return null;
                }

                return new SimConnectFacilityRunway
                {
                    AirportIcao = airportIcao,
                    CenterLatitude = Runway.Value.Latitude,
                    CenterLongitude = Runway.Value.Longitude,
                    HeadingTrueDegrees = NormalizeHeading(Runway.Value.Heading),
                    LengthFeet = Runway.Value.Length * FeetPerMeter,
                    PrimaryNumber = Runway.Value.PrimaryNumber,
                    PrimaryDesignator = Runway.Value.PrimaryDesignator,
                    SecondaryNumber = Runway.Value.SecondaryNumber,
                    SecondaryDesignator = Runway.Value.SecondaryDesignator,
                    HasPrimaryThresholdData = PrimaryThreshold is not null,
                    HasSecondaryThresholdData = SecondaryThreshold is not null,
                    PrimaryThresholdLengthFeet = (PrimaryThreshold?.Length ?? 0) * FeetPerMeter,
                    SecondaryThresholdLengthFeet = (SecondaryThreshold?.Length ?? 0) * FeetPerMeter,
                };
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FacilityRunwayPayload
        {
            public double Latitude;
            public double Longitude;
            public float Heading;
            public float Length;
            public int PrimaryNumber;
            public int PrimaryDesignator;
            public int SecondaryNumber;
            public int SecondaryDesignator;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FacilityPavementPayload
        {
            public float Length;
            public int Enable;
        }

        private static readonly string[] FacilityDefinitionFields =
        [
            "OPEN AIRPORT",
            "OPEN RUNWAY",
            "LATITUDE",
            "LONGITUDE",
            "HEADING",
            "LENGTH",
            "PRIMARY_NUMBER",
            "PRIMARY_DESIGNATOR",
            "SECONDARY_NUMBER",
            "SECONDARY_DESIGNATOR",
            "OPEN PRIMARY_THRESHOLD",
            "LENGTH",
            "ENABLE",
            "CLOSE PRIMARY_THRESHOLD",
            "OPEN SECONDARY_THRESHOLD",
            "LENGTH",
            "ENABLE",
            "CLOSE SECONDARY_THRESHOLD",
            "CLOSE RUNWAY",
            "CLOSE AIRPORT",
        ];
    }
}
