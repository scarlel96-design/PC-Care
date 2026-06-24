using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SmartPerformanceDoctor.App.Controls;

public sealed partial class WindowsWindowControls : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler? MinimizeRequested;
    public event EventHandler? MaximizeRequested;

    public WindowsWindowControls()
    {
        InitializeComponent();
    }

    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnMinimize(object sender, RoutedEventArgs e) => MinimizeRequested?.Invoke(this, EventArgs.Empty);

    private void OnMaximize(object sender, RoutedEventArgs e) => MaximizeRequested?.Invoke(this, EventArgs.Empty);
}