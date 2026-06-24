using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class VerifiedRepairViewModel : ObservableObject
{
    private readonly DriverAudioSystemRepairIntelligenceService _intelligence = new();
    private readonly RepairVerificationEngine _verification = new();

    private IReadOnlyList<IntelligentRepairPlan> _plans = Array.Empty<IntelligentRepairPlan>();
    private IntelligentRepairPlan? _selectedPlan;
    private string _target = "";
    private bool _riskAccepted;
    private string _status = "대기";
    private string _summary = "검증형 복구 계획을 선택하세요.";
    private IReadOnlyList<RepairEvidence> _evidence = Array.Empty<RepairEvidence>();
    private IReadOnlyList<string> _nextActions = Array.Empty<string>();

    public IReadOnlyList<IntelligentRepairPlan> Plans { get => _plans; private set => Set(ref _plans, value); }
    public IntelligentRepairPlan? SelectedPlan { get => _selectedPlan; set => Set(ref _selectedPlan, value); }
    public string Target { get => _target; set => Set(ref _target, value); }
    public bool RiskAccepted { get => _riskAccepted; set => Set(ref _riskAccepted, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public IReadOnlyList<RepairEvidence> Evidence { get => _evidence; private set => Set(ref _evidence, value); }
    public IReadOnlyList<string> NextActions { get => _nextActions; private set => Set(ref _nextActions, value); }

    public void Load()
    {
        Plans = _intelligence.BuildPlans();
        SelectedPlan = Plans.FirstOrDefault();
    }

    public async Task RunAsync(bool apply, CancellationToken cancellationToken)
    {
        if (SelectedPlan is null)
        {
            Status = "blocked";
            Summary = "선택된 복구 계획이 없습니다.";
            return;
        }

        if (SelectedPlan.RequiresTarget && string.IsNullOrWhiteSpace(Target))
        {
            Status = "blocked";
            Summary = "이 계획은 대상 InstanceId가 필요합니다.";
            return;
        }

        if (apply && !RiskAccepted)
        {
            Status = "blocked";
            Summary = "실제 복구 실행은 위험 확인 체크가 필요합니다.";
            return;
        }

        Status = apply ? "apply+verify running" : "dry-run+verify running";
        var result = await _verification.VerifyAsync(SelectedPlan, Target, apply, RiskAccepted, cancellationToken);
        Status = result.Status;
        Summary = result.Summary;
        Evidence = result.Evidence;
        NextActions = result.NextActions;
    }
}
