# Hardware Overlay

A lightweight always-on-top hardware monitor for Windows. Shows CPU/GPU temps,
load, RAM and disk stats in a small customizable overlay that sits in the corner
of your screen — useful while gaming or stress-testing.

Built with WPF on .NET 8, sensors via [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor).

<!-- Add a screenshot: docs/screenshot.png -->

## Features

- **Metrics** — CPU temp/load, GPU temp/load/VRAM, RAM usage/temp, motherboard temp, disk temp/activity.
- **Per-metric styling** — prefix, custom name, color, font size; enable/disable and reorder lines.
- **Disk renaming** — give drives readable names instead of raw hardware strings.
- **Multi-disk mode** — show all drives at once or pin a single one.
- **Position & look** — snap to any screen corner, X/Y margins, background color/opacity, text opacity, auto or fixed size.
- **6 languages** — English, Українська, Deutsch, Português, Español, 中文.
- **Tray app** — lives in the system tray, optional "run at Windows startup" (elevated, no UAC prompt).
- **Settings autosave** — `%APPDATA%\HardwareOverlay\settings.json`.

## Install

Download **`HardwareOverlay-Setup-x.y.z.exe`** from the [Releases](../../releases) page and run it.

- Windows 10/11 x64. Admin rights are required — hardware sensors need driver access.
- The app is unsigned, so SmartScreen may warn on first run: **More info → Run anyway**.
- Uninstall removes the app and its autostart task.

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) and, for the
installer, [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
# Single-file portable exe -> HardwareOverlay\publish\
dotnet publish .\HardwareOverlay\HardwareOverlay -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true `
  -o .\HardwareOverlay\HardwareOverlay\publish

# Installer -> installer\output\
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\installer\HardwareOverlay.iss
```

To sign the installer, drop a `.pfx` in `installer\certs\` and uncomment the
`SignTool` lines in `HardwareOverlay.iss`.

## Tech

.NET 8 · WPF · CommunityToolkit.Mvvm · LibreHardwareMonitorLib · H.NotifyIcon

## License

MIT — see [LICENSE](LICENSE).
