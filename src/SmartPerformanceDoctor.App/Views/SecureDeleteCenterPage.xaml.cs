using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models.Commercial;
using SmartPerformanceDoctor.App.Services.Commercial;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class SecureDeleteCenterPage : Page
{
    private readonly ProfessionalSecureDeleteService _service = new();
    private SecureDeletePlan? _plan;
    private readonly List<string> _paths = new();
    private SecureDeleteSecurityLevel _level = SecureDeleteSecurityLevel.Professional;

    public SecureDeleteCenterPage()
    {
        InitializeComponent();
        StatusText.Text = "베타 기능 — dry-run으로 대상과 풀체인을 확인한 뒤 실행하세요.";
    }

    private void SecurityLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SecurityLevelBox.SelectedItem is ComboBoxItem item &&
            Enum.TryParse<SecureDeleteSecurityLevel>(item.Tag?.ToString(), out var level))
        {
            _level = level;
            if (_paths.Count > 0)
            {
                RunDryRun();
            }
        }
    }

    private async void PickTargets(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        if (App.Shell is not null)
        {
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Shell));
        }

        picker.FileTypeFilter.Add("*");
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            _paths.Add(file.Path);
            RunDryRun();
        }
    }

    private void RunDryRun(object sender, RoutedEventArgs e) => RunDryRun();

    private void RunDryRun()
    {
        _plan = _service.PlanDryRun(_paths, _level);
        PlanSummaryText.Text =
            $"작업 ID: {_plan.OperationId}\n" +
            $"보안 등급: {_plan.SecurityLevel}\n" +
            $"대상 {_plan.Targets.Count}개 · 차단 {_plan.BlockedTargets.Count}개\n" +
            $"공인 복구 저항: {_plan.CertifiedResistanceLabel}\n" +
            $"기술 삭제 강도 Tier {_plan.TechnicalDeletionIntensity} · Level5 공인: {(_plan.Level5Certified ? "예" : "아니오")}\n" +
            $"위험: {_plan.ProfessionalRecoveryRisk}\n" +
            (string.IsNullOrWhiteSpace(_plan.ResistanceDisclaimer) ? "" : $"{_plan.ResistanceDisclaimer}\n") +
            $"풀체인: {_plan.ChainSummary}\n" +
            $"예상 시간: {_plan.EstimatedDuration}\n" +
            _plan.Limitations;
        TargetList.ItemsSource = _plan.Targets
            .Select(t => $"{t.Path} [{t.StorageType}] {t.RecommendedProtocol} · {t.OverwritePasses}패스")
            .ToList();
        StatusText.Text = "Dry-run 완료. 확인 문구 입력 후 실행하세요.";
    }

    private async void ApplyDelete(object sender, RoutedEventArgs e)
    {
        if (_plan is null || _plan.Targets.Count == 0)
        {
            StatusText.Text = "먼저 대상을 선택하고 dry-run을 실행하세요.";
            return;
        }

        try
        {
            var progress = new Progress<(int percent, string detail)>(p =>
            {
                ProgressBar.Value = p.percent;
                StatusText.Text = p.detail;
            });
            var result = await _service.ApplyAsync(_plan, ConfirmBox.Text ?? "", _level, progress);
            StatusText.Text =
                $"완료 · 삭제 {result.Deleted} · 실패 {result.Failed}\n" +
                $"감사 체인: {(result.AuditValid ? "정상" : "주의")}\n" +
                $"감사 로그: {result.AuditPath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }
}