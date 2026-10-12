using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HardwareOverlay.Models;

public partial class MetricSettings : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName), nameof(NameKey), nameof(Unit), nameof(SupportsHardwareSelection), nameof(SupportsSensorSelection), nameof(SupportsTotal))]
    private MetricType _type;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _prefix = string.Empty;

    // User label: replaces the metric name in the overlay and settings list.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _customName = string.Empty;

    [ObservableProperty] private string _color = "#FFFFFFFF";
    [ObservableProperty] private double _fontSize = 14;

    // For disks: the drive's Identifier from LibreHardwareMonitor. null = all drives.
    // For motherboard temperature: the chosen sensor's Identifier. null = auto-detect.
    [ObservableProperty] private string? _hardwareId;

    [ObservableProperty] private bool _showTotal;
    [ObservableProperty] private bool _showInGb;

    // Metric name localization key, used by LocConverter in XAML.
    [JsonIgnore] public string NameKey => MetricInfo.GetNameKey(Type);

    // Resolved name for code use. CustomName wins over the localized NameKey.
    [JsonIgnore] public string DisplayName =>
        string.IsNullOrWhiteSpace(CustomName) ? Localization.Loc.Instance.Resolve(NameKey) : CustomName;
    [JsonIgnore] public string Unit => MetricInfo.GetUnit(Type);
    [JsonIgnore] public bool SupportsHardwareSelection => MetricInfo.SupportsHardwareSelection(Type);
    [JsonIgnore] public bool SupportsSensorSelection => MetricInfo.SupportsSensorSelection(Type);
    [JsonIgnore] public bool SupportsTotal => MetricInfo.SupportsTotal(Type);
}
