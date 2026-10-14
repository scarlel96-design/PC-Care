using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models.Commercial;
using SmartPerformanceDoctor.App.Models.Security;
using SmartPerformanceDoctor.App.Services.Commercial;
using SmartPerformanceDoctor.App.Services.Pickers;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class SecureDeleteCenterPage : Page
{
    private readonly ProfessionalSecureDeleteService _service = new();
    private readonly IPathPickerService _pickerService = PathPickerService.Shared;
    private readonly SecureDeleteTargetSet _targetSet = new();
    private SecureDeletePlan? _plan;
    private SecureDeleteSecurityLevel _level = SecureDeleteSecurityLevel.Professional;
    private bool _pickerInProgress;
    private bool _dryRunInProgress;
    private bool _deleteInProgress;
    private CancellationTokenSource? _deleteCancellation;

    public SecureDeleteCenterPage()
    {
        InitializeComponent();
        StatusText.Text = "대기 · 파일 또는 폴더를 추가한 뒤 Dry Run으로 안전성을 확인하세요.";
        RefreshTargetList();
        RefreshButtons();
    }

    private async void SecurityLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SecurityLevelBox.SelectedItem is ComboBoxItem item
            && Enum.TryParse<SecureDeleteSecurityLevel>(item.Tag?.ToString(), out var level))
        {
            _level = level;
            if (_targetSet.Items.Count > 0 && IsLoaded)
            {
                await ExecuteDryRunAsync();
            }
        }
    }

    private async void AddFiles(object sender, RoutedEventArgs e) => await ExecuteAddFilesAsync();

    private async Task ExecuteAddFilesAsync()
    {
        if (!BeginPickerOperation("파일 선택 창 여는 중…"))
        {
            return;
        }

        try
        {
            var request = new PickerRequest(
                "SecureDeleteFile",
                "보안 삭제할 파일 선택",
                "파일 추가",
                PickerStartLocation.Documents,
                ["*"]);
            var result = await _pickerService.PickMultipleFilesAsync(App.Shell, request);
            switch (result.Status)
            {
                case PickerStatus.Success:
                    var added = 0;
                    var rejected = new List<string>();
                    foreach (var path in result.Value!)
                    {
                        var addResult = _targetSet.AddFile(path);
                        if (addResult.Added)
                        {
                            added++;
                        }
                        else
                        {
                            rejected.Add(addResult.Message);
                        }
                    }

                    ResetPlan();
                    RefreshTargetList();
                    StatusText.Text = rejected.Count == 0
                        ? $"선택 완료 · 파일 {added}개를 추가했습니다."
                        : $"파일 {added}개 추가 · {rejected.Count}개 제외: {string.Join(" / ", rejected.Distinct())}";
                    TargetList.Focus(FocusState.Programmatic);
                    break;
                case PickerStatus.Cancelled:
                    StatusText.Text = result.UserMessage;
                    AddFilesButton.Focus(FocusState.Programmatic);
                    break;
                case PickerStatus.Failed:
                    StatusText.Text = result.UserMessage;
                    AddFilesButton.Focus(FocusState.Programmatic);
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"파일 선택 처리 중 오류가 발생했습니다: {ex.Message}";
        }
        finally
        {
            EndPickerOperation();
        }
    }

    private async void AddFolder(object sender, RoutedEventArgs e) => await ExecuteAddFolderAsync();

    private async Task ExecuteAddFolderAsync()
    {
        if (!BeginPickerOperation("폴더 선택 창 여는 중…"))
        {
            return;
        }

        try
        {
            var request = new PickerRequest(
                "SecureDeleteFolder",
                "보안 삭제할 폴더 선택",
                "폴더 추가",
                PickerStartLocation.Documents);
            var result = await _pickerService.PickFolderAsync(App.Shell, request);
            switch (result.Status)
            {
                case PickerStatus.Success:
                    var addResult = _targetSet.AddDirectory(result.Value!);
                    if (addResult.Added)
                    {
                        ResetPlan();
                        RefreshTargetList();
                        StatusText.Text = addResult.Message;
                        TargetList.Focus(FocusState.Programmatic);
                    }
                    else
                    {
                        StatusText.Text = $"폴더를 추가하지 않았습니다: {addResult.Message}";
                        AddFolderButton.Focus(FocusState.Programmatic);
                    }

                    break;
                case PickerStatus.Cancelled:
                    StatusText.Text = result.UserMessage;
                    AddFolderButton.Focus(FocusState.Programmatic);
                    break;
                case PickerStatus.Failed:
                    StatusText.Text = result.UserMessage;
                    AddFolderButton.Focus(FocusState.Programmatic);
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"폴더 선택 처리 중 오류가 발생했습니다: {ex.Message}";
        }
        finally
        {
            EndPickerOperation();
        }
    }

    private bool BeginPickerOperation(string status)
    {
        if (_pickerInProgress || _dryRunInProgress || _deleteInProgress)
        {
            return false;
        }

        _pickerInProgress = true;
        StatusText.Text = status;
        RefreshButtons();
        return true;
    }

    private void EndPickerOperation()
    {
        _pickerInProgress = false;
        RefreshButtons();
    }

    private void RemoveSelectedTarget(object sender, RoutedEventArgs e)
    {
        if (TargetList.SelectedItem is not SecureDeleteSelection selection)
        {
            StatusText.Text = "제거할 대상을 목록에서 선택하세요.";
            return;
        }

        _targetSet.Remove(selection);
        ResetPlan();
        RefreshTargetList();
        StatusText.Text = "선택한 대상을 제거했습니다.";
    }

    private void ClearTargets(object sender, RoutedEventArgs e)
    {
        _targetSet.Clear();
        ResetPlan();
        RefreshTargetList();
        StatusText.Text = "대상 목록을 비웠습니다.";
    }

    private async void RunDryRun(object sender, RoutedEventArgs e) => await ExecuteDryRunAsync();

    private async Task ExecuteDryRunAsync()
    {
        if (_dryRunInProgress || _pickerInProgress || _deleteInProgress)
        {
            return;
        }

        if (_targetSet.Items.Count == 0)
        {
            StatusText.Text = "먼저 파일 또는 폴더를 추가하세요.";
            return;
        }

        _dryRunInProgress = true;
        StatusText.Text = "경로 검증 및 Dry Run 진행 중…";
        RefreshButtons();
        try
        {
            var paths = _targetSet.Items.Select(item => item.NormalizedPath).ToArray();
            _plan = await Task.Run(() => _service.PlanDryRun(paths, _level));
            PlanSummaryText.Text =
                $"작업 ID: {_plan.OperationId}\n" +
                $"보안 등급: {_plan.SecurityLevel}\n" +
                $"대상 {_plan.Targets.Count}개 · 차단 {_plan.BlockedTargets.Count}개\n" +
                $"복구 저항 Level {_plan.RecoveryResistanceLevel} · 위험: {_plan.ProfessionalRecoveryRisk}\n" +
                $"풀체인: {_plan.ChainSummary}\n" +
                $"예상 시간: {_plan.EstimatedDuration}\n" +
                _plan.Limitations;
            StatusText.Text = _plan.BlockedTargets.Count == 0
                ? "Dry Run 완료 · 확인 문구를 입력한 뒤 실행하세요."
                : $"Dry Run 완료 · 보호 규칙으로 {_plan.BlockedTargets.Count}개를 차단했습니다.";
        }
        catch (Exception ex)
        {
            _plan = null;
            StatusText.Text = $"Dry Run 실패: {ex.Message}";
        }
        finally
        {
            _dryRunInProgress = false;
            RefreshButtons();
        }
    }

    private async void ApplyDelete(object sender, RoutedEventArgs e) => await ExecuteDeleteAsync();

    private async Task ExecuteDeleteAsync()
    {
        if (_deleteInProgress)
        {
            return;
        }

        if (_plan is null || _plan.Targets.Count == 0)
        {
            StatusText.Text = "먼저 대상을 선택하고 Dry Run을 실행하세요.";
            return;
        }

        _deleteInProgress = true;
        _deleteCancellation = new CancellationTokenSource();
        ProgressBar.Value = 0;
        RefreshButtons();
        try
        {
            var progress = new Progress<(int percent, string detail)>(p =>
            {
                ProgressBar.Value = p.percent;
                StatusText.Text = $"보안 삭제 진행 중 · {p.percent}% · {Path.GetFileName(p.detail)}";
            });
            var result = await _service.ApplyAsync(
                _plan,
                ConfirmBox.Text ?? string.Empty,
                _level,
                progress,
                _deleteCancellation.Token);
            StatusText.Text =
                $"완료 · 삭제 {result.Deleted} · 실패 {result.Failed}\n" +
                $"감사 체인: {(result.AuditValid ? "정상" : "주의")}";
            _targetSet.Clear();
            ResetPlan();
            RefreshTargetList();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "보안 삭제 작업을 취소했습니다. 이미 처리된 대상은 되돌릴 수 없습니다.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"보안 삭제 실패: {ex.Message}";
        }
        finally
        {
            _deleteCancellation.Dispose();
            _deleteCancellation = null;
            _deleteInProgress = false;
            RefreshButtons();
        }
    }

    private void CancelDelete(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "현재 항목이 끝나는 대로 취소합니다…";
        _deleteCancellation?.Cancel();
    }

    private void RefreshTargetList()
    {
        TargetList.ItemsSource = _targetSet.Items.ToArray();
        RefreshButtons();
    }

    private void ResetPlan()
    {
        _plan = null;
        PlanSummaryText.Text = _targetSet.Items.Count == 0
            ? "대기 · 선택된 대상 없음"
            : $"대상 {_targetSet.Items.Count}개 · 변경 후 Dry Run이 필요합니다.";
    }

    private void RefreshButtons()
    {
        if (AddFilesButton is null)
        {
            return;
        }

        var idle = !_pickerInProgress && !_dryRunInProgress && !_deleteInProgress;
        AddFilesButton.IsEnabled = idle;
        AddFolderButton.IsEnabled = idle;
        RemoveSelectedButton.IsEnabled = idle && _targetSet.Items.Count > 0;
        ClearTargetsButton.IsEnabled = idle && _targetSet.Items.Count > 0;
        DryRunButton.IsEnabled = idle && _targetSet.Items.Count > 0;
        ApplyDeleteButton.IsEnabled = idle && _plan?.Targets.Count > 0;
        SecurityLevelBox.IsEnabled = idle;
        CancelDeleteButton.Visibility = _deleteInProgress ? Visibility.Visible : Visibility.Collapsed;
        CancelDeleteButton.IsEnabled = _deleteInProgress && _deleteCancellation?.IsCancellationRequested != true;
    }
}