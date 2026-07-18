namespace SmartPerformanceDoctor.App.Models;

public sealed record LocalLlmInsight(string Title, string Detail, double Confidence);

public sealed record LocalLlmAnalysisResult(
    bool Success,
    string Message,
    IReadOnlyList<LocalLlmInsight> Insights);