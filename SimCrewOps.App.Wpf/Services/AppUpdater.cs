using System.Diagnostics;
using System.IO;

namespace SimCrewOps.App.Wpf.Services;

public sealed class AppUpdater
{
    private static readonly string AppDirectory =
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

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

        LaunchUpdaterScript(tempZip);
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

    private static void LaunchUpdaterScript(string zipPath)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "SimTrackerV2-updater.ps1");
        var exePath = Path.Combine(AppDirectory, "SimTrackerV2.exe");
        var currentPid = Environment.ProcessId;

        var script = BuildUpdateScript(currentPid, zipPath, AppDirectory, exePath);
        File.WriteAllText(scriptPath, script, System.Text.Encoding.UTF8);

        var processStartInfo = new ProcessStartInfo("powershell.exe")
        {
            Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -NonInteractive -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process.Start(processStartInfo);
    }

    private static string BuildUpdateScript(int pid, string zipPath, string appDirectory, string exePath)
    {
        var safeZip = zipPath.Replace("'", "''");
        var safeDir = appDirectory.Replace("'", "''");
        var safeExe = exePath.Replace("'", "''");

        return $$"""
$ErrorActionPreference = 'Stop'

try {
    $proc = Get-Process -Id {{pid}} -ErrorAction SilentlyContinue
    if ($null -ne $proc) {
        $null = $proc.WaitForExit(30000)
    }
} catch { }

Start-Sleep -Milliseconds 800

try {
    Expand-Archive -Path '{{safeZip}}' -DestinationPath '{{safeDir}}' -Force
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [void][System.Windows.Forms.MessageBox]::Show(
        "SimCrewOps Tracker update failed:`n$_",
        'Update Error',
        'OK',
        'Error'
    )
    exit 1
}

Remove-Item -Path '{{safeZip}}' -Force -ErrorAction SilentlyContinue
Start-Process -FilePath '{{safeExe}}'
""";
    }
}
