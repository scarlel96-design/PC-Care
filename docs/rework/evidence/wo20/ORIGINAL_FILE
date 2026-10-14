using System.Security.Cryptography;
using System.Text.Json;

namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record LegacyRecoveryTarget(string Path, string CurrentIdentity, string CurrentHash,
    string BackupIdentity, string AuthenticatedOriginalHash, long OriginalLength, string Status);
public sealed record LegacyRecoveryPlan(string TransactionId, string JournalHash, string InstallIdentity,
    string TransactionIdentity, string OriginalPackageHash, string OriginalVersion, string Status,
    bool CanExecute, string ConsentBinding, IReadOnlyList<LegacyRecoveryTarget> Targets);

public sealed partial class UpdateTransaction
{
    /// <summary>Bind a read-only plan to independently signed original-version evidence.</summary>
    public LegacyRecoveryPlan PlanLegacyRecovery(string installRoot, string transactionId,
        string originalPackage, string packagePreviousVersion)
    {
        if (!Guid.TryParseExact(transactionId, "N", out _)) throw new InvalidDataException("LEGACY-ID");
        var diagnosis = DiagnoseLegacy(installRoot).SingleOrDefault(item => item.TransactionId == transactionId)
            ?? throw new InvalidDataException("LEGACY-NOT-FOUND");
        var targets = new List<LegacyRecoveryTarget>();
        var packageHash = ""; var originalVersion = ""; var state = "RecoveryDenied";
        if (diagnosis.Code.StartsWith("READONLY-MANUAL-REVIEW", StringComparison.Ordinal))
        {
            var packageFull = Path.GetFullPath(originalPackage);
            using var package = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(packageFull)!, Path.GetFileName(packageFull));
            packageHash = package.Hash();
            var verification = SignedPackage.Verify(package.Stream, packagePreviousVersion, _policy);
            if (verification.CanApply && verification.Manifest is not null)
            {
                var txDir = Path.Combine(_workRoot, "tx-" + transactionId);
                using var record = WindowsFileBoundary.OpenRead(txDir, "journal.json");
                if (record.Hash() != diagnosis.JournalHash) throw new InvalidDataException("LEGACY-CHANGED");
                var journal = ReadJournalFile(txDir, "journal.json")!;
                originalVersion = verification.Manifest.ToVersion;
                if (originalVersion == journal.FromVersion)
                {
                    state = "ManualReviewRequired";
                    foreach (var observed in diagnosis.Files)
                    {
                        var expected = verification.Manifest.Files.SingleOrDefault(item => item.Path == observed.Path);
                        var entry = journal.Files.Single(item => item.Path == observed.Path);
                        var status = "RecoveryDenied";
                        if (entry.Existed && expected is not null)
                        {
                            using var backup = WindowsFileBoundary.OpenRead(Path.Combine(txDir, "backup"), Relative(observed.Path));
                            VerifyProtectedFile(backup, Path.Combine(txDir, "backup"));
                            backup.Verify(expected.Length, expected.Sha256, observed.BackupIdentity);
                            if (expected.Sha256 == entry.OriginalSha256 && expected.Length == entry.OriginalLength)
                                status = "BackupVerified";
                        }
                        targets.Add(new(observed.Path, observed.InstalledIdentity, observed.InstalledHash,
                            observed.BackupIdentity, expected?.Sha256 ?? "", expected?.Length ?? -1, status));
                    }
                }
                record.Validate(); package.Validate();
            }
        }
        // A read-only plan grants no mutation authority. Administrator execution needs its own reviewed contract.
        var binding = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { diagnosis.TransactionId, diagnosis.JournalHash, diagnosis.InstallIdentity, diagnosis.TransactionIdentity,
            PackageHash = packageHash, OriginalVersion = originalVersion, Targets = targets })));
        return new(diagnosis.TransactionId, diagnosis.JournalHash, diagnosis.InstallIdentity,
            diagnosis.TransactionIdentity, packageHash, originalVersion, state, false, binding, targets);
    }
}
