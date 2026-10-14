using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.App.Services.SystemCare;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class SystemCareStabilityTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;
    public SystemCareStabilityTests(Xunit.Abstractions.ITestOutputHelper output)=>_output=output;
    [Fact]
    public void CareRollback_RecoversMoveBeforeCompletionRecord_AndRejectsTamper()
    {
        var audit = Path.Combine(Path.GetTempPath(), "care-journal-" + Guid.NewGuid().ToString("N"));
        var quarantine = Path.Combine(audit, "quarantine");
        var original = Path.Combine(audit, "original.txt");
        var held = Path.Combine(quarantine, Guid.NewGuid().ToString("N") + "_original.txt");
        Directory.CreateDirectory(quarantine);
        try
        {
            File.WriteAllText(original, "original data");
            CareRollbackService.RecordQuarantine(audit, original, held);
            File.Move(original, held); // Simulate termination before MarkQuarantined.
            File.WriteAllText(Path.Combine(audit, "quarantine_map.json"), "{broken");

            var recovered = CareRollbackService.Rollback(audit);
            Assert.True(recovered.Success);
            Assert.Equal("original data", File.ReadAllText(original));

            File.Delete(original);
            File.WriteAllText(original, "second data");
            var held2 = Path.Combine(quarantine, Guid.NewGuid().ToString("N") + "_original.txt");
            CareRollbackService.RecordQuarantine(audit, original, held2);
            File.Move(original, held2);
            File.WriteAllText(held2, "tampered");
            var rejected = CareRollbackService.Rollback(audit);
            Assert.False(rejected.Success);
            Assert.Equal(1, rejected.FailedCount);
            Assert.False(File.Exists(original));
            Assert.Equal("tampered", File.ReadAllText(held2));
        }
        finally
        {
            Directory.Delete(audit, recursive: true);
        }
    }

    [Fact]
    public void CareRollback_DoesNotOverwriteNewFileAtOriginalPath()
    {
        var audit = Path.Combine(Path.GetTempPath(), "care-rollback-" + Guid.NewGuid().ToString("N"));
        var quarantine = Path.Combine(audit, "quarantine");
        var original = Path.Combine(audit, "original.txt");
        var held = Path.Combine(quarantine, "held.txt");
        Directory.CreateDirectory(quarantine);
        try
        {
            File.WriteAllText(held, "quarantined");
            CareRollbackService.RecordQuarantine(audit, original, held);
            File.WriteAllText(original, "new user data");

            var result = CareRollbackService.Rollback(audit);

            Assert.False(result.Success);
            Assert.Equal(0, result.RestoredCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal("new user data", File.ReadAllText(original));
            Assert.Equal("quarantined", File.ReadAllText(held));
        }
        finally
        {
            Directory.Delete(audit, recursive: true);
        }
    }

    [Fact]
    public void CareFolderScanner_TempPath_CompletesQuickly()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var start=DateTimeOffset.UtcNow;
        using var process=System.Diagnostics.Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;
        var gc0=GC.CollectionCount(0);var gc1=GC.CollectionCount(1);var gc2=GC.CollectionCount(2);
        CareFolderScanner.FolderScanResult result;
        try { result=CareFolderScanner.Measure(Path.GetTempPath(),cts.Token,maxDepth:2,maxFiles:500,tempFolder:true); }
        finally
        {
            sw.Stop();
            _output.WriteLine($"SCAN_START={start:O}; SCAN_END={DateTimeOffset.UtcNow:O}; ELAPSED_MS={sw.Elapsed.TotalMilliseconds}; CPU_MS={(process.TotalProcessorTime-cpu).TotalMilliseconds}; THREADPOOL_THREADS={ThreadPool.ThreadCount}; GC0_DELTA={GC.CollectionCount(0)-gc0}; GC1_DELTA={GC.CollectionCount(1)-gc1}; GC2_DELTA={GC.CollectionCount(2)-gc2}; DISK_IO=NOT_MEASURED; BACKGROUND_EFFECT=UNKNOWN");
        }
        _output.WriteLine($"FILE_COUNT={result.FileCount}; NOTE={result.Note}; ESTIMATED={result.Estimated}");
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
