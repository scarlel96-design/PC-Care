using System.Text;

namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

/// <summary>R11/E-6.1: textual surface leak tokens (not for raw journal digest bytes — use <see cref="Av3JournalConfidentialityScanner"/>).</summary>
public static class Av3JournalLeakScanner
{
    public static readonly string[] ForbiddenTokens =
    [
        ":\\",
        "/Users/",
        "/home/",
        "C:\\Users",
        ".docx",
        ".pdf",
        ".txt",
        "password",
        "VMK",
        "DEK",
        "SECRET-MARKER",
        "spd-vault",
        "\\\\"
    ];

    public sealed class Av3JournalLeakScanResult
    {
        public bool Passed { get; init; }
        public int ViolationCount { get; init; }
        public string Channel { get; init; } = "";
    }

    public static Av3JournalLeakScanResult ScanText(string? text, string channel) =>
        Av3JournalTextualLeakScanner.ScanText(text, channel);

    /// <summary>UTF-8 encoded textual surface only (reports/traces) — never JNAL binary.</summary>
    public static Av3JournalLeakScanResult ScanUtf8TextualSurface(ReadOnlySpan<byte> utf8Text, string channel)
    {
        if (utf8Text.IsEmpty)
        {
            return new Av3JournalLeakScanResult { Passed = true, Channel = channel };
        }

        return Av3JournalTextualLeakScanner.ScanText(Encoding.UTF8.GetString(utf8Text), channel);
    }

    /// <summary>Obsolete: use <see cref="ScanUtf8TextualSurface"/> for UTF-8 text; use <see cref="Av3JournalConfidentialityScanner"/> for JNAL bytes.</summary>
    public static Av3JournalLeakScanResult ScanUtf8(ReadOnlySpan<byte> data, string channel) =>
        ScanUtf8TextualSurface(data, channel);

    public static Av3JournalLeakScanResult ScanException(Exception ex, string channel) =>
        ScanText(ex.Message, channel);
}