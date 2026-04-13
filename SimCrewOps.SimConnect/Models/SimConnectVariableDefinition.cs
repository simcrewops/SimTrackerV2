namespace SimCrewOps.SimConnect.Models;

public sealed record SimConnectVariableDefinition
{
    public required string Key { get; init; }
    public required string SimVarName { get; init; }
    public string? Unit { get; init; }
    public required SimConnectValueType ValueType { get; init; }
    public required SimConnectUpdateRate UpdateRate { get; init; }
    public bool RequiredForScoring { get; init; } = true;
}
