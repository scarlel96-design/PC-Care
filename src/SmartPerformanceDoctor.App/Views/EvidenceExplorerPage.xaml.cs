using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class EvidenceExplorerPage : Page
{
    private readonly ReportStore _store = new();
    private IReadOnlyList<string> _reportDirs = Array.Empty<string>();

    public EvidenceExplorerPage()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var reports = _store.LoadRecentReports();
        _reportDirs = reports
            .Select(r => Path.GetDirectoryName(string.IsNullOrWhiteSpace(r.JsonPath) ? r.ReportPath : r.JsonPath) ?? "")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        ReportList.ItemsSource = reports.Select(r => $"{r.CreatedAt} · {r.Title}").ToArray();
        EvidenceText.Text = "보고서를 선택하세요.";
    }

    private void OnReportSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ReportList.SelectedIndex < 0 || ReportList.SelectedIndex >= _reportDirs.Count)
        {
            return;
        }

        var dir = _reportDirs[ReportList.SelectedIndex];
        var evidence = Path.Combine(dir, "evidence.json");
        var timeline = Path.Combine(dir, "timeline.json");
        var expert = Path.Combine(dir, "report.expert.html");

        var parts = new List<string>();
        if (File.Exists(evidence))
        {
            parts.Add("--- evidence.json ---");
            parts.Add(File.ReadAllText(evidence));
        }

        if (File.Exists(timeline))
        {
            parts.Add("--- timeline.json ---");
            parts.Add(File.ReadAllText(timeline));
        }

        if (File.Exists(expert))
        {
            parts.Add("--- report.expert.html (생성됨) ---");
        }

        EvidenceText.Text = parts.Count > 0
            ? string.Join('\n', parts)
            : "evidence/timeline 파일이 없습니다. v45.0.1 이후 보고서부터 생성됩니다.";
    }
}