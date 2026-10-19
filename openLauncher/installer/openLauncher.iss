; openLauncher – Inno Setup 6 script (suite installer)
; Build: run build\build-all.ps1 – it builds openNotes, openCalendar and openKanban (sibling folders),
;        copies their setups into installer\payload\, publishes the launcher, then compiles this script.
; Manual: ISCC.exe /DAppVersion=1.0.0 installer\openLauncher.iss   (payload\*.exe must exist)
;
; The user picks the apps on the "Components" page. Every app setup is also copied to {app}\Installers,
; so openLauncher can install / reinstall the rest later without the internet.

#define AppName "openLauncher"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "openLauncher contributors"
#define AppExeName "openLauncher.exe"
#define SourceDir "..\artifacts\publish\win-x64"
#define Payload "payload"

[Setup]
AppId={{9C3E1A57-2B8D-4E6F-A1C4-7D5B3F9E2A60}
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
OutputBaseFilename=openSuite-Setup-{#AppVersion}-x64
SetupIconFile=openLauncher.ico
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
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "hungarian"; MessagesFile: "compiler:Languages\Hungarian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
hungarian.TypeFull=Teljes telepítés (minden alkalmazás)
english.TypeFull=Full installation (all apps)
hungarian.TypeLauncher=Csak az openLauncher
english.TypeLauncher=openLauncher only
hungarian.TypeCustom=Egyéni – én választom ki az alkalmazásokat
english.TypeCustom=Custom – let me choose the apps
hungarian.CompLauncher=openLauncher – indítás, telepítés és eltávolítás egy helyen
english.CompLauncher=openLauncher – start, install and remove the apps in one place
hungarian.CompNotes=openNotes – jegyzetek Markdownban
english.CompNotes=openNotes – Markdown notes
hungarian.CompCalendar=openCalendar – naptár és emlékeztetők
english.CompCalendar=openCalendar – calendar and reminders
hungarian.CompKanban=openKanban – Kanban táblák
english.CompKanban=openKanban – Kanban boards
hungarian.Installing=%1 telepítése…
english.Installing=Installing %1…

[Types]
Name: "full"; Description: "{cm:TypeFull}"
Name: "launcher"; Description: "{cm:TypeLauncher}"
Name: "custom"; Description: "{cm:TypeCustom}"; Flags: iscustom

[Components]
Name: "launcher"; Description: "{cm:CompLauncher}"; Types: full launcher custom; Flags: fixed
Name: "opennotes"; Description: "{cm:CompNotes}"; Types: full
Name: "opencalendar"; Description: "{cm:CompCalendar}"; Types: full
Name: "openkanban"; Description: "{cm:CompKanban}"; Types: full

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: launcher
; All setups are always bundled so the launcher can install the remaining apps later.
Source: "{#Payload}\openNotes-Setup.exe"; DestDir: "{app}\Installers"; Flags: ignoreversion
Source: "{#Payload}\openCalendar-Setup.exe"; DestDir: "{app}\Installers"; Flags: ignoreversion
Source: "{#Payload}\openKanban-Setup.exe"; DestDir: "{app}\Installers"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\openLauncher"; ValueType: string; ValueName: "ExePath"; ValueData: "{app}\{#AppExeName}"; Flags: uninsdeletekey

[Run]
; The selected apps are installed with their own setups, silently and in the same scope (per-user / all users).
Filename: "{app}\Installers\openNotes-Setup.exe"; Parameters: "{code:SilentArgs|openNotes}"; StatusMsg: "{cm:Installing,openNotes}"; Components: opennotes; Flags: waituntilterminated
Filename: "{app}\Installers\openCalendar-Setup.exe"; Parameters: "{code:SilentArgs|openCalendar}"; StatusMsg: "{cm:Installing,openCalendar}"; Components: opencalendar; Flags: waituntilterminated
Filename: "{app}\Installers\openKanban-Setup.exe"; Parameters: "{code:SilentArgs|openKanban}"; StatusMsg: "{cm:Installing,openKanban}"; Components: openkanban; Flags: waituntilterminated
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Installers"
Type: filesandordirs; Name: "{localappdata}\openLauncher"
; The apps themselves are NOT removed with the launcher: each has its own uninstaller
; (Windows Settings → Apps, or remove them in openLauncher first).

[Code]
function SilentArgs(App: String): String;
var
  Scope, LogDir: String;
begin
  if IsAdminInstallMode then Scope := '/ALLUSERS' else Scope := '/CURRENTUSER';
  { Same log folder openLauncher uses, so failed installs can be inspected from the app ("Naplók"). }
  LogDir := ExpandConstant('{localappdata}\openLauncher\Logs');
  ForceDirectories(LogDir);
  Result := '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- ' + Scope +
            ' "/LOG=' + LogDir + '\setup-' + App + '.log"';
end;
