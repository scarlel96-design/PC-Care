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
        Loaded += async (_, _) =>
        {
            try
            {
                var pending = await Task.Run(_service.InspectInterruptedOperations);
                if (pending.Count > 0 && !_deleteInProgress && _targetSet.Items.Count == 0)
                    StatusText.Text = $"이전 삭제 작업 {pending.Count}건의 결과를 추가 확인해야 합니다. 자동 재실행하지 않습니다.";
            }
            catch { StatusText.Text = "이전 삭제 기록을 확인하지 못했습니다. 원본과 기록을 보존하고 추가 확인하세요."; }
        };
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
                $"파일 단위 Clear · 인증 및 장치 Purge 미지원 · 위험: {_plan.ProfessionalRecoveryRisk}\n" +
                $"풀체인: {_plan.ChainSummary}\n" +
                $"예상 시간: {_plan.EstimatedDuration}\n" +
                _plan.Limitations;
            StatusText.Text = _plan.Targets.Any(t => t.Type == "folder")
                ? "폴더 전체 삭제는 파일 경계 검증 대기 중입니다. 개별 파일을 선택해 다시 검사하세요."
                : _plan.BlockedTargets.Count == 0
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
            var plan = _plan; var confirmation = ConfirmBox.Text ?? string.Empty; var level = _level;
            var cancellation = _deleteCancellation.Token;
            var result = await Task.Run(() => _service.ApplyDetailedAsync(plan, confirmation, level, progress, cancellation));
            StatusText.Text =
                $"{result.StatusLabel} · 삭제 {result.CompletedTargets} · 미완료 {result.IncompleteTargets} · 실패 {result.Failed}\n" +
                $"취소 요청: {(result.WasCancellationRequested ? "있음" : "없음")} · 추가 확인: {(result.RequiresManualReview ? "필요" : "없음")}\n" +
                "중단·실패한 파일에는 부분 덮어쓰기가 남을 수 있습니다. 원복을 보장하지 않습니다.\n" +
                $"감사 체인: {(result.AuditValid ? "정상" : "주의")}";
            if (result.Status == "Completed" && !result.RequiresManualReview) _targetSet.Clear();
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
        StatusText.Text = "취소 요청 중 · 이미 덮어쓴 데이터는 원복되지 않습니다…";
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
        ApplyDeleteButton.IsEnabled = idle && _plan?.Targets.Count > 0 && _plan.Targets.All(t => t.Type == "file");
        SecurityLevelBox.IsEnabled = idle;
        CancelDeleteButton.Visibility = _deleteInProgress ? Visibility.Visible : Visibility.Collapsed;
        CancelDeleteButton.IsEnabled = _deleteInProgress && _deleteCancellation?.IsCancellationRequested != true;
    }
}
