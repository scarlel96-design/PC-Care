namespace SmartPerformanceDoctor.App.Models.Update;

public sealed class UpdateProgressReport
{
    public int Percent { get; init; }
    public int StepIndex { get; init; }
    public int StepCount { get; init; }
    public string Phase { get; init; } = "";
    public string Action { get; init; } = "";
    public string Detail { get; init; } = "";
    public int? FileIndex { get; init; }
    public int? FileTotal { get; init; }
    public string? CurrentFile { get; init; }
    public TimeSpan Elapsed { get; init; }

    public string StepLabel => StepCount > 0 ? $"단계 {StepIndex}/{StepCount}" : "";
    public string FileLabel => FileIndex is int i && FileTotal is int t ? $"파일 {i}/{t}" : "";
}

public sealed class UpdateActivityEntry
{
    public string Time { get; init; } = "";
    public string Message { get; init; } = "";
}