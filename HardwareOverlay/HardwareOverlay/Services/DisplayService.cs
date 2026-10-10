using HardwareOverlay.Interop;
using HardwareOverlay.Models;

namespace HardwareOverlay.Services;

// Coordinates are in physical pixels.
public sealed record DisplayInfo(
    string Id,
    int Number,
    bool IsPrimary,
    int Width,
    int Height,
    int WorkLeft,
    int WorkTop,
    int WorkRight,
    int WorkBottom,
    uint Dpi)
{
    public double Scale => Dpi / 96.0;
}

public static class DisplayService
{
    public static IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var monitors = NativeMethods.EnumerateMonitors();
        var result = new List<DisplayInfo>(monitors.Count);

        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            result.Add(new DisplayInfo(
                m.DeviceName,
                ParseNumber(m.DeviceName) ?? i + 1,
                m.IsPrimary,
                m.Bounds.Right - m.Bounds.Left,
                m.Bounds.Bottom - m.Bounds.Top,
                m.WorkArea.Left,
                m.WorkArea.Top,
                m.WorkArea.Right,
                m.WorkArea.Bottom,
                m.Dpi));
        }

        return result.OrderBy(d => d.Number).ToList();
    }

    // Displays the overlay should be shown on. Never empty while at least one
    // display exists: falls back to the primary one.
    public static IReadOnlyList<DisplayInfo> ResolveTargets(OverlaySettings settings)
    {
        var displays = GetDisplays();
        if (settings.ShowOnAllMonitors)
            return displays;

        var selected = displays.Where(d => settings.MonitorIds.Contains(d.Id)).ToList();
        if (selected.Count > 0)
            return selected;

        var primary = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
        return primary is null ? Array.Empty<DisplayInfo>() : new[] { primary };
    }

    // "\\.\DISPLAY3" -> 3
    private static int? ParseNumber(string deviceName)
    {
        var digits = new string(deviceName.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var n) ? n : null;
    }
}
