namespace SmartPerformanceDoctor.App.Models;

public sealed record ReleaseGateResult(
    string Version,
    string Status,
    string Confidence,
    string Summary,
    IReadOnlyList<ReleaseGateItem> Gates,
    IReadOnlyList<ReleaseArtifact> Artifacts,
    IReadOnlyList<string> NextActions);
