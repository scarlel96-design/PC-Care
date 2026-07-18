using System.Diagnostics;
using System.Text.RegularExpressions;
using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

/// <summary>Windows stability signals — BSOD (BugCheck), WHEA, unexpected shutdown.</summary>
public static class SystemStabilityProbe
{
    private const int LookbackDays = 30;
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(10);

    public sealed class SystemStabilityReport
    {
        public int BugCheckCount30d { get; init; }

        public int UnexpectedShutdownCount30d { get; init; }

        public int WheaErrorCount30d { get; init; }

        public int MinidumpCount { get; init; }

        public IReadOnlyList<string> RecentBugCheckCodes { get; init; } = Array.Empty<string>();

        public bool HasRecentStabilityRisk =>
            BugCheckCount30d > 0 || UnexpectedShutdownCount30d > 0 || WheaErrorCount30d > 0;

        public IReadOnlyList<string> ToSignalLines()
        {
            var lines = new List<string>
            {
                $"bugcheck_30d={BugCheckCount30d}",
                $"unexpected_shutdown_30d={UnexpectedShutdownCount30d}",
                $"whea_error_30d={WheaErrorCount30d}",
                $"minidump_count={MinidumpCount}"
            };

            if (RecentBugCheckCodes.Count > 0)
            {
                lines.Add($"bugcheck_codes={string.Join(',', RecentBugCheckCodes)}");
            }

            if (HasRecentStabilityRisk)
            {
                lines.Add("bluescreen stability risk detected");
            }

            return lines;
        }

        public IReadOnlyList<CareFinding> ToCareFindings()
        {
            var findings = new List<CareFinding>();

            if (BugCheckCount30d > 0)
            {
                var codes = RecentBugCheckCodes.Count > 0
                    ? $" · 코드: {string.Join(", ", RecentBugCheckCodes.Take(3))}"
                    : "";
                findings.Add(new CareFinding
                {
                    Id = "stability.bsod",
                    Title = "블루스크린(BugCheck) 기록",
                    Detail = $"최근 {LookbackDays}일 {BugCheckCount30d}건{codes} · 드라이버·메모리·디스크 점검 권장",
                    RiskLabel = BugCheckCount30d >= 2 ? "주의" : "확인 필요",
                    RiskCode = BugCheckCount30d >= 2 ? "caution" : "review",
                    CanAutoApply = false
                });
            }

            if (UnexpectedShutdownCount30d > 0)
            {
                findings.Add(new CareFinding
                {
                    Id = "stability.unexpected_shutdown",
                    Title = "예기치 않은 종료",
                    Detail = $"최근 {LookbackDays}일 {UnexpectedShutdownCount30d}건 · 전원·과열·BSOD 가능성 확인",
                    RiskLabel = "확인 필요",
                    RiskCode = "review",
                    CanAutoApply = false
                });
            }

            if (WheaErrorCount30d > 0)
            {
                findings.Add(new CareFinding
                {
                    Id = "stability.whea",
                    Title = "하드웨어 오류(WHEA)",
                    Detail = $"최근 {LookbackDays}일 {WheaErrorCount30d}건 · RAM·CPU·GPU·SSD 하드웨어 점검 권장",
                    RiskLabel = WheaErrorCount30d >= 3 ? "주의" : "확인 필요",
                    RiskCode = WheaErrorCount30d >= 3 ? "caution" : "review",
                    CanAutoApply = false
                });
            }

            if (MinidumpCount > 0 && BugCheckCount30d == 0)
            {
                findings.Add(new CareFinding
                {
                    Id = "stability.minidump",
                    Title = "크래시 덤프 파일",
                    Detail = $"미니덤프 {MinidumpCount}개 · 이벤트 로그와 함께 원인 분석 권장",
                    RiskLabel = "확인 필요",
                    RiskCode = "review",
                    CanAutoApply = false
                });
            }

            if (findings.Count == 0)
            {
                findings.Add(new CareFinding
                {
                    Id = "stability.ok",
                    Title = "시스템 안정성",
                    Detail = $"최근 {LookbackDays}일 블루스크린·WHEA·비정상 종료 기록 없음",
                    RiskLabel = "안전",
                    RiskCode = "safe",
                    CanAutoApply = false
                });
            }

            return findings;
        }

        public IntelligenceSummary ToIntelligenceSummary()
        {
            if (!HasRecentStabilityRisk)
            {
                return new IntelligenceSummary
                {
                    Score = 92,
                    Status = "양호",
                    PlainSummary = $"최근 {LookbackDays}일 블루스크린·하드웨어 오류·비정상 종료 기록이 없습니다.",
                    RootCauses = []
                };
            }

            var penalty = BugCheckCount30d * 12 + WheaErrorCount30d * 8 + UnexpectedShutdownCount30d * 6;
            var score = Math.Clamp(88 - penalty, 35, 85);
            var causes = new List<RootCauseCandidate>();

            if (BugCheckCount30d > 0)
            {
                causes.Add(new RootCauseCandidate
                {
                    Area = "system",
                    Severity = BugCheckCount30d >= 2 ? "warning" : "info",
                    Evidence = $"bugcheck_{BugCheckCount30d}_30d",
                    Explanation = $"최근 {LookbackDays}일 블루스크린(BugCheck) {BugCheckCount30d}건이 기록되었습니다.",
                    Recommendation = "최신 Windows/칩셋/그래픽 드라이버 업데이트, 메모리 진단(mdsched), SFC/DISM 점검을 권장합니다.",
                    Confidence = 0.85
                });
            }

            if (WheaErrorCount30d > 0)
            {
                causes.Add(new RootCauseCandidate
                {
                    Area = "system",
                    Severity = "warning",
                    Evidence = $"whea_{WheaErrorCount30d}_30d",
                    Explanation = $"WHEA 하드웨어 오류 {WheaErrorCount30d}건 — CPU/GPU/RAM/디스크 불안정 신호일 수 있습니다.",
                    Recommendation = "과열·전원·오버클럭 여부를 확인하고 하드웨어 진단을 실행하세요.",
                    Confidence = 0.8
                });
            }

            if (UnexpectedShutdownCount30d > 0)
            {
                causes.Add(new RootCauseCandidate
                {
                    Area = "system",
                    Severity = "info",
                    Evidence = $"unexpected_shutdown_{UnexpectedShutdownCount30d}_30d",
                    Explanation = $"예기치 않은 시스템 종료 {UnexpectedShutdownCount30d}건 — 전원 차단·BSOD·강제 재부팅 가능성.",
                    Recommendation = "이벤트 뷰어 System 로그와 함께 전원·UPS·과열 상태를 확인하세요.",
                    Confidence = 0.75
                });
            }

            return new IntelligenceSummary
            {
                Score = score,
                Status = score < 60 ? "주의" : "확인 필요",
                PlainSummary = "블루스크린·하드웨어 오류·비정상 종료 신호가 감지되었습니다. 자동 복구 전 원인 점검이 필요합니다.",
                RootCauses = causes,
                Actions =
                [
                    new ActionPlanItem
                    {
                        Priority = "1",
                        Area = "Stability",
                        Action = "드라이버·Windows 업데이트 확인",
                        Reason = "BugCheck의 가장 흔한 원인은 드라이버/커널 충돌입니다.",
                        Risk = "낮음"
                    },
                    new ActionPlanItem
                    {
                        Priority = "2",
                        Area = "Stability",
                        Action = "SFC / DISM 점검",
                        Reason = "시스템 파일 손상이 반복 크래시를 유발할 수 있습니다.",
                        Risk = "낮음"
                    },
                    new ActionPlanItem
                    {
                        Priority = "3",
                        Area = "Stability",
                        Action = "메모리·하드웨어 진단",
                        Reason = "WHEA·반복 BSOD는 RAM/디스크 불안정과 연관될 수 있습니다.",
                        Risk = "중간"
                    }
                ]
            };
        }
    }

    public static SystemStabilityReport Analyze(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bugCheckTask = Task.Run(() => QueryEventLog(
            "System",
            "*[System[(EventID=1001) and TimeCreated[timediff(@SystemTime) <= 2592000000]]]",
            40,
            cancellationToken), cancellationToken);

        var shutdownTask = Task.Run(() => QueryEventLog(
            "System",
            "*[System[(EventID=6008) and TimeCreated[timediff(@SystemTime) <= 2592000000]]]",
            30,
            cancellationToken), cancellationToken);

        var wheaTask = Task.Run(() => QueryEventLog(
            "System",
            "*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] and (Level=2) and TimeCreated[timediff(@SystemTime) <= 2592000000]]]",
            30,
            cancellationToken), cancellationToken);

        var minidumpTask = Task.Run(CountMinidumps, cancellationToken);

        Task.WaitAll([bugCheckTask, shutdownTask, wheaTask, minidumpTask], cancellationToken);

        var bugCheckOutput = bugCheckTask.Result;
        return new SystemStabilityReport
        {
            BugCheckCount30d = CountEvents(bugCheckOutput),
            UnexpectedShutdownCount30d = CountEvents(shutdownTask.Result),
            WheaErrorCount30d = CountEvents(wheaTask.Result),
            MinidumpCount = minidumpTask.Result,
            RecentBugCheckCodes = ExtractBugCheckCodes(bugCheckOutput)
        };
    }

    private static string QueryEventLog(string logName, string xpath, int maxEvents, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "wevtutil.exe",
                Arguments = $"qe {logName} /q:\"{xpath}\" /c:{maxEvents} /rd:true /f:text",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return string.Empty;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(QueryTimeout);
            try
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit((int)QueryTimeout.TotalMilliseconds);
                return output;
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // ignored
                }

                return string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int CountEvents(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return 0;
        }

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.Contains("Event ID", StringComparison.OrdinalIgnoreCase)
                || line.Contains("EventID", StringComparison.OrdinalIgnoreCase)
                || line.Contains("이벤트 ID", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ExtractBugCheckCodes(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<string>();
        }

        var matches = Regex.Matches(output, @"0x[0-9A-Fa-f]{8}", RegexOptions.Multiline);
        return matches.Select(m => m.Value.ToUpperInvariant()).Distinct().Take(5).ToArray();
    }

    private static int CountMinidumps()
    {
        var count = 0;
        foreach (var dir in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps")
                 })
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            try
            {
                count += Directory.EnumerateFiles(dir, "*.dmp", SearchOption.TopDirectoryOnly).Take(50).Count();
            }
            catch
            {
                // ignored
            }
        }

        return count;
    }
}