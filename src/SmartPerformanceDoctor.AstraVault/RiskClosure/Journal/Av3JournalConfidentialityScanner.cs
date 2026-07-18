using SmartPerformanceDoctor.AstraVault.Journal;

namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

/// <summary>R11/E-6.1: binary structural scan — no UTF-8 token scan on digest bytes.</summary>
public static class Av3JournalConfidentialityScanner
{
    public sealed class Av3JournalConfidentialityResult
    {
        public bool Passed { get; init; }

        public int CleartextViolationCount { get; init; }

        public string PublicSummary { get; init; } = "";
    }

    public static Av3JournalConfidentialityResult Scan(ReadOnlySpan<byte> journalBytes)
    {
        if (journalBytes.Length < Av3JournalDescriptor.DescriptorSize)
        {
            return new Av3JournalConfidentialityResult
            {
                Passed = false,
                CleartextViolationCount = 1,
                PublicSummary = "journal_truncated"
            };
        }

        var descriptorBytes = journalBytes.Slice(0, Av3JournalDescriptor.DescriptorSize);
        Av3JournalDescriptor parsed;
        try
        {
            parsed = Av3JournalDescriptor.Parse(descriptorBytes);
        }
        catch
        {
            return new Av3JournalConfidentialityResult
            {
                Passed = false,
                CleartextViolationCount = 1,
                PublicSummary = "journal_descriptor_invalid"
            };
        }

        if (!Av3JournalBinaryFieldPolicy.ValidateParsedDescriptor(parsed, out var allowReason))
        {
            return new Av3JournalConfidentialityResult
            {
                Passed = false,
                CleartextViolationCount = 1,
                PublicSummary = allowReason
            };
        }

        if (journalBytes.Length > Av3JournalDescriptor.DescriptorSize)
        {
            var appendixViolations = Av3JournalBinaryFieldPolicy.ScanTrailingCleartextAppendix(
                journalBytes.Slice(Av3JournalDescriptor.DescriptorSize));
            return new Av3JournalConfidentialityResult
            {
                Passed = false,
                CleartextViolationCount = Math.Max(1, appendixViolations),
                PublicSummary = appendixViolations > 0 ? "cleartext_appendix_detected" : "unexpected_trailing_bytes"
            };
        }

        return new Av3JournalConfidentialityResult
        {
            Passed = true,
            CleartextViolationCount = 0,
            PublicSummary = "digest_only_ok"
        };
    }
}