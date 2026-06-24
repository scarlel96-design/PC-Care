using SmartPerformanceDoctor.App.Models.Commercial;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class RootCauseCandidate
{
    public string RuleId { get; init; } = "";
    public string Area { get; init; } = "";
    public string Message { get; init; } = "";
    public float Score { get; init; }
    public string Risk { get; init; } = "";
    public string ProtocolId { get; init; } = "";
}

public sealed class RootCauseScoringService
{
    private static readonly Dictionary<string, float> SeverityWeight = new(StringComparer.OrdinalIgnoreCase)
    {
        ["critical"] = 1.0f,
        ["high"] = 0.82f,
        ["medium"] = 0.55f,
        ["low"] = 0.3f,
        ["info"] = 0.15f
    };

    public IReadOnlyList<RootCauseCandidate> Rank(
        IReadOnlyList<DiagnosticSignal> signals,
        IReadOnlyList<CommercialRule> matchedRules)
    {
        if (matchedRules.Count == 0)
        {
            return Array.Empty<RootCauseCandidate>();
        }

        var recurrence = signals
            .GroupBy(s => s.Area, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var signalTokens = signals
            .SelectMany(s => s.NormalizedValue.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .Where(t => t.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return matchedRules
            .Select(rule =>
            {
                var severity = SeverityWeight.GetValueOrDefault(rule.Severity, 0.4f);
                var recurrenceBoost = recurrence.GetValueOrDefault(rule.Area, 1) * 0.08f;
                var haystack = $"{rule.Area} {rule.Category} {rule.UserMessage}".ToLowerInvariant();
                var tokenOverlap = signalTokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase)) * 0.04f;
                var score = rule.ConfidenceBase + severity + recurrenceBoost + tokenOverlap;
                return new RootCauseCandidate
                {
                    RuleId = rule.RuleId,
                    Area = rule.Area,
                    Message = rule.UserMessage,
                    Score = Math.Min(1f, score),
                    Risk = rule.Risk,
                    ProtocolId = rule.ProtocolId
                };
            })
            .OrderByDescending(x => x.Score)
            .Take(12)
            .ToArray();
    }
}