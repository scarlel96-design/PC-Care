using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 trusted anchor public-surface redaction.</summary>
public static class Av3TrustedAnchorPublicSurface
{
    public static bool IsPublicTextSafe(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (text.Contains("password", StringComparison.OrdinalIgnoreCase)
            || text.Contains("VMK", StringComparison.OrdinalIgnoreCase)
            || text.Contains("DEK", StringComparison.OrdinalIgnoreCase)
            || text.Contains(@":\Users\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Av3JournalLeakScanner.ScanText(text, "trusted_anchor_public_surface").Passed;
    }

    public static string ToPublicVerificationSummary(Av3TrustedAnchorVerification verification) =>
        $"trusted_status={verification.AnchorStatus} summary={verification.PublicSummary}";
}