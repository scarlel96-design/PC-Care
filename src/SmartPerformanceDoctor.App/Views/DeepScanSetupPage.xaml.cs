using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services.Commercial;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class DeepScanSetupPage : Page
{
    private readonly PrecisionScanService _scanner = new();

    public DeepScanSetupPage()
    {
        InitializeComponent();
    }

    private void RunPrecisionScan(object sender, RoutedEventArgs e)
    {
        var deep = ScanModeBox.SelectedIndex >= 2;
        var results = deep ? _scanner.RunDeepSet() : _scanner.RunStandardSet();
        ScanSummaryText.Text = string.Join(" · ", results.Select(r => $"{r.ScannerId}: {r.Summary}"));
        SignalList.ItemsSource = results
            .SelectMany(r => r.Signals)
            .Select(s => $"[{s.Severity}] {s.Area} — {s.Evidence}")
            .ToArray();
    }
}