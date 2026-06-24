using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class OperationProgressHub
{
    private readonly object _gate = new();
    private readonly List<OperationProgressEvent> _events = new();

    public static OperationProgressHub Shared { get; } = new();

    public event EventHandler<OperationProgressSnapshot>? SnapshotChanged;

    public OperationProgressSnapshot CurrentSnapshot
    {
        get
        {
            lock (_gate)
            {
                return BuildSnapshot();
            }
        }
    }

    public void Publish(string operationId, string source, string phase, string status, double progress, string message, bool canCancel = false)
    {
        OperationProgressSnapshot snapshot;
        lock (_gate)
        {
            _events.Add(new OperationProgressEvent(
                DateTimeOffset.Now,
                operationId,
                source,
                phase,
                status,
                Math.Clamp(progress, 0, 100),
                message,
                canCancel));

            if (_events.Count > 200)
            {
                _events.RemoveRange(0, _events.Count - 200);
            }

            snapshot = BuildSnapshot();
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }

    public void PublishStartupState()
    {
        if (CurrentSnapshot.Events.Count > 0)
        {
            return;
        }

        Publish("startup", "App", "Boot", "Ready", 100, "앱 진행 상태 허브가 준비되었습니다.");
    }

    public void Clear()
    {
        OperationProgressSnapshot snapshot;
        lock (_gate)
        {
            _events.Clear();
            snapshot = BuildSnapshot();
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }

    private OperationProgressSnapshot BuildSnapshot()
    {
        var latest = _events.LastOrDefault();
        if (latest is null)
        {
            return new OperationProgressSnapshot(
                "",
                "대기",
                0,
                false,
                false,
                Array.Empty<OperationProgressEvent>());
        }

        var active = !string.Equals(latest.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(latest.Status, "Failed", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(latest.Status, "Ready", StringComparison.OrdinalIgnoreCase);

        return new OperationProgressSnapshot(
            latest.OperationId,
            latest.Status,
            latest.Progress,
            active,
            latest.CanCancel,
            _events.OrderByDescending(x => x.Timestamp).Take(50).ToArray());
    }
}
