using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class FinalLockViewModel : ObservableObject
{
    private readonly FinalRC2LockService _service = new();

    private string _status = "대기";
    private string _confidence = "";
    private string _summary = "Final RC2 Lock을 실행하세요.";
    private IReadOnlyList<FinalLockGateItem> _gates = Array.Empty<FinalLockGateItem>();
    private IReadOnlyList<string> _acceptanceCriteria = Array.Empty<string>();
    private IReadOnlyList<string> _remainingManualChecks = Array.Empty<string>();

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Confidence { get => _confidence; private set => Set(ref _confidence, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public IReadOnlyList<FinalLockGateItem> Gates { get => _gates; private set => Set(ref _gates, value); }
    public IReadOnlyList<string> AcceptanceCriteria { get => _acceptanceCriteria; private set => Set(ref _acceptanceCriteria, value); }
    public IReadOnlyList<string> RemainingManualChecks { get => _remainingManualChecks; private set => Set(ref _remainingManualChecks, value); }

    public void Evaluate()
    {
        var result = _service.Evaluate();
        Status = result.Status;
        Confidence = result.Confidence;
        Summary = result.Summary;
        Gates = result.Gates;
        AcceptanceCriteria = result.AcceptanceCriteria;
        RemainingManualChecks = result.RemainingManualChecks;
    }
}
