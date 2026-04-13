using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace SimCrewOps.App.Wpf.Models;

public sealed record ScoreRowModel(
    string Label,
    string ScoreText,
    double FillWidth,
    Brush FillBrush);
