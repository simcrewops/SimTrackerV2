# Windows Packaging

This folder contains the Windows publish and packaging path for `SimTrackerV2`.

## What CI Produces

The GitHub Actions workflow publishes a self-contained `win-x64` WPF build and stages a release-style package layout:

- `SimTrackerV2/SimTrackerV2.exe`
- `SimTrackerV2/BUILD_INFO.txt`
- `README.txt`

That staged package is zipped and uploaded as a workflow artifact.

## Optional Bundled Runtime Assets

The package-prep script will include these files automatically if they exist:

- `packaging/windows/runtime/Microsoft.FlightSimulator.SimConnect.dll`
- `packaging/windows/data/ourairports-runways.csv`

These are optional:

- the managed SimConnect wrapper can also be discovered from the machine at runtime
- runway resolution will still prefer the live SimConnect Facility API and only use OurAirports as fallback

## Local Windows Publish

From a Windows machine with the .NET 8 SDK installed:

```powershell
dotnet publish SimCrewOps.App.Wpf/SimCrewOps.App.Wpf.csproj `
  -c Release `
  -p:PublishProfile=Properties\PublishProfiles\Alpha-win-x64.pubxml `
  -p:InformationalVersion=2.0.0-alpha-local

powershell -ExecutionPolicy Bypass -File packaging/windows/prepare-alpha-package.ps1 `
  -PublishOutputDirectory SimCrewOps.App.Wpf/artifacts/publish/SimTrackerV2-win-x64 `
  -PackageOutputDirectory artifacts/package/SimTrackerV2-alpha-win-x64 `
  -Version 2.0.0-alpha-local `
  -CommitSha local
```

## Installer Status

This pass builds a publishable alpha artifact path first. A traditional Windows installer can layer on top of this package layout without changing the app runtime structure.

An Inno Setup scaffold is included at:

- `packaging/windows/installer/SimTrackerV2.iss`

It targets the staged alpha package directory created by the prep script.
