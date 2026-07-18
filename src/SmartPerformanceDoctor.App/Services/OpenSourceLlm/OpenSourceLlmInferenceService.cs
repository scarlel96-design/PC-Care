using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services.OpenSourceLlm;

/// <summary>설치본 내장 경량 AI — 외부 다운로드 없음.</summary>
public sealed class OpenSourceLlmInferenceService
{
    public static OpenSourceLlmInferenceService Shared { get; } = new();

    private OpenSourceLlmInferenceService()
    {
    }

    public bool IsReady => true;

    public Task EnsureReadyAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report("내장 경량 AI 엔진이 준비되었습니다.");
        return Task.CompletedTask;
    }

    public Task<LocalLlmAnalysisResult> AnalyzeAsync(
        string scope,
        IReadOnlyList<string> signals,
        CancellationToken cancellationToken = default) =>
        EmbeddedLocalAiEngine.AnalyzeAsync(scope, signals);
}