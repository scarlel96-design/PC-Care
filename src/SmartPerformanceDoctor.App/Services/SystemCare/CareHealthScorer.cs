using SmartPerformanceDoctor.App.Models.SystemCare;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

public sealed class CareHealthReport
{
    public int Score { get; init; }
    public string Grade { get; init; } = "";
    public string Summary { get; init; } = "";
    public int SafeCount { get; init; }
    public int ReviewCount { get; init; }
    public int CautionCount { get; init; }
    public int BlockedCount { get; init; }
}

public static class CareHealthScorer
{
    public static CareHealthReport Score(IReadOnlyList<CareFinding> findings)
    {
        if (findings.Count == 0)
        {
            return new CareHealthReport
            {
                Score = 100,
                Grade = "우수",
                Summary = "검사 항목에서 특이 사항이 없습니다.",
                SafeCount = 0
            };
        }

        var safe = findings.Count(f => f.RiskCode == "safe");
        var review = findings.Count(f => f.RiskCode == "review");
        var caution = findings.Count(f => f.RiskCode == "caution");
        var highrisk = findings.Count(f => f.RiskCode == "highrisk");
        var blocked = findings.Count(f => f.RiskCode == "blocked");

        var penalty = review * 2 + caution * 5 + highrisk * 10 + blocked * 8;
        var score = Math.Clamp(100 - penalty, 0, 100);
        var grade = score switch
        {
            >= 90 => "우수",
            >= 75 => "양호",
            >= 55 => "보통",
            >= 35 => "주의",
            _ => "위험"
        };

        return new CareHealthReport
        {
            Score = score,
            Grade = grade,
            Summary = $"건강 점수 {score}점 ({grade}) · 안전 {safe} · 확인 {review} · 주의 {caution}",
            SafeCount = safe,
            ReviewCount = review,
            CautionCount = caution,
            BlockedCount = blocked
        };
    }
}