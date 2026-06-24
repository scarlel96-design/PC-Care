using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class ReportPage : Page
{
    private readonly ReportStore _reportStore = new();

    public ReportPage()
    {
        InitializeComponent();
        LoadReports();
    }

    private void RefreshReports(object sender, RoutedEventArgs e)
    {
        LoadReports();
    }

    private void LoadReports()
    {
        var reports = _reportStore.LoadRecentReports();
        ReportList.ItemsSource = reports;
        StatusText.Text = reports.Count == 0
            ? "저장된 보고서가 없습니다. PC 점검·복구를 실행하면 보고서가 생성됩니다."
            : $"보고서 {reports.Count}건 · 선택 후 열기(HTML → 요약TXT → JSON 순)";
    }

    private void ReportSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReportList.SelectedItem is not ReportSummary report)
        {
            return;
        }

        StatusText.Text =
            $"{report.Module} · {ReportStore.TranslateStatus(report.Status)} · {report.CreatedAt}\n" +
            $"{report.SummaryText}\n" +
            $"조치 {report.ActionsTakenCount}건 기록";
    }

    private void OpenSelectedReport(object sender, RoutedEventArgs e)
    {
        if (ReportList.SelectedItem is not ReportSummary report)
        {
            StatusText.Text = "열 보고서를 먼저 선택하세요.";
            return;
        }

        var path = File.Exists(report.ReportPath)
            ? report.ReportPath
            : File.Exists(report.SummaryPath)
                ? report.SummaryPath
                : report.JsonPath;

        OpenPath(path);
    }

    private void OpenReportsFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_reportStore.ReportsRoot);
        OpenPath(_reportStore.ReportsRoot);
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
            StatusText.Text = $"열기: {path}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"열기 실패: {ex.Message}";
        }
    }
}