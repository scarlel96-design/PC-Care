using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.App.Services.SystemCare;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class SystemCareStabilityTests
{
    [Fact]
    public void CareFolderScanner_TempPath_CompletesQuickly()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = CareFolderScanner.Measure(
            Path.GetTempPath(),
            cts.Token,
            maxDepth: 2,
            maxFiles: 500,
            tempFolder: true);

        sw.Stop();
        Assert.True(result.FileCount >= 0);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15), $"scan took {sw.Elapsed}");
        Assert.True(result.Note is "complete" or "estimated" or "file_cap");
    }

    [Fact]
    public void CareFolderScanner_SkipsReparsePoints_DoesNotHang()
    {
        var root = Path.Combine(Path.GetTempPath(), "care-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "sample.txt"), "ok");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = CareFolderScanner.Measure(root, cts.Token, maxDepth: 1, maxFiles: 10);
            Assert.Equal(1, result.FileCount);
            Assert.False(result.Estimated);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    [Fact]
    public void CareMemoryProbe_Capture_ReturnsUsage()
    {
        var snapshot = CareMemoryProbe.Capture(includeTopProcesses: false);

        Assert.True(snapshot.TotalMb > 0);
        Assert.InRange(snapshot.UsedPercent, 0, 100);
    }

    [Fact]
    public void SystemStabilityProbe_Analyze_ReturnsReport()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var report = SystemStabilityProbe.Analyze(cts.Token);
        sw.Stop();

        Assert.NotNull(report);
        Assert.True(report.BugCheckCount30d >= 0);
        Assert.True(report.WheaErrorCount30d >= 0);
        Assert.True(report.UnexpectedShutdownCount30d >= 0);
        Assert.NotEmpty(report.ToSignalLines());
        Assert.NotEmpty(report.ToCareFindings());
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(12), $"stability probe took {sw.Elapsed}");
    }

    [Fact]
    public void SystemStabilityProbe_Intelligence_HasScore()
    {
        var report = SystemStabilityProbe.Analyze();
        var intel = report.ToIntelligenceSummary();

        Assert.InRange(intel.Score, 0, 100);
        Assert.False(string.IsNullOrWhiteSpace(intel.PlainSummary));
    }

    [Fact]
    public async Task SystemCare_SmartScan_CompletesPastTempFolder()
    {
        var service = new SystemCareService();
        var taskIds = SystemCareService.ScanTasks
            .Where(t => t.IncludedInSmart)
            .Select(t => t.Id)
            .ToArray();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var lastMessage = "";
        var progress = new Progress<(int percent, string message)>(r => lastMessage = r.message);

        var result = await service.ScanByTasksAsync(
            CareScanMode.Smart,
            taskIds,
            progress,
            cts.Token);

        Assert.NotNull(result);
        Assert.Contains("disk.temp.user", result.EnabledTasks);
        Assert.DoesNotContain(result.Findings, f => f.Id.EndsWith(".timeout", StringComparison.Ordinal));
        Assert.True(result.HealthScore >= 0);
        Assert.NotEqual("사용자 임시 폴더 검사 중…", lastMessage);
    }
}