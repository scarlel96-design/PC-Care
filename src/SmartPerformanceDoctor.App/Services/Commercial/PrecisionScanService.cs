using System.Diagnostics;
using System.Runtime.InteropServices;
using SmartPerformanceDoctor.App.Models.Commercial;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class PrecisionScanResult
{
    public string ScannerId { get; init; } = "";
    public string Area { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyList<DiagnosticSignal> Signals { get; init; } = Array.Empty<DiagnosticSignal>();
}

public sealed class PrecisionScanService
{
    public IReadOnlyList<PrecisionScanResult> RunStandardSet() =>
        new[] { ScanMemoryPressure(), ScanDiskSpace(), ScanServiceHealth(), ScanEventLogBurst() };

    public IReadOnlyList<PrecisionScanResult> RunDeepSet()
    {
        var results = RunStandardSet().ToList();
        results.Add(ScanStartupImpact());
        results.Add(ScanWindowsUpdateHealth());
        return results;
    }

    private static PrecisionScanResult ScanMemoryPressure()
    {
        var signals = new List<DiagnosticSignal>();
        try
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref status))
            {
                var usedPct = status.TotalPhys == 0 ? 0 : (1 - (double)status.AvailPhys / status.TotalPhys) * 100;
                var severity = usedPct > 90 ? "critical" : usedPct > 80 ? "high" : usedPct > 70 ? "medium" : "info";
                signals.Add(new DiagnosticSignal
                {
                    SignalId = "memory.pressure.ratio",
                    Area = "memory",
                    Category = "memory_pressure",
                    Source = "GlobalMemoryStatusEx",
                    Severity = severity,
                    Confidence = 0.88f,
                    Evidence = $"메모리 사용률 {usedPct:F1}% (여유 {status.AvailPhys / 1024 / 1024:N0} MB)",
                    RawValue = usedPct.ToString("F1"),
                    NormalizedValue = $"memory used {usedPct:F1}",
                    RecommendedNextProbe = "deep_scan_recommended"
                });
            }
        }
        catch (Exception ex)
        {
            signals.Add(MakeErrorSignal("memory", "memory.pressure.scan_failed", ex.Message));
        }

        return new PrecisionScanResult
        {
            ScannerId = "MemoryPressureScanner",
            Area = "memory",
            Summary = $"메모리 압력 신호 {signals.Count}건",
            Signals = signals
        };
    }

    private static PrecisionScanResult ScanDiskSpace()
    {
        var signals = new List<DiagnosticSignal>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            var freePct = drive.TotalSize == 0 ? 100 : drive.AvailableFreeSpace * 100.0 / drive.TotalSize;
            var severity = freePct < 5 ? "critical" : freePct < 10 ? "high" : freePct < 20 ? "medium" : "info";
            signals.Add(new DiagnosticSignal
            {
                SignalId = $"disk.free.{drive.Name.TrimEnd('\\')}",
                Area = "disk",
                Category = "disk_storage",
                Source = "DriveInfo",
                Severity = severity,
                Confidence = 0.92f,
                Evidence = $"{drive.Name} 여유 {freePct:F1}% ({drive.AvailableFreeSpace / 1024 / 1024 / 1024:N1} GB)",
                RawValue = freePct.ToString("F1"),
                NormalizedValue = $"disk free {freePct:F1}",
                RecommendedNextProbe = freePct < 15 ? "disk_recovery_plan" : "none"
            });
        }

        return new PrecisionScanResult
        {
            ScannerId = "DiskSpaceScanner",
            Area = "disk",
            Summary = $"디스크 여유 공간 신호 {signals.Count}건",
            Signals = signals
        };
    }

    private static PrecisionScanResult ScanServiceHealth()
    {
        var critical = new[] { "Audiosrv", "AudioEndpointBuilder", "PlugPlay", "Dhcp", "Dnscache" };
        var signals = new List<DiagnosticSignal>();
        foreach (var name in critical)
        {
            var status = QueryServiceStatus(name);
            if (status is not null && !status.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
            {
                signals.Add(new DiagnosticSignal
                {
                    SignalId = $"service.{name}.not_running",
                    Area = "system",
                    Category = "service_health",
                    Source = "sc.exe",
                    Severity = "high",
                    Confidence = 0.9f,
                    Evidence = $"{name} 상태: {status}",
                    RawValue = status,
                    NormalizedValue = $"service {name} {status}".ToLowerInvariant(),
                    RecommendedNextProbe = "service_safe_restart"
                });
            }
        }

        return new PrecisionScanResult
        {
            ScannerId = "ServiceHealthScanner",
            Area = "system",
            Summary = signals.Count == 0 ? "핵심 서비스 정상" : $"비정상 서비스 {signals.Count}건",
            Signals = signals
        };
    }

    private static PrecisionScanResult ScanEventLogBurst()
    {
        var signals = new List<DiagnosticSignal>();
        try
        {
            var recentErrors = QueryRecentSystemErrors();
            var severity = recentErrors > 50 ? "critical" : recentErrors > 20 ? "high" : recentErrors > 5 ? "medium" : "info";
            signals.Add(new DiagnosticSignal
            {
                SignalId = "eventlog.system.error_burst",
                Area = "system",
                Category = "event_log_patterns",
                Source = "wevtutil",
                Severity = severity,
                Confidence = 0.8f,
                Evidence = $"최근 24시간 System 오류 {recentErrors}건",
                RawValue = recentErrors.ToString(),
                NormalizedValue = $"eventlog error {recentErrors}",
                RecommendedNextProbe = recentErrors > 10 ? "eventlog_triage" : "none"
            });
        }
        catch (Exception ex)
        {
            signals.Add(MakeErrorSignal("system", "eventlog.burst.scan_failed", ex.Message));
        }

        return new PrecisionScanResult
        {
            ScannerId = "EventLogBurstScanner",
            Area = "system",
            Summary = "이벤트 로그 버스트 분석 완료",
            Signals = signals
        };
    }

    private static int QueryRecentSystemErrors()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "wevtutil.exe",
            Arguments = "qe System /q:\"*[System[(Level=2) and TimeCreated[timediff(@SystemTime) <= 86400000]]]\" /c:200 /rd:true /f:text",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        });
        if (process is null)
        {
            return 0;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(8000);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.Contains("Event ID", StringComparison.OrdinalIgnoreCase));
    }

    private static PrecisionScanResult ScanStartupImpact()
    {
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var count = Directory.Exists(startup)
            ? Directory.GetFiles(startup).Length + Directory.GetDirectories(startup).Length
            : 0;

        return new PrecisionScanResult
        {
            ScannerId = "StartupImpactScanner",
            Area = "system",
            Summary = $"시작 프로그램 영향도 분석 ({count}항목)",
            Signals =
            [
                new DiagnosticSignal
                {
                    SignalId = "startup.user_folder.count",
                    Area = "system",
                    Category = "startup_logon",
                    Source = "ShellStartup",
                    Severity = count > 8 ? "medium" : "info",
                    Confidence = 0.7f,
                    Evidence = $"사용자 시작프로그램 폴더 항목 {count}개",
                    RawValue = count.ToString(),
                    NormalizedValue = $"startup count {count}",
                    RecommendedNextProbe = count > 8 ? "startup_review" : "none"
                }
            ]
        };
    }

    private static PrecisionScanResult ScanWindowsUpdateHealth()
    {
        var status = QueryServiceStatus("wuauserv");
        var signals = new List<DiagnosticSignal>();
        if (status is not null && !status.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add(new DiagnosticSignal
            {
                SignalId = "windows_update.service_unhealthy",
                Area = "system",
                Category = "windows_update",
                Source = "sc.exe",
                Severity = "medium",
                Confidence = 0.75f,
                Evidence = $"Windows Update 서비스 상태: {status}",
                RawValue = status,
                NormalizedValue = "windows update unhealthy",
                RecommendedNextProbe = "windows_update_repair_plan"
            });
        }

        return new PrecisionScanResult
        {
            ScannerId = "WindowsUpdateHealthScanner",
            Area = "system",
            Summary = "Windows Update 상태 확인",
            Signals = signals
        };
    }

    private static string? QueryServiceStatus(string serviceName)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {serviceName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("STATE", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Trim();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static DiagnosticSignal MakeErrorSignal(string area, string id, string message) => new()
    {
        SignalId = id,
        Area = area,
        Category = "scanner",
        Source = "PrecisionScanService",
        Severity = "low",
        Confidence = 0.4f,
        Evidence = message,
        RawValue = message,
        NormalizedValue = message.ToLowerInvariant(),
        RecommendedNextProbe = "retry_scan"
    };

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}