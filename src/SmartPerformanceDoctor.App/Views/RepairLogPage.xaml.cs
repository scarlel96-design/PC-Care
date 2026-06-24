using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class RepairLogPage : Page
{
    private readonly RepairLogStore _repairLogStore = new();

    public RepairLogPage()
    {
        InitializeComponent();
        LoadLogs();
    }

    private void RefreshLogs(object sender, RoutedEventArgs e)
    {
        LoadLogs();
    }

    private void LoadLogs()
    {
        LogList.ItemsSource = _repairLogStore.LoadRecentLogs();
    }

    private void OpenSelectedLog(object sender, RoutedEventArgs e)
    {
        if (LogList.SelectedItem is not RepairLogSummary log)
        {
            StatusText.Text = "열 로그를 먼저 선택하세요.";
            return;
        }

        OpenPath(log.Path);
    }

    private void OpenLogsFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_repairLogStore.LogsRoot);
        OpenPath(_repairLogStore.LogsRoot);
    }

    private void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            StatusText.Text = $"열기 요청: {path}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"열기 실패: {ex.Message}";
        }
    }
}
