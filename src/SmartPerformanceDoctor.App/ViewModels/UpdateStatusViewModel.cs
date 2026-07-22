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
    private readonly GitHubReleaseUpdateService _github = new();

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
    private bool _canDownloadGitHub;
    private string _githubLine = "GitHub 릴리즈를 확인하려면 [확인]을 누르세요.";
    private RemoteUpdateCheckResult? _lastGitHubCheck;
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
    public bool CanDownloadGitHub { get => _canDownloadGitHub; private set => Set(ref _canDownloadGitHub, value); }
    public RemoteUpdateCheckResult? AvailableGitHubUpdate => _lastGitHubCheck;
    public bool IsGitHubUpdateAvailable => CanDownloadGitHub && _lastGitHubCheck is not null;
    public string GitHubLine { get => _githubLine; private set => Set(ref _githubLine, value); }
    public IReadOnlyList<string> Changes { get => _changes; private set => Set(ref _changes, value); }
    public IReadOnlyList<UpdateHistoryEntry> History { get => _history; private set => Set(ref _history, value); }

    public void SetStatus(string message) => RunOnUi(() => StatusLine = message);

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

    public async Task CheckGitHubAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            AppendLog("이미 작업이 진행 중입니다.");
            return;
        }

        RunOnUi(() => BeginWork("GitHub", "GitHub 릴리즈를 확인합니다…"));

        try
        {
            var check = await _github.CheckLatestAsync(cancellationToken).ConfigureAwait(false);
            RunOnUi(() =>
            {
                _lastGitHubCheck = check;
                OnPropertyChanged(nameof(AvailableGitHubUpdate));
                GitHubLine = check.Message;
                TargetVersion = check.LatestVersion;
                CanDownloadGitHub = check.Success
                    && UpdateVersionComparer.IsNewer(check.LatestVersion, CurrentVersion)
                    && !string.IsNullOrWhiteSpace(check.UpdateDownloadUrl);
                OnPropertyChanged(nameof(IsGitHubUpdateAvailable));
                StatusLine = check.Message;
                PhaseLine = check.Success ? "확인 완료" : "확인 실패";
                ActionLine = CanDownloadGitHub ? "다운로드 가능" : "다운로드 불가";
                DetailLine = check.UpdateFileName ?? UpdateRemoteConfig.ReleasesPageUrl;
                Changes = check.ReleaseNotesLines.Count > 0
                    ? check.ReleaseNotesLines
                    : CanDownloadGitHub
                        ? new[] { "수정 사항 정보를 불러오지 못했습니다. GitHub 릴리스 페이지에서 확인하세요." }
                        : Array.Empty<string>();
            });
            AppendLog(check.Message);
        }
        catch (Exception ex)
        {
            RunOnUi(() =>
            {
                GitHubLine = $"GitHub 확인 오류: {ex.Message}";
                CanDownloadGitHub = false;
                PhaseLine = "실패";
            });
            AppendLog($"GitHub 확인 오류: {ex.Message}");
        }
        finally
        {
            RunOnUi(() => IsBusy = false);
        }
    }

    public async Task<bool> DownloadGitHubUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            AppendLog("이미 작업이 진행 중입니다.");
            return false;
        }

        RemoteUpdateCheckResult? check = null;
        var canDownload = false;
        RunOnUi(() =>
        {
            check = _lastGitHubCheck;
            canDownload = CanDownloadGitHub;
        });

        if (check is null || !canDownload)
        {
            RunOnUi(() => GitHubLine = "다운로드할 GitHub 업데이트가 없습니다.");
            AppendLog("다운로드할 GitHub 업데이트가 없습니다.");
            return false;
        }

        RunOnUi(() => BeginWork("다운로드", "GitHub에서 업데이트 패키지를 받습니다…"));

        try
        {
            var progress = new Progress<string>(message => AppendLog(message));
            var result = await _github.DownloadUpdatePackageAsync(
                    check,
                    progress,
                    CreateDownloadProgressHandler(),
                    cancellationToken)
                .ConfigureAwait(false);

            RunOnUi(() =>
            {
                GitHubLine = result.Message;
                StatusLine = result.Message;
                PhaseLine = result.Success ? "다운로드 완료" : "다운로드 실패";
                ActionLine = result.Success ? "패키지 준비됨" : "실패";
                DetailLine = result.LocalPath ?? "";
                CanDownloadGitHub = false;
                OnPropertyChanged(nameof(IsGitHubUpdateAvailable));
            });
            AppendLog(result.Message);

            if (!result.Success || string.IsNullOrWhiteSpace(result.LocalPath))
            {
                return false;
            }

            await SetSelectedPackageAsync(result.LocalPath).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            RunOnUi(() =>
            {
                GitHubLine = $"다운로드 오류: {ex.Message}";
                PhaseLine = "실패";
            });
            AppendLog($"다운로드 오류: {ex.Message}");
            return false;
        }
        finally
        {
            RunOnUi(() => IsBusy = false);
        }
    }
    /// <summary>
    /// 사용자는 한 번만 승인하고, 이후 다운로드·무결성 검사·적용을 끊김 없이 진행합니다.
    /// </summary>
    public async Task<UpdateApplyResult?> DownloadAndApplyGitHubUpdateAsync(CancellationToken cancellationToken = default)
    {
        var downloaded = await DownloadGitHubUpdateAsync(cancellationToken).ConfigureAwait(false);
        if (!downloaded)
        {
            return null;
        }

        var canApply = false;
        RunOnUi(() => canApply = CanApply);
        if (!canApply)
        {
            return null;
        }

        return await ApplySelectedAsync(cancellationToken).ConfigureAwait(false);
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
                AppendLog("앱을 종료한 뒤 보류 파일을 적용합니다. (Program Files면 UAC 관리자 확인이 필요할 수 있습니다)");
                RunOnUi(() => History = _channel.LoadHistory());
                var launched = UpdateInstallerService.LaunchPendingRestart();
                if (launched)
                {
                    RunOnUi(() => Application.Current.Exit());
                }
                else
                {
                    AppendLog("관리자 권한 적용 시작 실패 또는 UAC 취소 · 앱을 종료하지 않고 보류 상태를 유지합니다.");
                    RunOnUi(() =>
                    {
                        StatusLine = "업데이트 마무리를 시작하지 못했습니다. 앱은 계속 사용할 수 있습니다.";
                        PhaseLine = "마무리 대기";
                    });
                }
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

    private IProgress<RemoteUpdateDownloadProgress> CreateDownloadProgressHandler() =>
        new Progress<RemoteUpdateDownloadProgress>(report => RunOnUi(() => ApplyDownloadProgress(report), blocking: false));

    private void ApplyDownloadProgress(RemoteUpdateDownloadProgress report)
    {
        PhaseLine = report.Phase;
        StepLine = report.Phase == "다운로드 중" ? "1/3 · 다운로드" : "2/3 · 보안 검증";
        ActionLine = report.Phase;
        if (report.TotalBytes is > 0)
        {
            Progress = Math.Min(95, report.DownloadedBytes * 100d / report.TotalBytes.Value);
            DetailLine = $"{FormatBytes(report.DownloadedBytes)} / {FormatBytes(report.TotalBytes.Value)}";
        }
        else
        {
            Progress = report.Phase == "검증 완료" ? 100 : 0;
            DetailLine = report.Phase == "보안 검증 중" ? "SHA-256 무결성을 확인하고 있습니다." : report.Phase;
        }
    }

    private static string FormatBytes(long value) => value switch
    {
        >= 1024L * 1024 * 1024 => $"{value / 1024d / 1024 / 1024:0.0} GB",
        >= 1024L * 1024 => $"{value / 1024d / 1024:0.0} MB",
        >= 1024 => $"{value / 1024d:0.0} KB",
        _ => $"{value} B"
    };

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