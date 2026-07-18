using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using SmartPerformanceDoctor.App.Models.SystemCare;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

/// <summary>
/// Additional system probes for PCCare 50.2.2 — disk health, pending reboot,
/// auto-start service anomalies, Downloads bloat, proxy config, EmptyWorkingSet.
/// </summary>
public static class CareSystemProbes
{
    public static void AppendPhysicalDiskHealth(List<CareFinding> findings)
    {
        try
        {
            var json = RunPowerShell(
                "try { Get-PhysicalDisk | Select-Object FriendlyName,MediaType,HealthStatus,OperationalStatus,@{n='SizeGB';e={[math]::Round($_.Size/1GB,2)}} | ConvertTo-Json -Compress -Depth 4 } catch { '[]' }",
                20000);
            if (string.IsNullOrWhiteSpace(json) || json == "[]")
            {
                AppendDiskDriveFallback(findings);
                return;
            }

            var unhealthy = json.Contains("Unhealthy", StringComparison.OrdinalIgnoreCase)
                || json.Contains("Warning", StringComparison.OrdinalIgnoreCase)
                || json.Contains("Pred Fail", StringComparison.OrdinalIgnoreCase);

            findings.Add(new CareFinding
            {
                Id = "disk.health",
                Title = unhealthy ? "저장장치 상태 주의" : "저장장치 상태",
                Detail = unhealthy
                    ? "PhysicalDisk HealthStatus가 정상이 아닙니다. SMART/백업을 권장합니다."
                    : "PhysicalDisk 상태가 정상으로 보고됩니다.",
                RiskLabel = unhealthy ? "주의" : "안전",
                RiskCode = unhealthy ? "caution" : "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            AppendDiskDriveFallback(findings);
        }
    }

    private static void AppendDiskDriveFallback(List<CareFinding> findings)
    {
        try
        {
            var json = RunPowerShell(
                "Get-CimInstance Win32_DiskDrive | Select-Object Model,Status,InterfaceType | ConvertTo-Json -Compress -Depth 3",
                15000);
            var bad = !string.IsNullOrWhiteSpace(json)
                && (json.Contains("Pred Fail", StringComparison.OrdinalIgnoreCase)
                    || json.Contains("Error", StringComparison.OrdinalIgnoreCase));
            findings.Add(new CareFinding
            {
                Id = "disk.health",
                Title = bad ? "디스크 상태 주의" : "디스크 상태",
                Detail = bad ? "Win32_DiskDrive Status에 이상이 있습니다." : "디스크 상태 확인 완료.",
                RiskLabel = bad ? "주의" : "안전",
                RiskCode = bad ? "caution" : "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    public static void AppendPendingReboot(List<CareFinding> findings)
    {
        try
        {
            var paths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"
            };

            var hit = false;
            foreach (var path in paths)
            {
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key is not null)
                {
                    hit = true;
                    break;
                }
            }

            // Session Manager PendingFileRenameOperations
            using (var sm = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
            {
                if (sm?.GetValue("PendingFileRenameOperations") is string[] arr && arr.Length > 0)
                {
                    hit = true;
                }
            }

            findings.Add(new CareFinding
            {
                Id = "disk.pending_reboot",
                Title = hit ? "재부팅 대기" : "재부팅 대기 없음",
                Detail = hit
                    ? "Windows 업데이트/구성 변경이 재부팅을 기다리고 있습니다."
                    : "재부팅 대기 플래그가 없습니다.",
                RiskLabel = hit ? "확인 필요" : "안전",
                RiskCode = hit ? "review" : "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    public static void AppendAutoServiceAnomalies(List<CareFinding> findings)
    {
        try
        {
            var json = RunPowerShell(
                "Get-CimInstance Win32_Service | Where-Object { $_.StartMode -eq 'Auto' -and $_.State -ne 'Running' -and $_.Name -notmatch 'MapsBroker|sppsvc|edgeupdate|GoogleUpdater|DoSvc|CDPSvc|OneSyncSvc|WSearch' } | Select-Object -First 12 Name,DisplayName,State | ConvertTo-Json -Compress -Depth 3",
                25000);
            if (string.IsNullOrWhiteSpace(json) || json is "[]" or "null")
            {
                findings.Add(new CareFinding
                {
                    Id = "opt.service_anomaly",
                    Title = "자동 시작 서비스",
                    Detail = "중요한 자동 시작 서비스가 모두 실행 중입니다.",
                    RiskLabel = "안전",
                    RiskCode = "safe",
                    CanAutoApply = false
                });
                return;
            }

            var count = json.Split("\"Name\"", StringSplitOptions.None).Length - 1;
            findings.Add(new CareFinding
            {
                Id = "opt.service_anomaly",
                Title = "중지된 자동 시작 서비스",
                Detail = $"약 {Math.Max(1, count)}개 · 시작 실패·비활성·의존성 문제 가능",
                RiskLabel = count >= 3 ? "주의" : "확인 필요",
                RiskCode = count >= 3 ? "caution" : "review",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    public static void AppendDownloadsFolder(List<CareFinding> findings, CancellationToken ct)
    {
        try
        {
            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
            if (!Directory.Exists(downloads))
            {
                return;
            }

            var scan = CareFolderScanner.Measure(
                downloads,
                ct,
                maxDepth: 2,
                maxFiles: 2500,
                tempFolder: false);

            // Only surface when >= 1.5 GB
            if (scan.TotalBytes < 1500L * 1024 * 1024)
            {
                return;
            }

            findings.Add(new CareFinding
            {
                Id = "junk.downloads",
                Title = "다운로드 폴더 용량",
                Detail = $"{downloads} · 약 {scan.TotalBytes / 1024 / 1024} MB"
                    + (scan.Estimated ? " · 추정" : "")
                    + " · 수동 정리 권장",
                RiskLabel = scan.TotalBytes > 10L * 1024 * 1024 * 1024 ? "확인 필요" : "안전",
                RiskCode = scan.TotalBytes > 10L * 1024 * 1024 * 1024 ? "review" : "safe",
                CanAutoApply = false,
                TargetPath = downloads
            });
        }
        catch
        {
            // Skip.
        }
    }

    public static void AppendProxyFinding(List<CareFinding> findings)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            var enabled = key?.GetValue("ProxyEnable") is int i && i != 0;
            var server = key?.GetValue("ProxyServer") as string ?? "";
            findings.Add(new CareFinding
            {
                Id = "net.proxy",
                Title = enabled ? "프록시 사용 중" : "프록시 미사용",
                Detail = enabled
                    ? $"ProxyServer={server} · 네트워크 지연 원인일 수 있습니다."
                    : "사용자 프록시가 비활성 상태입니다.",
                RiskLabel = enabled ? "확인 필요" : "안전",
                RiskCode = enabled ? "review" : "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    public static void AppendDnsFlushReady(List<CareFinding> findings)
    {
        findings.Add(new CareFinding
        {
            Id = "opt.dns_flush",
            Title = "DNS 캐시 정리",
            Detail = "이름 해석이 느릴 때 DNS 캐시를 비울 수 있습니다.",
            RiskLabel = "안전",
            RiskCode = "safe",
            CanAutoApply = true
        });
    }

    public static bool EmptyWorkingSets(out string detail)
    {
        try
        {
            var trimmed = 0;
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id is 0 or 4)
                    {
                        continue;
                    }

                    if (EmptyWorkingSet(process.Handle))
                    {
                        trimmed++;
                    }
                }
                catch
                {
                    // Access denied / exited.
                }
                finally
                {
                    process.Dispose();
                }
            }

            detail = $"작업 집합 정리 시도 {trimmed}개 프로세스";
            return trimmed > 0;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    public static bool EmptyRecycleBin(out string detail)
    {
        try
        {
            // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
            var hr = SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4);
            detail = hr == 0 ? "휴지통을 비웠습니다." : $"휴지통 비우기 결과 코드 {hr}";
            return hr == 0 || hr == -2147418113; // often already empty / cancelled styles
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    private static string RunPowerShell(string script, int timeoutMs)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"pccare-probe-{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(temp, script, Encoding.UTF8);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{temp}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null)
            {
                return "";
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(timeoutMs);
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            }

            return output.Trim();
        }
        catch
        {
            return "";
        }
        finally
        {
            try { File.Delete(temp); } catch { /* ignore */ }
        }
    }

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, int dwFlags);
}
