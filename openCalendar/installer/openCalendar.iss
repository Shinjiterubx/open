; openCalendar – Inno Setup 6 script
; Build: run build\publish.ps1 (it publishes the app, then compiles this script).
; Manual: ISCC.exe /DAppVersion=1.0.0 installer\openCalendar.iss

#define AppName "openCalendar"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "openCalendar contributors"
#define AppExeName "openCalendar.exe"
#define SourceDir "..\artifacts\publish\win-x64"

[Setup]
AppId={{4B1D7E92-3C5A-4F8B-B6E1-9D2C7A5F0E34}
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
OutputBaseFilename=openCalendar-Setup-{#AppVersion}-x64
SetupIconFile=openCalendar.ico
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
hungarian.AutoStart=Indítás a Windows-zal (az emlékeztetőkhöz ajánlott)
english.AutoStart=Start with Windows (recommended for reminders)
hungarian.FileAssoc=.opencalendar fájlok megnyitása az openCalendar-ral
english.FileAssoc=Open .opencalendar files with openCalendar
hungarian.OtherTasks=Egyéb:
english.OtherTasks=Other:
hungarian.NoteFileType=openCalendar naptár
english.NoteFileType=openCalendar calendar

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:AutoStart}"; GroupDescription: "{cm:OtherTasks}"
Name: "fileassoc"; Description: "{cm:FileAssoc}"; GroupDescription: "{cm:OtherTasks}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; .opencalendar file association (HKA = HKCU for per-user, HKLM for all-users installs)
Root: HKA; Subkey: "Software\Classes\.opencalendar"; ValueType: string; ValueName: ""; ValueData: "openCalendar.Calendar"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.opencalendar"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-opencalendar+zip"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\openCalendar.Calendar"; ValueType: string; ValueName: ""; ValueData: "{cm:NoteFileType}"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\openCalendar.Calendar\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\openCalendar.Calendar\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: fileassoc
; Integration info written by the app on start (read by openKanban / openLauncher); removed on uninstall
Root: HKCU; Subkey: "Software\openCalendar"; ValueType: none; Flags: uninsdeletekey
; Autostart – same value the app writes from its settings window
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "openCalendar"; ValueData: """{app}\{#AppExeName}"" --background"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\openCalendar\Logs"
; Calendars in %APPDATA%\openCalendar are user data and are intentionally kept.

