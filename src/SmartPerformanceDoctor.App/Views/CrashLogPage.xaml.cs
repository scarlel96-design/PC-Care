using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class CrashLogPage : Page
{
    private readonly CrashLogStore _crashLogStore = new();

    public CrashLogPage()
    {
        InitializeComponent();
        Refresh();
    }

    private void RefreshLogs(object sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void OpenCrashFolder(object sender, RoutedEventArgs e)
    {
        RuntimePaths.EnsureUserFolders();
        Process.Start(new ProcessStartInfo
        {
            FileName = RuntimePaths.CrashLogsRoot,
            UseShellExecute = true
        });
    }

    private void Refresh()
    {
        CrashList.ItemsSource = _crashLogStore.LoadRecentCrashLogs();
    }
}
