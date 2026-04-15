using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace SimCrewOps.App.Wpf.Models;

public sealed record ScoreRowModel(
    string Label,
    string ScoreText,
    double FillWidth,
    Brush FillBrush,
    /// <summary>First deduction finding description. Empty string when the phase is clean.</summary>
    string FindingText = "");
