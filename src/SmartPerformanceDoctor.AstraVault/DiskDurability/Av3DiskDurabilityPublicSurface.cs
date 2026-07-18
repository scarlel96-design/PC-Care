using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>E-14 public error redaction for disk durability reports.</summary>
public static class Av3DiskDurabilityPublicSurface
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

        return Av3JournalLeakScanner.ScanText(text, "disk_durability_public_surface").Passed;
    }
}