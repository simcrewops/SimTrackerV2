namespace SimCrewOps.App.Wpf.Models;

public sealed class LiveFlight
{
    public string PilotId { get; set; } = string.Empty;
    public string PilotName { get; set; } = string.Empty;
    public string Callsign { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double Heading { get; set; }
    public double GroundSpeed { get; set; }
    public string Phase { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public bool IsMyFlight { get; set; }  // true if PilotId matches current user
}
