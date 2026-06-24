using SmartPerformanceDoctor.App.Models.Commercial;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class IntelligencePipelineService
{
    private readonly CommercialPackLoader _packs = CommercialPackLoader.Shared;
    private readonly RootCauseScoringService _scoring = new();
    private readonly PrecisionScanService _precision = new();

    public IntelligenceCenterSnapshot BuildSnapshot(IReadOnlyList<string>? rawSignals = null)
    {
        _packs.EnsureLoaded();
        var signals = NormalizeSignals(rawSignals ?? Array.Empty<string>());
        if (signals.Count == 0)
        {
            signals = _precision.RunStandardSet().SelectMany(x => x.Signals).ToArray();
        }

        var matched = MatchRules(signals);
        var ranked = _scoring.Rank(signals, matched);
        var insights = ranked
            .Take(8)
            .Select(r => $"[{r.Area}] {r.Message} (점수:{r.Score:F2} · 위험:{r.Risk})")
            .ToArray();

        return new IntelligenceCenterSnapshot
        {
            RuleCount = _packs.Rules.Count,
            ProtocolCount = _packs.Protocols.Count,
            PackVersion = _packs.PackVersion,
            Summary = $"규칙 {_packs.Rules.Count:N0}개 · 프로토콜 {_packs.Protocols.Count}개 · 매칭 {matched.Count}건 · 원인후보 {ranked.Count}건",
            TopInsights = insights.Length > 0 ? insights : new[] { "특별히 맞는 규칙이 없습니다. 자세한 점검을 권장합니다." }
        };
    }

    public IReadOnlyList<DiagnosticSignal> NormalizeSignals(IReadOnlyList<string> raw)
    {
        var list = new List<DiagnosticSignal>();
        for (var i = 0; i < raw.Count; i++)
        {
            var line = raw[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var lower = line.ToLowerInvariant();
            list.Add(new DiagnosticSignal
            {
                SignalId = $"signal.raw.{i}",
                Area = GuessArea(lower),
                Category = "scan",
                Source = "CoreEngine",
                Severity = lower.Contains("error") || lower.Contains("critical") ? "high" : lower.Contains("warn") ? "medium" : "info",
                Confidence = 0.7f,
                Evidence = line.Length > 240 ? line[..240] : line,
                RawValue = line,
                NormalizedValue = lower,
                RecommendedNextProbe = "deep_scan_recommended"
            });
        }
        return list;
    }

    private List<CommercialRule> MatchRules(IReadOnlyList<DiagnosticSignal> signals)
    {
        if (signals.Count == 0 || _packs.Rules.Count == 0) return new List<CommercialRule>();

        var blob = string.Join('\n', signals.Select(s => s.NormalizedValue));
        var tokens = blob
            .Split([' ', '\n', '\r', '\t', ',', ';', ':', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(64)
            .ToArray();

        return _packs.Rules
            .Select(rule =>
            {
                var category = rule.Category.Replace('_', '.');
                var haystack = $"{rule.Area} {category} {rule.UserMessage}".ToLowerInvariant();
                var tokenHits = tokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
                var areaHit = blob.Contains(rule.Area, StringComparison.OrdinalIgnoreCase) ? 2 : 0;
                var categoryHit = blob.Contains(category, StringComparison.OrdinalIgnoreCase) ? 3 : 0;
                var score = tokenHits + areaHit + categoryHit;
                return (rule, score);
            })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.rule.ConfidenceBase)
            .Take(48)
            .Select(x => x.rule)
            .ToList();
    }

    private static string GuessArea(string lower) => lower switch
    {
        _ when lower.Contains("audio") => "audio",
        _ when lower.Contains("driver") || lower.Contains("pnp") => "driver",
        _ when lower.Contains("disk") || lower.Contains("smart") => "disk",
        _ when lower.Contains("memory") => "memory",
        _ when lower.Contains("service") => "system",
        _ => "system"
    };
}