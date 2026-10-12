namespace HardwareOverlay.Models;

public static class MetricInfo
{
    private sealed record Meta(string NameKey, string DefaultPrefix, string Unit, string DefaultColor, bool DefaultEnabled);

    private const string CpuColor  = "#FF4FC3F7";
    private const string GpuColor  = "#FF81C784";
    private const string RamColor  = "#FFFFB74D";
    private const string MbColor   = "#FFE57373";
    private const string DiskColor = "#FFBA68C8";

    private static readonly Dictionary<MetricType, Meta> Map = new()
    {
        [MetricType.CpuTemperature]         = new("Metric_CpuTemp",  "CPU Temp:",  "°C", CpuColor,  true),
        [MetricType.CpuLoad]                = new("Metric_CpuLoad",  "CPU Load:",  "%",  CpuColor,  true),
        [MetricType.GpuTemperature]         = new("Metric_GpuTemp",  "GPU Temp:",  "°C", GpuColor,  true),
        [MetricType.GpuMemoryUsed]          = new("Metric_GpuVram",  "VRAM:",      "MB", GpuColor,  false),
        [MetricType.GpuLoad]                = new("Metric_GpuLoad",  "GPU Load:",  "%",  GpuColor,  true),
        [MetricType.RamUsed]                = new("Metric_RamUsed",  "RAM:",       "MB", RamColor,  true),
        [MetricType.RamLoad]                = new("Metric_RamLoad",  "RAM Load:",  "%",  RamColor,  false),
        [MetricType.RamTemperature]         = new("Metric_RamTemp",  "RAM Temp:",  "°C", RamColor,  false),
        [MetricType.MotherboardTemperature] = new("Metric_MbTemp",   "MB Temp:",   "°C", MbColor,   false),
        [MetricType.DiskTemperature]        = new("Metric_DiskTemp", "Disk Temp:", "°C", DiskColor, false),
        [MetricType.DiskLoad]               = new("Metric_DiskLoad", "Disk Load:", "%",  DiskColor, false),
    };

    public static string GetNameKey(MetricType type) => Map[type].NameKey;
    public static string GetDefaultPrefix(MetricType type) => Map[type].DefaultPrefix;
    public static string GetUnit(MetricType type) => Map[type].Unit;

    public static bool SupportsHardwareSelection(MetricType type) =>
        type is MetricType.DiskTemperature or MetricType.DiskLoad;

    // Single-sensor pick: the board exposes several temperature sensors
    // (e.g. Super I/O "Temperature #1"/"#2") and the user chooses one.
    public static bool SupportsSensorSelection(MetricType type) =>
        type is MetricType.MotherboardTemperature;

    public static bool SupportsTotal(MetricType type) =>
        type is MetricType.RamUsed or MetricType.GpuMemoryUsed;

    public static MetricSettings CreateDefault(MetricType type)
    {
        var meta = Map[type];
        return new MetricSettings
        {
            Type = type,
            IsEnabled = meta.DefaultEnabled,
            Prefix = meta.DefaultPrefix,
            Color = meta.DefaultColor,
            FontSize = 14
        };
    }
}
