using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class ProgressHudViewModel : ObservableObject
{
    private readonly OperationProgressHub _hub = OperationProgressHub.Shared;

    private string _activeOperationId = "";
    private string _overallStatus = "대기";
    private double _overallProgress;
    private bool _hasActiveOperation;
    private bool _canCancel;
    private IReadOnlyList<OperationProgressEvent> _events = Array.Empty<OperationProgressEvent>();

    public string ActiveOperationId { get => _activeOperationId; private set => Set(ref _activeOperationId, value); }
    public string OverallStatus { get => _overallStatus; private set => Set(ref _overallStatus, value); }
    public double OverallProgress { get => _overallProgress; private set => Set(ref _overallProgress, value); }
    public bool HasActiveOperation { get => _hasActiveOperation; private set => Set(ref _hasActiveOperation, value); }
    public bool CanCancel { get => _canCancel; private set => Set(ref _canCancel, value); }
    public IReadOnlyList<OperationProgressEvent> Events { get => _events; private set => Set(ref _events, value); }

    public ProgressHudViewModel()
    {
        _hub.SnapshotChanged += (_, snapshot) => Apply(snapshot);
        _hub.PublishStartupState();
        Apply(_hub.CurrentSnapshot);
    }

    public void Refresh()
    {
        Apply(_hub.CurrentSnapshot);
    }

    public void Clear()
    {
        _hub.Clear();
        Apply(_hub.CurrentSnapshot);
    }

    public void SimulateCoreProbe()
    {
        _hub.Publish("core-probe", "Core", "Probe", "Running", 10, "Core probe 시작", canCancel: false);
        _hub.Publish("core-probe", "Core", "Probe", "Running", 45, "Core engine/report/runtime 상태 수집", canCancel: false);
        _hub.Publish("core-probe", "Core", "Probe", "Completed", 100, "Core probe 완료", canCancel: false);
    }

    private void Apply(OperationProgressSnapshot snapshot)
    {
        ActiveOperationId = snapshot.ActiveOperationId;
        OverallStatus = snapshot.OverallStatus;
        OverallProgress = snapshot.OverallProgress;
        HasActiveOperation = snapshot.HasActiveOperation;
        CanCancel = snapshot.CanCancel;
        Events = snapshot.Events;
    }
}
