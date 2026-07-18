namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

/// <summary>R11/E-6.1: binary structural journal scan + textual public surfaces.</summary>
public static class Av3JournalConfidentialityValidator
{
    public sealed class Av3JournalConfidentialityValidationResult
    {
        public bool Passed { get; init; }
        public int TotalViolations { get; init; }
        public string PublicSummary { get; init; } = "";
    }

    public static Av3JournalConfidentialityValidationResult ValidateJournalBytes(ReadOnlySpan<byte> journalBytes)
    {
        var binary = Av3JournalConfidentialityScanner.Scan(journalBytes);
        return new Av3JournalConfidentialityValidationResult
        {
            Passed = binary.Passed,
            TotalViolations = binary.CleartextViolationCount,
            PublicSummary = binary.PublicSummary
        };
    }

    public static Av3JournalConfidentialityValidationResult ValidatePublicSurfaces(
        ReadOnlySpan<byte> journalBytes,
        string? reportJson,
        string? traceText,
        Exception? exception)
    {
        var binary = ValidateJournalBytes(journalBytes);
        var report = Av3JournalTextualLeakScanner.ScanText(reportJson, "report");
        var trace = Av3JournalTextualLeakScanner.ScanText(traceText, "trace");
        var ex = exception is null
            ? new Av3JournalLeakScanner.Av3JournalLeakScanResult { Passed = true, Channel = "exception" }
            : Av3JournalLeakScanner.ScanException(exception, "exception");

        var violations = binary.TotalViolations + report.ViolationCount + trace.ViolationCount + ex.ViolationCount;
        return new Av3JournalConfidentialityValidationResult
        {
            Passed = violations == 0,
            TotalViolations = violations,
            PublicSummary = violations == 0 ? "journal_and_surfaces_ok" : "leak_pattern_detected"
        };
    }
}