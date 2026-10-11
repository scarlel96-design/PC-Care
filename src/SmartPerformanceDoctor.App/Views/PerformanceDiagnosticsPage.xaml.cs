using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.Diagnostics;
using SmartPerformanceDoctor.App.Services.SystemCare;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class PerformanceDiagnosticsPage : Page
{
    private readonly DiagnosticLifecycle _lifecycle = new();
    public PerformanceDiagnosticsPage() { InitializeComponent(); Unloaded += (_, _) => _lifecycle.Cancel(invalidate: true); }
    private async void Start(object sender, RoutedEventArgs e)
    {
        if (!_lifecycle.TryBegin(out var lease) || lease is null) return;
        StartButton.IsEnabled = false; StopButton.IsEnabled = true; ExpectedLoad.IsEnabled = false;
        FindingsList.ItemsSource = null; PlansList.ItemsSource = null; Progress.Value = 0; StatusText.Text = "측정 중 · 30초";
        try
        {
            var progress = new Progress<PerformanceSample>(s =>
            {
                if (!_lifecycle.IsCurrent(lease)) return;
                static string Number(double? x, string unit) => x.HasValue ? $"{x:F1}{unit}" : "미측정";
                Progress.Value = Math.Min(30, s.Seconds);
                Metrics.Text = $"CPU {Number(s.CpuPercent, "%")} · 가용 메모리 {Number(s.AvailableMemoryPercent, "%")} · 디스크 지연 {Number(s.DiskLatencyMilliseconds, "ms")}";
            });
            var result = await new PerformanceCollector().CollectAsync(new(), lease.Token, progress,
                ExpectedLoad.IsChecked == true ? "ExpectedHighLoad" : "Unspecified");
            if (!_lifecycle.IsCurrent(lease)) return;
            FindingsList.ItemsSource = PerformanceFindingAdapter.Convert(result);
            PlansList.ItemsSource = RecoveryPlanner.Create(BottleneckAnalyzer.Analyze(result)).Select(p =>
            {
                var preview = RecoveryPlanner.DryRun(p);
                return new
                {
                    Title = RecoveryPlanner.ActionLabel(p.ProposedAction),
                    Detail = preview.Status == "PreviewAvailable" ? "검토 계획 · 실행 전 대상 확인과 별도 동의 필요" : "계획 차단 · 추가 근거 확인 필요",
                    Evidence = "기대 효과: 미확인 · " + p.ProblemDescription,
                    Preview = "미리보기: " + string.Join(" / ", preview.Preconditions) + $" · 실제 변경 {preview.SystemMutations}건"
                };
            }).ToArray();
            StatusText.Text = result.Errors.Any(x => x.Code == "Cancelled") ? "측정 중단 · 수집된 표본만 표시" : "측정 완료 · 병목 후보를 재측정으로 확인하세요";
            Coverage.Text = DiagnosticPresentation.Coverage(result);
        }
        catch (Exception ex) { if (_lifecycle.IsCurrent(lease)) StatusText.Text = "측정 중단: " + ex.GetType().Name; }
        finally { if (_lifecycle.Complete(lease)) { StartButton.IsEnabled = true; StopButton.IsEnabled = false; ExpectedLoad.IsEnabled = true; } }
    }
    private void Stop(object sender, RoutedEventArgs e) => _lifecycle.Cancel();
}
