# Windows Packaging

This folder contains the Windows Beta publish, packaging, and installer path for `SimTrackerV2`.

## What CI Produces

The GitHub Actions workflow publishes a self-contained `win-x64` single-file build and produces two end-user deliverables:

- `SimTrackerV2-Setup-<version>.exe`
- `SimTrackerV2-beta-win-x64.zip`

The installer EXE is the recommended delivery path. The ZIP remains available as a portable fallback and as the in-app updater payload.

## Runtime Bundling

The Windows app is published as a single-file executable:

- `SimTrackerV2.exe`

SimConnect native DLLs are embedded into the app and extracted on startup into `%LOCALAPPDATA%\SimCrewOps\SimTrackerV2\native`.
Users do not need to place DLLs manually.

## Local Windows Publish

From a Windows machine with the .NET 8 SDK installed:

```powershell
dotnet publish SimCrewOps.App.Wpf/SimCrewOps.App.Wpf.csproj `
  -c Release `
  -p:PublishProfile=Properties\PublishProfiles\Beta-win-x64.pubxml `
  -p:InformationalVersion=3.0.0-beta-local

powershell -ExecutionPolicy Bypass -File packaging/windows/prepare-beta-package.ps1 `
  -PublishOutputDirectory SimCrewOps.App.Wpf/artifacts/publish/SimTrackerV2-win-x64 `
  -PackageOutputDirectory artifacts/package/SimTrackerV2-beta-win-x64 `
  -Version 3.0.0-beta-local `
  -CommitSha local

iscc packaging\windows\installer\SimTrackerV2.iss `
  /DMyAppVersion="3.0.0-beta-local" `
  /DMyAppSourceDir="artifacts\package\SimTrackerV2-beta-win-x64"
```

## Install And Update Model

- Initial install: run `SimTrackerV2-Setup.exe`
- Portable fallback: unzip `SimTrackerV2-beta-win-x64.zip` and run `SimTrackerV2.exe`
- In-app updates: the tracker checks `beta-latest` on startup and every 4 hours, then downloads the portable ZIP and restarts into the new version

The installer and the in-app updater share the same packaged app payload.
