; openKanban – Inno Setup 6 script
; Build: run build\publish.ps1 (it publishes the app, then compiles this script).
; Manual: ISCC.exe /DAppVersion=1.0.0 installer\openKanban.iss

#define AppName "openKanban"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "openKanban contributors"
#define AppExeName "openKanban.exe"
#define SourceDir "..\artifacts\publish\win-x64"

[Setup]
AppId={{6A2C9D14-7E3B-4F51-A8C6-2B9E4D7F1A58}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (c) 2026 Szegedi Tamás and contributors – MIT License
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installer
OutputBaseFilename=openKanban-Setup-{#AppVersion}-x64
SetupIconFile=openKanban.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
WizardStyle=modern
WizardImageFile=wizard-large.bmp
WizardSmallImageFile=wizard-small.bmp
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; Per-user install by default (no admin rights needed); admins may choose "all users".
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "hungarian"; MessagesFile: "compiler:Languages\Hungarian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
hungarian.FileAssoc=.openKanban fájlok megnyitása az openKanban-nal
english.FileAssoc=Open .openKanban files with openKanban
hungarian.OtherTasks=Egyéb:
english.OtherTasks=Other:
hungarian.NoteFileType=openKanban workspace
english.NoteFileType=openKanban workspace

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc"; Description: "{cm:FileAssoc}"; GroupDescription: "{cm:OtherTasks}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; .openKanban file association (HKA = HKCU for per-user, HKLM for all-users installs)
Root: HKA; Subkey: "Software\Classes\.openKanban"; ValueType: string; ValueName: ""; ValueData: "openKanban.Workspace"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.openKanban"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-openkanban+zip"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\openKanban.Workspace"; ValueType: string; ValueName: ""; ValueData: "{cm:NoteFileType}"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\openKanban.Workspace\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\openKanban.Workspace\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: fileassoc

; Integration info written by the app on start (read by openCalendar / openLauncher)
Root: HKCU; Subkey: "Software\openKanban"; ValueType: none; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\openKanban\Logs"
; Workspaces in %APPDATA%\openKanban are user data and are intentionally kept.

