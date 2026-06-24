namespace SmartPerformanceDoctor.App.Models.Update;

public sealed record UpdateHistoryEntry(
    string AppliedAt,
    string FromVersion,
    string ToVersion,
    string PackageName,
    string Message)
{
    public string Summary => $"{FromVersion} → {ToVersion}";
}