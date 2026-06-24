using System.Collections.ObjectModel;
using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.SystemCare;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class SystemCareViewModel : ObservableObject
{
    private readonly SystemCareService _service = new();

    private int _selectedModeIndex;
    private string _statusLine = "스마트 검사는 자주 쓰는 항목을 빠르게 확인합니다. 정밀 점검는 원하는 항목만 골라 검사합니다.";
    private string _summaryLine = "";
    private string _healthLine = "";
    private string _progressLine = "";
    private double _progress;
    private bool _isBusy;
    private bool _hasResults;
    private CareScanResult? _lastScan;
    private IReadOnlyList<string> _findingLines = Array.Empty<string>();

    public ObservableCollection<CareScanTaskItem> ScanTasks { get; } = new();

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
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public bool HasResults { get => _hasResults; private set => Set(ref _hasResults, value); }
    public IReadOnlyList<string> FindingLines { get => _findingLines; private set => Set(ref _findingLines, value); }

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
                ? "스마트 검사 항목이 없습니다."
                : "정밀 점검할 항목을 체크리스트에서 선택하세요.";
            return;
        }

        IsBusy = true;
        HasResults = false;
        Progress = 0;
        SummaryLine = "";
        FindingLines = Array.Empty<string>();
        ProgressLine = mode == CareScanMode.Smart ? "스마트 검사를 시작합니다…" : "정밀 점검를 시작합니다…";

        try
        {
            var progress = new Progress<(int percent, string message)>(report =>
            {
                UiDispatcher.Run(() =>
                {
                    Progress = report.percent;
                    ProgressLine = report.message;
                });
            });

            _lastScan = await _service.ScanByTasksAsync(mode, taskIds, progress, cancellationToken);
            SummaryLine = _lastScan.Summary;
            HealthLine = $"건강 점수 {_lastScan.HealthScore}점 ({_lastScan.HealthGrade}) · 감사 체인 {(_lastScan.AuditChainValid ? "정상" : "주의")} · {_lastScan.AuditFolder}";
            FindingLines = _lastScan.Findings.Count == 0
                ? new[] { "검사 결과 특이 항목이 없습니다." }
                : _lastScan.Findings.Select(f => $"[{f.RiskLabel}] {f.Title} — {f.Detail}").ToArray();
            StatusLine = $"{_lastScan.ModuleTitle} 완료 · {taskIds.Length}개 항목";
            HasResults = true;
        }
        catch (Exception ex)
        {
            StatusLine = $"검사 오류: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task PreviewChangesAsync()
    {
        if (_lastScan is null)
        {
            StatusLine = "먼저 검사를 실행하세요.";
            return Task.CompletedTask;
        }

        var applyable = _lastScan.Findings.Count(f => f.CanAutoApply);
        var review = _lastScan.Findings.Count(f => f.RiskCode == "review");
        StatusLine = $"변경 전 확인 · 바로 적용 가능 {applyable}개 · 확인 필요 {review}개 · 기록 폴더: {_lastScan.AuditFolder}";
        return Task.CompletedTask;
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
        ProgressLine = "적용 준비 중…";

        try
        {
            var progress = new Progress<(int percent, string message)>(report =>
            {
                UiDispatcher.Run(() =>
                {
                    Progress = report.percent;
                    ProgressLine = report.message;
                });
            });

            var result = await _service.ApplySafeItemsAsync(_lastScan, includeReview, progress, cancellationToken);
            StatusLine = result.Message;
            SummaryLine = $"적용 {result.AppliedCount}개 · 건너뜀 {result.SkippedCount}개";
        }
        catch (Exception ex)
        {
            StatusLine = $"적용 오류: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task RollbackAsync()
    {
        if (_lastScan is null)
        {
            StatusLine = "롤백할 검사 기록이 없습니다.";
            return Task.CompletedTask;
        }

        var result = CareRollbackService.Rollback(_lastScan.AuditFolder);
        StatusLine = result.Message;
        return Task.CompletedTask;
    }

    private void ApplyModeDefaults()
    {
        if (SelectedModeIndex == 0)
        {
            foreach (var task in ScanTasks)
            {
                task.IsSelected = task.IncludedInSmart;
            }

            StatusLine = "스마트 검사: 자주 쓰는 항목을 빠르게 확인합니다. [검사 시작]을 누르세요.";
            return;
        }

        foreach (var task in ScanTasks)
        {
            task.IsSelected = true;
        }

        StatusLine = "정밀 점검: 아래 체크리스트에서 원하는 항목만 선택한 뒤 검사하세요.";
    }
}