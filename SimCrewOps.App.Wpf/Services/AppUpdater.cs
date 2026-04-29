using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace SimCrewOps.App.Wpf.Services;

/// <summary>
/// Downloads a new release ZIP and launches a PowerShell helper script that:
///   1. Waits for the current process to exit.
///   2. Extracts the ZIP over the existing installation directory.
///   3. Restarts SimTrackerV2.exe.
///
/// The caller is responsible for calling Application.Shutdown() immediately
/// after <see cref="DownloadAndApplyAsync"/> returns so the updater script
/// can overwrite the executable and supporting files.
/// </summary>
public sealed class AppUpdater
{
    // The directory that contains SimTrackerV2.exe.
    private static readonly string AppDirectory =
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    private readonly HttpClient _httpClient;

    public AppUpdater(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Raised on the thread that called <see cref="DownloadAndApplyAsync"/> with
    /// a value in [0.0, 1.0] as bytes arrive.
    /// </summary>
    public event EventHandler<double>? DownloadProgressChanged;

    /// <summary>
    /// Downloads the update, writes a PowerShell updater script to %TEMP%, and
    /// launches it hidden.  Returns when the download is complete and the script
    /// process has been started — the caller must then exit the application.
    /// </summary>
    public async Task DownloadAndApplyAsync(
        UpdateInfo update,
        CancellationToken cancellationToken = default)
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

        LaunchUpdaterScript(tempZip);
    }

    // ── Private implementation ────────────────────────────────────────────────

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

        while ((bytesRead = await contentStream
                   .ReadAsync(buffer, cancellationToken)
                   .ConfigureAwait(false)) > 0)
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

    private static void LaunchUpdaterScript(string zipPath)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "SimTrackerV2-updater.ps1");
        var exePath = Path.Combine(AppDirectory, "SimTrackerV2.exe");
        var currentPid = Environment.ProcessId;

        var script = BuildUpdateScript(currentPid, zipPath, AppDirectory, exePath);
        File.WriteAllText(scriptPath, script, System.Text.Encoding.UTF8);

        var psi = new ProcessStartInfo("powershell.exe")
        {
            // -NonInteractive prevents PowerShell from waiting for input on error paths.
            Arguments =
                $"-ExecutionPolicy Bypass -WindowStyle Hidden -NonInteractive -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process.Start(psi);
    }

    /// <summary>
    /// Builds the PowerShell script that performs the actual file replacement.
    /// Uses single-quoted strings throughout to avoid interpolation issues with paths.
    /// </summary>
    private static string BuildUpdateScript(
        int pid,
        string zipPath,
        string appDirectory,
        string exePath)
    {
        // Escape single quotes in paths (PowerShell single-quote escaping: '' = literal ')
        var safeZip = zipPath.Replace("'", "''");
        var safeDir = appDirectory.Replace("'", "''");
        var safeExe = exePath.Replace("'", "''");

        return $"""
$ErrorActionPreference = 'Stop'

# ── Step 1: wait for the tracker process to fully exit ────────────────────────
try {{
    $proc = Get-Process -Id {pid} -ErrorAction SilentlyContinue
    if ($null -ne $proc) {{
        $null = $proc.WaitForExit(30000)
    }}
}} catch {{ }}

# Give the OS a moment to release any file handles.
Start-Sleep -Milliseconds 800

# ── Step 2: extract the update package ───────────────────────────────────────
try {{
    Expand-Archive -Path '{safeZip}' -DestinationPath '{safeDir}' -Force
}} catch {{
    Add-Type -AssemblyName System.Windows.Forms
    [void][System.Windows.Forms.MessageBox]::Show(
        "SimCrewOps Tracker update failed:`n$_",
        'Update Error',
        'OK',
        'Error'
    )
    exit 1
}}

# ── Step 3: clean up the downloaded ZIP ──────────────────────────────────────
Remove-Item -Path '{safeZip}' -Force -ErrorAction SilentlyContinue

# ── Step 4: restart the tracker ──────────────────────────────────────────────
Start-Process -FilePath '{safeExe}'
""";
    }
}
