using System.Diagnostics;
using System.Text;
using HardwareOverlay.Models;
using LibreHardwareMonitor.Hardware;

namespace HardwareOverlay.Services;

public sealed record MetricRequest(MetricType Type, string? HardwareId);

public sealed record MetricReading(MetricType Type, string? HardwareId, string? HardwareName, float? Value);

public sealed record DiskInfo(string Id, string Name);

public sealed class HardwareMonitorService : IDisposable
{
    private enum Category { Cpu, Gpu, Memory, Motherboard, Storage }

    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true,
        IsStorageEnabled = true
    };

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private volatile MetricRequest[] _requests = Array.Empty<MetricRequest>();
    private volatile int _intervalMs = 1000;
    private volatile IReadOnlyList<DiskInfo> _disks = Array.Empty<DiskInfo>();
    private volatile bool _isInitialized;

    public IReadOnlyList<DiskInfo> Disks => _disks;
    public bool IsInitialized => _isInitialized;

    // Both events fire on a background thread; marshal to the UI via Dispatcher.InvokeAsync.
    public event Action? Initialized;
    public event Action<IReadOnlyList<MetricReading>>? ReadingsUpdated;

    public void Start()
    {
        if (_loopTask is not null)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loopTask = Task.Run(() => RunAsync(token), token);
    }

    public void SetRequests(IEnumerable<MetricRequest> requests) => _requests = requests.ToArray();

    public void SetInterval(int intervalMs) => _intervalMs = Math.Max(100, intervalMs);

    public static MetricRequest[] BuildRequests(OverlaySettings settings) =>
        settings.Metrics
            .Where(m => m.IsEnabled)
            .Select(m => new MetricRequest(m.Type, m.SupportsHardwareSelection && !string.IsNullOrEmpty(m.HardwareId) ? m.HardwareId : null))
            .ToArray();

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            _computer.Open();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HardwareMonitor] Failed to open Computer: {ex}");
            return;
        }

        _disks = GetHardware(HardwareType.Storage)
            .Select(h => new DiskInfo(h.Identifier.ToString(), h.Name))
            .ToList();

#if DEBUG
        foreach (var hw in _computer.Hardware)
            UpdateRecursive(hw);
        Debug.WriteLine(DumpSensors());
#endif

        _isInitialized = true;
        Initialized?.Invoke();

        while (!ct.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var readings = Poll(_requests);
                ReadingsUpdated?.Invoke(readings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareMonitor] Poll failed: {ex}");
            }

            var delay = Math.Max(50, _intervalMs - (int)sw.ElapsedMilliseconds);

            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private List<MetricReading> Poll(MetricRequest[] requests)
    {
        var result = new List<MetricReading>(requests.Length);
        if (requests.Length == 0)
            return result;

        UpdateHardware(requests);

        foreach (var request in requests)
        {
            if (MetricInfo.SupportsHardwareSelection(request.Type))
                ReadDisks(request, result);
            else
                result.Add(new MetricReading(request.Type, null, null, ReadSingle(request.Type)));
        }

        return result;
    }

    private void ReadDisks(MetricRequest request, List<MetricReading> result)
    {
        bool found = false;

        foreach (var disk in GetHardware(HardwareType.Storage))
        {
            var id = disk.Identifier.ToString();
            if (request.HardwareId is not null && request.HardwareId != id)
                continue;

            found = true;
            var value = request.Type == MetricType.DiskTemperature
                ? (FindSensor(disk, SensorType.Temperature, "Temperature") ?? FirstSensor(disk, SensorType.Temperature))?.Value
                : FindSensor(disk, SensorType.Load, "Total Activity")?.Value;

            result.Add(new MetricReading(request.Type, id, disk.Name, value));
        }

        // Selected drive is gone (USB unplugged etc.) — report N/A instead of hiding the line.
        if (!found)
            result.Add(new MetricReading(request.Type, request.HardwareId, null, null));
    }

    private float? ReadSingle(MetricType type)
    {
        switch (type)
        {
            case MetricType.CpuTemperature:
            {
                var cpu = PrimaryCpu;
                return (FindSensor(cpu, SensorType.Temperature,
                            "CPU Package", "Core (Tctl/Tdie)", "Core (Tctl)", "Core (Tdie)", "Core Average", "Core Max")
                        ?? FirstSensor(cpu, SensorType.Temperature))?.Value;
            }

            case MetricType.CpuLoad:
                return FindSensor(PrimaryCpu, SensorType.Load, "CPU Total")?.Value;

            case MetricType.GpuTemperature:
            {
                var gpu = PrimaryGpu;
                return (FindSensor(gpu, SensorType.Temperature, "GPU Core")
                        ?? FirstSensor(gpu, SensorType.Temperature))?.Value;
            }

            case MetricType.GpuLoad:
                return FindSensor(PrimaryGpu, SensorType.Load, "GPU Core", "D3D 3D")?.Value;

            case MetricType.GpuMemoryUsed:
                return FindSensor(PrimaryGpu, SensorType.SmallData, "GPU Memory Used", "D3D Dedicated Memory Used")?.Value;

            case MetricType.RamUsed:
                return FindSensor(MainMemory, SensorType.Data, "Memory Used")?.Value * 1024f;

            case MetricType.RamLoad:
                return FindSensor(MainMemory, SensorType.Load, "Memory")?.Value;

            case MetricType.RamTemperature:
            {
                // Sensors under the Memory hardware node.
                var direct = GetHardware(HardwareType.Memory)
                    .SelectMany(AllSensors)
                    .Where(s => s.SensorType == SensorType.Temperature && s.Value > 0)
                    .Select(s => s.Value)
                    .Max();

                if (direct is not null)
                    return direct;

                // Fallback: some boards expose DIMM temp under Motherboard/SuperIO.
                return _computer.Hardware
                    .SelectMany(AllSensors)
                    .Where(s => s.SensorType == SensorType.Temperature
                                && s.Value > 0
                                && (s.Name.Contains("DIMM", StringComparison.OrdinalIgnoreCase)
                                    || s.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase)
                                    || s.Name.Contains("DRAM", StringComparison.OrdinalIgnoreCase)
                                    || s.Name.Contains("SPD", StringComparison.OrdinalIgnoreCase)))
                    .Select(s => s.Value)
                    .Max();
            }

            case MetricType.MotherboardTemperature:
            {
                var mb = GetHardware(HardwareType.Motherboard).FirstOrDefault();
                return (FindSensor(mb, SensorType.Temperature, "System", "Motherboard", "Mainboard")
                        ?? AllSensors(mb).FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Value > 0))?.Value;
            }

            default:
                return null;
        }
    }

    private IHardware? PrimaryCpu => GetHardware(HardwareType.Cpu).FirstOrDefault();

    // Discrete GPUs take priority over Intel iGPUs.
    private IHardware? PrimaryGpu =>
        GetHardware(HardwareType.GpuNvidia)
            .Concat(GetHardware(HardwareType.GpuAmd))
            .Concat(GetHardware(HardwareType.GpuIntel))
            .FirstOrDefault();

    // There are several Memory devices (Total/Virtual Memory); take the physical one.
    private IHardware? MainMemory =>
        GetHardware(HardwareType.Memory).FirstOrDefault(h => h.Sensors.Any(s => s.Name == "Memory Used"))
        ?? GetHardware(HardwareType.Memory).FirstOrDefault();

    private void UpdateHardware(MetricRequest[] requests)
    {
        var needed = requests.Select(r => GetCategory(r.Type)).ToHashSet();

        // RAM temp may live under Motherboard — update it together with Memory.
        if (needed.Contains(Category.Memory))
            needed.Add(Category.Motherboard);

        foreach (var hardware in _computer.Hardware)
        {
            if (ToCategory(hardware.HardwareType) is { } category && needed.Contains(category))
                UpdateRecursive(hardware);
        }
    }

    private static void UpdateRecursive(IHardware hardware)
    {
        hardware.Update();
        foreach (var sub in hardware.SubHardware)
            UpdateRecursive(sub);
    }

    private IEnumerable<IHardware> GetHardware(HardwareType type) =>
        _computer.Hardware.Where(h => h.HardwareType == type);

    private static IEnumerable<ISensor> AllSensors(IHardware? hardware) =>
        hardware is null
            ? Enumerable.Empty<ISensor>()
            : hardware.Sensors.Concat(hardware.SubHardware.SelectMany(AllSensors));

    private static ISensor? FindSensor(IHardware? hardware, SensorType type, params string[] names)
    {
        var sensors = AllSensors(hardware)
            .Where(s => s.SensorType == type && s.Value.HasValue)
            .ToList();

        foreach (var name in names)
        {
            var match = sensors.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return null;
    }

    private static ISensor? FirstSensor(IHardware? hardware, SensorType type) =>
        AllSensors(hardware).FirstOrDefault(s => s.SensorType == type && s.Value.HasValue);

    private static Category GetCategory(MetricType type) => type switch
    {
        MetricType.CpuTemperature or MetricType.CpuLoad => Category.Cpu,
        MetricType.GpuTemperature or MetricType.GpuLoad or MetricType.GpuMemoryUsed => Category.Gpu,
        MetricType.RamUsed or MetricType.RamLoad or MetricType.RamTemperature => Category.Memory,
        MetricType.MotherboardTemperature => Category.Motherboard,
        MetricType.DiskTemperature or MetricType.DiskLoad => Category.Storage,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private static Category? ToCategory(HardwareType type) => type switch
    {
        HardwareType.Cpu => Category.Cpu,
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => Category.Gpu,
        HardwareType.Memory => Category.Memory,
        HardwareType.Motherboard => Category.Motherboard,
        HardwareType.Storage => Category.Storage,
        _ => null
    };

    private string DumpSensors()
    {
        var sb = new StringBuilder("===== LibreHardwareMonitor sensors =====\n");

        void Dump(IHardware hw, int depth)
        {
            var indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}[{hw.HardwareType}] {hw.Name}  ({hw.Identifier})");
            foreach (var s in hw.Sensors)
                sb.AppendLine($"{indent}  - {s.SensorType,-12} {s.Name,-35} = {s.Value}");
            foreach (var sub in hw.SubHardware)
                Dump(sub, depth + 1);
        }

        foreach (var hw in _computer.Hardware)
            Dump(hw, 0);

        return sb.ToString();
    }

    public void Dispose()
    {
        _cts?.Cancel();

        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
        }

        _computer.Close();
        _cts?.Dispose();
    }
}
