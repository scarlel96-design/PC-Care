using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class RepairWorkbenchViewModel : ObservableObject
{
    private readonly RepairHelperClient _client = new();
    private readonly OperationProgressHub _progress = OperationProgressHub.Shared;

    private string _status = "대기 중";
    private string _result = "Dry-run으로 계획을 먼저 확인하세요.";

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Result { get => _result; private set => Set(ref _result, value); }

    public async Task RunAsync(RepairActionDescriptor action, string target, bool dryRun, bool riskAccepted, CancellationToken cancellationToken)
    {
        if (action.RequiresTarget && string.IsNullOrWhiteSpace(target))
        {
            Status = "blocked";
            Result = "이 작업은 대상 InstanceId가 필요합니다.";
            return;
        }

        Status = dryRun ? "dry-run 요청 중" : "실행 요청 중";
        var operationId = $"repair-{action.Id}-{DateTimeOffset.Now:yyyyMMddHHmmss}";
        _progress.Publish(operationId, "RepairHelper", action.Id, "Running", 10, Status, canCancel: !dryRun);

        var response = await _client.SendAsync(new RepairHelperRequest
        {
            Action = action.Id,
            Target = string.IsNullOrWhiteSpace(target) ? action.DefaultTarget : target.Trim(),
            DryRun = dryRun,
            RiskAccepted = riskAccepted
        }, cancellationToken);

        Status = response.Status;
        var phase = response.Status is "dry-run" or "ok" or "planned" ? "Completed" : "Failed";
        _progress.Publish(operationId, "RepairHelper", action.Id, phase, 100, response.Message, canCancel: false);
        Result =
            $"상태: {response.Status}\n" +
            $"메시지: {response.Message}\n" +
            $"종료 코드: {response.ExitCode}\n" +
            $"관리자 권한: {response.Elevated}\n" +
            $"로그: {response.LogPath}\n" +
            $"STDOUT: {response.Stdout}\n" +
            $"STDERR: {response.Stderr}";
    }
}
