param(
    [Parameter(Mandatory = $true)]
    [string]$PublishOutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$PackageOutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$CommitSha
)

$ErrorActionPreference = "Stop"

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

$publishRoot = (Resolve-Path -LiteralPath $PublishOutputDirectory).Path
$packageRoot = $PackageOutputDirectory
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

Ensure-Directory -Path $packageRoot

# Single-file publish: the only output file is SimTrackerV2.exe.
# SimConnect native DLLs and the runway CSV are all bundled inside it.
$exeSource = Join-Path $publishRoot "SimTrackerV2.exe"
if (Test-Path -LiteralPath $exeSource) {
    Copy-Item -LiteralPath $exeSource -Destination (Join-Path $packageRoot "SimTrackerV2.exe") -Force
} else {
    # Fallback: copy everything if single-file output isn't found (e.g. local non-single-file build)
    Copy-Item -Path (Join-Path $publishRoot '*') -Destination $packageRoot -Recurse -Force
}

$versionFile = Join-Path $packageRoot "BUILD_INFO.txt"
@(
    "SimCrewOps Tracker Beta"
    "Version: $Version"
    "Commit:  $CommitSha"
    "Built UTC: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ""
    "SimConnect DLLs, runway data, and the .NET runtime are all bundled inside SimTrackerV2.exe."
    "No additional files are required — just run SimTrackerV2.exe."
    ""
    "Settings are stored under %LOCALAPPDATA%\SimCrewOps\SimTrackerV2 on first launch."
)
| Set-Content -LiteralPath $versionFile -Encoding UTF8
