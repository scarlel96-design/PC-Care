namespace SmartPerformanceDoctor.App.Models;

public sealed record ReportSummary(
    string Title,
    string Module,
    string Status,
    string StatusLabel,
    string SummaryText,
    string CreatedAt,
    int ActionsTakenCount,
    string ReportPath,
    string JsonPath,
    string SummaryPath);