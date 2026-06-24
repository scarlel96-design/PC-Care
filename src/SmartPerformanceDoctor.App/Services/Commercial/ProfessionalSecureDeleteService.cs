using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.App.Branding;
using SmartPerformanceDoctor.App.Models.Commercial;

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

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                continue;
            }

            var (allowed, reason) = PathSafetyGuard.Evaluate(path);
            var storage = SecureDeleteStorageProfiler.Profile(path);
            var fs = DetectFileSystem(path);
            var size = File.Exists(path) ? new FileInfo(path).Length : EstimateDirectorySize(path);
            var protocol = SecureDeleteStorageProfiler.SelectProtocol(storage, level);
            var passes = SecureDeleteStorageProfiler.GetOverwritePasses(storage, level);
            var chain = ForensicSecureDeleteEngine.BuildChainSteps(storage, level);

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

            if (allowed)
            {
                targets.Add(target);
            }
            else
            {
                blocked.Add(target);
            }
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
                $"보안 삭제 풀체인: ADS 제거 · 저장장치별 다중 덮어쓰기 · SSD 난독화/TRIM/retrim · VSS 잔존 위험 고지 · 난수 파일명.\n" +
                "섀도 복사본/복구 지점은 자동 삭제하지 않습니다.\n" +
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
        if (!string.Equals(confirm, AstraCareBranding.ShredConfirmation, StringComparison.Ordinal)
            && !string.Equals(confirm, AstraCareBranding.LegacyShredConfirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"확인 문구가 일치하지 않습니다. ({AstraCareBranding.ShredConfirmation})");
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
                    await ForensicSecureDeleteEngine.SecureDeleteFileAsync(
                        target.Path,
                        target.StorageType,
                        level,
                        cancellationToken);
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
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var storage = SecureDeleteStorageProfiler.Profile(file);
            await ForensicSecureDeleteEngine.SecureDeleteFileAsync(file, storage, level, cancellationToken);
        }

        Directory.Delete(directoryPath, true);
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
        try
        {
            return Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
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