using System.Text.Json;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.Contracts;
using SmartPerformanceDoctor.Contracts.Services;
using SmartPerformanceDoctor.Data;

namespace SmartPerformanceDoctor.App.Services;

public sealed class InferenceOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly KnowledgeService _knowledge = KnowledgeService.Shared;

    public InferenceResult Analyze(
        string scope,
        IReadOnlyList<IntelligenceSummary> diagnoses,
        IReadOnlyList<string> rawSignals,
        LocalLlmAnalysisResult? localLlm = null)
    {
        _knowledge.EnsureRulesLoaded();
        var policy = LoadPolicy();
        var scopedDiagnoses = FilterDiagnosesByScope(scope, diagnoses);
        var insights = new List<InferenceInsight>();

        if (localLlm?.Insights is { Count: > 0 })
        {
            foreach (var item in localLlm.Insights)
            {
                insights.Add(new InferenceInsight(
                    "local-llm",
                    item.Title,
                    item.Detail,
                    item.Confidence,
                    "경량 오픈소스 AI"));
            }
        }
        var repairIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedClusters = new List<SignalClusterPolicy>();

        var fusedScore = scopedDiagnoses.Count == 0
            ? 80
            : (int)scopedDiagnoses.Average(d => d.Score);

        foreach (var diagnosis in scopedDiagnoses)
        {
            foreach (var cause in diagnosis.RootCauses)
            {
                var penalty = policy.Fusion.SeverityPenalties.GetValueOrDefault(
                    cause.Severity.ToLowerInvariant(), 4);
                fusedScore = Math.Max(0, fusedScore - (int)(penalty * cause.Confidence));

                insights.Add(new InferenceInsight(
                    "rule-engine",
                    cause.Explanation,
                    cause.Recommendation,
                    cause.Confidence,
                    "최신 진단 엔진"));
            }
        }

        var normalizedSignals = rawSignals
            .Where(signal => !string.IsNullOrWhiteSpace(signal))
            .Select(signal => signal.Trim().ToLowerInvariant())
            .ToArray();
        var signalBlob = string.Join('\n', normalizedSignals);
        var repairNotNeeded = DetectRepairNotNeeded(signalBlob);

        foreach (var cluster in policy.SignalClusters)
        {
            if (!ClusterAppliesToScope(cluster, scope))
            {
                continue;
            }

            if (!MatchesCluster(cluster, normalizedSignals))
            {
                continue;
            }

            matchedClusters.Add(cluster);
        }

        foreach (var cluster in matchedClusters)
        {
            var learningKey = $"{scope}:{cluster.Id}";
            var learningWeight = _knowledge.Database.GetLearningWeight(learningKey);
            var confidence = Math.Clamp(
                cluster.Confidence * (1 - policy.Fusion.LearningWeightBlend)
                + (float)learningWeight * policy.Fusion.LearningWeightBlend,
                0,
                1);

            if (confidence < policy.Fusion.MinConfidence)
            {
                continue;
            }

            insights.Add(new InferenceInsight(
                "local-rules",
                cluster.Title,
                $"로컬 규칙 매칭: {cluster.Title}",
                confidence,
                "로컬 규칙 DB"));

            if (!string.IsNullOrWhiteSpace(cluster.Recommendation)
                && ScopeRepairFilter.IsAllowedForScope(cluster.Recommendation, scope))
            {
                repairIds.Add(cluster.Recommendation);
            }

            fusedScore = Math.Max(0, fusedScore - (int)(8 * confidence));
        }

        ApplyMultiHopCorrelation(matchedClusters, scope, insights, repairIds, ref fusedScore);
        ApplyRetrievalAugmentedInsights(scope, signalBlob, policy, insights, repairIds, ref fusedScore);
        fusedScore = ApplySelfConsistency(scope, scopedDiagnoses, matchedClusters, policy, fusedScore);

        var hasActionableDiagnosis = scopedDiagnoses
            .SelectMany(diagnosis => diagnosis.RootCauses)
            .Any(cause => cause.Confidence >= 0.75f
                && cause.Severity is "warning" or "critical");
        var hasCorrelatedEvidence = matchedClusters.Count > 0 || hasActionableDiagnosis;

        if (!repairNotNeeded && fusedScore < 90 && hasCorrelatedEvidence)
        {
            if (policy.RepairMapping.TryGetValue(scope, out var scopeActions))
            {
                foreach (var actionId in ScopeRepairFilter.FilterForScope(scopeActions, scope))
                {
                    repairIds.Add(actionId);
                }
            }
        }
        else if (repairNotNeeded)
        {
            repairIds.Clear();
            insights.Add(new InferenceInsight(
                "precision-gate",
                "복구 불필요",
                "정밀 스캔 결과 이상 신호가 없어 복구 작업을 건너뜁니다.",
                0.92f,
                "정밀 스캔 게이트"));
            fusedScore = Math.Max(fusedScore, 88);
        }

        repairIds.RemoveWhere(id => !ScopeRepairFilter.IsAllowedForScope(id, scope));

        var status = fusedScore >= 85 ? "양호" : fusedScore >= 65 ? "주의" : "위험";
        var summary = repairNotNeeded
            ? $"정밀 스캔 완료 · 점수 {fusedScore}점 · 복구 불필요 · 참고 {insights.Count}건"
            : $"로컬 규칙 분석 완료 · 점수 {fusedScore}점 · 참고 {insights.Count}건 · 복구 후보 {repairIds.Count}개";

        var enhanced = BuildEnhancedIntelligence(scopedDiagnoses, insights, fusedScore, status, summary);

        var result = new InferenceResult(
            scope,
            fusedScore,
            status,
            summary,
            insights,
            repairIds.ToArray(),
            enhanced);

        _knowledge.RecordInference(scope, result);
        return result;
    }

    private static void ApplyMultiHopCorrelation(
        IReadOnlyList<SignalClusterPolicy> clusters,
        string scope,
        List<InferenceInsight> insights,
        HashSet<string> repairIds,
        ref int fusedScore)
    {
        if (clusters.Count < 2)
        {
            return;
        }

        var areas = clusters.Select(c => c.Id).Distinct().ToArray();
        var compositeTitle = string.Join(" + ", clusters.Take(3).Select(c => c.Title));
        var compositeConfidence = Math.Clamp(clusters.Average(c => c.Confidence) + 0.06f, 0, 0.95f);

        insights.Add(new InferenceInsight(
            "multi-hop",
            $"복합 상관: {compositeTitle}",
            $"다중 신호 홉({areas.Length}개 영역) 교차 검증으로 복합 패턴이 확인되었습니다.",
            compositeConfidence,
            "다중 홉 상관"));

        foreach (var cluster in clusters)
        {
            if (!string.IsNullOrWhiteSpace(cluster.Recommendation)
                && ScopeRepairFilter.IsAllowedForScope(cluster.Recommendation, scope))
            {
                repairIds.Add(cluster.Recommendation);
            }
        }

        fusedScore = Math.Max(0, fusedScore - (int)(6 * compositeConfidence));
    }

    private void ApplyRetrievalAugmentedInsights(
        string scope,
        string signalBlob,
        InferencePolicyDocument policy,
        List<InferenceInsight> insights,
        HashSet<string> repairIds,
        ref int fusedScore)
    {
        var topK = policy.Fusion.RagTopK <= 0 ? 6 : policy.Fusion.RagTopK;
        var minSimilarity = policy.LlmReasoning?.MinRagSimilarity ?? 0.48f;
        var matches = _knowledge.Database.SearchLearningCorpus(scope, signalBlob, topK);

        foreach (var match in matches)
        {
            if (match.Similarity < minSimilarity)
            {
                continue;
            }

            insights.Add(new InferenceInsight(
                "rag-retrieval",
                match.Symptom,
                $"학습 코퍼스 유사 패턴(유사도 {match.Similarity:P0}) — {match.Symptom}",
                (float)Math.Clamp(match.Weight * match.Similarity, 0, 0.94),
                "RAG 학습 검색"));

            foreach (var action in match.SuggestedActions)
            {
                if (ScopeRepairFilter.IsAllowedForScope(action, scope))
                {
                    repairIds.Add(action);
                }
            }

            fusedScore = Math.Max(0, fusedScore - (int)(4 * match.Similarity));
        }
    }

    private int ApplySelfConsistency(
        string scope,
        IReadOnlyList<IntelligenceSummary> diagnoses,
        IReadOnlyList<SignalClusterPolicy> clusters,
        InferencePolicyDocument policy,
        int fusedScore)
    {
        var paths = policy.Fusion.SelfConsistencyPaths <= 0 ? 3 : policy.Fusion.SelfConsistencyPaths;
        var votes = new List<int>(paths);

        for (var path = 0; path < paths; path++)
        {
            var pathScore = diagnoses.Count == 0 ? 80 : (int)diagnoses.Average(d => d.Score);
            var jitter = (path - 1) * 2;
            pathScore = Math.Clamp(pathScore - clusters.Count * (3 + jitter), 0, 100);
            votes.Add(pathScore);
        }

        var median = votes.OrderBy(v => v).ElementAt(votes.Count / 2);
        return (fusedScore + median) / 2;
    }

    private static IReadOnlyList<IntelligenceSummary> FilterDiagnosesByScope(
        string scope,
        IReadOnlyList<IntelligenceSummary> diagnoses)
    {
        if (diagnoses.Count == 0)
        {
            return diagnoses;
        }

        var allowedModules = ScopeRepairFilter.ResolveModuleIds(scope).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return diagnoses
            .Select(d => new IntelligenceSummary
            {
                Score = d.Score,
                Status = d.Status,
                PlainSummary = d.PlainSummary,
                RootCauses = d.RootCauses
                    .Where(c => IsCauseAllowedForScope(c.Area, scope, allowedModules))
                    .ToList(),
                Actions = d.Actions
                    .Where(a => IsCauseAllowedForScope(a.Area, scope, allowedModules))
                    .ToList()
            })
            .Where(d => d.RootCauses.Count > 0 || d.Actions.Count > 0 || allowedModules.Count > 0)
            .ToArray();
    }

    private static bool IsCauseAllowedForScope(string area, string scope, HashSet<string> allowedModules)
    {
        if (string.IsNullOrWhiteSpace(area))
        {
            return scope is "system" or "full";
        }

        if (allowedModules.Contains(area))
        {
            return true;
        }

        return scope switch
        {
            "system" => area is "system" or "disk" or "memory" or "service",
            "driver" => area is "driver",
            "audio" => area is "audio",
            _ => true
        };
    }

    private static bool ClusterAppliesToScope(SignalClusterPolicy cluster, string scope)
    {
        if (cluster.Scopes is null || cluster.Scopes.Count == 0)
        {
            return true;
        }

        return cluster.Scopes.Any(s => string.Equals(s, scope, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesCluster(SignalClusterPolicy cluster, IReadOnlyList<string> signals)
    {
        var requiredMatches = Math.Clamp(cluster.MinKeywordMatches, 1, Math.Max(1, cluster.Keywords.Count));
        return signals.Any(signal =>
        {
            if (cluster.NegativeKeywords.Any(keyword =>
                    signal.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return cluster.Keywords
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(keyword => signal.Contains(keyword, StringComparison.OrdinalIgnoreCase)) >= requiredMatches;
        });
    }

    private static IntelligenceSummary BuildEnhancedIntelligence(
        IReadOnlyList<IntelligenceSummary> diagnoses,
        IReadOnlyList<InferenceInsight> insights,
        int fusedScore,
        string status,
        string summary)
    {
        var causes = diagnoses
            .SelectMany(d => d.RootCauses)
            .Take(6)
            .ToList();

        foreach (var insight in insights.Take(6))
        {
            causes.Add(new RootCauseCandidate
            {
                Area = insight.Category,
                Severity = insight.Confidence >= 0.7 ? "warning" : "info",
                Evidence = insight.Source,
                Explanation = insight.Title,
                Recommendation = insight.Detail,
                Confidence = insight.Confidence
            });
        }

        var actions = diagnoses
            .SelectMany(d => d.Actions)
            .Take(4)
            .ToList();

        return new IntelligenceSummary
        {
            Score = fusedScore,
            Status = status,
            PlainSummary = summary,
            RootCauses = causes,
            Actions = actions
        };
    }

    private static bool DetectRepairNotNeeded(string signalBlob)
    {
        if (string.IsNullOrWhiteSpace(signalBlob))
        {
            return false;
        }

        var markers = new[]
        {
            "repairneeded\":false",
            "repairneeded\": false",
            "repairneeded=false",
            "repairneeded: false",
            "no repair needed",
            "문제 장치 없음",
            "playback path looks healthy",
            "복구 불필요"
        };

        return markers.Any(marker => signalBlob.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static InferencePolicyDocument LoadPolicy()
    {
        var rulesRoot = RuntimePaths.RulesDirectory;
        var path = Path.Combine(rulesRoot, "inference_policies.json");
        if (!File.Exists(path))
        {
            return InferencePolicyDocument.CreateDefault();
        }

        try
        {
            return JsonSerializer.Deserialize<InferencePolicyDocument>(File.ReadAllText(path), JsonOptions)
                   ?? InferencePolicyDocument.CreateDefault();
        }
        catch
        {
            return InferencePolicyDocument.CreateDefault();
        }
    }

    private sealed class InferencePolicyDocument
    {
        public FusionPolicy Fusion { get; init; } = new();
        public List<SignalClusterPolicy> SignalClusters { get; init; } = new();
        public Dictionary<string, List<string>> RepairMapping { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public LlmReasoningPolicy? LlmReasoning { get; init; }

        public static InferencePolicyDocument CreateDefault() => new()
        {
            Fusion = new FusionPolicy(),
            SignalClusters = new List<SignalClusterPolicy>(),
            RepairMapping = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private sealed class FusionPolicy
    {
        public float MinConfidence { get; init; } = 0.45f;
        public float LearningWeightBlend { get; init; } = 0.35f;
        public int RagTopK { get; init; } = 6;
        public int SelfConsistencyPaths { get; init; } = 3;
        public int MultiHopDepth { get; init; } = 2;
        public Dictionary<string, int> SeverityPenalties { get; init; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["critical"] = 22,
            ["warning"] = 12,
            ["info"] = 4
        };
    }

    private sealed class LlmReasoningPolicy
    {
        public List<string> Techniques { get; init; } = new();
        public string CotTemplate { get; init; } = "";
        public float MinRagSimilarity { get; init; } = 0.48f;
    }

    private sealed class SignalClusterPolicy
    {
        public string Id { get; init; } = "";
        public List<string> Scopes { get; init; } = new();
        public List<string> Keywords { get; init; } = new();
        public int MinKeywordMatches { get; init; } = 1;
        public List<string> NegativeKeywords { get; init; } = new();
        public string Title { get; init; } = "";
        public string Recommendation { get; init; } = "";
        public float Confidence { get; init; } = 0.6f;
    }
}