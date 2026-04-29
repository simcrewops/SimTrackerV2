using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SimCrewOps.App.Wpf.Services;

/// <summary>
/// Describes an available update fetched from the GitHub Releases API.
/// </summary>
public sealed record UpdateInfo
{
    /// <summary>The version currently installed, e.g. "3.0.0.42".</summary>
    public required string CurrentVersion { get; init; }

    /// <summary>The latest available version, e.g. "3.0.0.55".</summary>
    public required string LatestVersion { get; init; }

    /// <summary>Direct download URL for the Windows ZIP asset.</summary>
    public required Uri DownloadUrl { get; init; }

    /// <summary>Expected size of the download in bytes (0 when unknown).</summary>
    public long DownloadSizeBytes { get; init; }

    /// <summary>True when LatestVersion is numerically greater than CurrentVersion.</summary>
    public required bool IsUpdateAvailable { get; init; }
}

/// <summary>
/// Checks the GitHub Releases API for a newer build of SimTrackerV2.
/// All failures are swallowed — the caller always receives null on error.
/// </summary>
public sealed class UpdateChecker
{
    // Fetch by explicit tag so we always get the rolling beta build regardless of
    // GitHub's "latest" flag ordering.  The tag is stable; only its associated
    // release body and assets change on each push.
    public const string ReleasesApiUrl =
        "https://api.github.com/repos/simcrewops/SimTrackerV2/releases/tags/beta-latest";

    public const string ExpectedAssetName = "SimTrackerV2-beta-win-x64.zip";

    private readonly HttpClient _httpClient;

    public UpdateChecker(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Contacts the GitHub Releases API and returns update information.
    /// Returns null on any network error, parse failure, or missing asset.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            // GitHub API requires a User-Agent header; version helps with rate-limit attribution.
            request.Headers.TryAddWithoutValidation("User-Agent", $"SimTrackerV2/{currentVersion}");
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

            using var response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var release = await response.Content
                .ReadFromJsonAsync<GitHubReleaseResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (release is null)
                return null;

            // Release name format: "SimCrewOps Tracker v3.0.0.42"
            var latestVersion = ParseVersionFromReleaseName(release.Name);
            if (latestVersion is null)
                return null;

            // Find the Windows x64 ZIP asset.
            var asset = release.Assets?.FirstOrDefault(a =>
                string.Equals(a.Name, ExpectedAssetName, StringComparison.OrdinalIgnoreCase));

            if (asset?.BrowserDownloadUrl is null)
                return null;

            return new UpdateInfo
            {
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                DownloadUrl = new Uri(asset.BrowserDownloadUrl),
                DownloadSizeBytes = asset.Size,
                IsUpdateAvailable = IsNewerVersion(latestVersion, currentVersion),
            };
        }
        catch
        {
            // Update checks must never surface an exception to the caller.
            return null;
        }
    }

    /// <summary>
    /// Returns the currently installed version string, stripped of any commit-hash suffix.
    /// For example "3.0.0.42+abc1234" becomes "3.0.0.42".
    /// Returns "dev" for local/CI builds that don't embed an informational version.
    /// </summary>
    public static string GetCurrentVersion()
    {
        var informational = Assembly
            .GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
            return plusIndex >= 0 ? informational[..plusIndex] : informational;
        }

        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "dev";
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Parses the version number from a release name like "SimCrewOps Tracker v3.0.0.42".
    /// Returns null when the last token doesn't parse as a System.Version.
    /// </summary>
    internal static string? ParseVersionFromReleaseName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        var candidate = parts[^1].TrimStart('v', 'V');
        return Version.TryParse(candidate, out _) ? candidate : null;
    }

    /// <summary>
    /// Returns true when <paramref name="latest"/> is numerically greater than
    /// <paramref name="current"/>. Both strings must be parseable as System.Version;
    /// returns false when either cannot be parsed (e.g. "dev" builds).
    /// </summary>
    internal static bool IsNewerVersion(string latest, string current)
    {
        if (!Version.TryParse(latest, out var latestVer))
            return false;
        if (!Version.TryParse(current, out var currentVer))
            return false;

        return latestVer > currentVer;
    }
}

// ── Internal GitHub API response models (file-scoped) ────────────────────────

file sealed record GitHubReleaseResponse
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("assets")]
    public IReadOnlyList<GitHubReleaseAsset>? Assets { get; init; }
}

file sealed record GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; init; }

    [JsonPropertyName("size")]
    public long Size { get; init; }
}
