namespace SmartPerformanceDoctor.App.Services;

public sealed class ProgressEventBridgeService
{
    private readonly OperationProgressHub _hub = OperationProgressHub.Shared;

    public void AttachAppLifecycle()
    {
        _hub.PublishStartupState();
    }

    public void PublishCoreProbeStarted()
    {
        _hub.Publish("core-probe", "Core", "Probe", "Running", 15, "Core Dashboard Bridge 상태를 확인하는 중입니다.", canCancel: false);
    }

    public void PublishCoreProbeCompleted()
    {
        _hub.Publish("core-probe", "Core", "Probe", "Completed", 100, "Core Dashboard Bridge 확인이 완료되었습니다.", canCancel: false);
    }

    public void PublishRepairDryRunStarted(string action)
    {
        _hub.Publish($"repair-{action}", "RepairHelper", "DryRun", "Running", 10, $"{action} dry-run 요청 준비 중입니다.", canCancel: true);
    }

    public void PublishRepairDryRunCompleted(string action)
    {
        _hub.Publish($"repair-{action}", "RepairHelper", "DryRun", "Completed", 100, $"{action} dry-run 결과 수신 완료.", canCancel: false);
    }
}
