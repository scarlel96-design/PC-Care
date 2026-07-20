using System.Collections.ObjectModel;
using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.SystemCare;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class SystemCareViewModel : ObservableObject
{
    private readonly SystemCareService _service = new();

    private int _selectedModeIndex;
    private string _statusLine = "빠른 검사로 PC 상태를 확인한 뒤, 필요한 항목만 정리하세요.";
    private string _summaryLine = "";
    private string _healthLine = "";
    private string _progressLine = "검사를 시작하면 진행 상황이 표시됩니다.";
    private string _progressDetailLine = "";
    private string _progressPhaseTitle = "대기 중";
    private string _healthScoreText = "--";
    private string _issueCountText = "0";
    private string _actionableCountText = "0";
    private string _safeCountText = "0";
    private string _checkedCountText = "0";
    private string _cleanupListHint = "검사 후 정리할 항목이 여기에 표시됩니다.";
    private double _progress;
    private bool _isBusy;
    private bool _hasResults;
    private bool _autoApplyAfterScan;
    private CareScanResult? _lastScan;

    public ObservableCollection<CareScanTaskItem> ScanTasks { get; } = new();
    public ObservableCollection<CareActionItem> ActionItems { get; } = new();
    public ObservableCollection<CareResultGroup> ResultGroups { get; } = new();

    public int SelectedModeIndex
    {
        get => _selectedModeIndex;
        set
        {
            if (_selectedModeIndex == value)
            {
                return;
            }

            Set(ref _selectedModeIndex, value);
            ApplyModeDefaults();
        }
    }

    public bool IsPrecisionMode => SelectedModeIndex == 1;

    public string StatusLine { get => _statusLine; private set => Set(ref _statusLine, value); }
    public string SummaryLine { get => _summaryLine; private set => Set(ref _summaryLine, value); }
    public string HealthLine { get => _healthLine; private set => Set(ref _healthLine, value); }
    public string ProgressLine { get => _progressLine; private set => Set(ref _progressLine, value); }
    public string ProgressDetailLine { get => _progressDetailLine; private set => Set(ref _progressDetailLine, value); }
    public string ProgressPhaseTitle { get => _progressPhaseTitle; private set => Set(ref _progressPhaseTitle, value); }
    public string HealthScoreText { get => _healthScoreText; private set => Set(ref _healthScoreText, value); }
    public string IssueCountText { get => _issueCountText; private set => Set(ref _issueCountText, value); }
    public string ActionableCountText { get => _actionableCountText; private set => Set(ref _actionableCountText, value); }
    public string SafeCountText { get => _safeCountText; private set => Set(ref _safeCountText, value); }
    public string CheckedCountText { get => _checkedCountText; private set => Set(ref _checkedCountText, value); }
    public string CleanupListHint { get => _cleanupListHint; private set => Set(ref _cleanupListHint, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public bool HasResults { get => _hasResults; private set => Set(ref _hasResults, value); }

    /// <summary>Single pre-check: auto-apply safe items after scan (merged from 3 checkboxes).</summary>
    public bool AutoApplyAfterScan
    {
        get => _autoApplyAfterScan;
        set
        {
            Set(ref _autoApplyAfterScan, value);
            // Auto-ack when user opts into auto apply.
            if (value)
            {
                // kept internal for apply path
            }
        }
    }

    public bool CanApply => HasResults && !IsBusy && ActionItems.Any(a => a.CanAutoApply);

    public SystemCareViewModel()
    {
        foreach (var task in SystemCareService.ScanTasks)
        {
            ScanTasks.Add(new CareScanTaskItem
            {
                Id = task.Id,
                Module = task.Module,
                Title = task.Title,
                Description = task.Description,
                IncludedInSmart = task.IncludedInSmart,
                IsSelected = task.IncludedInSmart
            });
        }

        ApplyModeDefaults();
    }

    public void SelectAllTasks()
    {
        foreach (var task in ScanTasks)
        {
            task.IsSelected = true;
        }
    }

    public void ClearAllTasks()
    {
        foreach (var task in ScanTasks)
        {
            task.IsSelected = false;
        }
    }

    public async Task StartScanAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        var mode = SelectedModeIndex == 0 ? CareScanMode.Smart : CareScanMode.Precision;
        var taskIds = mode == CareScanMode.Smart
            ? ScanTasks.Where(t => t.IncludedInSmart).Select(t => t.Id).ToArray()
            : ScanTasks.Where(t => t.IsSelected).Select(t => t.Id).ToArray();

        if (taskIds.Length == 0)
        {
            StatusLine = mode == CareScanMode.Smart
                ? "빠른 검사 항목이 없습니다."
                : "전체 검사할 항목을 선택하세요.";
            return;
        }

        IsBusy = true;
        HasResults = false;
        Progress = 0;
        SummaryLine = "";
        HealthLine = "";
        ActionItems.Clear();
        IssueCountText = "0";
        ActionableCountText = "0";
        SafeCountText = "0";
        CheckedCountText = "0";
        CleanupListHint = "검사 후 정리할 항목이 여기에 표시됩니다.";
        ResultGroups.Clear();
        HealthScoreText = "--";
        ProgressPhaseTitle = "검사 중";
        ProgressLine = mode == CareScanMode.Smart ? "빠른 검사를 시작합니다…" : "전체 검사를 시작합니다…";
        ProgressDetailLine = "잠시만 기다려 주세요.";
        OnPropertyChanged(nameof(CanApply));

        try
        {
            var progressTracker = new CareProgressTracker();
            var progress = new Progress<(int percent, string message)>(report =>
            {
                var state = progressTracker.AdvanceScan(report.percent, report.message);
                UiDispatcher.Run(() =>
                {
                    Progress = state.Percent;
                    ProgressPhaseTitle = state.Headline;
                    ProgressLine = report.message;
                    ProgressDetailLine = state.Flow;
                });
            });

            _lastScan = await _service.ScanByTasksAsync(mode, taskIds, progress, cancellationToken);
            SummaryLine = SimplifySummary(_lastScan.Summary);
            HealthScoreText = _lastScan.HealthScore.ToString();
            HealthLine = $"등급 {_lastScan.HealthGrade}";
            Progress = 100;
            ProgressPhaseTitle = "결과 · 100%";
            ProgressLine = "검사가 끝났습니다.";
            ProgressDetailLine = "4/4 · 아래에서 권장 조치를 확인하세요.";

            RebuildActionItems(_lastScan);
            var issues = ActionItems.Count;
            IssueCountText = issues.ToString();
            StatusLine = issues == 0
                ? "특별한 문제가 없습니다."
                : $"{issues}개 항목을 확인·정리할 수 있습니다.";
            HasResults = true;
            OnPropertyChanged(nameof(CanApply));

            if (AutoApplyAfterScan && ActionItems.Any(a => a.CanAutoApply))
            {
                await ApplySafeAsync(includeReview: false, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            StatusLine = $"검사 오류: {ex.Message}";
            ProgressPhaseTitle = "오류";
            ProgressLine = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanApply));
        }
    }

    public async Task ApplySafeAsync(bool includeReview, CancellationToken cancellationToken = default)
    {
        if (_lastScan is null || IsBusy)
        {
            StatusLine = "먼저 검사를 실행하세요.";
            return;
        }

        IsBusy = true;
        Progress = 0;
        ProgressPhaseTitle = "정리 중";
        ProgressLine = "권장 항목을 적용하는 중…";
        ProgressDetailLine = "";

        try
        {
            var displayedProgress = 0;
            var progress = new Progress<(int percent, string message)>(report =>
            {
                displayedProgress = Math.Max(displayedProgress, Math.Clamp(report.percent, 0, 100));
                UiDispatcher.Run(() =>
                {
                    Progress = displayedProgress;
                    ProgressPhaseTitle = $"정리 · {displayedProgress}%";
                    ProgressLine = report.message;
                    ProgressDetailLine = "대상 확인 · 안전 처리 · 결과 기록";
                });
            });

            // Auto-ack when applying from the single-opt-in flow.
            var result = await _service.ApplySafeItemsAsync(_lastScan, includeReview, progress, cancellationToken);
            StatusLine = result.Message;
            SummaryLine = $"정리 완료 · 적용 {result.AppliedCount}개 · 건너뜀 {result.SkippedCount}개";
            Progress = 100;
            ProgressPhaseTitle = "정리 완료 · 100%";
            ProgressLine = result.Message;
            ProgressDetailLine = "모든 처리 단계 완료";
        }
        catch (Exception ex)
        {
            StatusLine = $"적용 오류: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanApply));
        }
    }

    public Task RollbackAsync()
    {
        if (_lastScan is null)
        {
            StatusLine = "되돌릴 검사 기록이 없습니다.";
            return Task.CompletedTask;
        }

        var result = CareRollbackService.Rollback(_lastScan.AuditFolder);
        StatusLine = result.Message;
        return Task.CompletedTask;
    }

    private void RebuildActionItems(CareScanResult scan)
    {
        ActionItems.Clear();
        ResultGroups.Clear();

        var ordered = scan.Findings
            .OrderByDescending(x => RiskRank(x.RiskLabel))
            .ThenBy(x => x.Title)
            .ToArray();
        var actionable = ordered
            .Where(x => (!string.Equals(x.RiskLabel, "안전", StringComparison.Ordinal)
                         && !string.Equals(x.RiskCode, "unavailable", StringComparison.OrdinalIgnoreCase))
                        || x.CanAutoApply)
            .ToArray();

        foreach (var finding in actionable)
        {
            var safeCleanup = finding.CanAutoApply && string.Equals(finding.RiskLabel, "안전", StringComparison.Ordinal);
            ActionItems.Add(new CareActionItem
            {
                Title = finding.Title,
                Detail = finding.Detail,
                RiskLabel = safeCleanup ? "정리 가능" : finding.RiskLabel,
                ActionHint = finding.CanAutoApply
                    ? $"자동 처리 기준 통과 · 신뢰도 {finding.Confidence:P0}"
                    : $"확인 후 조치 · 신뢰도 {finding.Confidence:P0}",
                FixLabel = finding.CanAutoApply ? "정리" : "확인",
                CanAutoApply = finding.CanAutoApply,
                RiskOpacity = safeCleanup ? 0.75 : finding.RiskLabel is "주의" ? 1.0 : 0.85
            });
        }

        foreach (var group in ordered.GroupBy(finding => GetGroupTitle(finding.Id)))
        {
            var groupActionable = group.Count(finding =>
                (!string.Equals(finding.RiskLabel, "안전", StringComparison.Ordinal)
                 && !string.Equals(finding.RiskCode, "unavailable", StringComparison.OrdinalIgnoreCase))
                || finding.CanAutoApply);
            ResultGroups.Add(new CareResultGroup
            {
                Title = group.Key,
                Detail = groupActionable == 0
                    ? $"{group.Count()}개 확인 · 정상"
                    : $"{group.Count()}개 확인 · 조치 {groupActionable}개"
            });
        }

        ActionableCountText = actionable.Length.ToString();
        SafeCountText = ordered.Count(finding => string.Equals(finding.RiskLabel, "안전", StringComparison.Ordinal)).ToString();
        CheckedCountText = ordered.Length.ToString();
        CleanupListHint = actionable.Length == 0
            ? "정리할 항목이 없습니다. 현재 상태가 양호합니다."
            : "아래 항목만 정리 대상입니다. 정상 항목은 요약에만 표시합니다.";
    }

    private static string GetGroupTitle(string id) => id.Split('.', 2)[0] switch
    {
        "junk" or "disk" => "저장 공간",
        "privacy" => "개인정보",
        "opt" or "reg" or "shortcut" => "성능",
        "net" => "인터넷",
        "vuln" => "보안",
        "stability" => "안정성",
        _ => "기타"
    };

    private static int RiskRank(string label) => label switch
    {
        "주의" => 3,
        "확인 필요" => 2,
        "안전" => 0,
        _ => 1
    };

    private static string SimplifySummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return "검사 완료";
        }

        // Drop long audit-path noise if present.
        var cut = summary.IndexOf("감사", StringComparison.Ordinal);
        if (cut > 12)
        {
            return summary[..cut].Trim(' ', '·', '-', '—');
        }

        return summary.Length > 120 ? summary[..117] + "…" : summary;
    }

    private void ApplyModeDefaults()
    {
        if (SelectedModeIndex == 0)
        {
            foreach (var task in ScanTasks)
            {
                task.IsSelected = task.IncludedInSmart;
            }

            StatusLine = "빠른 검사: 자주 쓰는 항목을 확인합니다.";
            return;
        }

        foreach (var task in ScanTasks)
        {
            task.IsSelected = true;
        }

        StatusLine = "전체 검사: 원하는 항목을 고른 뒤 시작하세요.";
    }
}

/// <summary>ASC-style glance row for post-scan actions.</summary>
public sealed class CareActionItem
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string RiskLabel { get; init; } = "";
    public string ActionHint { get; init; } = "";
    public string FixLabel { get; init; } = "";
    public bool CanAutoApply { get; init; }
    public double RiskOpacity { get; init; } = 1.0;
}

public sealed class CareResultGroup
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
}