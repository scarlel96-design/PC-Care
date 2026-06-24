using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class StableLogLayoutPage : Page
{
    private readonly PreservedExecutionLogStore _store = new();

    public StableLogLayoutPage()
    {
        InitializeComponent();
        Refresh();
    }

    private void RefreshLogs(object sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void OpenSelected(object sender, RoutedEventArgs e)
    {
        if (LogList.SelectedItem is not StableLogEntry entry || string.IsNullOrWhiteSpace(entry.Path))
        {
            return;
        }

        if (!File.Exists(entry.Path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = entry.Path,
            UseShellExecute = true
        });
    }

    private void Refresh()
    {
        LogList.ItemsSource = _store.LoadRecent();
    }
}
