using System.Text.Json;

namespace SmartPerformanceDoctor.UpdateEngine;

public sealed partial class UpdateTransaction
{
    private string ClassifyLegacyContent(string transactionRoot, UpdateJournal journal,
        LegacyFileObservation observed, long length, SignedFile? original)
    {
        if (original is not null && original.Length == length && original.Sha256 == observed.InstalledHash)
            return "OriginalVerified";
        try
        {
            using var record = WindowsFileBoundary.OpenRead(transactionRoot, "committed-attestation.json");
            VerifyProtectedFile(record, transactionRoot);
            if (record.Stream.Length > 2 * 1024 * 1024) return "UnknownContent";
            using var document = JsonDocument.Parse(record.Stream); ValidateJsonProperties(document.RootElement);
            var signed = document.RootElement.Deserialize<CommittedInstallation>();
            if (signed is null || signed.TransactionId != journal.TransactionId ||
                signed.Manifest.ToVersion != journal.ToVersion || !SignedPackage.VerifyDeclaration(signed.Manifest, signed.Signature, _policy))
                return "UnknownContent";
            var target = signed.Manifest.Files.SingleOrDefault(f => f.Path == observed.Path);
            return target is not null && target.Length == length && target.Sha256 == observed.InstalledHash
                ? "UpdatedVerified" : "UnknownContent";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { return "UnknownContent"; }
    }
}
