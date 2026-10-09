using CommunityToolkit.Mvvm.ComponentModel;
using HardwareOverlay.Models;

namespace HardwareOverlay.ViewModels;

public sealed class OverlayLineViewModel : ObservableObject
{
    private float? _value;

    public OverlayLineViewModel(MetricSettings settings, string? hardwareId, bool showHardwareName)
    {
        Settings = settings;
        HardwareId = hardwareId;
        ShowHardwareName = showHardwareName;
    }

    public MetricSettings Settings { get; }
    public string? HardwareId { get; }
    public bool ShowHardwareName { get; }
    public string? HardwareName { get; private set; }

    public string Text => BuildText();

    public void Update(float? value, string? hardwareName)
    {
        _value = value;
        HardwareName = hardwareName ?? HardwareName;
        OnPropertyChanged(nameof(Text));
    }

    public void Refresh() => OnPropertyChanged(nameof(Text));

    private string BuildText()
    {
        var label = Settings.Prefix?.Trim() ?? string.Empty;

        // Priority: CustomName > hardware name (already resolves the disk alias).
        var name = !string.IsNullOrWhiteSpace(Settings.CustomName)
            ? Settings.CustomName.Trim()
            : ShowHardwareName ? HardwareName : null;

        if (!string.IsNullOrEmpty(name))
            label = string.IsNullOrEmpty(label) ? $"{name}:" : $"{label} {name}";

        var value = FormatValue(_value, Settings.Unit);
        return string.IsNullOrEmpty(label) ? value : $"{label} {value}";
    }

    private static string FormatValue(float? value, string unit)
    {
        if (value is null)
            return "N/A";

        return unit == "MB" ? $"{value.Value:F0} MB" : $"{value.Value:F0}{unit}";
    }
}
