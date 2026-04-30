; SimCrewOps Tracker — Inno Setup installer script (Beta channel)
;
; Usage (from repo root):
;   iscc packaging\windows\installer\SimTrackerV2.iss /DMyAppVersion="3.0.0-beta.42"
;
; The installer packages the single-file SimTrackerV2.exe produced by the Beta-win-x64
; publish profile. All runtime dependencies are bundled inside the EXE — no sidecar
; DLL placement or manual extraction is required for end users.

#ifndef MyAppVersion
  #define MyAppVersion "3.0.0"
#endif

#ifndef MyAppSourceDir
  #define MyAppSourceDir "..\..\artifacts\package\SimTrackerV2-beta-win-x64"
#endif

#define MyAppName "SimCrewOps Tracker"
#define MyAppPublisher "SimCrewOps"
#define MyAppExeName "SimTrackerV2.exe"
#define MyAppId "{0D8E4708-AEE3-46B6-9B9E-7F7A0D7145DA}"

[Setup]
AppId={{#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://simcrewops.com
AppSupportURL=https://simcrewops.com
AppUpdatesURL=https://simcrewops.com
DefaultDirName={localappdata}\SimCrewOps\SimTrackerV2
DefaultGroupName=SimCrewOps
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\..\..\artifacts\installer
OutputBaseFilename=SimTrackerV2-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
