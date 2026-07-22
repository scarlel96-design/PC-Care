using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.App.Branding;
using SmartPerformanceDoctor.App.Models.Commercial;
using SmartPerformanceDoctor.SecurityLab.Hardening;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;
using SmartPerformanceDoctor.SecurityLab.ShredNext;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class ProfessionalSecureDeleteService
{
    public SecureDeletePlan PlanDryRun(
        IReadOnlyList<string> paths,
        SecureDeleteSecurityLevel level = SecureDeleteSecurityLevel.Professional)
    {
        var operationId = $"secure-delete-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        var targets = new List<SecureDeleteTarget>();
        var blocked = new List<SecureDeleteTarget>();
        var useLab = ProductFeatureFlags.ShredNextEnabled;

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                continue;
            }

            var (allowed, reason) = PathSafetyGuard.Evaluate(path);
            // 50.3.0: SecurityLab path policy (UNC / special roots) as additional gate
            if (allowed && useLab)
            {
                var lab = LabSecurePath.Evaluate(path);
                if (!lab.Allowed)
                {
                    allowed = false;
                    reason = "Lab: " + lab.Reason;
                }
            }

            if (!allowed)
            {
                blocked.Add(new SecureDeleteTarget
                {
                    Path = path,
                    Type = Directory.Exists(path) ? "folder" : "file",
                    Size = 0,
                    StorageType = "Unknown",
                    FileSystem = "Unknown",
                    Risk = "blocked",
                    RecommendedProtocol = "blocked-before-profiling",
                    OverwritePasses = 0,
                    Blocked = true,
                    BlockReason = reason
                });
                continue;
            }

            var storage = SecureDeleteStorageProfiler.Profile(path);
            var fs = DetectFileSystem(path);
            var size = File.Exists(path) ? new FileInfo(path).Length : EstimateDirectorySize(path);
            var protocol = useLab && File.Exists(path)
                ? "lab.shred-next.v1+" + SecureDeleteStorageProfiler.SelectProtocol(storage, level)
                : SecureDeleteStorageProfiler.SelectProtocol(storage, level);
            var passes = SecureDeleteStorageProfiler.GetOverwritePasses(storage, level);

            var target = new SecureDeleteTarget
            {
                Path = path,
                Type = Directory.Exists(path) ? "folder" : "file",
                Size = size,
                StorageType = storage,
                FileSystem = fs,
                Risk = allowed ? "high" : "blocked",
                RecommendedProtocol = protocol,
                OverwritePasses = passes,
                Blocked = !allowed,
                BlockReason = reason
            };

            targets.Add(target);
        }

        var primaryStorage = targets.Count == 0 ? "Unknown" : targets[0].StorageType;
        var maxCertified = targets.Count == 0
            ? 0
            : targets.Max(t => SecureDeleteStorageProfiler.EstimateCertifiedResistanceLevel(t.StorageType, level));
        var maxIntensity = targets.Count == 0
            ? 0
            : targets.Max(t => SecureDeleteStorageProfiler.GetTechnicalDeletionIntensity(t.StorageType, level));
        var level5Certified = targets.Count > 0
            && targets.All(t => SecureDeleteStorageProfiler.IsLevel5Certified(t.StorageType, level));
        var disclaimer = SecureDeleteStorageProfiler.BuildResistanceDisclaimer(primaryStorage, level);
        var chainSummary = targets.Count == 0
            ? ""
            : string.Join(" → ", ForensicSecureDeleteEngine.BuildChainSteps(targets[0].StorageType, level));
        if (useLab)
        {
            chainSummary = "ShredNext(Lab) · " + chainSummary;
        }

        var recoveryRisk = maxCertified >= 4 && level5Certified
            ? "very-low"
            : maxCertified >= 3
                ? "low"
                : "storage-dependent";
        return new SecureDeletePlan
        {
            OperationId = operationId,
            Mode = "dry-run",
            SecurityLevel = level.ToString(),
            Targets = targets,
            BlockedTargets = blocked,
            RecoveryResistanceLevel = maxCertified,
            TechnicalDeletionIntensity = maxIntensity,
            Level5Certified = level5Certified,
            CertifiedResistanceLabel = SecureDeleteStorageProfiler.BuildCertifiedResistanceLabel(primaryStorage, level),
            ResistanceDisclaimer = disclaimer,
            ProfessionalRecoveryRisk = recoveryRisk,
            Limitations =
                "상용급 보안 삭제 원칙: (1) 경로 안전 검증 (2) dry-run (3) 확인 문구 " +
                $"「{AstraCareBranding.ShredIrreversibleConfirmation}」 (4) 저장장치별 체인 " +
                "(HDD 다중 패스 / SSD 랜덤+난독화+TRIM) (5) ADS 제거 (6) VSS 잔존 고지.\n" +
                (useLab ? "50.3.0 ShredNext: Lab 경로 정책(UNC 차단 등) + 파일 Lab 덮어쓰기 엔진 병행.\n" : "") +
                "금고 내부 데이터 삭제는 키 파기(crypto-shred)가 핵심입니다.\n" +
                "섀도 복사본/복구 지점/클라우드 사본은 자동 삭제하지 않습니다.\n" +
                (string.IsNullOrWhiteSpace(disclaimer) ? AstraCareBranding.ShredSsdLimitation : disclaimer),
            EstimatedDuration = TimeSpan.FromSeconds(Math.Max(
                12,
                targets.Sum(t =>
                    t.StorageType is "SSD" or "NVMe"
                        ? SecureDeleteStorageProfiler.GetSsdObfuscationPasses(level) * 2 + 8
                        : Math.Max(1, t.OverwritePasses) * 3 + 6))).ToString(@"mm\:ss"),
            ChainSummary = chainSummary
        };
    }

    public async Task<(int Deleted, int Failed, string AuditPath, bool AuditValid)> ApplyAsync(
        SecureDeletePlan plan,
        string typedConfirmation,
        SecureDeleteSecurityLevel level = SecureDeleteSecurityLevel.Professional,
        IProgress<(int percent, string detail)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var confirm = typedConfirmation.Trim();
        var phraseOk =
            string.Equals(confirm, AstraCareBranding.ShredIrreversibleConfirmation, StringComparison.Ordinal)
            || string.Equals(confirm, AstraCareBranding.ShredConfirmation, StringComparison.Ordinal)
            || string.Equals(confirm, AstraCareBranding.LegacyShredConfirmation, StringComparison.Ordinal);
        if (!phraseOk)
        {
            throw new InvalidOperationException(
                $"확인 문구가 일치하지 않습니다. 다음 중 하나를 정확히 입력하세요: " +
                $"「{AstraCareBranding.ShredIrreversibleConfirmation}」 또는 「{AstraCareBranding.ShredConfirmation}」");
        }

        if (plan.Targets.Count == 0)
        {
            throw new InvalidOperationException("삭제 가능한 대상이 없습니다. dry-run 결과를 확인하세요.");
        }

        var auditDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPerformanceDoctor",
            "secure_delete",
            "audit");
        Directory.CreateDirectory(auditDir);
        var auditPath = Path.Combine(auditDir, $"{plan.OperationId}.log");
        var hmacKey = SecureDeleteAuditChain.EnsureOperationKey(plan.OperationId);

        var deleted = 0;
        var failed = 0;
        var index = 0;
        var chain = "GENESIS";
        var scannedVolumes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var shadowAdvisories = new List<string>();

        foreach (var target in plan.Targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            progress?.Report(((int)(index / (double)plan.Targets.Count * 100), target.Path));

            try
            {
                var contentHash = ComputeTargetHash(target.Path);
                var pathHash = HashPath(target.Path);
                var protocol = SecureDeleteStorageProfiler.SelectProtocol(target.StorageType, level);

                if (File.Exists(target.Path))
                {
                    if (ProductFeatureFlags.ShredNextEnabled)
                    {
                        // Lab shred-next for single files (2-pass random + rename)
                        try
                        {
                            LabProductGate.EnsureEnabled("shred");
                            LabShredEngine.CryptoShredFile(target.Path);
                        }
                        catch
                        {
                            await ForensicSecureDeleteEngine.SecureDeleteFileAsync(
                                target.Path,
                                target.StorageType,
                                level,
                                cancellationToken);
                        }
                    }
                    else
                    {
                        await ForensicSecureDeleteEngine.SecureDeleteFileAsync(
                            target.Path,
                            target.StorageType,
                            level,
                            cancellationToken);
                    }

                    deleted++;
                }
                else if (Directory.Exists(target.Path))
                {
                    await SecureDeleteDirectoryAsync(target.Path, level, cancellationToken);
                    deleted++;
                }

                var root = Path.GetPathRoot(Path.GetFullPath(target.Path));
                if (!string.IsNullOrWhiteSpace(root) && scannedVolumes.Add(root))
                {
                    var shadowRisk = ForensicSecureDeleteEngine.ScanShadowCopyRisk(target.Path);
                    if (shadowRisk.HasRisk && !string.IsNullOrWhiteSpace(shadowRisk.Advisory))
                    {
                        shadowAdvisories.Add(shadowRisk.Advisory);
                    }
                }

                chain = SecureDeleteAuditChain.Append(
                    auditPath, plan.OperationId, pathHash, contentHash, protocol, "deleted", chain, hmacKey);
            }
            catch
            {
                failed++;
                chain = SecureDeleteAuditChain.Append(
                    auditPath,
                    plan.OperationId,
                    HashPath(target.Path),
                    "n/a",
                    target.RecommendedProtocol,
                    "failed",
                    chain,
                    hmacKey);
            }
        }

        var auditValid = SecureDeleteAuditChain.Verify(auditPath, hmacKey);
        WriteSecureDeleteReport(plan, deleted, failed, auditPath, auditValid, level);
        CryptographicOperations.ZeroMemory(hmacKey);
        return (deleted, failed, auditPath, auditValid);
    }

    private static async Task SecureDeleteDirectoryAsync(
        string directoryPath,
        SecureDeleteSecurityLevel level,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        var regularDirectories = new List<string>();
        var reparsePoints = new List<string>();
        pending.Push(directoryPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch
            {
                continue;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                reparsePoints.Add(current);
                continue;
            }

            regularDirectories.Add(current);
            IEnumerable<string> files;
            IEnumerable<string> childDirectories;
            try
            {
                files = Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly).ToArray();
                childDirectories = Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (allowed, _) = PathSafetyGuard.Evaluate(file);
                if (!allowed)
                {
                    continue;
                }

                try
                {
                    var fileAttributes = File.GetAttributes(file);
                    if ((fileAttributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                var storage = SecureDeleteStorageProfiler.Profile(file);
                await ForensicSecureDeleteEngine.SecureDeleteFileAsync(file, storage, level, cancellationToken);
            }

            foreach (var childDirectory in childDirectories)
            {
                try
                {
                    var childAttributes = File.GetAttributes(childDirectory);
                    if ((childAttributes & FileAttributes.ReparsePoint) != 0)
                    {
                        reparsePoints.Add(childDirectory);
                    }
                    else
                    {
                        pending.Push(childDirectory);
                    }
                }
                catch
                {
                    // Inaccessible child stays untouched.
                }
            }
        }

        // Reparse points are never followed. Only the link itself is removed.
        foreach (var reparsePoint in reparsePoints.OrderByDescending(path => path.Length))
        {
            try { Directory.Delete(reparsePoint, false); } catch { /* link may be locked */ }
        }

        foreach (var directory in regularDirectories.OrderByDescending(path => path.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory, false);
                }
            }
            catch
            {
                // Remaining inaccessible or locked entries stay untouched.
            }
        }

        if (Directory.Exists(directoryPath))
        {
            throw new IOException("일부 파일 또는 폴더가 잠겨 있어 대상 전체를 삭제하지 못했습니다.");
        }
    }

    private static string ComputeTargetHash(string path)
    {
        if (!File.Exists(path))
        {
            return "dir:" + HashPath(path);
        }

        try
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        }
        catch
        {
            return "unreadable:" + HashPath(path);
        }
    }

    private static string HashPath(string path) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path))).ToLowerInvariant()[..16];

    private static string DetectFileSystem(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
            return new DriveInfo(root).DriveFormat;
        }
        catch
        {
            return "Unknown";
        }
    }

    private static long EstimateDirectorySize(string dir)
    {
        long total = 0;
        var pending = new Stack<string>();
        pending.Push(dir);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                        {
                            total = checked(total + info.Length);
                        }
                    }
                    catch (OverflowException)
                    {
                        return long.MaxValue;
                    }
                    catch
                    {
                        // Inaccessible files are excluded from the estimate.
                    }
                }

                foreach (var child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                {
                    pending.Push(child);
                }
            }
            catch
            {
                // Inaccessible branches are excluded from the estimate.
            }
        }

        return total;
    }
    private static void WriteSecureDeleteReport(
        SecureDeletePlan plan,
        int deleted,
        int failed,
        string auditPath,
        bool auditValid,
        SecureDeleteSecurityLevel level)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "SmartPerformanceDoctor",
            "SecureDeleteReports");
        Directory.CreateDirectory(root);
        var basePath = Path.Combine(root, plan.OperationId);
        var payload = new
        {
            plan.OperationId,
            protocol = "astra-shred-v4-forensic-chain",
            securityLevel = level.ToString(),
            plan.RecoveryResistanceLevel,
            plan.TechnicalDeletionIntensity,
            plan.Level5Certified,
            plan.CertifiedResistanceLabel,
            plan.ResistanceDisclaimer,
            plan.ProfessionalRecoveryRisk,
            plan.ChainSummary,
            plan.Limitations,
            deleted,
            failed,
            auditPath,
            auditChainValid = auditValid,
            completedAt = DateTimeOffset.Now.ToString("o")
        };

        File.WriteAllText($"{basePath}.json", JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(
            $"{basePath}.html",
            $"""
            <html><head><meta charset="utf-8"/><title>Secure Delete Report</title></head>
            <body style="font-family:Segoe UI,sans-serif;padding:24px">
            <h1>보안 삭제 보고서 (v4 풀체인)</h1>
            <p><b>작업 ID:</b> {plan.OperationId}</p>
            <p><b>보안 등급:</b> {level}</p>
            <p><b>체인:</b> {plan.ChainSummary}</p>
            <p><b>삭제:</b> {deleted} · <b>실패:</b> {failed}</p>
            <p><b>공인 복구 저항:</b> {plan.CertifiedResistanceLabel}</p>
            <p><b>기술 삭제 강도 Tier:</b> {plan.TechnicalDeletionIntensity}</p>
            <p><b>Level 5 공인 보증:</b> {(plan.Level5Certified ? "예" : "아니오 (저장장치·범위 조건 미충족)")}</p>
            {(string.IsNullOrWhiteSpace(plan.ResistanceDisclaimer) ? "" : $"<p><b>고지:</b> {plan.ResistanceDisclaimer}</p>")}
            <p><b>감사 체인:</b> {(auditValid ? "정상" : "주의")}</p>
            <p><b>감사 로그:</b> {auditPath}</p>
            <p style="color:#666">{plan.Limitations}</p>
            </body></html>
            """);
    }
}