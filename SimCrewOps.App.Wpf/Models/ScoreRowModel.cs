using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace SimCrewOps.App.Wpf.Models;

/// <summary>
/// A single row in a score breakdown display (phase label, bar, score, and findings).
/// </summary>
public sealed record ScoreRowModel(
    string Label,
    string ScoreText,
    double FillWidth,
    Brush FillBrush,
    IReadOnlyList<FindingRowModel> Findings);

/// <summary>
/// A single deduction finding shown beneath a phase row in the post-flight review.
/// </summary>
public sealed record FindingRowModel(
    /// <summary>Formatted display text, e.g. "↳ Touchdown sink rate 680 fpm  −2.5 pts".</summary>
    string DisplayText,
    bool IsAutomaticFail = false);
