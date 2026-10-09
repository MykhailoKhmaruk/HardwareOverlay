using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HardwareOverlay.Models;

namespace HardwareOverlay.Services;

public sealed class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private const int SaveDebounceMs = 500;

    private CancellationTokenSource? _saveCts;
    private bool _autoSaveEnabled;

    public string FilePath { get; }

    public OverlaySettings Settings { get; private set; } = OverlaySettings.CreateDefault();

    public SettingsManager(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HardwareOverlay",
            "settings.json");
    }

    // Call once at startup, before any window is created.
    public OverlaySettings Load()
    {
        OverlaySettings? loaded = null;

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                loaded = JsonSerializer.Deserialize<OverlaySettings>(json, JsonOptions);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Debug.WriteLine($"[SettingsManager] Failed to load settings: {ex}");
            BackupCorruptedFile();
        }

        Settings = loaded ?? OverlaySettings.CreateDefault();
        Settings.Normalize();

        Attach(Settings);
        _autoSaveEnabled = true;

        Save();
        return Settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            var tempPath = FilePath + ".tmp";

            File.WriteAllText(tempPath, json);
            File.Move(tempPath, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[SettingsManager] Failed to save settings: {ex}");
        }
    }

    // Debounced save; called from the UI thread.
    public async void RequestSave()
    {
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(SaveDebounceMs, cts.Token);
            Save();
        }
        catch (TaskCanceledException)
        {
        }
    }

    // Call on exit so pending changes aren't lost.
    public void Flush()
    {
        _saveCts?.Cancel();
        Save();
    }

    public void ResetToDefaults()
    {
        var defaults = OverlaySettings.CreateDefault();

        Settings.BackgroundOpacity = defaults.BackgroundOpacity;
        Settings.BackgroundColor = defaults.BackgroundColor;
        Settings.TextOpacity = defaults.TextOpacity;
        Settings.AutoSize = defaults.AutoSize;
        Settings.Width = defaults.Width;
        Settings.Height = defaults.Height;
        Settings.Corner = defaults.Corner;
        Settings.MarginX = defaults.MarginX;
        Settings.MarginY = defaults.MarginY;
        Settings.UpdateIntervalMs = defaults.UpdateIntervalMs;
        Settings.Language = defaults.Language;
        Settings.StartWithWindows = defaults.StartWithWindows;

        Settings.Metrics.Clear();
        foreach (var metric in defaults.Metrics)
            Settings.Metrics.Add(metric);
    }

    private void Attach(OverlaySettings settings)
    {
        settings.PropertyChanged += OnAnyPropertyChanged;
        settings.Metrics.CollectionChanged += OnMetricsCollectionChanged;

        foreach (var metric in settings.Metrics)
            metric.PropertyChanged += OnAnyPropertyChanged;
    }

    private void OnMetricsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (MetricSettings metric in e.OldItems)
                metric.PropertyChanged -= OnAnyPropertyChanged;

        if (e.NewItems is not null)
            foreach (MetricSettings metric in e.NewItems)
                metric.PropertyChanged += OnAnyPropertyChanged;

        if (_autoSaveEnabled)
            RequestSave();
    }

    private void OnAnyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_autoSaveEnabled)
            RequestSave();
    }

    private void BackupCorruptedFile()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Copy(FilePath, FilePath + ".bak", overwrite: true);
        }
        catch (IOException)
        {
        }
    }
}
