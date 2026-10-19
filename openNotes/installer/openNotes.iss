; openNotes – Inno Setup 6 script
; Build: run build\publish.ps1 (it publishes the app, then compiles this script).
; Manual: ISCC.exe /DAppVersion=1.0.0 installer\openNotes.iss

#define AppName "openNotes"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "openNotes contributors"
#define AppExeName "openNotes.exe"
#define SourceDir "..\artifacts\publish\win-x64"

[Setup]
AppId={{8E3C5B2A-6F1D-4C7E-9A42-0B7D5E1F3C21}
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
OutputBaseFilename=openNotes-Setup-{#AppVersion}-x64
SetupIconFile=openNotes.ico
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
hungarian.AutoStart=Indítás a Windows-zal (a gyorsbillentyűkhöz ajánlott)
english.AutoStart=Start with Windows (recommended for the global hotkeys)
hungarian.FileAssoc=.openNote fájlok megnyitása az openNotes-szal
english.FileAssoc=Open .openNote files with openNotes
hungarian.OtherTasks=Egyéb:
english.OtherTasks=Other:
hungarian.NoteFileType=openNotes jegyzet
english.NoteFileType=openNotes note
hungarian.WebView2Missing=Az előnézethez szükséges Microsoft Edge WebView2 Runtime nem található.%n%nMegnyissam a letöltési oldalt? (Windows 11-en és frissített Windows 10-en általában már telepítve van.)
english.WebView2Missing=The Microsoft Edge WebView2 Runtime required for the preview was not found.%n%nOpen the download page?

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
; .openNote file association (HKA = HKCU for per-user, HKLM for all-users installs)
Root: HKA; Subkey: "Software\Classes\.openNote"; ValueType: string; ValueName: ""; ValueData: "openNotes.Note"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.openNote"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-opennote+zip"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\openNotes.Note"; ValueType: string; ValueName: ""; ValueData: "{cm:NoteFileType}"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\openNotes.Note\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\openNotes.Note\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: fileassoc
; Integration info written by the app on start (read by openCalendar); removed on uninstall
Root: HKCU; Subkey: "Software\openNotes"; ValueType: none; Flags: uninsdeletekey
; Autostart – same value the app writes from its settings window
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "openNotes"; ValueData: """{app}\{#AppExeName}"" --background"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\openNotes\PreviewCache"
Type: filesandordirs; Name: "{localappdata}\openNotes\WebView2"
; Notes in %APPDATA%\openNotes are user data and are intentionally kept.

[Code]
const
  WebView2ClientKey = 'Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  WebView2DownloadUrl = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';

function HasWebView2Version(const RootKey: Integer; const SubKey: String): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(RootKey, SubKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0');
end;

function IsWebView2Installed(): Boolean;
begin
  Result := HasWebView2Version(HKLM, 'SOFTWARE\WOW6432Node\' + WebView2ClientKey)
         or HasWebView2Version(HKLM, 'SOFTWARE\' + WebView2ClientKey)
         or HasWebView2Version(HKCU, 'Software\' + WebView2ClientKey);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrorCode: Integer;
begin
  if (CurStep = ssPostInstall) and (not WizardSilent) and (not IsWebView2Installed()) then
  begin
    if MsgBox(ExpandConstant('{cm:WebView2Missing}'), mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', WebView2DownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;
