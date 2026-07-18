using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class UnifiedCareViewModel : ObservableObject
{
    private readonly UnifiedCareService _service = new();
    private readonly object _stepGate = new();

    private string _scope = "quick";
    private bool _includeRepair;
    private bool _riskAccepted;
    private bool _isRunning;
    private string _status = "대기";
    private string _summary = "범위와 실행 방식을 선택한 뒤 시작하세요.";
    private string _sessionLine = "";
    private int _progress;

    public string Scope { get => _scope; set => Set(ref _scope, value); }
    public bool IncludeRepair { get => _includeRepair; set => Set(ref _includeRepair, value); }
    public bool RiskAccepted { get => _riskAccepted; set => Set(ref _riskAccepted, value); }
    public bool IsRunning { get => _isRunning; private set => Set(ref _isRunning, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public string SessionLine { get => _sessionLine; private set => Set(ref _sessionLine, value); }
    public int Progress { get => _progress; private set => Set(ref _progress, value); }
    public ObservableCollection<CareStepResult> Steps { get; } = new();

    public void ApplyScope(string scope)
    {
        Scope = string.IsNullOrWhiteSpace(scope) ? "quick" : scope;
    }

    public void ResetSession()
    {
        if (UiDispatcher.HasThreadAccess)
        {
            ResetSessionCore();
            return;
        }

        UiDispatcher.Run(ResetSessionCore, DispatcherQueuePriority.High);
    }

    public void ResetSessionCore()
    {
        IsRunning = false;
        Status = "대기";
        Summary = "범위와 실행 방식을 선택한 뒤 시작하세요.";
        SessionLine = "";
        Progress = 0;
        Steps.Clear();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        Status = "실행 중";
        Progress = 8;
        Summary = IncludeRepair
            ? "점검 후 문제가 있으면 복구까지 진행합니다."
            : "점검만 수행합니다. PC에는 변경을 가하지 않습니다.";
        Steps.Clear();

        var liveSteps = new List<CareStepResult>();
        try
        {
            var result = await _service.RunAsync(
                new CareRequest(Scope, IncludeRepair, RiskAccepted),
                step =>
                {
                    lock (_stepGate)
                    {
                        liveSteps.Add(step);
                    }

                    var snapshot = liveSteps.ToArray();
                    UiDispatcher.Run(() =>
                    {
                        Steps.Clear();
                        // Keep the summary readable: the complete audit remains on disk,
                        // while this surface shows only the latest eight live steps.
                        for (var i = snapshot.Length - 1; i >= Math.Max(0, snapshot.Length - 8); i--)
                        {
                            Steps.Add(snapshot[i]);
                        }

                        if (step.Phase == "diagnosis" && step.Title.Contains('%'))
                        {
                            var pctStart = step.Title.LastIndexOf('—') + 1;
                            if (pctStart > 0 && int.TryParse(step.Title.AsSpan(pctStart).Trim().TrimEnd('%'), out var pct))
                            {
                                Progress = Math.Clamp(pct, Progress, 95);
                            }
                        }
                        else
                        {
                            Progress = Math.Min(95, Progress + 6);
                        }

                        Status = step.Title;
                        Summary = step.Detail;
                    }, DispatcherQueuePriority.High);
                },
                cancellationToken).ConfigureAwait(false);

            UiDispatcher.Run(() =>
            {
                Progress = 100;
                Status = result.Completed ? "완료" : "일부 완료";
                Summary = result.Summary;
                SessionLine = result.SessionId is not null
                    ? $"세션 {result.SessionId} · 점수 Δ {result.HealthDelta?.ToString() ?? "-"} · 감사 {(result.AuditChainValid ? "정상" : "주의")} · {result.AuditFolder}"
                    : "";
            }, DispatcherQueuePriority.High);
        }
        catch (OperationCanceledException)
        {
            UiDispatcher.Run(() =>
            {
                Status = "중단됨";
                Summary = "사용자 또는 화면 전환으로 작업이 중단되었습니다.";
            }, DispatcherQueuePriority.High);
        }
        catch (Exception ex)
        {
            UiDispatcher.Run(() =>
            {
                Status = "오류";
                Summary = $"실행 중 문제가 발생했습니다: {ex.Message}";
            }, DispatcherQueuePriority.High);
        }
        finally
        {
            UiDispatcher.Run(() => IsRunning = false, DispatcherQueuePriority.High);
        }
    }
}