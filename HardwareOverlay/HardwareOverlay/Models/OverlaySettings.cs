using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HardwareOverlay.Models;

public partial class OverlaySettings : ObservableObject
{
    [ObservableProperty] private double _backgroundOpacity = 0.5;
    [ObservableProperty] private string _backgroundColor = "#FF000000";
    [ObservableProperty] private double _textOpacity = 1.0;
    [ObservableProperty] private bool _autoSize = true;
    [ObservableProperty] private double _width = 220;
    [ObservableProperty] private double _height = 200;
    [ObservableProperty] private ScreenCorner _corner = ScreenCorner.TopRight;
    [ObservableProperty] private double _marginX = 10;
    [ObservableProperty] private double _marginY = 10;
    [ObservableProperty] private int _updateIntervalMs = 1000;
    [ObservableProperty] private AppLanguage _language = AppLanguage.Ukrainian;
    [ObservableProperty] private bool _startWithWindows;

    // Collection order = line order in the overlay.
    public ObservableCollection<MetricSettings> Metrics { get; set; } = new();

    // Custom drive names: HardwareId -> label used in the overlay and combo box.
    public Dictionary<string, string> DiskAliases { get; set; } = new();

    // Sets or clears a drive alias; fires PropertyChanged so autosave picks it up.
    public void SetDiskAlias(string hardwareId, string? alias)
    {
        if (string.IsNullOrEmpty(hardwareId))
            return;

        if (string.IsNullOrWhiteSpace(alias))
            DiskAliases.Remove(hardwareId);
        else
            DiskAliases[hardwareId] = alias.Trim();

        OnPropertyChanged(nameof(DiskAliases));
    }

    public static OverlaySettings CreateDefault()
    {
        var settings = new OverlaySettings();
        settings.Normalize();
        return settings;
    }

    // Clamps out-of-range values and adds metrics missing from older JSON.
    public void Normalize()
    {
        BackgroundOpacity = Math.Clamp(BackgroundOpacity, 0, 1);
        TextOpacity = Math.Clamp(TextOpacity, 0, 1);
        Width = Math.Clamp(Width, 50, 2000);
        Height = Math.Clamp(Height, 30, 2000);
        MarginX = Math.Clamp(MarginX, 0, 1000);
        MarginY = Math.Clamp(MarginY, 0, 1000);
        UpdateIntervalMs = Math.Clamp(UpdateIntervalMs, 250, 10000);
        if (!Enum.IsDefined(Language))
            Language = AppLanguage.Ukrainian;

        var seen = new HashSet<MetricType>();
        for (int i = Metrics.Count - 1; i >= 0; i--)
        {
            if (Metrics[i] is null || !seen.Add(Metrics[i].Type))
                Metrics.RemoveAt(i);
        }

        foreach (var metric in Metrics)
            metric.FontSize = Math.Clamp(metric.FontSize, 6, 72);

        foreach (var type in Enum.GetValues<MetricType>())
        {
            if (!seen.Contains(type))
                Metrics.Add(MetricInfo.CreateDefault(type));
        }
    }
}
