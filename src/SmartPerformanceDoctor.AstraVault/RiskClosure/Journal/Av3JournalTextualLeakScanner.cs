using System.Text;

namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

/// <summary>R11/E-6.1: forbidden token scan for textual surfaces only (not raw journal digests).</summary>
public static class Av3JournalTextualLeakScanner
{
    public static int CountForbiddenTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return Av3JournalLeakScanner.ForbiddenTokens.Count(token =>
            text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    public static int CountForbiddenTokens(ReadOnlySpan<byte> utf8Text)
    {
        if (utf8Text.IsEmpty)
        {
            return 0;
        }

        return CountForbiddenTokens(Encoding.UTF8.GetString(utf8Text));
    }

    public static Av3JournalLeakScanner.Av3JournalLeakScanResult ScanText(string? text, string channel)
    {
        var violations = CountForbiddenTokens(text);
        return new Av3JournalLeakScanner.Av3JournalLeakScanResult
        {
            Passed = violations == 0,
            ViolationCount = violations,
            Channel = channel
        };
    }
}