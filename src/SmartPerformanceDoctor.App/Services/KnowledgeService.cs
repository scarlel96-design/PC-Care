using System.Text.Json;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.Data;

namespace SmartPerformanceDoctor.App.Services;

public sealed class KnowledgeService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static KnowledgeService? _shared;
    private readonly KnowledgeDatabase _database = new();
    private bool _rulesLoaded;

    public static KnowledgeService Shared => _shared ??= new KnowledgeService();

    public KnowledgeDatabase Database => _database;

    public void EnsureRulesLoaded()
    {
        if (_rulesLoaded)
        {
            return;
        }

        var rulesRoot = ResolveRulesRoot();
        if (!Directory.Exists(rulesRoot))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(rulesRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            var category = Path.GetFileNameWithoutExtension(file);
            var json = File.ReadAllText(file);
            _database.UpsertRule("rules", category, json, AppInfo.Version);
        }

        var policiesPath = Path.Combine(rulesRoot, "remediation_policies.json");
        if (File.Exists(policiesPath))
        {
            _database.UpsertRule("policy", "remediation", File.ReadAllText(policiesPath), AppInfo.Version);
        }

        var inferencePath = Path.Combine(rulesRoot, "inference_policies.json");
        if (File.Exists(inferencePath))
        {
            _database.UpsertRule("inference", "policies", File.ReadAllText(inferencePath), AppInfo.Version);
        }

        LearningCorpusSeeder.SeedIfNeeded(_database, rulesRoot);
        _rulesLoaded = true;
    }

    public void RecordInference(string scope, InferenceResult result)
    {
        EnsureRulesLoaded();
        try
        {
            _database.RecordInferenceRun(
                scope,
                result.FusedScore,
                result.Status,
                result.Summary,
                JsonSerializer.Serialize(result.Insights, JsonOptions),
                JsonSerializer.Serialize(result.RecommendedRepairActionIds, JsonOptions));
        }
        catch
        {
            // Inference persistence must never crash the care session.
        }
    }

    public void RecordEngineRun(
        string module,
        string status,
        int score,
        IReadOnlyList<string> signals,
        object? intelligence,
        long durationMs)
    {
        EnsureRulesLoaded();
        var intelligenceJson = intelligence is null ? null : JsonSerializer.Serialize(intelligence, JsonOptions);
        _database.RecordDiagnosticRun(module, status, score, signals, intelligenceJson, durationMs);
    }

    public void RecordRepairOutcome(
        string area,
        string action,
        bool dryRun,
        string status,
        int exitCode,
        string message)
    {
        EnsureRulesLoaded();
        var success = status is "dry-run" or "ok" or "planned" && exitCode == 0;
        _database.RecordRepairAction(area, action, dryRun, status, exitCode, message, success);
    }

    public string BuildLearningInsight()
    {
        EnsureRulesLoaded();
        var summary = _database.GetDiagnosticSummary();
        var top = _database.GetTopLearningStats(3);
        if (summary.TotalRuns == 0 && top.Count == 0)
        {
            return "아직 학습 기록이 없습니다. 점검이나 복구를 실행하면 정확도가 점점 높아집니다.";
        }

        var topLine = top.Count == 0
            ? "복구 패턴 학습 중"
            : $"가장 많이 시도한 복구: {top[0].PatternKey.Replace(':', '·')} (성공률 {top[0].Weight:P0})";

        return $"최근 30일 점검 {summary.TotalRuns}회 · 평균 점수 {summary.AverageScore}점 · {topLine}";
    }

    private static string ResolveRulesRoot()
    {
        if (Directory.Exists(RuntimePaths.RulesDirectory))
        {
            return RuntimePaths.RulesDirectory;
        }

        return Path.Combine(FindProjectRoot(), "content", "rules");
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "README.md")) &&
                (Directory.Exists(Path.Combine(dir, "content", "rules")) || Directory.Exists(Path.Combine(dir, "rules"))))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return AppContext.BaseDirectory;
    }

    public void Dispose()
    {
        _database.Dispose();
        if (ReferenceEquals(_shared, this))
        {
            _shared = null;
        }
    }
}