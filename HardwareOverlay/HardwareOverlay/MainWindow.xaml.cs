using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using HardwareOverlay.Interop;
using HardwareOverlay.Models;
using HardwareOverlay.ViewModels;

namespace HardwareOverlay;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly OverlaySettings _settings;

    public MainWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        _settings = viewModel.Settings;

        _settings.PropertyChanged += OnSettingsPropertyChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        SizeChanged += (_, _) => UpdatePosition();

        ApplySize();
    }

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

    private void UpdatePosition()
    {
        var area = SystemParameters.WorkArea;
        var mx = _settings.MarginX;
        var my = _settings.MarginY;
        var w = ActualWidth;
        var h = ActualHeight;

        (Left, Top) = _settings.Corner switch
        {
            ScreenCorner.TopLeft     => (area.Left + mx,      area.Top + my),
            ScreenCorner.TopRight    => (area.Right - w - mx, area.Top + my),
            ScreenCorner.BottomLeft  => (area.Left + mx,      area.Bottom - h - my),
            _                        => (area.Right - w - mx, area.Bottom - h - my)
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        base.OnClosed(e);
    }
}