using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SmartPerformanceDoctor.App.Models.Update;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.Update;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class UpdateStatusViewModel : ObservableObject
{
    private readonly UpdateChannelService _channel = new();
    private readonly UpdateInstallerService _installer = new();

    private string _currentVersion = AppInfo.Version;
    private string _targetVersion = "-";
    private string _statusLine = "업데이트 파일을 선택하세요.";
    private string _phaseLine = "대기";
    private string _actionLine = "";
    private string _detailLine = "";
    private string _stepLine = "";
    private string _fileLine = "";
    private string _elapsedLine = "";
    private string _verifyLine = "";
    private string _selectedPackagePath = "";
    private double _progress;
    private bool _isBusy;
    private bool _canApply;
    private UpdatePackageInspection? _lastInspection;
    private IReadOnlyList<string> _changes = Array.Empty<string>();
    private IReadOnlyList<UpdateHistoryEntry> _history = Array.Empty<UpdateHistoryEntry>();

    public ObservableCollection<UpdateActivityEntry> ActivityLog { get; } = new();

    public string CurrentVersion { get => _currentVersion; private set => Set(ref _currentVersion, value); }
    public string TargetVersion { get => _targetVersion; private set => Set(ref _targetVersion, value); }
    public string StatusLine { get => _statusLine; private set => Set(ref _statusLine, value); }
    public string PhaseLine { get => _phaseLine; private set => Set(ref _phaseLine, value); }
    public string ActionLine { get => _actionLine; private set => Set(ref _actionLine, value); }
    public string DetailLine { get => _detailLine; private set => Set(ref _detailLine, value); }
    public string StepLine { get => _stepLine; private set => Set(ref _stepLine, value); }
    public string FileLine { get => _fileLine; private set => Set(ref _fileLine, value); }
    public string ElapsedLine { get => _elapsedLine; private set => Set(ref _elapsedLine, value); }
    public string VerifyLine { get => _verifyLine; private set => Set(ref _verifyLine, value); }
    public string SelectedPackagePath { get => _selectedPackagePath; private set => Set(ref _selectedPackagePath, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public bool CanApply { get => _canApply; private set => Set(ref _canApply, value); }
    public IReadOnlyList<string> Changes { get => _changes; private set => Set(ref _changes, value); }
    public IReadOnlyList<UpdateHistoryEntry> History { get => _history; private set => Set(ref _history, value); }

    public void Refresh()
    {
        RunOnUi(() =>
        {
            CurrentVersion = AppInfo.Version;
            var verify = AppVersionService.VerifyInstalledVersion(null);
            VerifyLine = verify.Details;
            History = _channel.LoadHistory();

            if (string.IsNullOrWhiteSpace(SelectedPackagePath))
            {
                var status = _channel.BuildStatus();
                StatusLine = status.Message;
                TargetVersion = status.LatestVersion;
                CanApply = false;
                Changes = Array.Empty<string>();
                return;
            }
        });

        if (!string.IsNullOrWhiteSpace(SelectedPackagePath))
        {
            _ = InspectSelectedAsync();
        }
    }

    public async Task SetSelectedPackageAsync(string? path)
    {
        RunOnUi(() =>
        {
            SelectedPackagePath = path ?? "";
            _lastInspection = null;
            _installer.InvalidateInspectionCache();
        });

        if (string.IsNullOrWhiteSpace(path))
        {
            Refresh();
            return;
        }

        await InspectSelectedAsync().ConfigureAwait(false);
    }

    public async Task<UpdateApplyResult?> ApplySelectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            AppendLog("이미 작업이 진행 중입니다.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(SelectedPackagePath))
        {
            RunOnUi(() =>
            {
                StatusLine = "업데이트 파일을 먼저 선택하세요.";
                ActionLine = "파일 없음";
            });
            AppendLog("업데이트 파일을 먼저 선택하세요.");
            return null;
        }

        RunOnUi(() => BeginWork("적용", "업데이트 적용을 준비합니다…"));

        try
        {
            var inspection = await ResolveInspectionAsync(cancellationToken).ConfigureAwait(false);
            if (!inspection.CanApply)
            {
                RunOnUi(() =>
                {
                    StatusLine = inspection.Message;
                    ActionLine = "적용 불가";
                    DetailLine = inspection.Status;
                    PhaseLine = "중단";
                    IsBusy = false;
                    CanApply = false;
                });
                AppendLog(inspection.Message);
                return null;
            }

            AppendLog($"적용 시작 · {inspection.Manifest?.FromVersion} → {inspection.Manifest?.ToVersion}");
            var progress = CreateProgressHandler();
            var result = await _installer.ApplyAsync(inspection, progress, cancellationToken).ConfigureAwait(false);

            var verify = AppVersionService.VerifyInstalledVersion(result.ToVersion);
            RunOnUi(() =>
            {
                StatusLine = result.Message;
                TargetVersion = result.ToVersion;
                CurrentVersion = AppInfo.Version;
                PhaseLine = result.Success ? "완료" : "실패";
                ActionLine = result.Success ? "업데이트 적용됨" : "적용 실패";
                DetailLine = result.Success
                    ? $"즉시 적용 {result.FilesApplied}개 · 보류 {result.FilesDeferred}개"
                    : result.Message;
                VerifyLine = verify.Details;
                Progress = result.Success ? 100 : Progress;
            });
            AppendLog(result.Success ? "적용 완료" : $"실패: {result.Message}");

            if (result.Success && result.RestartScheduled)
            {
                AppendLog("앱을 종료한 뒤 보류 파일을 적용합니다.");
                RunOnUi(() => History = _channel.LoadHistory());
                UpdateInstallerService.LaunchPendingRestart();
                RunOnUi(() => Application.Current.Exit());
            }
            else
            {
                RunOnUi(() => History = _channel.LoadHistory());
            }

            return result;
        }
        catch (Exception ex)
        {
            RunOnUi(() =>
            {
                StatusLine = $"업데이트 오류: {ex.Message}";
                ActionLine = "오류";
                PhaseLine = "실패";
            });
            AppendLog($"업데이트 오류: {ex.Message}");
            return null;
        }
        finally
        {
            RunOnUi(() =>
            {
                IsBusy = false;
                CanApply = _lastInspection?.CanApply == true;
            });
        }
    }

    private async Task<UpdatePackageInspection> ResolveInspectionAsync(CancellationToken cancellationToken)
    {
        UpdatePackageInspection? cached = null;
        RunOnUi(() =>
        {
            if (_lastInspection is not null
                && string.Equals(_lastInspection.PackagePath, SelectedPackagePath, StringComparison.OrdinalIgnoreCase)
                && _lastInspection.IsValid)
            {
                cached = _lastInspection;
            }
        });

        if (cached is not null)
        {
            AppendLog("검사 결과를 사용합니다.");
            return cached;
        }

        AppendLog("적용 전 패키지를 다시 검사합니다…");
        var progress = CreateProgressHandler();
        var inspection = await _installer.InspectAsync(SelectedPackagePath, progress, cancellationToken).ConfigureAwait(false);
        RunOnUi(() => _lastInspection = inspection);
        return inspection;
    }

    private async Task InspectSelectedAsync()
    {
        string? packagePath = null;
        RunOnUi(() => packagePath = SelectedPackagePath);
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return;
        }

        RunOnUi(() => BeginWork("검사", "업데이트 패키지를 검사합니다…"));
        UpdatePackageInspection? inspection = null;

        try
        {
            var progress = CreateProgressHandler();
            inspection = await _installer.InspectAsync(packagePath, progress, cancellationToken: default).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RunOnUi(() =>
            {
                StatusLine = $"검사 오류: {ex.Message}";
                ActionLine = "오류";
                PhaseLine = "실패";
            });
            AppendLog($"검사 오류: {ex.Message}");
        }

        if (inspection is not null)
        {
            RunOnUi(() =>
            {
                _lastInspection = inspection;
                ApplyInspection(inspection);
                IsBusy = false;
            });
        }
        else
        {
            RunOnUi(() => IsBusy = false);
        }
    }

    private IProgress<UpdateProgressReport> CreateProgressHandler() =>
        new Progress<UpdateProgressReport>(report => RunOnUi(() => ApplyProgress(report), blocking: false));

    private void BeginWork(string phase, string detail)
    {
        IsBusy = true;
        CanApply = false;
        Progress = 0;
        PhaseLine = phase;
        ActionLine = "진행 중";
        DetailLine = detail;
        StepLine = phase;
        FileLine = "";
        ElapsedLine = "";
        ActivityLog.Clear();
        AppendLogCore(detail);
    }

    private void ApplyProgress(UpdateProgressReport report)
    {
        Progress = report.Percent;
        PhaseLine = report.Phase;
        ActionLine = report.Action;
        DetailLine = report.Detail;
        StepLine = string.IsNullOrWhiteSpace(report.StepLabel) ? report.Phase : $"{report.StepLabel} · {report.Phase}";
        FileLine = string.IsNullOrWhiteSpace(report.FileLabel)
            ? ""
            : $"{report.FileLabel}" + (string.IsNullOrWhiteSpace(report.CurrentFile) ? "" : $" · {report.CurrentFile}");
        ElapsedLine = $"경과 {report.Elapsed:mm\\:ss}";

        var log = string.IsNullOrWhiteSpace(report.CurrentFile)
            ? $"{report.Action}: {report.Detail}"
            : $"{report.Action}: {report.CurrentFile} — {report.Detail}";
        AppendLogCore(log);
    }

    private void AppendLog(string message) => RunOnUi(() => AppendLogCore(message), blocking: false);

    private void AppendLogCore(string message)
    {
        ActivityLog.Insert(0, new UpdateActivityEntry
        {
            Time = DateTime.Now.ToString("HH:mm:ss"),
            Message = message
        });

        while (ActivityLog.Count > 80)
        {
            ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }

    private void ApplyInspection(UpdatePackageInspection inspection)
    {
        TargetVersion = inspection.Manifest?.ToVersion ?? "-";
        StatusLine = inspection.Message;
        CanApply = inspection.CanApply;
        Changes = inspection.Manifest?.Changes.Count > 0
            ? inspection.Manifest.Changes
            : Array.Empty<string>();
        PhaseLine = inspection.Status;
        ActionLine = inspection.CanApply ? "적용 가능" : "적용 불가";
        DetailLine = Path.GetFileName(SelectedPackagePath);
        Progress = inspection.CanApply ? 100 : 0;
        StepLine = inspection.CanApply ? "검사 완료" : inspection.Status;
        AppendLogCore(inspection.CanApply
            ? "이 패키지는 적용할 수 있습니다. [업데이트 적용]을 누르세요."
            : inspection.Message);
    }

    private static void RunOnUi(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.High, bool blocking = true)
    {
        if (UiDispatcher.HasThreadAccess || UiDispatcher.Queue is null)
        {
            action();
            return;
        }

        if (!blocking)
        {
            UiDispatcher.Run(action, priority);
            return;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        UiDispatcher.Run(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, priority);
        tcs.Task.GetAwaiter().GetResult();
    }
}