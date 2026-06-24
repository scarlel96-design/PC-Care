using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class RepairHelperE2EGateViewModel : ObservableObject
{
    private readonly RepairHelperE2EGateService _service = new();

    private string _status = "대기";
    private string _summary = "복구 품질 점검을 실행하세요.";
    private IReadOnlyList<RepairHelperE2ECheckItem> _checks = Array.Empty<RepairHelperE2ECheckItem>();
    private IReadOnlyList<RepairRootCauseScore> _scores = Array.Empty<RepairRootCauseScore>();

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public IReadOnlyList<RepairHelperE2ECheckItem> Checks { get => _checks; private set => Set(ref _checks, value); }
    public IReadOnlyList<RepairRootCauseScore> Scores { get => _scores; private set => Set(ref _scores, value); }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Status = "실행 중";
        Summary = "복구 품질 시뮬레이션 점검을 실행 중입니다...";
        var result = await _service.RunDryRunGateAsync(cancellationToken);
        Status = result.Status;
        Summary = result.Summary;
        Checks = result.Checks;
        Scores = _service.Score(result.Checks);
    }
}
