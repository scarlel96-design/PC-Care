using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using SmartPerformanceDoctor.App.Models.SystemCare;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

public sealed class SystemCareService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static IReadOnlyList<CareModuleInfo> Modules { get; } =
    [
        new() { Kind = CareModuleKind.Registry, Title = "레지스트리 점검", Description = "잘못된 경로·오래된 항목을 찾습니다. 삭제 전 백업합니다." },
        new() { Kind = CareModuleKind.Disk, Title = "디스크 점검", Description = "남은 공간·임시 파일·휴지통 크기를 확인합니다." },
        new() { Kind = CareModuleKind.Privacy, Title = "개인정보 정리", Description = "최근 사용 기록·임시 흔적을 찾습니다." },
        new() { Kind = CareModuleKind.Junk, Title = "불필요 파일 정리", Description = "임시 폴더·오래된 캐시를 찾습니다." },
        new() { Kind = CareModuleKind.Shortcut, Title = "바로가기 점검", Description = "깨진 바로가기를 찾습니다." },
        new() { Kind = CareModuleKind.Optimization, Title = "속도 개선", Description = "메모리·전원·시작 항목·백그라운드 부담을 확인합니다." },
        new() { Kind = CareModuleKind.Internet, Title = "인터넷 설정", Description = "DNS·네트워크 관련 상태를 확인합니다." },
        new() { Kind = CareModuleKind.Vulnerability, Title = "보안 업데이트", Description = "Windows 업데이트 상태를 확인합니다." }
    ];

    public static IReadOnlyList<CareScanTaskDefinition> ScanTasks { get; } =
    [
        new() { Id = "reg.uninstall", Module = CareModuleKind.Registry, Title = "설치·제거 프로그램 경로", Description = "설치 폴더·제거 exe 경로 확인", IncludedInSmart = false },
        new() { Id = "reg.run", Module = CareModuleKind.Registry, Title = "자동 실행 항목", Description = "시작 시 실행 경로 확인", IncludedInSmart = false },
        new() { Id = "reg.recentdocs", Module = CareModuleKind.Registry, Title = "최근 문서 기록", Description = "최근 문서 레지스트리 흔적", IncludedInSmart = false },
        new() { Id = "reg.runonce", Module = CareModuleKind.Registry, Title = "RunOnce 자동 실행", Description = "1회 실행 예약 항목 경로 검증", IncludedInSmart = false },
        new() { Id = "reg.apppaths", Module = CareModuleKind.Registry, Title = "App Paths", Description = "응용 프로그램 경로 등록 무결성", IncludedInSmart = false },
        new() { Id = "reg.services", Module = CareModuleKind.Registry, Title = "서비스 ImagePath", Description = "서비스 실행 파일 경로 검증", IncludedInSmart = false },
        new() { Id = "disk.free", Module = CareModuleKind.Disk, Title = "디스크 여유 공간", Description = "고정 드라이브별 남은 용량", IncludedInSmart = true },
        new() { Id = "disk.temp.user", Module = CareModuleKind.Disk, Title = "사용자 임시 폴더", Description = "사용자 Temp 폴더 크기", IncludedInSmart = true },
        new() { Id = "disk.temp.win", Module = CareModuleKind.Disk, Title = "Windows 임시 폴더", Description = "Windows Temp 폴더 크기", IncludedInSmart = false },
        new() { Id = "disk.ssd_trim", Module = CareModuleKind.Disk, Title = "SSD TRIM 상태", Description = "SSD/NVMe TRIM·retrim 권장 안내", IncludedInSmart = true },
        new() { Id = "privacy.recent", Module = CareModuleKind.Privacy, Title = "최근 파일 바로가기", Description = "최근 항목 폴더 크기", IncludedInSmart = true },
        new() { Id = "privacy.thumbcache", Module = CareModuleKind.Privacy, Title = "미리보기 이미지 캐시", Description = "thumbcache 파일", IncludedInSmart = false },
        new() { Id = "junk.temp.user", Module = CareModuleKind.Junk, Title = "사용자 Temp 오래된 파일", Description = "7일 이상 임시 파일", IncludedInSmart = true },
        new() { Id = "junk.temp.local", Module = CareModuleKind.Junk, Title = "로컬 Temp 오래된 파일", Description = "LocalAppData Temp 정리 후보", IncludedInSmart = true },
        new() { Id = "shortcut", Module = CareModuleKind.Shortcut, Title = "깨진 바로가기", Description = "바탕화면·시작 메뉴 .lnk", IncludedInSmart = true },
        new() { Id = "opt.startup", Module = CareModuleKind.Optimization, Title = "시작 프로그램", Description = "시작 폴더 항목 수", IncludedInSmart = true },
        new() { Id = "opt.visual", Module = CareModuleKind.Optimization, Title = "시각 효과 안내", Description = "저사양 PC 화면 설정 안내", IncludedInSmart = false },
        new() { Id = "net.dns", Module = CareModuleKind.Internet, Title = "DNS 캐시", Description = "네트워크 DNS 상태 안내", IncludedInSmart = false },
        new() { Id = "net.gateway", Module = CareModuleKind.Internet, Title = "기본 게이트웨이", Description = "게이트웨이 연결·지연 확인", IncludedInSmart = true },
        new() { Id = "net.adapter", Module = CareModuleKind.Internet, Title = "네트워크 어댑터", Description = "활성 어댑터·링크 속도", IncludedInSmart = true },
        new() { Id = "vuln.wuauserv", Module = CareModuleKind.Vulnerability, Title = "Windows 업데이트 서비스", Description = "wuauserv 실행 상태", IncludedInSmart = true },
        new() { Id = "vuln.firewall", Module = CareModuleKind.Vulnerability, Title = "Windows 방화벽", Description = "도메인/개인/공용 프로필 상태", IncludedInSmart = true },
        new() { Id = "vuln.defender", Module = CareModuleKind.Vulnerability, Title = "Defender 상태", Description = "실시간 보호·정의 업데이트", IncludedInSmart = true },
        new() { Id = "vuln.uac", Module = CareModuleKind.Vulnerability, Title = "UAC 수준", Description = "사용자 계정 컨트롤 설정", IncludedInSmart = true },
        new() { Id = "junk.recycle", Module = CareModuleKind.Junk, Title = "휴지통", Description = "휴지통 용량·항목 수", IncludedInSmart = true },
        new() { Id = "junk.prefetch", Module = CareModuleKind.Junk, Title = "Prefetch 캐시", Description = "Windows Prefetch 폴더", IncludedInSmart = false },
        new() { Id = "junk.wu_cache", Module = CareModuleKind.Junk, Title = "Windows Update 캐시", Description = "SoftwareDistribution\\Download", IncludedInSmart = false },
        new() { Id = "opt.memory", Module = CareModuleKind.Optimization, Title = "메모리 사용량", Description = "RAM·커밋 사용률·상위 프로세스", IncludedInSmart = true },
        new() { Id = "opt.powerplan", Module = CareModuleKind.Optimization, Title = "전원 계획", Description = "활성 전원 프로필 확인", IncludedInSmart = true },
        new() { Id = "opt.sysmain", Module = CareModuleKind.Optimization, Title = "SysMain(슈퍼페치)", Description = "SysMain 서비스 상태", IncludedInSmart = true },
        new() { Id = "opt.searchindex", Module = CareModuleKind.Optimization, Title = "Windows Search", Description = "인덱서 서비스 상태", IncludedInSmart = false },
        new() { Id = "opt.startup_reg", Module = CareModuleKind.Optimization, Title = "레지스트리 자동 실행", Description = "Run/RunOnce 항목 수", IncludedInSmart = true },
        new() { Id = "opt.visual_anim", Module = CareModuleKind.Optimization, Title = "시각 효과·애니메이션", Description = "화면 효과 설정 확인", IncludedInSmart = true },
        new() { Id = "opt.gamebar", Module = CareModuleKind.Optimization, Title = "Xbox Game Bar", Description = "게임 DVR·녹화 설정", IncludedInSmart = false },
        new() { Id = "junk.browser_cache", Module = CareModuleKind.Junk, Title = "브라우저 캐시", Description = "Edge/Chrome 캐시 크기", IncludedInSmart = true },
        new() { Id = "junk.logs", Module = CareModuleKind.Junk, Title = "로그·진단 파일", Description = "CBS·Windows Logs 폴더", IncludedInSmart = false },
        new() { Id = "junk.delivery", Module = CareModuleKind.Junk, Title = "전달 최적화 캐시", Description = "Delivery Optimization 저장소", IncludedInSmart = true },
        new() { Id = "disk.pagefile", Module = CareModuleKind.Disk, Title = "페이지 파일", Description = "페이지 파일 설정 안내", IncludedInSmart = false },
        new() { Id = "disk.hiberfil", Module = CareModuleKind.Disk, Title = "최대 절전 파일", Description = "hiberfil.sys 크기", IncludedInSmart = false },
        new() { Id = "net.dns_resolve", Module = CareModuleKind.Internet, Title = "DNS 응답 테스트", Description = "로컬 DNS 해석 지연", IncludedInSmart = true }
    ];

    public Task<CareScanResult> ScanByTasksAsync(
        CareScanMode mode,
        IReadOnlyList<string> taskIds,
        IProgress<(int percent, string message)>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            progress?.Report((5, "검사 준비 중…"));
            cancellationToken.ThrowIfCancellationRequested();

            var enabled = taskIds.Where(id => ScanTasks.Any(t => t.Id == id)).Distinct().ToArray();
            if (enabled.Length == 0)
            {
                throw new InvalidOperationException("검사할 항목을 하나 이상 선택하세요.");
            }

            var audit = CreateAuditFolder(mode);
            CareAuditChain.InitializeManifest(audit, mode, enabled);
            CareAuditChain.Append(audit, "scan-start", $"{mode} · {enabled.Length}개 항목");
            var findings = new List<CareFinding>();
            for (var i = 0; i < enabled.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var taskId = enabled[i];
                var task = ScanTasks.First(t => t.Id == taskId);
                var pct = 8 + (int)((i + 1) / (double)enabled.Length * 84);
                progress?.Report((pct, $"{task.Title} 검사 중…"));
                RunScanTask(taskId, findings, cancellationToken);
            }

            var title = mode == CareScanMode.Smart ? "스마트 검사" : "정밀 점검";
            var health = CareHealthScorer.Score(findings);
            CareAuditChain.Append(audit, "scan-complete", $"발견 {findings.Count} · 점수 {health.Score}");
            var result = new CareScanResult
            {
                Mode = mode,
                Module = CareModuleKind.Registry,
                ModuleTitle = title,
                Summary = $"{BuildSummary(findings)} · {health.Summary}",
                HealthScore = health.Score,
                HealthGrade = health.Grade,
                AuditChainValid = CareAuditChain.Verify(audit),
                EnabledTasks = enabled,
                Findings = findings,
                AuditFolder = audit
            };

            File.WriteAllText(Path.Combine(audit, "scan.json"), JsonSerializer.Serialize(result, JsonOptions), Encoding.UTF8);
            progress?.Report((100, "검사 완료"));
            return result;
        }, cancellationToken);

    public Task<CareApplyResult> ApplySafeItemsAsync(
        CareScanResult scan,
        bool includeReviewItems,
        IProgress<(int percent, string message)>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var applied = 0;
            var skipped = 0;
            var quarantine = Path.Combine(scan.AuditFolder, "quarantine");
            Directory.CreateDirectory(quarantine);
            CareAuditChain.Append(scan.AuditFolder, "apply-start", $"대상 필터 · review={includeReviewItems}");

            var targets = scan.Findings.Where(f => f.CanAutoApply || (includeReviewItems && f.RiskCode == "review")).ToArray();
            progress?.Report((10, $"적용 대상 {targets.Length}개 확인"));

            for (var i = 0; i < targets.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = targets[i];
                var pct = 10 + (int)((i + 1) / (double)Math.Max(1, targets.Length) * 80);
                progress?.Report((pct, $"적용 중: {item.Title}"));

                if (TryApplyFinding(item, quarantine, scan.AuditFolder, InferModule(item)))
                {
                    applied++;
                    CareAuditChain.Append(scan.AuditFolder, "apply-item", item.Id);
                }
                else
                {
                    skipped++;
                }
            }

            CareAuditChain.Append(scan.AuditFolder, "apply-complete", $"적용 {applied} · 건너뜀 {skipped}");
            File.WriteAllText(
                Path.Combine(scan.AuditFolder, "apply_result.json"),
                JsonSerializer.Serialize(new { applied, skipped, includeReviewItems }, JsonOptions),
                Encoding.UTF8);

            progress?.Report((100, "적용 완료"));
            return new CareApplyResult
            {
                Success = true,
                AppliedCount = applied,
                SkippedCount = skipped,
                AuditFolder = scan.AuditFolder,
                Message = applied > 0
                    ? $"적용 {applied}개 · 건너뜀 {skipped}개"
                    : "적용할 수 있는 항목이 없습니다."
            };
        }, cancellationToken);

    private static string BuildSummary(IReadOnlyList<CareFinding> findings)
    {
        if (findings.Count == 0)
        {
            return "특이 항목 없음";
        }

        var safe = findings.Count(f => f.RiskCode == "safe");
        var review = findings.Count(f => f.RiskCode == "review");
        var caution = findings.Count(f => f.RiskCode == "caution");
        var highrisk = findings.Count(f => f.RiskCode == "highrisk");
        var blocked = findings.Count(f => f.RiskCode == "blocked");
        return $"총 {findings.Count}개 · 안전 {safe} · 확인 {review} · 주의 {caution} · 고위험 {highrisk} · 제외 {blocked}";
    }

    private static string CreateAuditFolder(CareScanMode mode)
    {
        var label = mode == CareScanMode.Smart ? "smart" : "precision";
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var productRoot = new[]
        {
            Path.Combine(localAppData, "PCCare"),
            Path.Combine(localAppData, "AstraCare"),
            Path.Combine(localAppData, "SmartPerformanceDoctor")
        }.FirstOrDefault(Directory.Exists) ?? Path.Combine(localAppData, "PCCare");
        var root = Path.Combine(
            productRoot,
            "system_care",
            label,
            DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void RunScanTask(string taskId, List<CareFinding> findings, CancellationToken ct)
    {
        switch (taskId)
        {
            case "reg.uninstall":
                ScanUninstallKeys(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", findings);
                ScanUninstallKeys(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", findings);
                ct.ThrowIfCancellationRequested();
                break;
            case "reg.run":
                ScanRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", findings);
                ScanRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", findings);
                ct.ThrowIfCancellationRequested();
                break;
            case "reg.recentdocs":
                AddPrivacyTraceFinding(findings, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs");
                ct.ThrowIfCancellationRequested();
                break;
            case "reg.runonce":
                ScanRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", findings);
                ScanRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", findings);
                ct.ThrowIfCancellationRequested();
                break;
            case "reg.apppaths":
                ScanAppPaths(findings);
                ct.ThrowIfCancellationRequested();
                break;
            case "reg.services":
                ScanServiceImagePaths(findings);
                ct.ThrowIfCancellationRequested();
                break;
            case "disk.free":
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    ct.ThrowIfCancellationRequested();
                    var freePct = drive.TotalSize == 0 ? 100 : drive.AvailableFreeSpace * 100.0 / drive.TotalSize;
                    findings.Add(new CareFinding
                    {
                        Id = $"disk.free.{drive.Name}",
                        Title = $"{drive.Name} 여유 {freePct:F1}%",
                        Detail = $"남은 용량 {drive.AvailableFreeSpace / 1024 / 1024 / 1024:F1} GB",
                        RiskLabel = freePct < 10 ? "주의" : "안전",
                        RiskCode = freePct < 10 ? "caution" : "safe",
                        CanAutoApply = false
                    });
                }
                break;
            case "disk.temp.user":
                AddFolderSizeFinding(findings, "사용자 임시 폴더", Path.GetTempPath(), "disk.temp.user", safeApply: true);
                break;
            case "disk.temp.win":
                AddFolderSizeFinding(findings, "Windows 임시 폴더", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), "disk.temp.win", safeApply: false);
                break;
            case "disk.ssd_trim":
                AppendSsdTrimAdvisory(findings);
                break;
            case "privacy.recent":
                AddFolderSizeFinding(findings, "최근 파일 바로가기", Environment.GetFolderPath(Environment.SpecialFolder.Recent), "privacy.recent", safeApply: true);
                break;
            case "privacy.thumbcache":
                AppendThumbCacheFinding(findings);
                break;
            case "junk.temp.user":
                AddOldTempFilesFinding(findings, Path.GetTempPath(), "junk.temp.user");
                break;
            case "junk.temp.local":
                var localTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
                if (Directory.Exists(localTemp))
                {
                    AddOldTempFilesFinding(findings, localTemp, "junk.temp.local");
                }
                break;
            case "shortcut":
                AppendShortcutFindings(findings, ct);
                break;
            case "opt.startup":
                AppendStartupFinding(findings);
                break;
            case "opt.visual":
                findings.Add(new CareFinding
                {
                    Id = "opt.visual",
                    Title = "시각 효과 설정",
                    Detail = "배터리·저사양 PC는 단순 화면 모드가 도움이 될 수 있습니다.",
                    RiskLabel = "안전",
                    RiskCode = "safe",
                    CanAutoApply = false
                });
                break;
            case "net.dns":
                findings.Add(new CareFinding
                {
                    Id = "net.dns",
                    Title = "DNS 캐시",
                    Detail = "문제가 있을 때만 캐시 비우기를 권장합니다. DNS 서버 자동 변경은 수행하지 않습니다.",
                    RiskLabel = "안전",
                    RiskCode = "safe",
                    CanAutoApply = false
                });
                break;
            case "net.gateway":
                AppendGatewayFinding(findings);
                break;
            case "net.adapter":
                AppendNetworkAdapterFindings(findings);
                break;
            case "vuln.wuauserv":
                var status = QueryServiceState("wuauserv");
                findings.Add(new CareFinding
                {
                    Id = "vuln.wuauserv",
                    Title = "Windows 업데이트 서비스",
                    Detail = status,
                    RiskLabel = status.Contains("실행", StringComparison.Ordinal) ? "안전" : "확인 필요",
                    RiskCode = status.Contains("실행", StringComparison.Ordinal) ? "safe" : "review",
                    CanAutoApply = false
                });
                break;
            case "vuln.firewall":
                AppendFirewallFinding(findings);
                break;
            case "vuln.defender":
                AppendDefenderFinding(findings);
                break;
            case "vuln.uac":
                AppendUacFinding(findings);
                break;
            case "junk.recycle":
                AppendRecycleBinFinding(findings);
                break;
            case "junk.prefetch":
                AddFolderSizeFinding(
                    findings,
                    "Prefetch 캐시",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"),
                    "junk.prefetch",
                    safeApply: false);
                break;
            case "junk.wu_cache":
                AddFolderSizeFinding(
                    findings,
                    "Windows Update 다운로드 캐시",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\Download"),
                    "junk.wu_cache",
                    safeApply: false);
                break;
            case "opt.memory":
                AppendMemoryFinding(findings);
                break;
            case "opt.powerplan":
                AppendPowerPlanFinding(findings);
                break;
            case "opt.sysmain":
                AppendServiceAdvisory(findings, "SysMain", "opt.sysmain", "SSD에서는 비활성도 정상일 수 있습니다.");
                break;
            case "opt.searchindex":
                AppendServiceAdvisory(findings, "WSearch", "opt.searchindex", "인덱서 중지 시 검색이 느려질 수 있습니다.");
                break;
            case "opt.startup_reg":
                AppendStartupRegistryFinding(findings);
                break;
            case "opt.visual_anim":
                AppendVisualEffectsFinding(findings);
                break;
            case "opt.gamebar":
                AppendGameBarFinding(findings);
                break;
            case "junk.browser_cache":
                AppendBrowserCacheFindings(findings);
                break;
            case "junk.logs":
                AppendLogFolderFindings(findings);
                break;
            case "junk.delivery":
                AppendDeliveryCacheFinding(findings);
                break;
            case "disk.pagefile":
                AppendPagefileFinding(findings);
                break;
            case "disk.hiberfil":
                AppendHiberfilFinding(findings);
                break;
            case "net.dns_resolve":
                AppendDnsResolveFinding(findings);
                break;
        }
    }

    private static void AppendRecycleBinFinding(List<CareFinding> findings)
    {
        try
        {
            var recycle = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\Windows\Explorer");
            if (!Directory.Exists(recycle))
            {
                return;
            }

            long size = 0;
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(recycle, "$I*", SearchOption.TopDirectoryOnly))
            {
                count++;
                try
                {
                    size += new FileInfo(file).Length;
                }
                catch
                {
                    // Skip.
                }
            }

            if (count == 0)
            {
                return;
            }

            findings.Add(new CareFinding
            {
                Id = "junk.recycle",
                Title = "휴지통 메타데이터",
                Detail = $"항목 흔적 {count}개 · 약 {size / 1024} KB · 비우기 권장",
                RiskLabel = "안전",
                RiskCode = "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static CareModuleKind InferModule(CareFinding item)
    {
        if (item.Id.StartsWith("reg.", StringComparison.Ordinal))
        {
            return CareModuleKind.Registry;
        }

        if (item.Id.StartsWith("disk.", StringComparison.Ordinal))
        {
            return CareModuleKind.Disk;
        }

        if (item.Id.StartsWith("privacy.", StringComparison.Ordinal))
        {
            return CareModuleKind.Privacy;
        }

        if (item.Id.StartsWith("junk.", StringComparison.Ordinal))
        {
            return CareModuleKind.Junk;
        }

        if (item.Id.StartsWith("shortcut.", StringComparison.Ordinal))
        {
            return CareModuleKind.Shortcut;
        }

        if (item.Id.StartsWith("opt.", StringComparison.Ordinal))
        {
            return CareModuleKind.Optimization;
        }

        if (item.Id.StartsWith("net.", StringComparison.Ordinal))
        {
            return CareModuleKind.Internet;
        }

        if (item.Id.StartsWith("vuln.", StringComparison.Ordinal))
        {
            return CareModuleKind.Vulnerability;
        }

        return CareModuleKind.Registry;
    }

    private static void AppendThumbCacheFinding(List<CareFinding> findings)
    {
        var thumb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Explorer");
        if (!Directory.Exists(thumb))
        {
            return;
        }

        var thumbCache = Directory.GetFiles(thumb, "thumbcache_*.db", SearchOption.TopDirectoryOnly).Length;
        if (thumbCache > 0)
        {
            findings.Add(new CareFinding
            {
                Id = "privacy.thumbcache",
                Title = "미리보기 이미지 캐시",
                Detail = $"파일 {thumbCache}개 · 정리 가능",
                RiskLabel = "안전",
                RiskCode = "safe",
                CanAutoApply = true,
                TargetPath = thumb
            });
        }
    }

    private static void AppendShortcutFindings(List<CareFinding> findings, CancellationToken ct)
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs)
        };

        foreach (var folder in folders.Distinct())
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var lnk in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                if (IsShortcutBroken(lnk))
                {
                    findings.Add(new CareFinding
                    {
                        Id = $"shortcut.{Guid.NewGuid():N}",
                        Title = "깨진 바로가기",
                        Detail = lnk,
                        RiskLabel = "안전",
                        RiskCode = "safe",
                        CanAutoApply = true,
                        TargetPath = lnk
                    });
                }
            }
        }
    }

    private static void AppendStartupFinding(List<CareFinding> findings)
    {
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var count = Directory.Exists(startup)
            ? Directory.GetFiles(startup).Length + Directory.GetDirectories(startup).Length
            : 0;
        findings.Add(new CareFinding
        {
            Id = "opt.startup",
            Title = "시작 프로그램",
            Detail = $"시작 폴더 항목 {count}개",
            RiskLabel = count > 8 ? "확인 필요" : "안전",
            RiskCode = count > 8 ? "review" : "safe",
            CanAutoApply = false
        });
    }

    private static IReadOnlyList<CareFinding> ScanRegistry(string depth, IProgress<(int, string)>? progress, CancellationToken ct)
    {
        var list = new List<CareFinding>();
        progress?.Report((20, "설치 목록 확인 중…"));
        ScanUninstallKeys(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", list);
        ScanUninstallKeys(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", list);
        ct.ThrowIfCancellationRequested();

        progress?.Report((50, "자동 실행 항목 확인 중…"));
        ScanRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", list);
        ScanRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", list);

        if (depth != "quick")
        {
            progress?.Report((70, "최근 문서 기록 확인 중…"));
            AddPrivacyTraceFinding(list, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs");
        }

        return list;
    }

    private static void ScanUninstallKeys(RegistryKey root, string subPath, List<CareFinding> list)
    {
        using var key = root.OpenSubKey(subPath);
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(name);
            var display = sub?.GetValue("DisplayName") as string ?? name;
            var loc = sub?.GetValue("InstallLocation") as string ?? "";
            var uninstall = sub?.GetValue("UninstallString") as string ?? "";
            if (!string.IsNullOrWhiteSpace(loc) && !Directory.Exists(loc))
            {
                list.Add(new CareFinding
                {
                    Id = $"reg.uninstall.{name}",
                    Title = $"설치 경로 없음: {display}",
                    Detail = loc,
                    RiskLabel = "확인 필요",
                    RiskCode = "review",
                    CanAutoApply = false
                });
            }

            if (!string.IsNullOrWhiteSpace(uninstall) && uninstall.Contains(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var exe = ExtractExePath(uninstall);
                if (!string.IsNullOrWhiteSpace(exe) && !File.Exists(exe))
                {
                    list.Add(new CareFinding
                    {
                        Id = $"reg.uninstall.exe.{name}",
                        Title = $"제거 프로그램 없음: {display}",
                        Detail = exe,
                        RiskLabel = "확인 필요",
                        RiskCode = "review",
                        CanAutoApply = false
                    });
                }
            }
        }
    }

    private static void ScanRunKey(RegistryKey root, string subPath, List<CareFinding> list)
    {
        using var key = root.OpenSubKey(subPath);
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetValueNames())
        {
            var value = key.GetValue(name)?.ToString() ?? "";
            var exe = ExtractExePath(value);
            if (!string.IsNullOrWhiteSpace(exe) && !File.Exists(exe))
            {
                list.Add(new CareFinding
                {
                    Id = $"reg.run.{name}",
                    Title = $"자동 실행 경로 없음: {name}",
                    Detail = value,
                    RiskLabel = "주의",
                    RiskCode = "caution",
                    CanAutoApply = false
                });
            }
        }
    }

    private static void AddPrivacyTraceFinding(List<CareFinding> list, RegistryKey root, string subPath)
    {
        using var key = root.OpenSubKey(subPath);
        if (key is null)
        {
            return;
        }

        var count = key.SubKeyCount + key.ValueCount;
        if (count > 0)
        {
            list.Add(new CareFinding
            {
                Id = "reg.recentdocs",
                Title = "최근 문서 기록",
                Detail = $"항목 {count}개 · 정리 가능",
                RiskLabel = "안전",
                RiskCode = "safe",
                CanAutoApply = false
            });
        }
    }

    private static IReadOnlyList<CareFinding> ScanDisk(IProgress<(int, string)>? progress, CancellationToken ct)
    {
        var list = new List<CareFinding>();
        progress?.Report((30, "디스크 여유 공간 확인 중…"));
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            ct.ThrowIfCancellationRequested();
            var freePct = drive.TotalSize == 0 ? 100 : drive.AvailableFreeSpace * 100.0 / drive.TotalSize;
            list.Add(new CareFinding
            {
                Id = $"disk.free.{drive.Name}",
                Title = $"{drive.Name} 여유 {freePct:F1}%",
                Detail = $"남은 용량 {drive.AvailableFreeSpace / 1024 / 1024 / 1024:F1} GB",
                RiskLabel = freePct < 10 ? "주의" : "안전",
                RiskCode = freePct < 10 ? "caution" : "safe",
                CanAutoApply = false
            });
        }

        progress?.Report((60, "임시 폴더 크기 확인 중…"));
        AddFolderSizeFinding(list, "사용자 임시 폴더", Path.GetTempPath(), "disk.temp.user", safeApply: true);
        var winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        AddFolderSizeFinding(list, "Windows 임시 폴더", winTemp, "disk.temp.win", safeApply: false);

        return list;
    }

    private static IReadOnlyList<CareFinding> ScanPrivacy(IProgress<(int, string)>? progress, CancellationToken ct)
    {
        progress?.Report((40, "개인 기록 폴더 확인 중…"));
        var list = new List<CareFinding>();
        var recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        AddFolderSizeFinding(list, "최근 파일 바로가기", recent, "privacy.recent", safeApply: true);
        ct.ThrowIfCancellationRequested();

        var thumb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Explorer");
        if (Directory.Exists(thumb))
        {
            var thumbCache = Directory.GetFiles(thumb, "thumbcache_*.db", SearchOption.TopDirectoryOnly).Length;
            if (thumbCache > 0)
            {
                list.Add(new CareFinding
                {
                    Id = "privacy.thumbcache",
                    Title = "미리보기 이미지 캐시",
                    Detail = $"파일 {thumbCache}개 · 정리 가능",
                    RiskLabel = "안전",
                    RiskCode = "safe",
                    CanAutoApply = true,
                    TargetPath = thumb
                });
            }
        }

        return list;
    }

    private static IReadOnlyList<CareFinding> ScanJunk(IProgress<(int, string)>? progress, CancellationToken ct)
    {
        progress?.Report((35, "불필요 파일 검색 중…"));
        var list = new List<CareFinding>();
        AddOldTempFilesFinding(list, Path.GetTempPath(), "junk.temp.user");
        ct.ThrowIfCancellationRequested();
        var localTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
        if (Directory.Exists(localTemp))
        {
            AddOldTempFilesFinding(list, localTemp, "junk.temp.local");
        }

        return list;
    }

    private static IReadOnlyList<CareFinding> ScanShortcuts(IProgress<(int, string)>? progress, CancellationToken ct)
    {
        progress?.Report((40, "바로가기 확인 중…"));
        var list = new List<CareFinding>();
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs)
        };

        foreach (var folder in folders.Distinct())
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var lnk in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                if (IsShortcutBroken(lnk))
                {
                    list.Add(new CareFinding
                    {
                        Id = $"shortcut.{Guid.NewGuid():N}",
                        Title = "깨진 바로가기",
                        Detail = lnk,
                        RiskLabel = "안전",
                        RiskCode = "safe",
                        CanAutoApply = true,
                        TargetPath = lnk
                    });
                }
            }
        }

        return list;
    }

    private static IReadOnlyList<CareFinding> ScanOptimization(IProgress<(int, string)>? progress, CancellationToken ct)
    {
        progress?.Report((45, "시작 프로그램 확인 중…"));
        var list = new List<CareFinding>();
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var count = Directory.Exists(startup)
            ? Directory.GetFiles(startup).Length + Directory.GetDirectories(startup).Length
            : 0;
        list.Add(new CareFinding
        {
            Id = "opt.startup",
            Title = "시작 프로그램",
            Detail = $"시작 폴더 항목 {count}개",
            RiskLabel = count > 8 ? "확인 필요" : "안전",
            RiskCode = count > 8 ? "review" : "safe",
            CanAutoApply = false
        });

        ct.ThrowIfCancellationRequested();
        list.Add(new CareFinding
        {
            Id = "opt.visual",
            Title = "시각 효과 설정",
            Detail = "배터리·저사양 PC는 단순 화면 모드가 도움이 될 수 있습니다.",
            RiskLabel = "안전",
            RiskCode = "safe",
            CanAutoApply = false
        });
        return list;
    }

    private static IReadOnlyList<CareFinding> ScanInternet(IProgress<(int, string)>? progress, CancellationToken ct)
    {
        progress?.Report((50, "네트워크 상태 확인 중…"));
        var list = new List<CareFinding>
        {
            new()
            {
                Id = "net.dns",
                Title = "DNS 캐시",
                Detail = "문제가 있을 때만 캐시 비우기를 권장합니다.",
                RiskLabel = "안전",
                RiskCode = "safe",
                CanAutoApply = false
            }
        };
        ct.ThrowIfCancellationRequested();
        return list;
    }

    private static IReadOnlyList<CareFinding> ScanVulnerability(IProgress<(int, string)>? progress, CancellationToken ct)
    {
        progress?.Report((55, "Windows 업데이트 확인 중…"));
        ct.ThrowIfCancellationRequested();
        var status = QueryServiceState("wuauserv");
        return
        [
            new CareFinding
            {
                Id = "vuln.wuauserv",
                Title = "Windows 업데이트 서비스",
                Detail = status,
                RiskLabel = status.Contains("실행", StringComparison.Ordinal) ? "안전" : "확인 필요",
                RiskCode = status.Contains("실행", StringComparison.Ordinal) ? "safe" : "review",
                CanAutoApply = false
            }
        ];
    }

    private static bool TryApplyFinding(CareFinding item, string quarantine, string auditFolder, CareModuleKind module)
    {
        if (item.RiskCode is "blocked" or "highrisk" or "caution")
        {
            return false;
        }

        try
        {
            switch (module)
            {
                case CareModuleKind.Junk:
                case CareModuleKind.Privacy:
                    if (!string.IsNullOrWhiteSpace(item.TargetPath) && Directory.Exists(item.TargetPath))
                    {
                        foreach (var file in Directory.EnumerateFiles(item.TargetPath, "*", SearchOption.TopDirectoryOnly))
                        {
                            TryQuarantineFile(file, quarantine, auditFolder);
                        }
                        return true;
                    }
                    break;
                case CareModuleKind.Shortcut:
                    if (!string.IsNullOrWhiteSpace(item.TargetPath) && File.Exists(item.TargetPath))
                    {
                        TryQuarantineFile(item.TargetPath, quarantine, auditFolder);
                        return true;
                    }
                    break;
            }

            if (item.Id.StartsWith("junk.", StringComparison.Ordinal))
            {
                return CleanOldTempFiles(item.Detail, quarantine, auditFolder);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static void AddFolderSizeFinding(List<CareFinding> list, string title, string path, string id, bool safeApply)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        long size = 0;
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                size += new FileInfo(file).Length;
                count++;
                if (count > 5000)
                {
                    break;
                }
            }
            catch
            {
                // Skip locked files.
            }
        }

        if (size < 10 * 1024 * 1024)
        {
            return;
        }

        list.Add(new CareFinding
        {
            Id = id,
            Title = title,
            Detail = $"{path} · 약 {size / 1024 / 1024} MB",
            RiskLabel = safeApply ? "안전" : "확인 필요",
            RiskCode = safeApply ? "safe" : "review",
            CanAutoApply = safeApply,
            TargetPath = path
        });
    }

    private static void AddOldTempFilesFinding(List<CareFinding> list, string tempPath, string idPrefix)
    {
        if (!Directory.Exists(tempPath))
        {
            return;
        }

        var old = 0;
        long size = 0;
        var cutoff = DateTime.Now.AddDays(-7);
        foreach (var file in Directory.EnumerateFiles(tempPath, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime < cutoff)
                {
                    old++;
                    size += info.Length;
                }
            }
            catch
            {
                // Skip.
            }
        }

        if (old == 0)
        {
            return;
        }

        list.Add(new CareFinding
        {
            Id = $"{idPrefix}.old",
            Title = "오래된 임시 파일",
            Detail = $"{tempPath} · {old}개 · 약 {size / 1024 / 1024} MB",
            RiskLabel = "안전",
            RiskCode = "safe",
            CanAutoApply = true,
            TargetPath = tempPath
        });
    }

    private static bool CleanOldTempFiles(string detail, string quarantine, string auditFolder)
    {
        var path = detail.Split('·')[0].Trim();
        if (!Directory.Exists(path))
        {
            return false;
        }

        var cutoff = DateTime.Now.AddDays(-7);
        var cleaned = 0;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (new FileInfo(file).LastWriteTime < cutoff && TryQuarantineFile(file, quarantine, auditFolder))
                {
                    cleaned++;
                }
            }
            catch
            {
                // Skip locked.
            }
        }

        return cleaned > 0;
    }

    private static bool TryQuarantineFile(string source, string quarantine, string auditFolder)
    {
        var name = Path.GetFileName(source);
        var dest = Path.Combine(quarantine, $"{DateTimeOffset.Now:HHmmss}_{name}");
        Directory.CreateDirectory(quarantine);
        File.Move(source, dest, true);
        CareRollbackService.RecordQuarantine(auditFolder, source, dest);
        return true;
    }

    private static bool IsShortcutBroken(string lnkPath)
    {
        try
        {
            var target = ResolveShortcutTarget(lnkPath);
            return !string.IsNullOrWhiteSpace(target) && !File.Exists(target) && !Directory.Exists(target);
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveShortcutTarget(string lnkPath)
    {
        var bytes = File.ReadAllBytes(lnkPath);
        if (bytes.Length < 0x4C)
        {
            return null;
        }

        var flags = BitConverter.ToUInt32(bytes, 0x14);
        var pos = 0x4C;
        if ((flags & 0x1) != 0)
        {
            pos += 2 + BitConverter.ToUInt16(bytes, pos);
        }

        if ((flags & 0x2) != 0)
        {
            pos += 2 + BitConverter.ToUInt16(bytes, pos);
        }

        if ((flags & 0x4) != 0)
        {
            pos += 2 + BitConverter.ToUInt16(bytes, pos);
        }

        if ((flags & 0x20) == 0 || pos + 2 > bytes.Length)
        {
            return null;
        }

        var len = BitConverter.ToUInt16(bytes, pos);
        pos += 2;
        if (pos + len > bytes.Length)
        {
            return null;
        }

        return Encoding.Unicode.GetString(bytes, pos, len).TrimEnd('\0');
    }

    private static string ExtractExePath(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.StartsWith('"'))
        {
            var end = trimmed.IndexOf('"', 1);
            return end > 1 ? trimmed[1..end] : trimmed;
        }

        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }

    private static string QueryServiceState(string serviceName)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {serviceName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return "확인 불가";
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) ? "실행 중" : output.Trim();
        }
        catch
        {
            return "확인 불가";
        }
    }

    private static void ScanAppPaths(List<CareFinding> findings)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(name);
            var path = sub?.GetValue(null) as string ?? "";
            var exe = ExtractExePath(path);
            if (!string.IsNullOrWhiteSpace(exe) && !File.Exists(exe))
            {
                findings.Add(new CareFinding
                {
                    Id = $"reg.apppaths.{name}",
                    Title = $"App Path 없음: {name}",
                    Detail = exe,
                    RiskLabel = "확인 필요",
                    RiskCode = "review",
                    CanAutoApply = false
                });
            }
        }
    }

    private static void ScanServiceImagePaths(List<CareFinding> findings)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetSubKeyNames().Take(200))
        {
            using var sub = key.OpenSubKey(name);
            var image = sub?.GetValue("ImagePath") as string ?? "";
            if (string.IsNullOrWhiteSpace(image))
            {
                continue;
            }

            var exe = ExtractExePath(image);
            if (string.IsNullOrWhiteSpace(exe) || File.Exists(exe))
            {
                continue;
            }

            var isSystem = exe.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase);
            findings.Add(new CareFinding
            {
                Id = $"reg.services.{name}",
                Title = $"서비스 경로 없음: {name}",
                Detail = image,
                RiskLabel = isSystem ? "고위험" : "확인 필요",
                RiskCode = isSystem ? "highrisk" : "review",
                CanAutoApply = false
            });
        }
    }

    private static void AppendSsdTrimAdvisory(List<CareFinding> findings)
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            try
            {
                findings.Add(new CareFinding
                {
                    Id = $"disk.ssd_trim.{drive.Name}",
                    Title = $"{drive.Name} 저장장치 관리",
                    Detail = "SSD/NVMe: TRIM/retrim 권장·일반 조각 모음 금지. HDD: 분석 후 조각 모음 가능. chkdsk /scan 우선.",
                    RiskLabel = "안전",
                    RiskCode = "safe",
                    CanAutoApply = false
                });
            }
            catch
            {
                // Skip.
            }
        }
    }

    private static void AppendGatewayFinding(List<CareFinding> findings)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ping",
                Arguments = "-n 1 -w 1000 8.8.8.8",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            var ok = output.Contains("TTL=", StringComparison.OrdinalIgnoreCase);
            findings.Add(new CareFinding
            {
                Id = "net.gateway",
                Title = "외부 연결 테스트",
                Detail = ok ? "8.8.8.8 응답 정상 · DNS/게이트웨이 추가 점검 가능" : "외부 연결 실패 · 어댑터·프록시·방화벽 확인",
                RiskLabel = ok ? "안전" : "확인 필요",
                RiskCode = ok ? "safe" : "review",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendNetworkAdapterFindings(List<CareFinding> findings)
    {
        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                         .Take(8))
            {
                var speedMbps = nic.Speed > 0 ? nic.Speed / 1_000_000 : 0;
                findings.Add(new CareFinding
                {
                    Id = $"net.adapter.{nic.Id}",
                    Title = $"어댑터: {nic.Name}",
                    Detail = $"{nic.NetworkInterfaceType} · {speedMbps} Mbps · {nic.OperationalStatus}",
                    RiskLabel = speedMbps > 0 && speedMbps < 100 ? "확인 필요" : "안전",
                    RiskCode = speedMbps > 0 && speedMbps < 100 ? "review" : "safe",
                    CanAutoApply = false
                });
            }
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendFirewallFinding(List<CareFinding> findings)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
            var enabled = key?.GetValue("EnableFirewall");
            var on = enabled is int i && i != 0;
            findings.Add(new CareFinding
            {
                Id = "vuln.firewall",
                Title = "Windows 방화벽 (표준 프로필)",
                Detail = on ? "방화벽 활성" : "방화벽 비활성 — 보안 위험",
                RiskLabel = on ? "안전" : "주의",
                RiskCode = on ? "safe" : "caution",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendDefenderFinding(List<CareFinding> findings)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection");
            var disabled = key?.GetValue("DisableRealtimeMonitoring");
            var off = disabled is int i && i != 0;
            findings.Add(new CareFinding
            {
                Id = "vuln.defender",
                Title = "Microsoft Defender",
                Detail = off ? "실시간 보호 꺼짐" : "실시간 보호 활성",
                RiskLabel = off ? "주의" : "안전",
                RiskCode = off ? "caution" : "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendUacFinding(List<CareFinding> findings)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            var level = key?.GetValue("ConsentPromptBehaviorAdmin") as int? ?? 5;
            var enabled = level is not 0;
            findings.Add(new CareFinding
            {
                Id = "vuln.uac",
                Title = "UAC (사용자 계정 컨트롤)",
                Detail = enabled ? $"관리자 승인 수준: {level}" : "UAC가 사실상 비활성화됨",
                RiskLabel = enabled ? "안전" : "주의",
                RiskCode = enabled ? "safe" : "caution",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendMemoryFinding(List<CareFinding> findings)
    {
        try
        {
            var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var used = Process.GetProcesses()
                .Select(p =>
                {
                    try
                    {
                        p.Refresh();
                        return p.WorkingSet64;
                    }
                    catch
                    {
                        return 0L;
                    }
                })
                .Sum();
            var usedPct = total > 0 ? used * 100.0 / total : 0;
            var top = Process.GetProcesses()
                .Select(p =>
                {
                    try
                    {
                        p.Refresh();
                        return (Name: p.ProcessName, Ws: p.WorkingSet64);
                    }
                    catch
                    {
                        return (Name: p.ProcessName, Ws: 0L);
                    }
                })
                .OrderByDescending(x => x.Ws)
                .Take(3)
                .Select(x => $"{x.Name} {x.Ws / 1024 / 1024}MB")
                .ToArray();

            findings.Add(new CareFinding
            {
                Id = "opt.memory",
                Title = "메모리 사용량",
                Detail = $"추정 사용 {usedPct:F0}% · 상위: {string.Join(", ", top)}",
                RiskLabel = usedPct > 85 ? "확인 필요" : "안전",
                RiskCode = usedPct > 85 ? "review" : "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendPowerPlanFinding(List<CareFinding> findings)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/getactivescheme",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            var saver = output.Contains("절전", StringComparison.OrdinalIgnoreCase)
                || output.Contains("power saver", StringComparison.OrdinalIgnoreCase);
            findings.Add(new CareFinding
            {
                Id = "opt.powerplan",
                Title = "활성 전원 계획",
                Detail = output.Trim().Length > 0 ? output.Trim() : "확인 불가",
                RiskLabel = saver ? "확인 필요" : "안전",
                RiskCode = saver ? "review" : "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendServiceAdvisory(List<CareFinding> findings, string serviceName, string id, string note)
    {
        var status = QueryServiceState(serviceName);
        var running = status.Contains("실행", StringComparison.Ordinal);
        findings.Add(new CareFinding
        {
            Id = id,
            Title = $"{serviceName} 서비스",
            Detail = $"{status} · {note}",
            RiskLabel = running ? "안전" : "확인 필요",
            RiskCode = running ? "safe" : "review",
            CanAutoApply = false
        });
    }

    private static void AppendStartupRegistryFinding(List<CareFinding> findings)
    {
        var count = 0;
        count += CountRegistryValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
        count += CountRegistryValues(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
        count += CountRegistryValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce");
        findings.Add(new CareFinding
        {
            Id = "opt.startup_reg",
            Title = "레지스트리 자동 실행",
            Detail = $"Run/RunOnce 항목 {count}개",
            RiskLabel = count > 12 ? "확인 필요" : "안전",
            RiskCode = count > 12 ? "review" : "safe",
            CanAutoApply = false
        });
    }

    private static int CountRegistryValues(RegistryKey root, string subPath)
    {
        using var key = root.OpenSubKey(subPath);
        return key?.GetValueNames().Length ?? 0;
    }

    private static void AppendVisualEffectsFinding(List<CareFinding> findings)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
            var visual = key?.GetValue("VisualFXSetting") as int? ?? 0;
            var maxEffects = visual == 1;
            findings.Add(new CareFinding
            {
                Id = "opt.visual_anim",
                Title = "시각 효과 설정",
                Detail = maxEffects
                    ? "최적 성능이 아닌 시각 효과 모드 — 저사양 PC는 단순 모드 권장"
                    : "시각 효과가 성능 우선 또는 Windows 기본 설정",
                RiskLabel = maxEffects ? "확인 필요" : "안전",
                RiskCode = maxEffects ? "review" : "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendGameBarFinding(List<CareFinding> findings)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore");
            var enabled = key?.GetValue("GameDVR_Enabled") as int? ?? 1;
            var on = enabled != 0;
            findings.Add(new CareFinding
            {
                Id = "opt.gamebar",
                Title = "Xbox Game Bar 녹화",
                Detail = on ? "게임 DVR 활성 — 게임·저사양 PC에서 성능 저하 가능" : "게임 DVR 비활성",
                RiskLabel = on ? "확인 필요" : "안전",
                RiskCode = on ? "review" : "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendBrowserCacheFindings(List<CareFinding> findings)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            ("Edge", Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache")),
            ("Chrome", Path.Combine(local, @"Google\Chrome\User Data\Default\Cache"))
        };

        foreach (var (name, path) in candidates)
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            AddFolderSizeFinding(findings, $"{name} 캐시", path, $"junk.browser_cache.{name.ToLowerInvariant()}", safeApply: true);
        }
    }

    private static void AppendLogFolderFindings(List<CareFinding> findings)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        AddFolderSizeFinding(findings, "CBS 로그", Path.Combine(windows, "Logs", "CBS"), "junk.logs.cbs", safeApply: false);
        AddFolderSizeFinding(findings, "Windows Logs", Path.Combine(windows, "Logs"), "junk.logs.win", safeApply: false);
    }

    private static void AppendDeliveryCacheFinding(List<CareFinding> findings)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\Windows\DeliveryOptimization\Cache");
        AddFolderSizeFinding(findings, "전달 최적화 캐시", path, "junk.delivery", safeApply: true);
    }

    private static void AppendPagefileFinding(List<CareFinding> findings)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management");
            var auto = key?.GetValue("PagingFiles") as string[] ?? Array.Empty<string>();
            findings.Add(new CareFinding
            {
                Id = "disk.pagefile",
                Title = "페이지 파일",
                Detail = auto.Length > 0
                    ? $"설정: {string.Join(", ", auto)} · 자동 관리 유지 권장"
                    : "페이지 파일 정보 확인 불가",
                RiskLabel = "안전",
                RiskCode = "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendHiberfilFinding(List<CareFinding> findings)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "hiberfil.sys");
        if (!File.Exists(path))
        {
            findings.Add(new CareFinding
            {
                Id = "disk.hiberfil",
                Title = "최대 절전 파일",
                Detail = "최대 절전 비활성 — hiberfil.sys 없음",
                RiskLabel = "안전",
                RiskCode = "safe",
                CanAutoApply = false
            });
            return;
        }

        try
        {
            var size = new FileInfo(path).Length;
            findings.Add(new CareFinding
            {
                Id = "disk.hiberfil",
                Title = "최대 절전 파일",
                Detail = $"hiberfil.sys 약 {size / 1024 / 1024 / 1024:F1} GB · 수정은 powercfg로만 권장",
                RiskLabel = size > 8L * 1024 * 1024 * 1024 ? "확인 필요" : "안전",
                RiskCode = size > 8L * 1024 * 1024 * 1024 ? "review" : "safe",
                CanAutoApply = false
            });
        }
        catch
        {
            // Skip.
        }
    }

    private static void AppendDnsResolveFinding(List<CareFinding> findings)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var host = System.Net.Dns.GetHostEntry("www.microsoft.com");
            sw.Stop();
            var ok = host.AddressList.Length > 0;
            findings.Add(new CareFinding
            {
                Id = "net.dns_resolve",
                Title = "DNS 응답 테스트",
                Detail = ok
                    ? $"www.microsoft.com 해석 {sw.ElapsedMilliseconds}ms"
                    : "DNS 해석 실패",
                RiskLabel = ok && sw.ElapsedMilliseconds <= 120 ? "안전" : "확인 필요",
                RiskCode = ok && sw.ElapsedMilliseconds <= 120 ? "safe" : "review",
                CanAutoApply = false
            });
        }
        catch
        {
            findings.Add(new CareFinding
            {
                Id = "net.dns_resolve",
                Title = "DNS 응답 테스트",
                Detail = "DNS 해석 실패 — 어댑터·프록시 확인",
                RiskLabel = "확인 필요",
                RiskCode = "review",
                CanAutoApply = false
            });
        }
    }
}