using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services.Commercial;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class IntelligenceCenterPage : Page
{
    private readonly IntelligencePipelineService _pipeline = new();

    public IntelligenceCenterPage()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var snapshot = _pipeline.BuildSnapshot();
        SummaryText.Text = snapshot.Summary;
        RuleCountText.Text = $"규칙 {snapshot.RuleCount:N0}개 (버전 {snapshot.PackVersion})";
        ProtocolCountText.Text = $"복구 방법 {snapshot.ProtocolCount}개";
        InsightList.ItemsSource = snapshot.TopInsights;
    }
}