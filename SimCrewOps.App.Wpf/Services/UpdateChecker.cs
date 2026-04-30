using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SimCrewOps.App.Wpf.Services;

public sealed record UpdateInfo
{
    public required string CurrentVersion { get; init; }
    public required string LatestVersion { get; init; }
    public required Uri DownloadUrl { get; init; }
    public long DownloadSizeBytes { get; init; }
    public required bool IsUpdateAvailable { get; init; }
}

public sealed class UpdateChecker
{
    public const string ReleasesApiUrl =
        "https://api.github.com/repos/simcrewops/SimTrackerV2/releases/tags/beta-latest";

    public const string ExpectedAssetName = "SimTrackerV2-beta-win-x64.zip";

    private readonly HttpClient _httpClient;

    public UpdateChecker(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", $"SimTrackerV2/{currentVersion}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

            using var response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var release = await response.Content
                .ReadFromJsonAsync<GitHubReleaseResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (release is null)
            {
                return null;
            }

            var latestVersion = ParseVersionFromReleaseName(release.Name);
            if (latestVersion is null)
            {
                return null;
            }

            var asset = release.Assets?.FirstOrDefault(a =>
                string.Equals(a.Name, ExpectedAssetName, StringComparison.OrdinalIgnoreCase));

            if (asset?.BrowserDownloadUrl is null)
            {
                return null;
            }

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
            return null;
        }
    }

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

    internal static string? ParseVersionFromReleaseName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var candidate = parts[^1].TrimStart('v', 'V');
        return Version.TryParse(candidate, out _) ? candidate : null;
    }

    internal static bool IsNewerVersion(string latest, string current)
    {
        if (!Version.TryParse(latest, out var latestVersion))
        {
            return false;
        }

        if (!Version.TryParse(current, out var currentVersion))
        {
            return false;
        }

        return latestVersion > currentVersion;
    }
}

file sealed record GitHubReleaseResponse
{
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
