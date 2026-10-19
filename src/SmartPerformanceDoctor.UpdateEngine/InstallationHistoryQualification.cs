using System.Text.Json;
namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record InstallationHistoryResult(bool Verified, string State, string Code, int Transactions, int Files)
{
    public int RetainedDirectoryHandles { get; init; }
    public int ReusedDirectoryHandles { get; init; }
    public int RetainedFileHandles { get; init; }
}
public sealed partial class UpdateTransaction
{
    private CommittedInstallation ParseHistoryDeclaration(Stream stream)
    {
        using var timing = PerformanceProfile?.Measure("HistoryJsonParse");
        using var document = JsonDocument.Parse(stream);
        ValidateJsonProperties(document.RootElement);
        return document.RootElement.Deserialize<CommittedInstallation>()!;
    }
    private bool VerifyHistorySignature(SignedManifest manifest, string signature)
    {
        using var timing = PerformanceProfile?.Measure("HistorySignatureVerification");
        return SignedPackage.VerifyDeclaration(manifest, signature, _policy);
    }
    /// <summary>Verify retained signed history without changing installation data.</summary>
    public InstallationHistoryResult InspectInstallationHistory(string installRoot)
    {
        using var measurements = PerformanceProfile?.Activate();
        using var historyTimer = PerformanceProfile?.Measure("HistoryTotal");
        using var directoryHandles = WindowsFileBoundary.RetainSharedDirectoryHandles();
        var current = InspectInstallation(installRoot);
        if (!current.Verified)
            return new(false, current.RequiresReview ? "ManualReviewRequired" : "InstallationUnattested", current.Code, 0, 0);
        var held = new List<WindowsFileBoundary.FileLease>();
        try
        {
            using var install = WindowsFileBoundary.PinDirectory(installRoot);
            using var work = WindowsFileBoundary.PinDirectory(_workRoot);
            var records = new List<(Version From, Version To, UpdateJournal Journal, CommittedInstallation Declaration)>();
            var scanned = 0;
            foreach (var directory in Directory.EnumerateDirectories(_workRoot, "tx-*"))
            {
                using var transactionTimer = PerformanceProfile?.Measure("HistoryTransactionRead");
                if (++scanned > 4096) throw new InvalidDataException("HISTORY-LIMIT");
                // Keep both journal candidates stable while the existing reader selects a valid record.
                foreach (var name in new[] { "journal.json", "journal.previous.json" })
                {
                    if (!File.Exists(Path.Combine(directory, name))) continue;
                    var journalFile = WindowsFileBoundary.OpenMetadata(directory, name);
                    held.Add(journalFile);
                    VerifyProtectedFile(journalFile, directory);
                }
                if (!TryReadTrustedJournal(directory, installRoot, out var journal, out _) || journal is null)
                    throw new InvalidDataException("HISTORY-JOURNAL");
                if (journal.Stage == UpdateStage.Restored || journal.SchemaVersion == 1) continue;
                if (journal.Stage != UpdateStage.Committed) throw new InvalidDataException("HISTORY-INCOMPLETE");
                var record = WindowsFileBoundary.OpenMetadata(directory, "committed-attestation.json"); held.Add(record);
                VerifyProtectedFile(record, directory);
                if (record.Stream.Length > 2 * 1024 * 1024) throw new InvalidDataException("HISTORY-LIMIT");
                var declaration = ParseHistoryDeclaration(record.Stream);
                if (declaration.TransactionId != journal.TransactionId || declaration.InstallIdentity != install.Identity ||
                    declaration.Manifest.PackageId != journal.PackageId || !VerifyHistorySignature(declaration.Manifest, declaration.Signature))
                    throw new InvalidDataException("HISTORY-SIGNATURE");
                if (!Version.TryParse(declaration.Manifest.FromVersion, out var from) || !Version.TryParse(declaration.Manifest.ToVersion, out var to) || from >= to ||
                    !Version.TryParse(journal.FromVersion, out var journalFrom) || !Version.TryParse(journal.ToVersion, out var journalTo) || from != journalFrom || to != journalTo)
                    throw new InvalidDataException("HISTORY-VERSION");
                if (declaration.Manifest.Files.Count != journal.Files.Count) throw new InvalidDataException("HISTORY-SCOPE");
                var journalByPath = journal.Files.ToDictionary(x => x.Path, StringComparer.Ordinal);
                foreach (var file in declaration.Manifest.Files)
                {
                    if (!journalByPath.TryGetValue(file.Path, out var entry)) throw new InvalidDataException("HISTORY-SCOPE");
                    if (!string.Equals(entry.NewSha256, file.Sha256, StringComparison.OrdinalIgnoreCase) || entry.NewLength != file.Length)
                        throw new InvalidDataException("HISTORY-SCOPE");
                }
                records.Add((from, to, journal, declaration));
            }
            if (records.Count == 0) throw new InvalidDataException("HISTORY-INCOMPLETE");
            var ordered = records.OrderBy(x => x.To).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                if (ordered[i - 1].To != ordered[i].From) throw new InvalidDataException("HISTORY-CHAIN-REVIEW");
            InstallationPointer? active = null;
            foreach (var name in new[] { "installation-current.json", "installation-previous.json" })
            {
                if (!File.Exists(Path.Combine(_workRoot, name))) continue;
                var pointer = WindowsFileBoundary.OpenMetadata(_workRoot, name); held.Add(pointer); VerifyProtectedFile(pointer, _workRoot);
                if (pointer.Stream.Length > 4096) throw new InvalidDataException("HISTORY-LIMIT");
                using var doc = JsonDocument.Parse(pointer.Stream); ValidateJsonProperties(doc.RootElement);
                var candidate = doc.RootElement.Deserialize<InstallationPointer>()!;
                if (records.Any(x => x.Journal.TransactionId == candidate.TransactionId)) { active = candidate; break; }
            }
            if (active?.TransactionId != ordered[^1].Journal.TransactionId) throw new InvalidDataException("HISTORY-ORDER");
            var expected = new Dictionary<string, (SignedFile File, string Identity)>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(Path.Combine(_workRoot, "installation-enrollment.json")))
            {
                var enrollment = WindowsFileBoundary.OpenMetadata(_workRoot, "installation-enrollment.json"); held.Add(enrollment); VerifyProtectedFile(enrollment, _workRoot);
                if (enrollment.Stream.Length > 2 * 1024 * 1024) throw new InvalidDataException("HISTORY-LIMIT");
                using var doc = JsonDocument.Parse(enrollment.Stream); ValidateJsonProperties(doc.RootElement);
                var saved = doc.RootElement.Deserialize<InstallationEnrollment>()!;
                if (saved.InstallIdentity != install.Identity || !VerifyHistorySignature(saved.Manifest, saved.Signature) ||
                    !Version.TryParse(saved.Manifest.ToVersion, out var version) || version != ordered[0].From || saved.Files.Count != saved.Manifest.Files.Count)
                    throw new InvalidDataException("HISTORY-ENROLLMENT");
                foreach (var item in saved.Manifest.Files)
                {
                    var bound = saved.Files.Single(x => x.Path == item.Path);
                    if (bound.Length != item.Length || !string.Equals(bound.Hash, item.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("HISTORY-SCOPE");
                    expected[item.Path] = (item, bound.Identity);
                }
            }
            foreach (var record in ordered)
            {
                var journalByPath = record.Journal.Files.ToDictionary(x => x.Path, StringComparer.Ordinal);
                foreach (var file in record.Declaration.Manifest.Files)
                    expected[file.Path] = (file, journalByPath[file.Path].InstalledIdentity);
            }
            foreach (var entry in expected)
            {
                var file = WindowsFileBoundary.OpenRead(installRoot, Relative(entry.Key)); held.Add(file);
                VerifyInstallationFile(file, entry.Value.File.Length, entry.Value.File.Sha256, entry.Value.Identity);
            }
            foreach (var file in held) file.Validate();
            install.Validate(); work.Validate();
            directoryHandles.Validate();
            return new(true, "InstallationVerified", "RETAINED-SIGNED-HISTORY-VERIFIED", records.Count, expected.Count)
            {
                RetainedDirectoryHandles = directoryHandles.UniqueHandles,
                ReusedDirectoryHandles = directoryHandles.ReusedHandles,
                RetainedFileHandles = held.Count
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException or NullReferenceException)
        {
            var reason = ex is FileNotFoundException or DirectoryNotFoundException || ex.InnerException is System.ComponentModel.Win32Exception { NativeErrorCode: 2 or 3 } ? "HISTORY-INCOMPLETE" :
                ex is InvalidDataException && ex.Message.StartsWith("HISTORY-", StringComparison.Ordinal) ? ex.Message :
                ex.Message is "FILE-INTEGRITY" ? "HISTORY-CONTENT-MISMATCH" : InstallationFailureCode(ex);
            return new(false, reason is "HISTORY-INCOMPLETE" or "HISTORY-JOURNAL" ? "InstallationHistoryIncomplete" : "InstallationHistoryMismatch", reason, 0, 0);
        }
        finally { foreach (var file in held) file.Dispose(); }
    }
}

public sealed record QualificationObservation(string RunId,string MachineBinding,string BootBinding,string Source,
    string RootIdentity,string ManifestDigest,bool BytesVerified,string RecoveryState);
public sealed record QualificationComparison(bool Verified,string State,bool RebootObserved);
public static class InstallationQualificationComparison
{
    public static QualificationComparison Compare(QualificationObservation before,QualificationObservation after,bool requireReboot)
    {
        if(string.IsNullOrWhiteSpace(before.RunId)||before.RunId!=after.RunId||
            string.IsNullOrWhiteSpace(before.MachineBinding)||before.MachineBinding!=after.MachineBinding||
            string.IsNullOrWhiteSpace(before.Source)||before.Source!=after.Source||
            string.IsNullOrWhiteSpace(before.RootIdentity)||before.RootIdentity!=after.RootIdentity)
            return new(false,"EvidenceBindingMismatch",false);
        var reboot=!string.IsNullOrWhiteSpace(before.BootBinding)&&!string.IsNullOrWhiteSpace(after.BootBinding)&&before.BootBinding!=after.BootBinding;
        if(requireReboot&&!reboot)return new(false,"RebootNotObserved",false);
        if(!before.BytesVerified||!after.BytesVerified||string.IsNullOrWhiteSpace(before.ManifestDigest)||before.ManifestDigest!=after.ManifestDigest)
            return new(false,"ContentReviewRequired",reboot);
        if(after.RecoveryState is not ("NoPendingTransaction" or "RecoveryCompleted"))return new(false,"RecoveryReviewRequired",reboot);
        return new(true,"ObservedBytesMatch",reboot);
    }
}
