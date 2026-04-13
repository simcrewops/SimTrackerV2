using System.Windows.Media;

namespace SimCrewOps.App.Wpf.Models;

public sealed record ScoreRowModel(
    string Label,
    string ScoreText,
    double FillWidth,
    Brush FillBrush);
