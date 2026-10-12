; Inno Setup script — Hardware Overlay installer
; Compile: ISCC.exe HardwareOverlay.iss

#define AppName    "Hardware Overlay"
#define AppVersion "1.0.1"
#define AppExe     "HardwareOverlay.exe"
#define PawnIOMinVersion "2.2.0"

[Setup]
AppId={{5E8C4F2A-3B7D-4E1F-9A6C-2D8B0F1E4A7C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={autopf}\HardwareOverlay
DefaultGroupName={#AppName}
OutputDir=output
OutputBaseFilename=HardwareOverlay-Setup
PrivilegesRequired=admin
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Kill the running overlay before install/upgrade
CloseApplications=force
UninstallDisplayIcon={app}\{#AppExe}

; ==================== CODE SIGNING ====================
; Uncomment once signtool.exe (Windows SDK) and a .pfx certificate are in place.
; $f expands to the file being signed (setup exe and the uninstaller).
; SignTool=codesign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f "certs\codesign.pfx" /p YOUR_PFX_PASSWORD $f
; SignedUninstaller=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "..\HardwareOverlay\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
; PawnIO driver (required by LibreHardwareMonitor 0.9.5+): https://github.com/namazso/PawnIO.Setup/releases
Source: "redist\PawnIO_setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: PawnIONeedsInstall

[Icons]
Name: "{group}\{#AppName}";    Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

; Launch after install. shellexec is required — CreateProcess can't elevate,
; and the exe demands admin rights via its manifest (error 740 without this).
[Run]
Filename: "{tmp}\PawnIO_setup.exe"; Parameters: "-install -silent"; StatusMsg: "Installing PawnIO driver..."; Flags: waituntilterminated; Check: PawnIONeedsInstall
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: postinstall nowait skipifsilent shellexec

; Drop the autostart task on uninstall
[UninstallRun]
Filename: "schtasks.exe"; Parameters: "/delete /tn ""HardwareOverlay_Autostart"" /f"; Flags: runhidden; RunOnceId: "RemoveAutostart"

[Code]
const
  PawnIOKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO';

function NextVersionPart(var S: String): Integer;
var
  P: Integer;
begin
  P := Pos('.', S);
  if P = 0 then
  begin
    Result := StrToIntDef(S, 0);
    S := '';
  end
  else
  begin
    Result := StrToIntDef(Copy(S, 1, P - 1), 0);
    Delete(S, 1, P);
  end;
end;

function CompareVersions(A, B: String): Integer;
var
  I, X, Y: Integer;
begin
  Result := 0;
  for I := 1 to 4 do
  begin
    X := NextVersionPart(A);
    Y := NextVersionPart(B);
    if X <> Y then
    begin
      if X > Y then Result := 1 else Result := -1;
      Exit;
    end;
  end;
end;

function PawnIONeedsInstall: Boolean;
var
  Version: String;
begin
  if not ((IsWin64 and RegQueryStringValue(HKLM64, PawnIOKey, 'DisplayVersion', Version))
          or RegQueryStringValue(HKLM32, PawnIOKey, 'DisplayVersion', Version)) then
    Version := '';
  Result := (Version = '') or (CompareVersions(Version, '{#PawnIOMinVersion}') < 0);
end;
