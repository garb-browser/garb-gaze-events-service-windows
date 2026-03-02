; GARB Eye Tracking Service - Inno Setup Installer Script
; Installs the GARB Gaze Events Service for Windows
; Requires: .NET Framework 4.8 (pre-installed on Windows 10/11)
;           Tobii Eye Tracker 5 (or compatible) with Tobii Experience software

#define MyAppName "GARB Eye Tracking Service"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "GARB Browser"
#define MyAppURL "https://github.com/garb-browser"
#define MyAppExeName "Interaction_Interactors_101.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\GARB Eye Tracking Service
DefaultGroupName=GARB
DisableProgramGroupPage=yes
OutputBaseFilename=GARB-Eye-Tracking-Service-Setup
OutputDir=output
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
; Minimum Windows 10
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start GARB Eye Tracking Service automatically on login"; GroupDescription: "Startup:"

[Files]
; Main application files from publish/ directory
Source: "{#SourcePath}\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Add to startup if selected
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "GARBEyeTracker"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
; Launch after install
Filename: "{app}\{#MyAppExeName}"; Description: "Launch GARB Eye Tracking Service"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill the process before uninstalling
Filename: "taskkill"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden

[Code]
// Check for .NET Framework 4.8
function IsDotNetInstalled(): Boolean;
var
  Release: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) then
  begin
    // 4.8 = 528040 or higher
    Result := (Release >= 528040);
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  
  if not IsDotNetInstalled() then
  begin
    MsgBox('.NET Framework 4.8 or later is required.' + #13#10 + #13#10 +
           'Please install .NET Framework 4.8 from:' + #13#10 +
           'https://dotnet.microsoft.com/download/dotnet-framework/net48' + #13#10 + #13#10 +
           'Note: Windows 10 (May 2019 Update) and Windows 11 include .NET 4.8 by default.',
           mbError, MB_OK);
    Result := False;
  end;
end;

// Kill existing instance before install
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    Exec('taskkill', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
