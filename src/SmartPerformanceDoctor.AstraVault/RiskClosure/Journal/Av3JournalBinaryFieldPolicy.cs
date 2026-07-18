using SmartPerformanceDoctor.AstraVault.Journal;

namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

/// <summary>R11/E-6.1: v1 digest-only journal — allowed on-disk fields (no path/filename/plaintext metadata).</summary>
public static class Av3JournalBinaryFieldPolicy
{
    public static readonly string[] ForbiddenConceptualFields =
    [
        "filename",
        "path",
        "extension",
        "plaintext_metadata",
        "password",
        "vmk",
        "dek",
        "user_path"
    ];

    public static bool ValidateParsedDescriptor(Av3JournalDescriptor descriptor, out string publicReason)
    {
        publicReason = "ok";
        if (descriptor.PreviousMetadataRootCiphertextDigest.Length != 32
            || descriptor.TargetMetadataRootCiphertextDigest.Length != 32
            || descriptor.ObjectWriteSetDigest.Length != 32
            || descriptor.MetadataWriteDigest.Length != 32
            || descriptor.RecordDigest.Length != 32)
        {
            publicReason = "digest_field_size_invalid";
            return false;
        }

        return true;
    }

    public static int ScanTrailingCleartextAppendix(ReadOnlySpan<byte> appendix)
    {
        if (appendix.IsEmpty)
        {
            return 0;
        }

        return Av3JournalTextualLeakScanner.CountForbiddenTokens(appendix);
    }
}