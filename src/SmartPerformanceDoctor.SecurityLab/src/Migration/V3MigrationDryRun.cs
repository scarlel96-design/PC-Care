using System.Text.Json;

namespace SmartPerformanceDoctor.SecurityLab.Migration;

/// <summary>
/// Read-only scan of a product-style spd-vault-v3 directory layout.
/// Does NOT unlock or decrypt (no product App dependency).
/// Produces a migration plan for re-encrypt import into Lab v4.
/// </summary>
public static class V3MigrationDryRun
{
    public const string ProductFormatHint = "spd-vault-v3";
    public const string LabFormat = "spd-vault-v4-lab";

    public sealed class Report
    {
        public bool LooksLikeProductVault { get; init; }
        public string VaultRoot { get; init; } = "";
        public string DetectedLayout { get; init; } = "";
        public bool HasMarker { get; init; }
        public bool HasKeyEnvelope { get; init; }
        public bool HasManifest { get; init; }
        public bool HasDataDir { get; init; }
        public int ShardFileCount { get; init; }
        public long ShardTotalBytes { get; init; }
        public int RedundantCopyCount { get; init; }
        public bool HasRecoveryEnvelope { get; init; }
        public bool HasAuditLog { get; init; }
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> PlannedSteps { get; init; } = Array.Empty<string>();
        public string RecommendedStrategy { get; init; } = "re-encrypt-import";
        public bool CanAutoConvertBytes { get; init; }
        public string Notes { get; init; } = "";
    }

    public static Report Analyze(string productVaultRoot)
    {
        var root = Path.GetFullPath(productVaultRoot);
        var warnings = new List<string>();
        var steps = new List<string>();

        var marker = Path.Combine(root, "vault.svdb");
        var envelope = Path.Combine(root, "key_envelope.bin");
        var manifest = Path.Combine(root, "vault_manifest.json.enc");
        var dataDir = Path.Combine(root, "data");
        var redundant = Path.Combine(root, "data", "redundant");
        var recoveryEnv = Path.Combine(root, "recovery", "recovery_envelope.bin");
        var audit = Path.Combine(root, "audit", "vault_audit.log.enc");

        var hasMarker = File.Exists(marker);
        var hasEnvelope = File.Exists(envelope);
        var hasManifest = File.Exists(manifest);
        var hasData = Directory.Exists(dataDir);

        var shardFiles = hasData
            ? Directory.EnumerateFiles(dataDir, "*.blob", SearchOption.TopDirectoryOnly).ToArray()
            : Array.Empty<string>();
        long shardBytes = 0;
        foreach (var f in shardFiles)
        {
            try { shardBytes += new FileInfo(f).Length; } catch { /* skip */ }
        }

        var redundantCount = Directory.Exists(redundant)
            ? Directory.EnumerateFiles(redundant, "*.blob", SearchOption.TopDirectoryOnly).Count()
            : 0;

        var looksLike = hasMarker && hasEnvelope && hasManifest;
        if (!Directory.Exists(root))
        {
            warnings.Add("경로가 존재하지 않습니다.");
        }
        else if (!looksLike)
        {
            warnings.Add("제품 v3 필수 파일(vault.svdb / key_envelope.bin / vault_manifest.json.enc)이 일부 없습니다.");
        }

        if (looksLike && shardFiles.Length == 0)
        {
            warnings.Add("샤드 파일이 없습니다. 빈 금고이거나 데이터가 다른 위치에 있을 수 있습니다.");
        }

        if (Directory.Exists(Path.Combine(root, "objects")))
        {
            warnings.Add("objects/ 폴더가 있습니다. 이미 Lab v4 흔적과 혼재했을 수 있습니다.");
        }

        steps.Add("1. 전체 금고 폴더를 읽기 전용 백업한다.");
        steps.Add("2. 제품 앱에서 v3 금고를 잠금 해제한다 (비밀번호 필요 — 이 dry-run은 복호화하지 않음).");
        steps.Add("3. 각 항목을 임시 경로로 내보낸다 (제품 Export API).");
        steps.Add($"4. LabVaultService로 새 {LabFormat} 금고를 생성한다.");
        steps.Add("5. 내보낸 파일을 Lab Import로 재암호화한다.");
        steps.Add("6. Content SHA-256(또는 바이트 비교)로 전 항목 검증한다.");
        steps.Add("7. 사용자 승인 후에만 v3 원본 보관/폐기 정책을 적용한다. 자동 삭제는 금지.");

        var layout = looksLike
            ? ProductFormatHint
            : hasMarker || hasEnvelope || hasManifest
                ? "partial-product-layout"
                : "unknown";

        return new Report
        {
            LooksLikeProductVault = looksLike,
            VaultRoot = root,
            DetectedLayout = layout,
            HasMarker = hasMarker,
            HasKeyEnvelope = hasEnvelope,
            HasManifest = hasManifest,
            HasDataDir = hasData,
            ShardFileCount = shardFiles.Length,
            ShardTotalBytes = shardBytes,
            RedundantCopyCount = redundantCount,
            HasRecoveryEnvelope = File.Exists(recoveryEnv),
            HasAuditLog = File.Exists(audit),
            Warnings = warnings,
            PlannedSteps = steps,
            RecommendedStrategy = "re-encrypt-import",
            CanAutoConvertBytes = false,
            Notes =
                "바이트 단위 v3→v4 변환은 포맷 갭(헤더·AEAD·샤드 이름) 때문에 지원하지 않습니다. " +
                "복호화 없는 dry-run이므로 entry 개수는 샤드 파일 수 추정치이며, 폴더 번들/메타 항목 수와 다를 수 있습니다."
        };
    }

    public static string ToJson(Report report) =>
        JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });

    public static string ToHumanSummary(Report report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== v3 → Lab v4 Migration Dry-Run ===");
        sb.AppendLine($"Root: {report.VaultRoot}");
        sb.AppendLine($"Looks like product v3: {report.LooksLikeProductVault}");
        sb.AppendLine($"Layout: {report.DetectedLayout}");
        sb.AppendLine($"Shards: {report.ShardFileCount} files, {report.ShardTotalBytes:N0} bytes");
        sb.AppendLine($"Redundant copies: {report.RedundantCopyCount}");
        sb.AppendLine($"Strategy: {report.RecommendedStrategy}");
        sb.AppendLine($"Byte-level auto convert: {report.CanAutoConvertBytes}");
        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("Warnings:");
            foreach (var w in report.Warnings)
            {
                sb.AppendLine($"  - {w}");
            }
        }

        sb.AppendLine("Planned steps:");
        foreach (var s in report.PlannedSteps)
        {
            sb.AppendLine($"  {s}");
        }

        sb.AppendLine(report.Notes);
        return sb.ToString();
    }
}
