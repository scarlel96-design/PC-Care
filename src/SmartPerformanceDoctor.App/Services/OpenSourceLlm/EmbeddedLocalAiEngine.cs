using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services.OpenSourceLlm;

/// <summary>설치본에 내장된 경량 오픈소스 규칙·신호 분석 엔진 (네트워크·외부 GGUF 불필요).</summary>
public static class EmbeddedLocalAiEngine
{
    private static readonly (string[] Keys, string Title, string Detail, double Confidence)[] Heuristics =
    {
        (new[] { "disk", "storage", "공간", "용량", "c: ", "디스크" }, "저장 공간", "디스크·임시 파일·캐시 관련 신호가 있습니다. 정리·여유 공간 확보를 권장합니다.", 0.72),
        (new[] { "memory", "ram", "메모리", "commit", "페이징" }, "메모리 사용", "메모리 압박 또는 과다 사용 패턴이 보입니다. 불필요한 앱 종료를 검토하세요.", 0.7),
        (new[] { "driver", "드라이버", "wmi", "device" }, "드라이버·장치", "장치·드라이버 계층에서 이상 신호가 있습니다. 드라이버 점검·재시작을 권장합니다.", 0.68),
        (new[] { "audio", "sound", "오디오", "endpoint" }, "오디오", "오디오 장치·엔드포인트 관련 신호가 있습니다.", 0.65),
        (new[] { "service", "서비스", "stopped", "failed" }, "시스템 서비스", "중지·실패한 서비스 관련 신호가 있습니다.", 0.66),
        (new[] { "error", "fail", "exception", "오류", "실패" }, "오류 패턴", "점검 로그에 오류·실패 키워드가 반복됩니다. 상세 로그 확인을 권장합니다.", 0.6),
        (new[] { "slow", "latency", "지연", "100%", "cpu" }, "성능", "CPU·응답 지연 관련 신호가 있습니다.", 0.62),
    };

    public static Task<LocalLlmAnalysisResult> AnalyzeAsync(string scope, IReadOnlyList<string> signals)
    {
        var blob = string.Join('\n', signals).ToLowerInvariant();
        var insights = new List<LocalLlmInsight>();

        foreach (var (keys, title, detail, confidence) in Heuristics)
        {
            if (keys.Any(k => blob.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                insights.Add(new LocalLlmInsight(title, detail, confidence));
            }
        }

        if (insights.Count == 0 && signals.Count > 0)
        {
            var sample = signals.Where(s => !string.IsNullOrWhiteSpace(s)).Take(3).ToArray();
            var summary = sample.Length == 0
                ? "수집된 신호가 적습니다. 기본 규칙 분석으로 이어집니다."
                : string.Join(" / ", sample.Select(s => Truncate(s, 80)));
            insights.Add(new LocalLlmInsight(
                "내장 AI 요약",
                $"범위 [{scope}] — {summary}",
                0.58));
        }

        if (insights.Count == 0)
        {
            insights.Add(new LocalLlmInsight(
                "내장 AI",
                "특이 신호 없음. 로컬 규칙 엔진 결과를 우선 참고하세요.",
                0.5));
        }

        return Task.FromResult(new LocalLlmAnalysisResult(
            true,
            $"내장 경량 AI 분석 완료 · {insights.Count}건",
            insights.Take(5).ToArray()));
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}