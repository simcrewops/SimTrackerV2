using SimCrewOps.App.Wpf.Services;
using Xunit;

namespace SimCrewOps.App.Wpf.Tests;

public sealed class AppUpdaterTests
{
    [Fact]
    public void BuildUpdateScript_IncludesVersionVerification()
    {
        var script = AppUpdater.BuildUpdateScript(123, "C:\\zip", "C:\\dir", "C:\\dir\\app.exe", "3.0.0-beta.400");
        Assert.Contains("VersionInfo.ProductVersion", script);
        Assert.Contains("3.0.0-beta.400", script);
    }

    [Fact]
    public void BuildUpdateScript_IncludesForceKill()
    {
        var script = AppUpdater.BuildUpdateScript(123, "C:\\zip", "C:\\dir", "C:\\dir\\app.exe", "1.0.0");
        Assert.Contains("Stop-Process -Id 123 -Force", script);
        Assert.Contains("Get-Process -Name 'SimTrackerV2'", script);
        Assert.Contains("Stop-Process -Force", script);
    }

    [Fact]
    public void BuildUpdateScript_ShowsErrorDialogOnExtractFailure()
    {
        var script = AppUpdater.BuildUpdateScript(123, "C:\\zip", "C:\\dir", "C:\\dir\\app.exe", "1.0.0");
        Assert.Contains("MessageBox", script);
        Assert.Contains("Update extraction failed", script);
    }
}
