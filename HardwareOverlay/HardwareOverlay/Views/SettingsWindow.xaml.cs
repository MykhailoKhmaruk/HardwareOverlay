using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using HardwareOverlay.Models;
using HardwareOverlay.ViewModels;

namespace HardwareOverlay.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OnPaletteColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MetricSettings metric, DataContext: string color })
            metric.Color = color;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
