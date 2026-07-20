using SmartPerformanceDoctor.App.Models.SystemCare;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

/// <summary>
/// Central false-positive and auto-apply gate. Detection can be broad, while
/// automated action requires high-confidence, current, boundary-checked evidence.
/// </summary>
public static class CareFindingQualityGate
{
    private static readonly string[] IncompleteMeasurementMarkers =
    [
        "샘플 기반 추정",
        "일부 샘플링",
        "검사 중 오류",
        "확인하지 못"
    ];

    public static IReadOnlyList<CareFinding> Evaluate(IReadOnlyList<CareFinding> findings)
    {
        var dnsIssue = findings.Any(f =>
            f.Id.StartsWith("net.dns_resolve", StringComparison.OrdinalIgnoreCase)
            && f.RiskCode is "review" or "caution" or "highrisk");

        return findings
            .Select(finding => Assess(finding, dnsIssue))
            .GroupBy(FindingKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(f => RiskRank(f.RiskCode))
                .ThenByDescending(f => f.Confidence)
                .First())
            .OrderBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static CareFinding Assess(CareFinding finding, bool dnsIssue)
    {
        if (finding.Id.EndsWith(".error", StringComparison.OrdinalIgnoreCase))
        {
            return Copy(finding, "검사 제한", "unavailable", false, 0,
                "probe-unavailable", "검사 실패를 PC 문제로 판정하지 않음");
        }

        var evidence = string.IsNullOrWhiteSpace(finding.Evidence)
            ? BuildEvidence(finding)
            : finding.Evidence;
        var confidence = Math.Clamp(finding.Confidence, 0, 1);
        var canAutoApply = finding.CanAutoApply;
        var blockReason = string.Empty;

        if (canAutoApply && !string.Equals(finding.RiskCode, "safe", StringComparison.OrdinalIgnoreCase))
        {
            canAutoApply = false;
            blockReason = "확인 필요 항목은 자동 처리하지 않음";
        }

        if (canAutoApply && IncompleteMeasurementMarkers.Any(marker =>
                finding.Detail.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            canAutoApply = false;
            blockReason = "불완전한 측정 결과는 자동 처리하지 않음";
        }

        if (finding.Id.Equals("opt.dns_flush", StringComparison.OrdinalIgnoreCase))
        {
            canAutoApply = dnsIssue;
            blockReason = dnsIssue ? string.Empty : "DNS 실패 또는 지연 증거 없음";
            confidence = dnsIssue ? Math.Max(confidence, 0.9) : Math.Min(confidence, 0.55);
        }
        else if (canAutoApply && string.IsNullOrWhiteSpace(finding.TargetPath))
        {
            canAutoApply = finding.Id.Equals("junk.recycle", StringComparison.OrdinalIgnoreCase);
            if (!canAutoApply)
            {
                blockReason = "검증 가능한 처리 대상 경로 없음";
            }
        }
        else if (canAutoApply && !IsApprovedCleanupTarget(finding.TargetPath!))
        {
            canAutoApply = false;
            blockReason = "보호 경계 밖의 경로";
        }

        if (canAutoApply && confidence < 0.85)
        {
            canAutoApply = false;
            blockReason = "자동 처리 신뢰도 기준 미달";
        }

        return Copy(finding, finding.RiskLabel, finding.RiskCode, canAutoApply, confidence, evidence, blockReason);
    }

    private static string FindingKey(CareFinding finding)
    {
        var target = string.IsNullOrWhiteSpace(finding.TargetPath)
            ? string.Empty
            : SafeNormalizePath(finding.TargetPath);
        return $"{finding.Id}|{target}";
    }

    private static string BuildEvidence(CareFinding finding) =>
        string.IsNullOrWhiteSpace(finding.TargetPath)
            ? finding.Detail
            : $"{SafeNormalizePath(finding.TargetPath)} · {finding.Detail}";

    internal static bool IsApprovedCleanupTarget(string path)
    {
        try
        {
            var fullPath = NormalizePath(path);
            var roots = new[]
            {
                Path.GetTempPath(),
                Environment.GetFolderPath(Environment.SpecialFolder.Recent),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "INetCache"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data"),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs)
            }
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(NormalizePath)
            .ToArray();

            return roots.Any(root =>
                fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static string SafeNormalizePath(string path)
    {
        try
        {
            return NormalizePath(path);
        }
        catch
        {
            return path.Trim();
        }
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static int RiskRank(string riskCode) => riskCode.ToLowerInvariant() switch
    {
        "highrisk" => 5,
        "caution" => 4,
        "review" => 3,
        "blocked" => 2,
        "safe" => 1,
        _ => 0
    };

    private static CareFinding Copy(
        CareFinding source,
        string riskLabel,
        string riskCode,
        bool canAutoApply,
        double confidence,
        string evidence,
        string blockReason) => new()
    {
        Id = source.Id,
        Title = source.Title,
        Detail = source.Detail,
        RiskLabel = riskLabel,
        RiskCode = riskCode,
        CanAutoApply = canAutoApply,
        TargetPath = source.TargetPath,
        Confidence = confidence,
        Evidence = evidence,
        DetectionSource = source.DetectionSource,
        AutoApplyBlockReason = blockReason
    };
}