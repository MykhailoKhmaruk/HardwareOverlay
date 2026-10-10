using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using HardwareOverlay.Interop;
using HardwareOverlay.Models;
using HardwareOverlay.Services;
using HardwareOverlay.ViewModels;

namespace HardwareOverlay;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly OverlaySettings _settings;

    public MainWindow(OverlayViewModel viewModel, string monitorId)
    {
        InitializeComponent();

        DataContext = viewModel;
        _settings = viewModel.Settings;
        MonitorId = monitorId;

        _settings.PropertyChanged += OnSettingsPropertyChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        SizeChanged += (_, _) => UpdatePosition();
        // WPF applies the system-suggested rect after this event, so reposition afterwards.
        DpiChanged += (_, _) => Dispatcher.InvokeAsync(UpdatePosition, DispatcherPriority.Loaded);

        ApplySize();
    }

    public string MonitorId { get; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeMethods.MakeOverlayWindow(new WindowInteropHelper(this).Handle);
        UpdatePosition();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(OverlaySettings.AutoSize):
            case nameof(OverlaySettings.Width):
            case nameof(OverlaySettings.Height):
                ApplySize();
                break;

            case nameof(OverlaySettings.Corner):
            case nameof(OverlaySettings.MarginX):
            case nameof(OverlaySettings.MarginY):
                UpdatePosition();
                break;
        }
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.WorkArea))
            UpdatePosition();
    }

    private void ApplySize()
    {
        if (_settings.AutoSize)
        {
            ClearValue(WidthProperty);
            ClearValue(HeightProperty);
            SizeToContent = SizeToContent.WidthAndHeight;
        }
        else
        {
            SizeToContent = SizeToContent.Manual;
            Width = _settings.Width;
            Height = _settings.Height;
        }
    }

    public void UpdatePosition()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var display = DisplayService.GetDisplays().FirstOrDefault(d => d.Id == MonitorId);
        if (display is null)
            return;

        // Window size and margins are in DIPs; the target monitor's DPI defines the pixel size.
        var scale = display.Scale;
        var mx = (int)Math.Round(_settings.MarginX * scale);
        var my = (int)Math.Round(_settings.MarginY * scale);
        var w = (int)Math.Round(ActualWidth * scale);
        var h = (int)Math.Round(ActualHeight * scale);

        var (x, y) = _settings.Corner switch
        {
            ScreenCorner.TopLeft     => (display.WorkLeft + mx,      display.WorkTop + my),
            ScreenCorner.TopRight    => (display.WorkRight - w - mx, display.WorkTop + my),
            ScreenCorner.BottomLeft  => (display.WorkLeft + mx,      display.WorkBottom - h - my),
            _                        => (display.WorkRight - w - mx, display.WorkBottom - h - my)
        };

        NativeMethods.MoveWindow(hwnd, x, y);
    }

    protected override void OnClosed(EventArgs e)
    {
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        base.OnClosed(e);
    }
}