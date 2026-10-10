using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.Diagnostics;
using SmartPerformanceDoctor.App.Services.SystemCare;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class PerformanceDiagnosticsPage : Page
{
    private CancellationTokenSource? _cancellation;
    private int _generation;
    public PerformanceDiagnosticsPage() { InitializeComponent(); Unloaded += (_, _) => { _generation++; _cancellation?.Cancel(); }; }
    private async void Start(object sender, RoutedEventArgs e)
    {
        if (_cancellation is not null) return;
        _cancellation = new();
        var generation = ++_generation;
        StartButton.IsEnabled = false; StopButton.IsEnabled = true; ExpectedLoad.IsEnabled = false;
        FindingsList.ItemsSource = null; Progress.Value = 0; StatusText.Text = "측정 중 · 30초";
        try
        {
            var progress = new Progress<PerformanceSample>(s =>
            {
                if (generation != _generation) return;
                static string Number(double? x, string unit) => x.HasValue ? $"{x:F1}{unit}" : "Unknown";
                Progress.Value = Math.Min(30, s.Seconds);
                Metrics.Text = $"CPU {Number(s.CpuPercent, "%")} · 가용 메모리 {Number(s.AvailableMemoryPercent, "%")} · 디스크 지연 {Number(s.DiskLatencyMilliseconds, "ms")}";
            });
            var result = await new PerformanceCollector().CollectAsync(new(), _cancellation.Token, progress,
                ExpectedLoad.IsChecked == true ? "ExpectedHighLoad" : "Unspecified");
            if (generation != _generation) return;
            FindingsList.ItemsSource = PerformanceFindingAdapter.Convert(result);
            StatusText.Text = result.Errors.Any(x => x.Code == "Cancelled") ? "측정 중단 · 수집된 표본만 표시" : "측정 완료 · 병목 후보를 재측정으로 확인하세요";
            Coverage.Text = $"{result.Samples.Count}개 표본 · {result.DurationSeconds:F1}초 · 이벤트 손실 {result.EventsLost} · "
                + string.Join(", ", result.Errors.Select(x => x.Provider + ": " + x.Code))
                + " · 온도·클럭·부팅 원인·서비스 상관관계 Unknown";
        }
        catch (Exception ex) { if (generation == _generation) StatusText.Text = "측정 중단: " + ex.GetType().Name; }
        finally { _cancellation.Dispose(); _cancellation = null; StartButton.IsEnabled = true; StopButton.IsEnabled = false; ExpectedLoad.IsEnabled = true; }
    }
    private void Stop(object sender, RoutedEventArgs e) => _cancellation?.Cancel();
}
