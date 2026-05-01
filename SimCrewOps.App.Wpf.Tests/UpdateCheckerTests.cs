using SimCrewOps.App.Wpf.Services;
using Xunit;

namespace SimCrewOps.App.Wpf.Tests;

public sealed class UpdateCheckerTests
{
    // ── ParseVersionFromReleaseName ───────────────────────────────────────────

    [Theory]
    [InlineData("SimCrewOps Tracker v3.0.0-beta.42",  "3.0.0-beta.42")]
    [InlineData("SimCrewOps Tracker v3.0.0-beta.123", "3.0.0-beta.123")]
    [InlineData("SimCrewOps Tracker v3.0.1",          "3.0.1")]
    [InlineData("v3.0.0-beta.1",                      "3.0.0-beta.1")]
    public void ParseVersionFromReleaseName_ValidBetaAndStable_ReturnsVersionString(
        string releaseName, string expected)
    {
        Assert.Equal(expected, UpdateChecker.ParseVersionFromReleaseName(releaseName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no version here")]
    [InlineData("SimCrewOps Tracker")]   // last token has no digits
    public void ParseVersionFromReleaseName_InvalidInput_ReturnsNull(string? releaseName)
    {
        Assert.Null(UpdateChecker.ParseVersionFromReleaseName(releaseName));
    }

    // ── BetaVersion.TryParse — +sha stripping ────────────────────────────────

    [Fact]
    public void BetaVersion_TryParse_InformationalVersionWithShaSuffix_ParsesCorrectly()
    {
        // InformationalVersion produced by CI: "3.0.0-beta.42+abc1234def5678"
        // GetCurrentVersion() strips +sha before passing to IsNewerVersion;
        // BetaVersion also handles it defensively.
        Assert.True(BetaVersion.TryParse("3.0.0-beta.42+abc1234def5678", out var v));
        Assert.Equal(3,  v.Major);
        Assert.Equal(0,  v.Minor);
        Assert.Equal(0,  v.Patch);
        Assert.Equal(42, v.BetaBuild);
    }

    // ── IsNewerVersion ────────────────────────────────────────────────────────

    [Theory]
    // beta build number comparison
    [InlineData("3.0.0-beta.124", "3.0.0-beta.123", true)]
    [InlineData("3.0.0-beta.123", "3.0.0-beta.123", false)]
    [InlineData("3.0.0-beta.122", "3.0.0-beta.123", false)]
    // semver core wins before beta build number
    [InlineData("3.0.1-beta.1",   "3.0.0-beta.999", true)]
    [InlineData("3.1.0-beta.1",   "3.0.9-beta.999", true)]
    // stable (no prerelease) ranks above any beta of the same core
    [InlineData("3.0.0",          "3.0.0-beta.999", true)]
    [InlineData("3.0.0-beta.999", "3.0.0",          false)]
    // plain stable comparisons still work
    [InlineData("3.0.1",          "3.0.0",          true)]
    [InlineData("3.0.0",          "3.0.0",          false)]
    public void IsNewerVersion_ReturnsExpected(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateChecker.IsNewerVersion(latest, current));
    }

    [Theory]
    [InlineData("not-a-version", "3.0.0-beta.1")]
    [InlineData("3.0.0-beta.1",  "not-a-version")]
    public void IsNewerVersion_UnparsableInput_ReturnsFalse(string latest, string current)
    {
        Assert.False(UpdateChecker.IsNewerVersion(latest, current));
    }
}
