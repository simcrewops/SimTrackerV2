namespace SimCrewOps.App.Wpf.Models;

public sealed record MetricTileModel(string Label, string Value, bool IsAlert = false, string Unit = "", string Hint = "");
