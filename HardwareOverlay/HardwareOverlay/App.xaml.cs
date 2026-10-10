using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using H.NotifyIcon;
using Microsoft.Win32;
using HardwareOverlay.Localization;
using HardwareOverlay.Models;
using HardwareOverlay.Services;
using HardwareOverlay.ViewModels;
using HardwareOverlay.Views;

namespace HardwareOverlay;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string MutexName = "HardwareOverlay_SingleInstance_Mutex";

    private Mutex? _mutex;
    private bool _ownsMutex;

    private SettingsManager? _settingsManager;
    private HardwareMonitorService? _monitor;
    private OverlayViewModel? _overlayViewModel;
    private readonly Dictionary<string, MainWindow> _overlayWindows = new();
    private SettingsWindow? _settingsWindow;
    private TaskbarIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            MessageBox.Show(Loc.Instance.Resolve("Msg_AlreadyRunning"), Loc.Instance.Resolve("App_Title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _settingsManager = new SettingsManager();
        _settingsManager.Load();

        // Initial language, plus follow changes made in settings.
        Loc.Instance.Current = _settingsManager.Settings.Language;

        // If enabled, re-create the autostart task so the exe path stays current.
        if (_settingsManager.Settings.StartWithWindows)
            AutostartService.Apply(true);

        _settingsManager.Settings.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(OverlaySettings.Language):
                    Loc.Instance.Current = _settingsManager.Settings.Language;
                    break;
                case nameof(OverlaySettings.StartWithWindows):
                    AutostartService.Apply(_settingsManager.Settings.StartWithWindows);
                    break;
                case nameof(OverlaySettings.ShowOnAllMonitors):
                case nameof(OverlaySettings.MonitorIds):
                    SyncOverlayWindows();
                    break;
            }
        };

        _monitor = new HardwareMonitorService();

        _overlayViewModel = new OverlayViewModel(_settingsManager.Settings, _monitor, Dispatcher);
        SyncOverlayWindows();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _monitor.Start();

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.ForceCreate(false);
    }

    // Raised on a system thread.
    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(SyncOverlayWindows);

    // One overlay window per target monitor; existing windows are reused.
    private void SyncOverlayWindows()
    {
        if (_settingsManager is null || _overlayViewModel is null)
            return;

        var targets = DisplayService.ResolveTargets(_settingsManager.Settings)
            .Select(d => d.Id)
            .ToHashSet();

        foreach (var id in _overlayWindows.Keys.Where(id => !targets.Contains(id)).ToList())
        {
            _overlayWindows[id].Close();
            _overlayWindows.Remove(id);
        }

        foreach (var id in targets)
        {
            if (_overlayWindows.TryGetValue(id, out var window))
            {
                window.UpdatePosition();
                continue;
            }

            window = new MainWindow(_overlayViewModel, id);
            _overlayWindows[id] = window;
            window.Show();
        }
    }

    private void ShowSettings()
    {
        if (_settingsManager is null || _monitor is null)
            return;

        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(new SettingsViewModel(_settingsManager, _monitor, Dispatcher));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => ShowSettings();

    private void OnTrayDoubleClick(object sender, RoutedEventArgs e) => ShowSettings();

    private void OnExitClick(object sender, RoutedEventArgs e) => Shutdown();

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine(e.Exception);
        MessageBox.Show(e.Exception.Message, Loc.Instance.Resolve("App_Title"),
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _settingsManager?.Flush();
        _overlayViewModel?.Dispose();
        _monitor?.Dispose();
        _trayIcon?.Dispose();

        if (_ownsMutex)
            _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        base.OnExit(e);
    }
}