using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SmartPerformanceDoctor.Data;

public sealed class KnowledgeDatabase : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _sync = new();
    private readonly SqliteConnection _connection;
    private readonly string _databasePath;

    public KnowledgeDatabase(string? databasePath = null)
    {
        _databasePath = databasePath ?? ResolveDefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        _connection = new SqliteConnection($"Data Source={_databasePath}");
        _connection.Open();
        InitializeSchema();
    }

    public string DatabasePath => _databasePath;

    public void RecordDiagnosticRun(
        string module,
        string status,
        int score,
        IReadOnlyList<string> signals,
        string? intelligenceJson,
        long durationMs)
    {
        lock (_sync)
        {
            RecordDiagnosticRunCore(module, status, score, signals, intelligenceJson, durationMs);
        }
    }

    public void RecordInferenceRun(
        string scope,
        int fusedScore,
        string status,
        string summary,
        string insightsJson,
        string repairPlanJson)
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO inference_runs (scope, fused_score, status, summary, insights_json, repair_plan_json, created_at)
                VALUES ($scope, $score, $status, $summary, $insights, $repairPlan, $created);
                """;
            cmd.Parameters.AddWithValue("$scope", scope);
            cmd.Parameters.AddWithValue("$score", fusedScore);
            cmd.Parameters.AddWithValue("$status", status);
            cmd.Parameters.AddWithValue("$summary", summary);
            cmd.Parameters.AddWithValue("$insights", insightsJson);
            cmd.Parameters.AddWithValue("$repairPlan", repairPlanJson);
            cmd.Parameters.AddWithValue("$created", DateTimeOffset.Now.ToString("o"));
            cmd.ExecuteNonQuery();
        }
    }

    private void RecordDiagnosticRunCore(
        string module,
        string status,
        int score,
        IReadOnlyList<string> signals,
        string? intelligenceJson,
        long durationMs)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO diagnostic_runs (module, status, score, signals_json, intelligence_json, duration_ms, created_at)
            VALUES ($module, $status, $score, $signals, $intelligence, $duration, $created);
            """;
        cmd.Parameters.AddWithValue("$module", module);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$score", score);
        cmd.Parameters.AddWithValue("$signals", JsonSerializer.Serialize(signals, JsonOptions));
        cmd.Parameters.AddWithValue("$intelligence", intelligenceJson ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$duration", durationMs);
        cmd.Parameters.AddWithValue("$created", DateTimeOffset.Now.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void RecordRepairAction(
        string area,
        string action,
        bool dryRun,
        string status,
        int exitCode,
        string message,
        bool success)
    {
        lock (_sync)
        {
            RecordRepairActionCore(area, action, dryRun, status, exitCode, message, success);
        }
    }

    private void RecordRepairActionCore(
        string area,
        string action,
        bool dryRun,
        string status,
        int exitCode,
        string message,
        bool success)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO repair_actions (area, action, dry_run, status, exit_code, message, success, created_at)
            VALUES ($area, $action, $dryRun, $status, $exitCode, $message, $success, $created);
            """;
        cmd.Parameters.AddWithValue("$area", area);
        cmd.Parameters.AddWithValue("$action", action);
        cmd.Parameters.AddWithValue("$dryRun", dryRun ? 1 : 0);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$exitCode", exitCode);
        cmd.Parameters.AddWithValue("$message", message);
        cmd.Parameters.AddWithValue("$success", success ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", DateTimeOffset.Now.ToString("o"));
        cmd.ExecuteNonQuery();

        UpdateLearningFeedback($"{area}:{action}", success);
    }

    public void UpsertRule(string category, string ruleKey, string ruleJson, string version)
    {
        lock (_sync)
        {
            UpsertRuleCore(category, ruleKey, ruleJson, version);
        }
    }

    private void UpsertRuleCore(string category, string ruleKey, string ruleJson, string version)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rules_registry (category, rule_key, rule_json, version, loaded_at)
            VALUES ($category, $ruleKey, $ruleJson, $version, $loadedAt)
            ON CONFLICT(category, rule_key) DO UPDATE SET
                rule_json = excluded.rule_json,
                version = excluded.version,
                loaded_at = excluded.loaded_at;
            """;
        cmd.Parameters.AddWithValue("$category", category);
        cmd.Parameters.AddWithValue("$ruleKey", ruleKey);
        cmd.Parameters.AddWithValue("$ruleJson", ruleJson);
        cmd.Parameters.AddWithValue("$version", version);
        cmd.Parameters.AddWithValue("$loadedAt", DateTimeOffset.Now.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void IndexReport(string path, string? module)
    {
        lock (_sync)
        {
            IndexReportCore(path, module);
        }
    }

    private void IndexReportCore(string path, string? module)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO reports_index (path, module, created_at)
            VALUES ($path, $module, $created)
            ON CONFLICT(path) DO UPDATE SET module = excluded.module, created_at = excluded.created_at;
            """;
        cmd.Parameters.AddWithValue("$path", path);
        cmd.Parameters.AddWithValue("$module", module ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$created", DateTimeOffset.Now.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void BulkSeedLearningPatterns(IReadOnlyList<string> patternKeys)
    {
        lock (_sync)
        {
            BulkSeedLearningPatternsCore(patternKeys);
        }
    }

    public void BulkSeedLearningCorpus(IReadOnlyList<LearningPatternRecord> records)
    {
        lock (_sync)
        {
            BulkSeedLearningCorpusCore(records);
        }
    }

    public int GetLearningCorpusCount()
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM learning_patterns;";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    private void BulkSeedLearningPatternsCore(IReadOnlyList<string> patternKeys)
    {
        using var tx = _connection.BeginTransaction();
        foreach (var key in patternKeys)
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO learning_feedback (pattern_key, success_count, failure_count, weight, last_updated)
                VALUES ($key, 0, 0, 0.72, $updated)
                ON CONFLICT(pattern_key) DO NOTHING;
                """;
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$updated", DateTimeOffset.Now.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private void BulkSeedLearningCorpusCore(IReadOnlyList<LearningPatternRecord> records)
    {
        using var tx = _connection.BeginTransaction();
        foreach (var record in records)
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO learning_patterns (pattern_key, category, module, symptom, signals_json, actions_json, context_json, weight, created_at)
                VALUES ($key, $category, $module, $symptom, $signals, $actions, $context, $weight, $created)
                ON CONFLICT(pattern_key) DO NOTHING;
                """;
            cmd.Parameters.AddWithValue("$key", record.PatternKey);
            cmd.Parameters.AddWithValue("$category", record.Category);
            cmd.Parameters.AddWithValue("$module", record.Module);
            cmd.Parameters.AddWithValue("$symptom", record.Symptom);
            cmd.Parameters.AddWithValue("$signals", record.SignalsJson);
            cmd.Parameters.AddWithValue("$actions", record.ActionsJson);
            cmd.Parameters.AddWithValue("$context", record.ContextJson);
            cmd.Parameters.AddWithValue("$weight", record.Weight);
            cmd.Parameters.AddWithValue("$created", DateTimeOffset.Now.ToString("o"));
            cmd.ExecuteNonQuery();

            using var feedback = _connection.CreateCommand();
            feedback.Transaction = tx;
            feedback.CommandText = """
                INSERT INTO learning_feedback (pattern_key, success_count, failure_count, weight, last_updated)
                VALUES ($key, 0, 0, $weight, $updated)
                ON CONFLICT(pattern_key) DO NOTHING;
                """;
            feedback.Parameters.AddWithValue("$key", record.PatternKey);
            feedback.Parameters.AddWithValue("$weight", record.Weight);
            feedback.Parameters.AddWithValue("$updated", DateTimeOffset.Now.ToString("o"));
            feedback.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public double GetLearningWeight(string patternKey)
    {
        lock (_sync)
        {
            return GetLearningWeightCore(patternKey);
        }
    }

    private double GetLearningWeightCore(string patternKey)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT weight FROM learning_feedback WHERE pattern_key = $key LIMIT 1;";
        cmd.Parameters.AddWithValue("$key", patternKey);
        var result = cmd.ExecuteScalar();
        return result is double d ? d : result is long l ? l : 1.0;
    }

    public IReadOnlyList<LearningCorpusMatch> SearchLearningCorpus(string module, string signalBlob, int limit = 6)
    {
        lock (_sync)
        {
            return SearchLearningCorpusCore(module, signalBlob, limit);
        }
    }

    private IReadOnlyList<LearningCorpusMatch> SearchLearningCorpusCore(string module, string signalBlob, int limit)
    {
        if (string.IsNullOrWhiteSpace(signalBlob))
        {
            return Array.Empty<LearningCorpusMatch>();
        }

        var tokens = signalBlob
            .Split([' ', '\n', '\r', '\t', ',', ';', ':', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(48)
            .ToArray();

        if (tokens.Length == 0)
        {
            return Array.Empty<LearningCorpusMatch>();
        }

        var matches = new List<LearningCorpusMatch>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT pattern_key, module, symptom, signals_json, actions_json, weight
            FROM learning_patterns
            WHERE module = $module OR module = 'full'
            ORDER BY weight DESC
            LIMIT 400;
            """;
        cmd.Parameters.AddWithValue("$module", module);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var symptom = reader.GetString(2);
            var signalsJson = reader.GetString(3);
            var actionsJson = reader.GetString(4);
            var haystack = $"{symptom} {signalsJson}".ToLowerInvariant();
            var hitCount = tokens.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
            if (hitCount == 0)
            {
                continue;
            }

            var similarity = Math.Min(1.0, hitCount / (double)Math.Max(4, tokens.Length));
            var actions = ParseJsonStringArray(actionsJson);
            matches.Add(new LearningCorpusMatch(
                reader.GetString(0),
                reader.GetString(1),
                symptom,
                similarity,
                reader.GetDouble(5),
                actions));
        }

        return matches
            .OrderByDescending(m => m.Similarity * m.Weight)
            .Take(limit)
            .ToArray();
    }

    private static IReadOnlyList<string> ParseJsonStringArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<LearningStat> GetTopLearningStats(int limit = 10)
    {
        lock (_sync)
        {
            return GetTopLearningStatsCore(limit);
        }
    }

    private IReadOnlyList<LearningStat> GetTopLearningStatsCore(int limit)
    {
        var list = new List<LearningStat>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT pattern_key, success_count, failure_count, weight, last_updated
            FROM learning_feedback
            ORDER BY (success_count + failure_count) DESC, weight DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new LearningStat(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetDouble(3),
                reader.GetString(4)));
        }

        return list;
    }

    public DiagnosticSummary GetDiagnosticSummary()
    {
        lock (_sync)
        {
            return GetDiagnosticSummaryCore();
        }
    }

    private DiagnosticSummary GetDiagnosticSummaryCore()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                COUNT(*),
                COALESCE(AVG(score), 0),
                COALESCE(SUM(CASE WHEN status = 'ok' THEN 1 ELSE 0 END), 0)
            FROM diagnostic_runs
            WHERE created_at >= datetime('now', '-30 days');
            """;
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new DiagnosticSummary(reader.GetInt32(0), (int)reader.GetDouble(1), reader.GetInt32(2));
    }

    private void UpdateLearningFeedback(string patternKey, bool success)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO learning_feedback (pattern_key, success_count, failure_count, weight, last_updated)
            VALUES ($key, $successCount, $failureCount, $weight, $updated)
            ON CONFLICT(pattern_key) DO UPDATE SET
                success_count = success_count + $successCount,
                failure_count = failure_count + $failureCount,
                weight = CASE
                    WHEN (success_count + failure_count + $successCount + $failureCount) = 0 THEN 1.0
                    ELSE CAST(success_count + $successCount AS REAL) / (success_count + failure_count + $successCount + $failureCount)
                END,
                last_updated = $updated;
            """;
        cmd.Parameters.AddWithValue("$key", patternKey);
        cmd.Parameters.AddWithValue("$successCount", success ? 1 : 0);
        cmd.Parameters.AddWithValue("$failureCount", success ? 0 : 1);
        cmd.Parameters.AddWithValue("$weight", success ? 1.0 : 0.5);
        cmd.Parameters.AddWithValue("$updated", DateTimeOffset.Now.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private void InitializeSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS diagnostic_runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                module TEXT NOT NULL,
                status TEXT NOT NULL,
                score INTEGER NOT NULL DEFAULT 0,
                signals_json TEXT NOT NULL DEFAULT '[]',
                intelligence_json TEXT,
                duration_ms INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS repair_actions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                area TEXT NOT NULL,
                action TEXT NOT NULL,
                dry_run INTEGER NOT NULL,
                status TEXT NOT NULL,
                exit_code INTEGER NOT NULL DEFAULT 0,
                message TEXT NOT NULL DEFAULT '',
                success INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS learning_feedback (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                pattern_key TEXT NOT NULL UNIQUE,
                success_count INTEGER NOT NULL DEFAULT 0,
                failure_count INTEGER NOT NULL DEFAULT 0,
                weight REAL NOT NULL DEFAULT 1.0,
                last_updated TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS learning_patterns (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                pattern_key TEXT NOT NULL UNIQUE,
                category TEXT NOT NULL,
                module TEXT NOT NULL,
                symptom TEXT NOT NULL,
                signals_json TEXT NOT NULL,
                actions_json TEXT NOT NULL,
                context_json TEXT NOT NULL,
                weight REAL NOT NULL DEFAULT 0.72,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS rules_registry (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                category TEXT NOT NULL,
                rule_key TEXT NOT NULL,
                rule_json TEXT NOT NULL,
                version TEXT NOT NULL,
                loaded_at TEXT NOT NULL,
                UNIQUE(category, rule_key)
            );

            CREATE TABLE IF NOT EXISTS reports_index (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                path TEXT NOT NULL UNIQUE,
                module TEXT,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS inference_runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                scope TEXT NOT NULL,
                fused_score INTEGER NOT NULL DEFAULT 0,
                status TEXT NOT NULL,
                summary TEXT NOT NULL DEFAULT '',
                insights_json TEXT NOT NULL DEFAULT '[]',
                repair_plan_json TEXT NOT NULL DEFAULT '[]',
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_diagnostic_runs_created ON diagnostic_runs(created_at);
            CREATE INDEX IF NOT EXISTS idx_inference_runs_created ON inference_runs(created_at);
            CREATE INDEX IF NOT EXISTS idx_repair_actions_created ON repair_actions(created_at);
            CREATE INDEX IF NOT EXISTS idx_reports_index_created ON reports_index(created_at);
            CREATE INDEX IF NOT EXISTS idx_learning_patterns_module ON learning_patterns(module);
            CREATE INDEX IF NOT EXISTS idx_learning_patterns_category ON learning_patterns(category);
            """;
        cmd.ExecuteNonQuery();
    }

    private static string ResolveDefaultPath()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPerformanceDoctor",
            "data");
        return Path.Combine(root, "knowledge.db");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

public sealed record LearningStat(
    string PatternKey,
    int SuccessCount,
    int FailureCount,
    double Weight,
    string LastUpdated);

public sealed record DiagnosticSummary(int TotalRuns, int AverageScore, int SuccessfulRuns);

public sealed record LearningPatternRecord(
    string PatternKey,
    string Category,
    string Module,
    string Symptom,
    string SignalsJson,
    string ActionsJson,
    string ContextJson,
    double Weight);

public sealed record LearningCorpusMatch(
    string PatternKey,
    string Module,
    string Symptom,
    double Similarity,
    double Weight,
    IReadOnlyList<string> SuggestedActions);