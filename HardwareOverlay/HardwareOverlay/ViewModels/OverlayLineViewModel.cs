using CommunityToolkit.Mvvm.ComponentModel;
using HardwareOverlay.Models;

namespace HardwareOverlay.ViewModels;

public sealed class OverlayLineViewModel : ObservableObject
{
    private float? _value;
    private float? _total;

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

    public void Update(float? value, string? hardwareName, float? total = null)
    {
        _value = value;
        _total = total ?? _total;
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

        var value = Settings.SupportsTotal ? FormatMemory() : FormatValue(_value, Settings.Unit);
        return string.IsNullOrEmpty(label) ? value : $"{label} {value}";
    }

    private string FormatMemory()
    {
        if (_value is null)
            return "N/A";

        bool gb = Settings.ShowInGb;
        string Fmt(float mb) => gb ? $"{mb / 1024f:F1}" : $"{mb:F0}";
        var unit = gb ? "GB" : "MB";

        return Settings.ShowTotal && _total is not null
            ? $"{Fmt(_value.Value)} / {Fmt(_total.Value)} {unit}"
            : $"{Fmt(_value.Value)} {unit}";
    }

    private static string FormatValue(float? value, string unit)
    {
        if (value is null)
            return "N/A";

        return unit == "MB" ? $"{value.Value:F0} MB" : $"{value.Value:F0}{unit}";
    }
}
