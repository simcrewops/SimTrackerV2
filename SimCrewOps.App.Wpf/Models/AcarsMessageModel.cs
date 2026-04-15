namespace SimCrewOps.App.Wpf.Models;

/// <summary>
/// A single message in the ACARS / Comms thread.
/// </summary>
public sealed record AcarsMessageModel(
    /// <summary>Sender label shown above the bubble (e.g. "DISPATCH", "YOU").</summary>
    string Sender,
    /// <summary>Message body text.</summary>
    string Body,
    /// <summary>Timestamp string displayed below the bubble.</summary>
    string Timestamp,
    /// <summary>True = right-aligned pilot outbound; false = left-aligned inbound.</summary>
    bool IsOutbound);
