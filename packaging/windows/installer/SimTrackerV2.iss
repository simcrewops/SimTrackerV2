#define MyAppName "SimTrackerV2"
#define MyAppVersion "2.0.0-alpha"
#define MyAppPublisher "SimCrewOps"
#define MyAppExeName "SimTrackerV2.exe"
#define MyAppSourceDir "..\..\artifacts\package\SimTrackerV2-alpha-win-x64\SimTrackerV2"

[Setup]
AppId={{0D8E4708-AEE3-46B6-9B9E-7F7A0D7145DA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\SimCrewOps\SimTrackerV2
DefaultGroupName=SimCrewOps
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\..\artifacts\installer
OutputBaseFilename=SimTrackerV2-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SimCrewOps\SimTrackerV2"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\SimTrackerV2"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch SimTrackerV2"; Flags: nowait postinstall skipifsilent
