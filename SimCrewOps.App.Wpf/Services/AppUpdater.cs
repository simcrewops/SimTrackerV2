using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace SimCrewOps.App.Wpf.Services;

public sealed class AppUpdater
{
    private static readonly string AppDirectory =
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    internal const string RequiredExeName = "SimTrackerV2.exe";

    private readonly HttpClient _httpClient;

    public AppUpdater(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public event EventHandler<double>? DownloadProgressChanged;

    public async Task DownloadAndApplyAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var tempZip = Path.Combine(
            Path.GetTempPath(),
            $"SimTrackerV2-update-{update.LatestVersion}.zip");

        await DownloadWithProgressAsync(
            update.DownloadUrl,
            tempZip,
            update.DownloadSizeBytes,
            cancellationToken).ConfigureAwait(false);

        VerifyZipPayload(tempZip);
        LaunchUpdaterScript(tempZip, update.LatestVersion);
    }

    /// <summary>
    /// Verifies that the downloaded ZIP contains the expected executable.
    /// Deletes the ZIP and throws if the payload looks wrong, so the current install is never
    /// replaced by a corrupt or mismatched archive.
    /// </summary>
    internal static void VerifyZipPayload(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var hasExe = zip.Entries.Any(e =>
            string.Equals(e.Name, RequiredExeName, StringComparison.OrdinalIgnoreCase));

        if (hasExe)
            return;

        // Clean up before throwing so temp storage is not littered with bad ZIPs.
        zip.Dispose();
        try { File.Delete(zipPath); } catch { /* best-effort */ }

        throw new InvalidOperationException(
            $"Downloaded update does not contain {RequiredExeName} — update aborted to protect the current installation.");
    }

    private async Task DownloadWithProgressAsync(
        Uri url,
        string destination,
        long expectedBytes,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "SimTrackerV2-updater");

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? expectedBytes;

        await using var contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var fileStream = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        var buffer = new byte[65536];
        long downloadedBytes = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream
                .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                .ConfigureAwait(false);

            downloadedBytes += bytesRead;
            if (totalBytes > 0)
            {
                DownloadProgressChanged?.Invoke(this, (double)downloadedBytes / totalBytes);
            }
        }
    }

    private static void LaunchUpdaterScript(string zipPath, string expectedVersion)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "SimTrackerV2-updater.ps1");
        var exePath = Path.Combine(AppDirectory, "SimTrackerV2.exe");
        var currentPid = Environment.ProcessId;

        var script = BuildUpdateScript(currentPid, zipPath, AppDirectory, exePath, expectedVersion);
        File.WriteAllText(scriptPath, script, System.Text.Encoding.UTF8);

        var processStartInfo = new ProcessStartInfo("powershell.exe")
        {
            Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -NonInteractive -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process.Start(processStartInfo);
    }

    internal static string BuildUpdateScript(int pid, string zipPath, string appDirectory, string exePath, string expectedVersion)
    {
        var safeZip = zipPath.Replace("'", "''");
        var safeDir = appDirectory.Replace("'", "''");
        var safeExe = exePath.Replace("'", "''");
        var safeVer = expectedVersion.Replace("'", "''");

        return $$"""
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

function Show-Error($message) {
    [void][System.Windows.Forms.MessageBox]::Show(
        $message, 'SimCrewOps Tracker Update', 'OK', 'Error', 'DefaultDesktopOnly')
}

# Step 1 — graceful exit (give the app 5s to quit on its own)
try {
    $proc = Get-Process -Id {{pid}} -ErrorAction SilentlyContinue
    if ($null -ne $proc) {
        $null = $proc.WaitForExit(5000)
    }
} catch { }

# Step 2 — force-kill if still running (graceful exit timed out)
try {
    $proc = Get-Process -Id {{pid}} -ErrorAction SilentlyContinue
    if ($null -ne $proc -and -not $proc.HasExited) {
        Stop-Process -Id {{pid}} -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 800
    }
} catch { }

# Step 3 — kill any other SimTrackerV2 processes (handle holders, child processes, etc.)
Get-Process -Name 'SimTrackerV2' -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# Step 4 — extract the update; abort with user-visible error on failure
try {
    Expand-Archive -Path '{{safeZip}}' -DestinationPath '{{safeDir}}' -Force -ErrorAction Stop
} catch {
    Show-Error("Update extraction failed:`n$_`n`nPlease close the tracker fully and try again, or reinstall manually.")
    exit 1
}

# Step 5 — verify the new SimTrackerV2.exe matches the expected version
try {
    $newVersion = (Get-Item '{{safeExe}}' -ErrorAction Stop).VersionInfo.ProductVersion
    if ($null -eq $newVersion -or $newVersion -notlike '*{{safeVer}}*') {
        Show-Error("Update verification failed.`n`nExpected version: {{safeVer}}`nFound version: $newVersion`n`nThe update may not have applied correctly. Please reinstall manually.")
        exit 1
    }
} catch {
    Show-Error("Update verification failed:`n$_`n`nPlease reinstall manually.")
    exit 1
}

# Step 6 — clean up and relaunch
Remove-Item -Path '{{safeZip}}' -Force -ErrorAction SilentlyContinue
Start-Process -FilePath '{{safeExe}}'
""";
    }
}
