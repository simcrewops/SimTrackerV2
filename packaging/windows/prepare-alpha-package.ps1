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

$appRoot = Join-Path $packageRoot "SimTrackerV2"
$dataRoot = Join-Path $appRoot "data"
Ensure-Directory -Path $appRoot
Ensure-Directory -Path $dataRoot

Copy-Item -Path (Join-Path $publishRoot '*') -Destination $appRoot -Recurse -Force

$optionalNativeSimConnect = Join-Path $repoRoot "packaging/windows/runtime/SimConnect.dll"
if (Test-Path -LiteralPath $optionalNativeSimConnect) {
    Copy-Item -LiteralPath $optionalNativeSimConnect -Destination (Join-Path $appRoot "SimConnect.dll") -Force
}

$optionalManagedWrapper = Join-Path $repoRoot "packaging/windows/runtime/Microsoft.FlightSimulator.SimConnect.dll"
if (Test-Path -LiteralPath $optionalManagedWrapper) {
    Copy-Item -LiteralPath $optionalManagedWrapper -Destination (Join-Path $appRoot "Microsoft.FlightSimulator.SimConnect.dll") -Force
}

$optionalRunwaysCsv = Join-Path $repoRoot "packaging/windows/data/ourairports-runways.csv"
if (Test-Path -LiteralPath $optionalRunwaysCsv) {
    Copy-Item -LiteralPath $optionalRunwaysCsv -Destination (Join-Path $dataRoot "ourairports-runways.csv") -Force
}

$versionFile = Join-Path $appRoot "BUILD_INFO.txt"
@(
    "SimTrackerV2 Alpha Package"
    "Version: $Version"
    "Commit: $CommitSha"
    "Built UTC: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ""
    "Notes:"
    "- settings.json will be created under %LOCALAPPDATA%\SimCrewOps\SimTrackerV2 on first launch."
    "- Place SimConnect.dll in packaging/windows/runtime before packaging if you want the native client library bundled."
    "- Place Microsoft.FlightSimulator.SimConnect.dll in packaging/windows/runtime before packaging if you want the managed fallback/provider assembly bundled."
    "- Place ourairports-runways.csv in packaging/windows/data before packaging if you want a bundled fallback runway dataset."
)
| Set-Content -LiteralPath $versionFile -Encoding UTF8

$readmePath = Join-Path $packageRoot "README.txt"
@(
    "SimTrackerV2 alpha package"
    ""
    "Contents:"
    "- SimTrackerV2\\SimTrackerV2.exe"
    "- SimTrackerV2\\BUILD_INFO.txt"
    ""
    "Run:"
    "1. Extract the zip."
    "2. Launch SimTrackerV2.exe."
    "3. Configure your API token in Settings."
    ""
    "Optional runtime assets:"
    "- SimConnect.dll can be bundled next to the executable."
    "- Microsoft.FlightSimulator.SimConnect.dll can be bundled next to the executable."
    "- data\\ourairports-runways.csv can be bundled for fallback runway resolution."
)
| Set-Content -LiteralPath $readmePath -Encoding UTF8
