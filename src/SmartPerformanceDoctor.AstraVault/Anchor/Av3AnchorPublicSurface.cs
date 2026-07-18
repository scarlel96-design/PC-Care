using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-11 anchor public-surface redaction helpers.</summary>
public static class Av3AnchorPublicSurface
{
    public static string RedactDigest(string? digestHex)
    {
        if (string.IsNullOrWhiteSpace(digestHex))
        {
            return "digest_redacted";
        }

        if (digestHex.Length <= 8)
        {
            return "digest_redacted";
        }

        return $"{digestHex[..4]}…{digestHex[^4..]}";
    }

    public static string ToPublicSnapshotSummary(Av3AnchorSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "anchor_snapshot_unavailable";
        }

        return $"provider={snapshot.ProviderKind} generation={snapshot.Generation} counter={snapshot.MonotonicCounter} digest={RedactDigest(snapshot.WitnessDigestHex)}";
    }

    public static string ToPublicUpdateSummary(Av3AnchorUpdateResult result) =>
        result.Success
            ? $"anchor_update_ok generation={result.Snapshot?.Generation}"
            : $"anchor_update_fail reason={result.FailureReason} class={result.PublicErrorClass}";

    public static string ToPublicVerificationSummary(Av3AnchorVerificationResult result) =>
        $"status={result.Status} verified={result.Verified} summary={result.PublicSummary}";

    public static bool IsPublicTextSafe(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!Av3WriterAccessGate.IsPublicErrorClassSafe(text)
            && !text.StartsWith("anchor_", StringComparison.Ordinal)
            && !text.StartsWith("provider=", StringComparison.Ordinal)
            && !text.StartsWith("status=", StringComparison.Ordinal)
            && text != "ok")
        {
            return false;
        }

        return Av3JournalLeakScanner.ScanText(text, "anchor_public_surface").Passed;
    }
}