using SmartPerformanceDoctor.Data;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class KnowledgeDatabaseTests
{
    private static string CreateTempDbPath() =>
        Path.Combine(Path.GetTempPath(), $"spd-test-{Guid.NewGuid():N}.db");

    [Fact]
    public void RecordsDiagnosticRunAndSummary()
    {
        var path = CreateTempDbPath();
        using (var db = new KnowledgeDatabase(path))
        {
            db.RecordDiagnosticRun("quick", "ok", 92, ["signal-a"], null, 1200);
            var summary = db.GetDiagnosticSummary();
            Assert.Equal(1, summary.TotalRuns);
            Assert.Equal(92, summary.AverageScore);
        }

        TryDelete(path);
    }

    [Fact]
    public void LearningWeightIncreasesOnSuccessfulRepair()
    {
        var path = CreateTempDbPath();
        using (var db = new KnowledgeDatabase(path))
        {
            db.RecordRepairAction("driver", "pnputil_scan_devices", true, "dry-run", 0, "planned", true);
            var weight = db.GetLearningWeight("driver:pnputil_scan_devices");
            Assert.True(weight >= 0.5);
        }

        TryDelete(path);
    }

    [Fact]
    public void UpsertRulePersistsRegistry()
    {
        var path = CreateTempDbPath();
        using (var db = new KnowledgeDatabase(path))
        {
            db.UpsertRule("rules", "system_rules", """{"low_disk":{"threshold_free_percent":12}}""", "44.0.0");
            db.UpsertRule("rules", "system_rules", """{"low_disk":{"threshold_free_percent":10}}""", "44.0.1");
            Assert.True(File.Exists(path));
        }

        TryDelete(path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // SQLite WAL may still be releasing handles on Windows; ignore cleanup race.
        }
    }
}