using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwareOverlay.Localization;
using HardwareOverlay.Models;
using HardwareOverlay.Services;

namespace HardwareOverlay.ViewModels;

// NameKey is a Loc key for the localized corner name.
public sealed record CornerOption(ScreenCorner Value, string NameKey);

// Name is the native language name, shown as-is.
public sealed record LanguageOption(AppLanguage Value, string Name);

public sealed partial class DiskOption : ObservableObject
{
    public DiskOption(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    // Pseudo entries like "All disks"/"Not found" can't be renamed.
    public bool IsEditable => Id.Length > 0 && NameKey is null;

    // ComboBox/overlay label: alias if set, else the hardware name.
    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? Name : Alias;

    // When set, the combo shows the translation of this key and Name as suffix.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName), nameof(IsEditable))]
    private string? _nameKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _name = string.Empty;

    // User's drive name. Persisted to Settings.DiskAliases via the VM.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _alias = string.Empty;
}

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsManager _settingsManager;
    private readonly HardwareMonitorService _monitor;
    private readonly Dispatcher _dispatcher;

    public SettingsViewModel(SettingsManager settingsManager, HardwareMonitorService monitor, Dispatcher dispatcher)
    {
        _settingsManager = settingsManager;
        _monitor = monitor;
        _dispatcher = dispatcher;

        DiskOptions.Add(new DiskOption(string.Empty, string.Empty) { NameKey = "Disk_All" });

        // A saved drive that's disconnected gets a placeholder item so the
        // combo box doesn't lose the selection.
        foreach (var metric in Settings.Metrics.Where(m => m.SupportsHardwareSelection && !string.IsNullOrEmpty(m.HardwareId)))
        {
            if (DiskOptions.All(d => d.Id != metric.HardwareId))
                AddDiskOption(new DiskOption(metric.HardwareId!, $"({metric.HardwareId})") { NameKey = "Disk_NotFound" });
        }

        _monitor.Initialized += OnMonitorInitialized;
        if (_monitor.IsInitialized)
            SyncDisks();
    }

    public OverlaySettings Settings => _settingsManager.Settings;

    public ObservableCollection<DiskOption> DiskOptions { get; } = new();

    public IReadOnlyList<CornerOption> CornerOptions { get; } = new[]
    {
        new CornerOption(ScreenCorner.TopLeft, "Corner_TopLeft"),
        new CornerOption(ScreenCorner.TopRight, "Corner_TopRight"),
        new CornerOption(ScreenCorner.BottomLeft, "Corner_BottomLeft"),
        new CornerOption(ScreenCorner.BottomRight, "Corner_BottomRight")
    };

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } = new[]
    {
        new LanguageOption(AppLanguage.English, "English"),
        new LanguageOption(AppLanguage.Ukrainian, "Українська"),
        new LanguageOption(AppLanguage.German, "Deutsch"),
        new LanguageOption(AppLanguage.Portuguese, "Português"),
        new LanguageOption(AppLanguage.Spanish, "Español"),
        new LanguageOption(AppLanguage.Chinese, "中文")
    };

    public IReadOnlyList<string> PaletteColors { get; } = new[]
    {
        "#FFFFFFFF", "#FF4FC3F7", "#FF81C784", "#FFFFB74D",
        "#FFE57373", "#FFBA68C8", "#FFFFF176", "#FF90A4AE"
    };

    private void OnMonitorInitialized() => _dispatcher.InvokeAsync(SyncDisks);

    private void AddDiskOption(DiskOption option)
    {
        if (Settings.DiskAliases.TryGetValue(option.Id, out var alias))
            option.Alias = alias;

        option.PropertyChanged += OnDiskOptionChanged;
        DiskOptions.Add(option);
    }

    // Alias edit in the UI -> write to settings (autosave via PropertyChanged).
    private void OnDiskOptionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is DiskOption option && e.PropertyName == nameof(DiskOption.Alias))
            Settings.SetDiskAlias(option.Id, option.Alias);
    }

    // Only adds/updates items — removing them would reset the combo's SelectedValue.
    private void SyncDisks()
    {
        foreach (var disk in _monitor.Disks)
        {
            var existing = DiskOptions.FirstOrDefault(d => d.Id == disk.Id);
            if (existing is null)
            {
                AddDiskOption(new DiskOption(disk.Id, disk.Name));
            }
            else
            {
                // A "Not found" placeholder becomes a normal entry again.
                existing.Name = disk.Name;
                existing.NameKey = null;
            }
        }
    }

    [RelayCommand]
    private void MoveUp(MetricSettings? metric)
    {
        if (metric is null) return;
        var index = Settings.Metrics.IndexOf(metric);
        if (index > 0)
            Settings.Metrics.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveDown(MetricSettings? metric)
    {
        if (metric is null) return;
        var index = Settings.Metrics.IndexOf(metric);
        if (index >= 0 && index < Settings.Metrics.Count - 1)
            Settings.Metrics.Move(index, index + 1);
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var result = MessageBox.Show(Loc.Instance.Resolve("Dialog_ResetText"), Loc.Instance.Resolve("App_Title"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            _settingsManager.ResetToDefaults();
    }

    public void Dispose() => _monitor.Initialized -= OnMonitorInitialized;
}
