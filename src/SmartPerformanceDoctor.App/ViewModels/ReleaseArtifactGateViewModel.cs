using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.ViewModels;

public sealed class ReleaseArtifactGateViewModel : ObservableObject
{
    private readonly ReleaseArtifactVerificationService _service = new();

    private string _status = "대기";
    private string _confidence = "";
    private string _summary = "릴리즈 산출물 게이트를 실행하세요.";
    private IReadOnlyList<ReleaseGateItem> _gates = Array.Empty<ReleaseGateItem>();
    private IReadOnlyList<ReleaseArtifact> _artifacts = Array.Empty<ReleaseArtifact>();
    private IReadOnlyList<string> _nextActions = Array.Empty<string>();

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Confidence { get => _confidence; private set => Set(ref _confidence, value); }
    public string Summary { get => _summary; private set => Set(ref _summary, value); }
    public IReadOnlyList<ReleaseGateItem> Gates { get => _gates; private set => Set(ref _gates, value); }
    public IReadOnlyList<ReleaseArtifact> Artifacts { get => _artifacts; private set => Set(ref _artifacts, value); }
    public IReadOnlyList<string> NextActions { get => _nextActions; private set => Set(ref _nextActions, value); }

    public void Evaluate()
    {
        var result = _service.Evaluate();
        Status = result.Status;
        Confidence = result.Confidence;
        Summary = result.Summary;
        Gates = result.Gates;
        Artifacts = result.Artifacts;
        NextActions = result.NextActions;
    }
}
