using System.Diagnostics;
using Microsoft.Win32;
using SmartPerformanceDoctor.App.Models.SystemCare;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

public sealed record OptimizationResult(string Id, string Title, string Detail, bool Success);

public static class SystemOptimizationService
{
    public static async Task<IReadOnlyList<OptimizationResult>> ApplyRecommendedAsync(
        string scope,
        CancellationToken cancellationToken = default)
    {
        var results = new List<OptimizationResult>();
        var aggressive = scope is "system" or "full";

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(FlushDnsCache());

            if (aggressive || scope is "quick")
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(SetHighPerformancePowerPlan());
                results.Add(DisableGameDvr());
                results.Add(OptimizeVisualEffects());
                results.Add(DisableTransparency());
                results.Add(NormalizeTcpAutotuning());
            }

            if (aggressive)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(TrimStandbyMemory());
                results.Add(RunProcessIdleTasks());
            }
        }, cancellationToken).ConfigureAwait(false);

        return results;
    }

    public static OptimizationResult FlushDnsCache() =>
        RunCommand("ipconfig", "/flushdns", "opt.dns_flush", "DNS 캐시 비우기");

    public static OptimizationResult SetHighPerformancePowerPlan()
    {
        var active = RunCommand("powercfg.exe", "/getactivescheme", "opt.powerplan.probe", "전원 계획 확인", requireSuccess: false);
        if (active.Success && (active.Detail.Contains("고성능", StringComparison.OrdinalIgnoreCase)
            || active.Detail.Contains("high performance", StringComparison.OrdinalIgnoreCase)
            || active.Detail.Contains("ultimate", StringComparison.OrdinalIgnoreCase)))
        {
            return new OptimizationResult("opt.powerplan", "전원 계획", "이미 고성능 프로필이 활성화되어 있습니다.", true);
        }

        var list = RunCommand("powercfg.exe", "/list", "opt.powerplan.list", "전원 프로필 목록", requireSuccess: false);
        var highPerfGuid = ExtractPowerSchemeGuid(list.Detail, "고성능", "high performance", "ultimate");
        if (highPerfGuid is null)
        {
            return new OptimizationResult("opt.powerplan", "전원 계획", "고성능 프로필을 찾지 못했습니다. Windows 설정에서 수동 변경을 권장합니다.", false);
        }

        return RunCommand("powercfg.exe", $"/setactive {highPerfGuid}", "opt.powerplan", "고성능 전원 계획 적용");
    }

    public static OptimizationResult DisableGameDvr()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore");
            key?.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
            using var capture = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR");
            capture?.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
            return new OptimizationResult("opt.gamebar", "게임 DVR", "Xbox Game Bar 녹화·캡처를 비활성화했습니다.", true);
        }
        catch (Exception ex)
        {
            return new OptimizationResult("opt.gamebar", "게임 DVR", ex.Message, false);
        }
    }

    public static OptimizationResult OptimizeVisualEffects()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
            key?.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
            return new OptimizationResult("opt.visual_anim", "시각 효과", "Windows 시각 효과를 '최적 성능'으로 설정했습니다.", true);
        }
        catch (Exception ex)
        {
            return new OptimizationResult("opt.visual_anim", "시각 효과", ex.Message, false);
        }
    }

    public static OptimizationResult DisableTransparency()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            key?.SetValue("EnableTransparency", 0, RegistryValueKind.DWord);
            return new OptimizationResult("opt.transparency", "투명 효과", "창·작업 표시줄 투명 효과를 비활성화했습니다.", true);
        }
        catch (Exception ex)
        {
            return new OptimizationResult("opt.transparency", "투명 효과", ex.Message, false);
        }
    }

    public static OptimizationResult NormalizeTcpAutotuning() =>
        RunCommand("netsh", "int tcp set global autotuninglevel=normal", "opt.tcp_autotune", "TCP 자동 조율 최적화");

    public static OptimizationResult TrimStandbyMemory()
    {
        try
        {
            // Direct EmptyWorkingSet — avoids PowerShell Add-Type overhead (50.2.2).
            if (CareSystemProbes.EmptyWorkingSets(out var detail))
            {
                return new OptimizationResult(
                    "opt.standby_trim",
                    "메모리 정리",
                    $"실행 중 프로세스의 작업 집합을 정리해 여유 RAM을 확보했습니다. ({detail})",
                    true);
            }

            return new OptimizationResult("opt.standby_trim", "메모리 정리", detail, false);
        }
        catch (Exception ex)
        {
            return new OptimizationResult("opt.standby_trim", "메모리 정리", ex.Message, false);
        }
    }

    public static OptimizationResult RunProcessIdleTasks() =>
        RunCommand("rundll32.exe", "advapi32.dll,ProcessIdleTasks", "opt.idle_tasks", "유휴 유지보수 작업 예약");

    public static bool TryApplyFinding(CareFinding item)
    {
        if (item.RiskCode is "blocked" or "highrisk" or "caution")
        {
            return false;
        }

        return item.Id switch
        {
            "net.dns" or "net.dns_resolve" or "opt.dns_flush" => FlushDnsCache().Success,
            "opt.gamebar" => DisableGameDvr().Success,
            "opt.visual_anim" or "opt.visual" => OptimizeVisualEffects().Success,
            "opt.powerplan" => SetHighPerformancePowerPlan().Success,
            "opt.transparency" => DisableTransparency().Success,
            "opt.tcp_autotune" => NormalizeTcpAutotuning().Success,
            "opt.standby_trim" or "opt.memory" => TrimStandbyMemory().Success,
            _ => false
        };
    }

    private static OptimizationResult RunCommand(
        string fileName,
        string arguments,
        string id,
        string title,
        bool requireSuccess = true)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return new OptimizationResult(id, title, "프로세스를 시작하지 못했습니다.", false);
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(20000);
            var detail = string.IsNullOrWhiteSpace(output) ? error.Trim() : output.Trim();
            if (detail.Length > 400)
            {
                detail = detail[..400] + "…";
            }

            var success = !requireSuccess || process.ExitCode == 0;
            return new OptimizationResult(id, title, string.IsNullOrWhiteSpace(detail) ? "완료" : detail, success);
        }
        catch (Exception ex)
        {
            return new OptimizationResult(id, title, ex.Message, false);
        }
    }

    private static string? ExtractPowerSchemeGuid(string listOutput, params string[] labels)
    {
        foreach (var line in listOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!labels.Any(label => line.Contains(label, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var start = line.IndexOf('(');
            var end = line.IndexOf(')');
            if (start >= 0 && end > start)
            {
                return line[(start + 1)..end].Trim();
            }
        }

        return null;
    }
}