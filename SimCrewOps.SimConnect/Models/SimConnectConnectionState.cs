namespace SimCrewOps.SimConnect.Models;

public enum SimConnectConnectionState
{
    Idle,
    WaitingForSimulatorProcess,
    Connecting,
    Connected,
    Faulted,
    Disconnected,
}
