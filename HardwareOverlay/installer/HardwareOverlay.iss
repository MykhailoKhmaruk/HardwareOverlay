; Inno Setup script — Hardware Overlay installer
; Compile: ISCC.exe HardwareOverlay.iss

#define AppName    "Hardware Overlay"
#define AppVersion "1.0.0"
#define AppExe     "HardwareOverlay.exe"

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

[Icons]
Name: "{group}\{#AppName}";    Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

; Launch after install (the exe elevates itself via the manifest)
[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: postinstall nowait skipifsilent

; Drop the autostart task on uninstall
[UninstallRun]
Filename: "schtasks.exe"; Parameters: "/delete /tn ""HardwareOverlay_Autostart"" /f"; Flags: runhidden; RunOnceId: "RemoveAutostart"
