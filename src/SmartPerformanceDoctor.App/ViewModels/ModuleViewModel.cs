using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class ModuleViewModel : ObservableObject
{
    private readonly EngineClient _engineClient = new();
    private readonly OperationProgressHub _progressHub = OperationProgressHub.Shared;

    private string _status = "대기 중";
    private int _progress;
    private string _latestMessage = "작업을 시작하면 여기에 진단 흐름이 표시됩니다.";
    private IReadOnlyList<EngineEvent> _events = Array.Empty<EngineEvent>();
    private IntelligenceSummary? _intelligence;

    public string Status { get => _status; private set => Set(ref _status, value); }
    public int Progress { get => _progress; private set => Set(ref _progress, value); }
    public string LatestMessage { get => _latestMessage; private set => Set(ref _latestMessage, value); }
    public IReadOnlyList<EngineEvent> Events { get => _events; private set => Set(ref _events, value); }
    public IntelligenceSummary? Intelligence { get => _intelligence; private set => Set(ref _intelligence, value); }
    public string? HtmlReportPath { get; private set; }
    public string? JsonReportPath { get; private set; }

    public void ResetForModule(ModuleDescriptor module)
    {
        Status = "대기 중";
        Progress = 0;
        LatestMessage = $"{module.Title} 실행 버튼을 누르면 최신 진단 엔진이 점검을 시작합니다.";
        Events = Array.Empty<EngineEvent>();
        Intelligence = null;
        HtmlReportPath = null;
        JsonReportPath = null;
    }

    public void SetFailure(string message)
    {
        Status = "failed";
        LatestMessage = message;
        Progress = Math.Max(Progress, 5);
    }

    public async Task RunAsync(
        ModuleDescriptor module,
        CancellationToken cancellationToken,
        Action<EngineEvent>? onProgress = null)
    {
        Status = "실행 중";
        Progress = 3;
        LatestMessage = $"{module.Title} 엔진을 준비 중입니다.";

        var operationId = $"module-{module.Id}-{DateTimeOffset.Now:yyyyMMddHHmmss}";
        _progressHub.Publish(operationId, "Core", "Start", "Running", 3, LatestMessage, canCancel: true);

        var request = new EngineEnvelope
        {
            Method = "run_module",
            Params = new Dictionary<string, string>
            {
                ["module"] = module.Id,
                ["risk"] = module.RiskLevel
            }
        };

        var liveEvents = new List<EngineEvent>();
        var response = await _engineClient.SendAsync(request, cancellationToken, evt =>
        {
            liveEvents.Add(evt);
            Events = liveEvents.ToArray();
            Progress = evt.Progress;
            LatestMessage = DiagnosticMessageLocalizer.Localize(evt.Message);
            _progressHub.Publish(operationId, "Core", module.Id, "Running", evt.Progress, evt.Message, canCancel: true);
            onProgress?.Invoke(evt);
        }).ConfigureAwait(false);

        Events = response.Events;
        Intelligence = response.Intelligence;
        HtmlReportPath = response.HtmlReportPath ?? response.ReportPath;
        JsonReportPath = response.JsonReportPath;
        Progress = response.Status == "ok" ? 100 : Math.Max(Progress, 75);
        Status = response.Status;
        LatestMessage = response.Message;

        var finalPhase = response.Status == "ok" ? "Completed" : "Failed";
        _progressHub.Publish(operationId, "Core", module.Id, finalPhase, Progress, LatestMessage, canCancel: false);
    }
}
