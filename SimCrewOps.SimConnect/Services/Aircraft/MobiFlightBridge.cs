using System.Diagnostics;
using System.Runtime.InteropServices;
using SimCrewOps.SimConnect.Services;

namespace SimCrewOps.SimConnect.Services.Aircraft;

/// <summary>
/// Reads aircraft-proprietary LVARs (Local Variables) via the MobiFlight WASM module.
///
/// Protocol summary:
///   1. Map default "MobiFlight.Command" / "MobiFlight.Response" areas.
///   2. Send "MF.Ping" — wait for "MF.Pong" on Response (proves module is installed).
///   3. Send "MF.Clients.Add.SimCrewOps" — wait for "MF.Clients.Add.SimCrewOps.Finished".
///      MobiFlight WASM creates dedicated "SimCrewOps.*" areas for us.
///   4. Map "SimCrewOps.Command", "SimCrewOps.Response", "SimCrewOps.LVars".
///   5. Per LVAR: AddToClientDataDefinition (float32, sequential offset) +
///      RequestClientData (ON_SET, CHANGED) + send "MF.SimVars.Add.(L:VarName)".
///   6. Each LVAR value arrives as a 4-byte float at its slot offset in the LVars area.
///
/// If MobiFlight is not installed, Ping produces no Pong and State stays NotInstalled.
/// Callers check <see cref="IsActive"/> before using <see cref="GetValue"/>.
/// </summary>
internal sealed class MobiFlightBridge
{
    // ── Constants ────────────────────────────────────────────────────────────────

    private const string ClientName = "SimCrewOps";

    // Default MobiFlight channel — used for ping and named-client registration only.
    private const uint DefaultCommandAreaId  = 50;
    private const uint DefaultResponseAreaId = 51;
    private const uint DefaultCommandDefId   = 50;
    private const uint DefaultResponseDefId  = 51;
    private const uint DefaultResponseReqId  = 51;

    // Our named client channel — created by WASM after registration.
    private const uint OurCommandAreaId  = 52;
    private const uint OurResponseAreaId = 53;
    private const uint OurLVarsAreaId    = 54;
    private const uint OurCommandDefId   = 52;
    private const uint OurResponseDefId  = 53;
    private const uint OurResponseReqId  = 53;

    // LVAR slots — float32, sequential offsets in the LVars area.
    // Def IDs / Req IDs: base + slotIndex. Offset in bytes: slotIndex * 4.
    private const uint LvarDefIdBase = 60;
    private const uint LvarReqIdBase = 60;

    // ── Fields ───────────────────────────────────────────────────────────────────

    private nint _handle;
    private NativeSimConnectExports? _exports;

    // lvarName → (slotIndex, latest float value)
    private readonly Dictionary<string, (uint SlotIndex, float Value)> _slots = new(StringComparer.OrdinalIgnoreCase);

    // Profile to subscribe when bridge becomes Active after the aircraft is already known.
    private AircraftProfile? _pendingProfile;

    // ── Public API ───────────────────────────────────────────────────────────────

    public MobiFlightBridgeState State { get; private set; } = MobiFlightBridgeState.Idle;
    public bool IsActive => State == MobiFlightBridgeState.Active;

    /// <summary>
    /// Call once after SimConnect_Open succeeds.
    /// Sets up default channel areas, sends the initial ping.
    /// </summary>
    public void Initialize(nint simConnectHandle, NativeSimConnectExports exports)
    {
        _handle  = simConnectHandle;
        _exports = exports;

        SetupDefaultChannels();
        SendPing();
        State = MobiFlightBridgeState.Pinging;
    }

    /// <summary>
    /// Subscribe to the LVARs required by this aircraft profile.
    /// Deferred automatically if the bridge is not yet Active.
    /// </summary>
    public void SubscribeToProfile(AircraftProfile profile)
    {
        if (!IsActive)
        {
            _pendingProfile = profile;
            return;
        }

        ApplyProfile(profile);
    }

    /// <summary>
    /// Call from the SimConnect dispatch loop whenever a ClientData message arrives (RecvId = 14).
    /// </summary>
    public void HandleClientData(nint dispatchPointer)
    {
        var header = Marshal.PtrToStructure<RecvClientDataHeader>(dispatchPointer);
        var payloadPtr = nint.Add(dispatchPointer, Marshal.SizeOf<RecvClientDataHeader>());

        if (header.RequestId == DefaultResponseReqId || header.RequestId == OurResponseReqId)
        {
            HandleResponseString(Marshal.PtrToStructure<ResponseString>(payloadPtr));
        }
        else if (header.RequestId >= LvarReqIdBase)
        {
            var slotIndex = header.RequestId - LvarReqIdBase;
            var raw = Marshal.PtrToStructure<FloatValue>(payloadPtr);
            UpdateSlotValue(slotIndex, raw.Value);
        }
    }

    /// <summary>Returns the most recent float value for an LVAR, or 0 if not subscribed.</summary>
    public float GetValue(string lvarName) =>
        _slots.TryGetValue(lvarName, out var slot) ? slot.Value : 0f;

    // ── Internal protocol steps ───────────────────────────────────────────────────

    private void SetupDefaultChannels()
    {
        Try(_exports!.MapClientDataNameToId(_handle, "MobiFlight.Command",  DefaultCommandAreaId),  "Map MobiFlight.Command");
        Try(_exports!.MapClientDataNameToId(_handle, "MobiFlight.Response", DefaultResponseAreaId), "Map MobiFlight.Response");

        // 1024-byte string definitions
        Try(_exports!.AddToClientDataDefinition(_handle, DefaultCommandDefId,  0, 1024, 0f, 0), "DefineCommandArea");
        Try(_exports!.AddToClientDataDefinition(_handle, DefaultResponseDefId, 0, 1024, 0f, 0), "DefineResponseArea");

        // Subscribe to default response channel (ON_SET = 2, CHANGED flag = 1)
        Try(_exports!.RequestClientData(_handle, DefaultResponseAreaId, DefaultResponseReqId, DefaultResponseDefId, 2u, 1u, 0u, 0u, 0u), "RequestClientData(DefaultResponse)");
    }

    private void SendPing()
    {
        // First command after connect is sometimes silently dropped by the sim.
        // Send a dummy no-op first, then the real ping, so Ping is reliably received.
        WriteCommand(DefaultCommandAreaId, DefaultCommandDefId, "MF.DummyCmd");
        WriteCommand(DefaultCommandAreaId, DefaultCommandDefId, "MF.Ping");
        Debug.WriteLine("[MobiFlight] Ping sent — waiting for Pong.");
    }

    private void HandleResponseString(ResponseString response)
    {
        var text = response.Data?.TrimEnd('\0') ?? string.Empty;
        if (text.Length == 0) return;

        Debug.WriteLine($"[MobiFlight] Response: {text}");

        if (State == MobiFlightBridgeState.Pinging && text == "MF.Pong")
        {
            OnPongReceived();
        }
        else if (State == MobiFlightBridgeState.Registering &&
                 text.Equals($"MF.Clients.Add.{ClientName}.Finished", StringComparison.OrdinalIgnoreCase))
        {
            OnRegistrationComplete();
        }
    }

    private void OnPongReceived()
    {
        State = MobiFlightBridgeState.Registering;
        Debug.WriteLine($"[MobiFlight] Pong received — registering named client '{ClientName}'.");
        WriteCommand(DefaultCommandAreaId, DefaultCommandDefId, $"MF.Clients.Add.{ClientName}");
    }

    private void OnRegistrationComplete()
    {
        // Map the dedicated named-client data areas WASM created for us.
        Try(_exports!.MapClientDataNameToId(_handle, $"{ClientName}.Command",  OurCommandAreaId),  $"Map {ClientName}.Command");
        Try(_exports!.MapClientDataNameToId(_handle, $"{ClientName}.Response", OurResponseAreaId), $"Map {ClientName}.Response");
        Try(_exports!.MapClientDataNameToId(_handle, $"{ClientName}.LVars",    OurLVarsAreaId),    $"Map {ClientName}.LVars");

        // Define and subscribe to our named response channel.
        Try(_exports!.AddToClientDataDefinition(_handle, OurCommandDefId,  0, 1024, 0f, 0), $"DefineOurCommand");
        Try(_exports!.AddToClientDataDefinition(_handle, OurResponseDefId, 0, 1024, 0f, 0), $"DefineOurResponse");
        Try(_exports!.RequestClientData(_handle, OurResponseAreaId, OurResponseReqId, OurResponseDefId, 2u, 1u, 0u, 0u, 0u), "RequestClientData(OurResponse)");

        State = MobiFlightBridgeState.Active;
        Debug.WriteLine($"[MobiFlight] Bridge active — client '{ClientName}' registered.");

        if (_pendingProfile is not null)
        {
            ApplyProfile(_pendingProfile);
            _pendingProfile = null;
        }
    }

    private void ApplyProfile(AircraftProfile profile)
    {
        // Clear previous subscriptions and resubscribe for the new aircraft.
        _slots.Clear();
        WriteCommand(OurCommandAreaId, OurCommandDefId, "MF.SimVars.Clear");

        var mappings = new[]
        {
            profile.TaxiLight,
            profile.LandingLight,
            profile.BeaconLight,
            profile.StrobeLight,
        };

        foreach (var mapping in mappings)
        {
            if (mapping.Source == LightVariableSource.LVar &&
                !string.IsNullOrEmpty(mapping.LvarName) &&
                !_slots.ContainsKey(mapping.LvarName))
            {
                SubscribeLvar(mapping.LvarName);
            }
        }

        Debug.WriteLine($"[MobiFlight] Subscribed to {_slots.Count} LVARs for profile '{profile.Name}'.");
    }

    private void SubscribeLvar(string lvarName)
    {
        var slotIndex = (uint)_slots.Count;
        var defId  = LvarDefIdBase + slotIndex;
        var reqId  = LvarReqIdBase + slotIndex;
        var offset = slotIndex * 4u; // 4 bytes per float32

        // Register a 4-byte float slot at the sequential offset in the LVars area.
        Try(_exports!.AddToClientDataDefinition(_handle, defId, offset, 4u, 0f, 0u), $"DefLvar:{lvarName}");

        // Subscribe ON_SET (period=2), CHANGED (flag=1)
        Try(_exports!.RequestClientData(_handle, OurLVarsAreaId, reqId, defId, 2u, 1u, 0u, 0u, 0u), $"ReqLvar:{lvarName}");

        // Tell MobiFlight WASM to start populating this slot.
        WriteCommand(OurCommandAreaId, OurCommandDefId, $"MF.SimVars.Add.(L:{lvarName})");

        _slots[lvarName] = (slotIndex, 0f);
        Debug.WriteLine($"[MobiFlight] Subscribed L:{lvarName} → slot {slotIndex} (offset {offset}).");
    }

    private void UpdateSlotValue(uint slotIndex, float value)
    {
        foreach (var key in _slots.Keys.ToArray())
        {
            if (_slots[key].SlotIndex == slotIndex)
            {
                _slots[key] = (slotIndex, value);
                Debug.WriteLine($"[MobiFlight] L:{key} = {value}");
                return;
            }
        }
    }

    // ── Command writing helper ────────────────────────────────────────────────────

    private void WriteCommand(uint areaId, uint defId, string command)
    {
        if (_exports is null) return;

        var buffer = new byte[1024];
        var encoded = System.Text.Encoding.ASCII.GetBytes(command);
        Array.Copy(encoded, buffer, Math.Min(encoded.Length, 1023));

        var gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            Try(_exports.SetClientData(_handle, areaId, defId, 0u, 0u, 1024u, gcHandle.AddrOfPinnedObject()),
                $"SetClientData({command})");
        }
        finally
        {
            gcHandle.Free();
        }
    }

    private static void Try(int hresult, string operation)
    {
        if (hresult < 0)
        {
            Debug.WriteLine($"[MobiFlight] {operation} failed: 0x{hresult:X8}");
        }
    }

    // ── P/Invoke structs (local to bridge) ───────────────────────────────────────

    // SIMCONNECT_RECV_CLIENT_DATA has the same layout as SIMCONNECT_RECV_SIMOBJECT_DATA.
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RecvClientDataHeader
    {
        public readonly uint Size;
        public readonly uint Version;
        public readonly uint RecvId;     // 14 = SIMCONNECT_RECV_ID_CLIENT_DATA
        public readonly uint RequestId;
        public readonly uint ObjectId;
        public readonly uint DefineId;
        public readonly uint Flags;
        public readonly uint EntryNumber;
        public readonly uint OutOf;
        public readonly uint DefineCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct FloatValue
    {
        public readonly float Value;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct ResponseString
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string Data;
    }
}

public enum MobiFlightBridgeState
{
    Idle,
    Pinging,       // MF.Ping sent, waiting for MF.Pong
    Registering,   // MF.Clients.Add sent, waiting for .Finished
    Active,        // Named client areas mapped, LVARs subscribed and flowing
    NotInstalled,  // No Pong received — MobiFlight WASM not present
}
