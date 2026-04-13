using System.Windows;
using SimCrewOps.Persistence.Models;

namespace SimCrewOps.App.Wpf.Views;

public partial class RecoveryDialog : Window
{
    public bool ResumeRequested { get; private set; }
    public bool DiscardRequested { get; private set; }

    public RecoveryDialog(SessionRecoverySnapshot snapshot)
    {
        InitializeComponent();
        DataContext = new RecoveryDialogModel(snapshot);
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        ResumeRequested = true;
        DialogResult = true;
        Close();
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        DiscardRequested = true;
        DialogResult = false;
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private sealed class RecoveryDialogModel
    {
        public RecoveryDialogModel(SessionRecoverySnapshot snapshot)
        {
            var state = snapshot.CurrentSession?.State;
            SummaryText = "SimCrewOps found a saved in-progress flight session. You can resume with the recovered snapshot or discard it before starting fresh.";
            FlightText = $"Phase: {state?.CurrentPhase.ToString().ToUpperInvariant() ?? "UNKNOWN"}";
            SavedText = snapshot.CurrentSession is null
                ? "No saved current session"
                : $"Saved: {snapshot.CurrentSession.SavedUtc.ToLocalTime():g}";
            PendingText = $"Pending uploads: {snapshot.PendingCompletedSessions.Count}";
        }

        public string SummaryText { get; }
        public string FlightText { get; }
        public string SavedText { get; }
        public string PendingText { get; }
    }
}
