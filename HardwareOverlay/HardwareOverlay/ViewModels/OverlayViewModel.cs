using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;
using HardwareOverlay.Models;
using HardwareOverlay.Services;

namespace HardwareOverlay.ViewModels;

public sealed class OverlayViewModel : IDisposable
{
    private readonly HardwareMonitorService _monitor;
    private readonly Dispatcher _dispatcher;

    public OverlaySettings Settings { get; }
    public ObservableCollection<OverlayLineViewModel> Lines { get; } = new();

    public OverlayViewModel(OverlaySettings settings, HardwareMonitorService monitor, Dispatcher dispatcher)
    {
        Settings = settings;
        _monitor = monitor;
        _dispatcher = dispatcher;

        Settings.PropertyChanged += OnSettingsPropertyChanged;
        Settings.Metrics.CollectionChanged += OnMetricsCollectionChanged;
        foreach (var metric in Settings.Metrics)
            metric.PropertyChanged += OnMetricPropertyChanged;

        _monitor.ReadingsUpdated += OnReadingsUpdated;
        _monitor.SetInterval(Settings.UpdateIntervalMs);
        PushRequests();
    }

    private void PushRequests() => _monitor.SetRequests(HardwareMonitorService.BuildRequests(Settings));

    private void OnReadingsUpdated(IReadOnlyList<MetricReading> readings) =>
        _dispatcher.InvokeAsync(() => ApplyReadings(readings), DispatcherPriority.Background);

    private void ApplyReadings(IReadOnlyList<MetricReading> readings)
    {
        var visible = new List<(MetricSettings Settings, MetricReading Reading)>(readings.Count);

        foreach (var reading in readings)
        {
            var metric = Settings.Metrics.FirstOrDefault(m => m.Type == reading.Type);
            if (metric is { IsEnabled: true })
                visible.Add((metric, reading));
        }

        bool sameLayout = visible.Count == Lines.Count;
        for (int i = 0; sameLayout && i < visible.Count; i++)
        {
            sameLayout = ReferenceEquals(Lines[i].Settings, visible[i].Settings)
                         && Lines[i].HardwareId == visible[i].Reading.HardwareId;
        }

        if (!sameLayout)
        {
            Lines.Clear();
            foreach (var (metric, reading) in visible)
            {
                // Name is shown for "all disks" mode, or for a single drive that
                // has a user alias.
                bool showName = metric.SupportsHardwareSelection
                                && (string.IsNullOrEmpty(metric.HardwareId)
                                    || Settings.DiskAliases.ContainsKey(metric.HardwareId!));
                Lines.Add(new OverlayLineViewModel(metric, reading.HardwareId, showName));
            }
        }

        for (int i = 0; i < visible.Count; i++)
            Lines[i].Update(visible[i].Reading.Value, ResolveName(visible[i].Reading), visible[i].Reading.Total);
    }

    // Disk alias wins over the name reported by LibreHardwareMonitor.
    private string? ResolveName(MetricReading reading) =>
        reading.HardwareId is not null
        && Settings.DiskAliases.TryGetValue(reading.HardwareId, out var alias)
            ? alias
            : reading.HardwareName;

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(OverlaySettings.UpdateIntervalMs):
                _monitor.SetInterval(Settings.UpdateIntervalMs);
                break;

            // Disk alias changed — rebuild the lines.
            case nameof(OverlaySettings.DiskAliases):
                Lines.Clear();
                PushRequests();
                break;
        }
    }

    private void OnMetricPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MetricSettings metric)
            return;

        switch (e.PropertyName)
        {
            case nameof(MetricSettings.IsEnabled):
            case nameof(MetricSettings.HardwareId):
                RemoveLines(metric);
                PushRequests();
                break;

            case nameof(MetricSettings.Prefix):
            case nameof(MetricSettings.CustomName):
            case nameof(MetricSettings.ShowTotal):
            case nameof(MetricSettings.ShowInGb):
                foreach (var line in Lines.Where(l => ReferenceEquals(l.Settings, metric)))
                    line.Refresh();
                break;
        }
    }

    private void OnMetricsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (MetricSettings metric in e.OldItems)
                metric.PropertyChanged -= OnMetricPropertyChanged;

        if (e.NewItems is not null)
            foreach (MetricSettings metric in e.NewItems)
                metric.PropertyChanged += OnMetricPropertyChanged;

        PushRequests();
    }

    private void RemoveLines(MetricSettings metric)
    {
        for (int i = Lines.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(Lines[i].Settings, metric))
                Lines.RemoveAt(i);
        }
    }

    public void Dispose()
    {
        _monitor.ReadingsUpdated -= OnReadingsUpdated;
        Settings.PropertyChanged -= OnSettingsPropertyChanged;
        Settings.Metrics.CollectionChanged -= OnMetricsCollectionChanged;
        foreach (var metric in Settings.Metrics)
            metric.PropertyChanged -= OnMetricPropertyChanged;
    }
}
