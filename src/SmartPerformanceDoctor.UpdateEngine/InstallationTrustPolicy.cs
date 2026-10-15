using System.IO.Compression;
using System.Security.AccessControl;
using System.Text.Json;

namespace SmartPerformanceDoctor.UpdateEngine;

public enum InstallationTrustState
{
    FreshInstallation, LegacyUnattestedInstallation, SignedInstallationVerified, SignedInstallationMismatch,
    PendingTransaction, RecoveryRequired, AttestationMissing, AttestationCorrupted, MigrationReviewRequired
}
public sealed record InstallationTrustDecision(InstallationTrustState State, bool Verified, bool RequiresReview, string Code);
public sealed record EnrolledFile(string Path, long Length, string Hash, string Identity);
public sealed record InstallationEnrollment(string InstallIdentity, SignedManifest Manifest, string Signature,
    IReadOnlyList<EnrolledFile> Files, DateTimeOffset CreatedAt);

public sealed partial class UpdateTransaction
{
    public InstallationTrustDecision InspectInstallationTrust(string installRoot)
    {
        var recovery = InspectRecovery(installRoot);
        if (recovery.State is UpdateRecoveryState.RecoveryAvailable or UpdateRecoveryState.RecoveryInProgress)
            return new(InstallationTrustState.PendingTransaction, false, true, recovery.Code);
        if (recovery.State is UpdateRecoveryState.RecoveryRequired or UpdateRecoveryState.RecoveryFailed)
            return new(InstallationTrustState.RecoveryRequired, false, true, recovery.Code);
        if (File.Exists(Path.Combine(_workRoot, "installation-current.json")))
        {
            var result = InspectInstallation(installRoot);
            if (result.Verified) return new(InstallationTrustState.SignedInstallationVerified, true, false, result.Code);
            var state = result.Code == "INSTALLATION-CONTENT-MISMATCH" ? InstallationTrustState.SignedInstallationMismatch :
                result.RequiresReview ? InstallationTrustState.AttestationCorrupted : InstallationTrustState.MigrationReviewRequired;
            return new(state, false, true, result.Code);
        }
        if (File.Exists(Path.Combine(_workRoot, "installation-enrollment.json")))
        {
            try
            {
                using var install = WindowsFileBoundary.PinDirectory(installRoot);
                if (_strictAcl) ValidateProtectedSecurity(new DirectoryInfo(installRoot).GetAccessControl(), false);
                using var record = WindowsFileBoundary.OpenRead(_workRoot, "installation-enrollment.json");
                VerifyProtectedFile(record, _workRoot);
                if (record.Stream.Length > 2 * 1024 * 1024) throw new InvalidDataException("ENROLLMENT-LIMIT");
                using var doc = JsonDocument.Parse(record.Stream); ValidateJsonProperties(doc.RootElement);
                var saved = doc.RootElement.Deserialize<InstallationEnrollment>()!;
                if (saved.InstallIdentity != install.Identity || !SignedPackage.VerifyDeclaration(saved.Manifest, saved.Signature, _policy) ||
                    saved.Files.Count != saved.Manifest.Files.Count) throw new InvalidDataException("ENROLLMENT-UNTRUSTED");
                foreach (var expected in saved.Manifest.Files)
                {
                    var bound = saved.Files.Single(f => f.Path == expected.Path);
                    if (bound.Hash != expected.Sha256 || bound.Length != expected.Length) throw new InvalidDataException("ENROLLMENT-SCOPE");
                    using var target = WindowsFileBoundary.OpenRead(installRoot, Relative(expected.Path));
                    if (_strictAcl) ValidateProtectedSecurity(target.ReadSecurity(), false);
                    target.Verify(expected.Length, expected.Sha256, bound.Identity);
                }
                install.Validate(); record.Validate();
                return new(InstallationTrustState.SignedInstallationVerified, true, false, "SIGNED-ENROLLMENT-VERIFIED");
            }
            catch (JsonException) { return new(InstallationTrustState.AttestationCorrupted, false, true, "ENROLLMENT-CORRUPTED"); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or NullReferenceException)
            { return new(InstallationTrustState.SignedInstallationMismatch, false, true, "ENROLLMENT-REVIEW-REQUIRED"); }
        }
        if (Directory.Exists(_workRoot) && Directory.EnumerateDirectories(_workRoot, "tx-*").Any())
            return new(InstallationTrustState.AttestationMissing, false, true, "ATTESTATION-MISSING-WITH-HISTORY");
        var existing = Directory.Exists(installRoot) && Directory.EnumerateFileSystemEntries(installRoot).Any();
        return new(existing ? InstallationTrustState.LegacyUnattestedInstallation : InstallationTrustState.FreshInstallation,
            false, false, existing ? "LEGACY-UNATTESTED" : "FRESH-INSTALLATION");
    }

    /// <summary>Enroll matching installed bytes from a signed package. Preserve existing records.</summary>
    public InstallationTrustDecision EnrollInstallation(string installRoot, string packagePath, string previousVersion, bool consent)
    {
        if (!consent) return new(InstallationTrustState.MigrationReviewRequired, false, true, "ENROLLMENT-CONSENT-REQUIRED");
        if (_strictAcl)
        {
            if (!new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)) throw new UnauthorizedAccessException("ENROLLMENT-ADMIN-REQUIRED");
            InitializeProtectedRoot(); ValidateProtectedSecurity(new DirectoryInfo(installRoot).GetAccessControl(), false);
        }
        else Directory.CreateDirectory(_workRoot);
        using var install = WindowsFileBoundary.PinDirectory(installRoot);
        using var work = WindowsFileBoundary.PinDirectory(_workRoot);
        var status = InspectInstallationTrust(installRoot);
        if (status.State != InstallationTrustState.LegacyUnattestedInstallation)
            return new(InstallationTrustState.MigrationReviewRequired, false, true, "ENROLLMENT-EXISTING-STATE-PRESERVED");
        using var gate = WindowsFileBoundary.OpenExclusive(_workRoot, "update.lock", !File.Exists(Path.Combine(_workRoot,"update.lock")));
        VerifyProtectedFile(gate, _workRoot);
        if (File.Exists(Path.Combine(_workRoot, "installation-current.json")) || File.Exists(Path.Combine(_workRoot, "installation-enrollment.json")) ||
            Directory.EnumerateDirectories(_workRoot, "tx-*").Any())
            return new(InstallationTrustState.MigrationReviewRequired, false, true, "ENROLLMENT-STATE-CHANGED");
        using var package = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(packagePath))!, Path.GetFileName(packagePath));
        var verified = SignedPackage.Verify(package.Stream, previousVersion, _policy);
        if (!verified.CanApply || verified.Manifest is null)
            return new(InstallationTrustState.MigrationReviewRequired, false, true, "ENROLLMENT-SIGNATURE-REJECTED");
        var pinned = new List<WindowsFileBoundary.FileLease>();
        try
        {
            var files = new List<EnrolledFile>();
            foreach (var expected in verified.Manifest.Files)
            {
                var target = WindowsFileBoundary.OpenRead(installRoot, Relative(expected.Path)); pinned.Add(target);
                if (_strictAcl) ValidateProtectedSecurity(target.ReadSecurity(), false);
                target.Verify(expected.Length, expected.Sha256);
                files.Add(new(expected.Path, expected.Length, expected.Sha256, target.Identity));
            }
            package.Stream.Position = 0;
            using var zip = new ZipArchive(package.Stream, ZipArchiveMode.Read, true);
            using var signature = zip.GetEntry(SignedPackage.SignatureName)!.Open();
            var envelope = JsonSerializer.Deserialize<SignatureEnvelope>(signature)!;
            using var record = WindowsFileBoundary.OpenWrite(_workRoot, "installation-enrollment.json", true);
            VerifyProtectedFile(record, _workRoot);
            record.Stream.Write(JsonSerializer.SerializeToUtf8Bytes(new InstallationEnrollment(install.Identity, verified.Manifest,
                envelope.Signature, files, DateTimeOffset.UtcNow)));
            record.Stream.Flush(true); record.Validate(); install.Validate(); work.Validate(); package.Validate();
            foreach (var file in pinned) file.Validate();
            return new(InstallationTrustState.SignedInstallationVerified, true, false, "SIGNED-ENROLLMENT-CREATED");
        }
        finally { foreach (var file in pinned) file.Dispose(); }
    }
}
