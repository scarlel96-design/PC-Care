using System.Text.Json;

namespace SmartPerformanceDoctor.UpdateEngine;

public enum LegacyApprovalState
{
    ApprovalRequested, ApprovalReserved, RestorationInterrupted, RestorationInProgress,
    RestorationCommitted, RecoveryReviewRequired
}
public sealed record LegacyApprovalObservation(string RequestId, string TransactionId,
    LegacyApprovalState State, string Code, string LastCheckpoint, bool ApprovalConsumed,
    bool CanResumeRecovery, bool RequiresNewApproval, bool FilesVerified)
{
    public bool? CallerAcknowledged => null;
}

public sealed partial class UpdateTransaction
{
    /// <summary>Reconcile protected approval evidence and actual bytes. Never mutate or resume work.</summary>
    public LegacyApprovalObservation InspectLegacyApproval(string installRoot, string requestPath, string expectedRequestHash)
    {
        string requestId = "", transactionId = "";
        var consumed = false;
        LegacyApprovalObservation Review(string code) => new(requestId, transactionId,
            LegacyApprovalState.RecoveryReviewRequired, code, "Unverified", consumed, false, true, false);
        try
        {
            using var requestFile = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(requestPath))!, Path.GetFileName(requestPath));
            if (requestFile.Stream.Length > 1024 * 1024 || requestFile.Hash() != expectedRequestHash) return Review("LEGACY-REQUEST-CHANGED");
            using var doc = JsonDocument.Parse(requestFile.Stream); ValidateJsonProperties(doc.RootElement);
            var request = doc.RootElement.Deserialize<LegacyRestoreRequest>() ?? throw new InvalidDataException("LEGACY-REQUEST");
            if (request.Approval is null || !Guid.TryParseExact(request.Approval.RequestId, "N", out _)) return Review("LEGACY-REQUEST-ID");
            requestId = request.Approval.RequestId; transactionId = request.Plan.TransactionId;
            if (!Guid.TryParseExact(transactionId, "N", out _)) return Review("LEGACY-TRANSACTION-ID");
            var receiptName = "legacy-approval-" + requestId + ".json";
            if (!File.Exists(Path.Combine(_workRoot, receiptName)))
            {
                var plan = PlanLegacyRecovery(installRoot, transactionId, request.OriginalPackage, request.PackagePreviousVersion);
                if (plan.ConsentBinding != request.Plan.ConsentBinding) return Review("LEGACY-PLAN-CHANGED");
                return new(requestId, transactionId, LegacyApprovalState.ApprovalRequested, "APPROVAL-NOT-RESERVED",
                    "RequestOnly", false, false, true, false);
            }
            consumed = true;
            if (_strictAcl) VerifyProtectedAcl(_workRoot);
            using var receipt = WindowsFileBoundary.OpenRead(_workRoot, receiptName); VerifyProtectedFile(receipt, _workRoot);
            if (receipt.Stream.Length > 1024 * 1024) return Review("LEGACY-RECEIPT-LIMIT");
            using var rd = JsonDocument.Parse(receipt.Stream); ValidateJsonProperties(rd.RootElement);
            var scope = rd.RootElement.Deserialize<LegacyApprovalScope>() ?? throw new InvalidDataException("LEGACY-RECEIPT");
            if (scope.RequestId != requestId || scope.TransactionId != transactionId || scope.RequestHash != expectedRequestHash ||
                scope.RequestIdentity != requestFile.Identity || scope.PlanBinding != request.Plan.ConsentBinding ||
                scope.PackageIdentity != request.Plan.PackageIdentity || !Guid.TryParseExact(scope.AdministratorSession,"N",out _) ||
                string.IsNullOrWhiteSpace(scope.AdministratorSid) || scope.Targets.Count == 0 ||
                scope.Targets.Select(t=>t.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=scope.Targets.Count)
                return Review("LEGACY-RECEIPT-BINDING");
            using var install = WindowsFileBoundary.PinDirectory(installRoot);
            var tx = Path.Combine(_workRoot, "tx-" + transactionId);
            using var transaction = WindowsFileBoundary.PinDirectory(tx);
            if (install.Identity != request.Plan.InstallIdentity || transaction.Identity != request.Plan.TransactionIdentity)
                return Review("LEGACY-DIRECTORY-IDENTITY");
            using var package = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(request.OriginalPackage))!, Path.GetFileName(request.OriginalPackage));
            if (package.Identity != scope.PackageIdentity || package.Hash() != request.Plan.OriginalPackageHash)
                return Review("LEGACY-PACKAGE-CHANGED");
            var signed = SignedPackage.Verify(package.Stream, request.PackagePreviousVersion, _policy, scope.ApprovedAt);
            if (!signed.CanApply || signed.Manifest is null || signed.Manifest.ToVersion != request.Plan.OriginalVersion)
                return Review("LEGACY-SIGNATURE-REJECTED");
            using var journalFile = WindowsFileBoundary.OpenRead(tx,"journal.json"); VerifyProtectedFile(journalFile,tx);
            var journal = ReadJournalFile(tx,"journal.json") ?? throw new InvalidDataException("JOURNAL-MISSING");
            if (journal.TransactionId != transactionId || journal.Files.Count != scope.Targets.Count) return Review("LEGACY-JOURNAL-SCOPE");
            if (journal.SchemaVersion == 1 && journalFile.Hash() != request.Plan.JournalHash) return Review("LEGACY-JOURNAL-CHANGED");
            if (journal.SchemaVersion == 2)
            {
                if (!TryReadTrustedJournal(tx,installRoot,out var trusted,out _) || trusted is null ||
                    JsonSerializer.Serialize(trusted,JsonOptions)!=JsonSerializer.Serialize(journal,JsonOptions)) return Review("LEGACY-JOURNAL-UNTRUSTED");
                using var original = WindowsFileBoundary.OpenRead(tx,"legacy-original-"+request.Plan.JournalHash+".json");
                VerifyProtectedFile(original,tx);
                if (original.Hash() != request.Plan.JournalHash) return Review("LEGACY-ORIGINAL-JOURNAL-CHANGED");
            }
            else if (journal.SchemaVersion != 1) return Review("LEGACY-JOURNAL-SCHEMA");
            var allOriginal = true;
            foreach (var target in scope.Targets)
            {
                var expected = signed.Manifest.Files.SingleOrDefault(f=>f.Path==target.Path);
                var entry = journal.Files.SingleOrDefault(f=>f.Path==target.Path);
                if (expected is null || entry is null || expected.Length!=target.OriginalLength || expected.Sha256!=target.AuthenticatedOriginalHash)
                    return Review("LEGACY-SIGNED-SCOPE");
                using var backup = WindowsFileBoundary.OpenRead(Path.Combine(tx,"backup"),Relative(target.Path));
                VerifyProtectedFile(backup,Path.Combine(tx,"backup"));
                backup.Verify(expected.Length,expected.Sha256,target.BackupIdentity);
                using var current = WindowsFileBoundary.OpenRead(installRoot,Relative(target.Path));
                if (_strictAcl) ValidateProtectedSecurity(current.ReadSecurity(),false);
                current.Validate(target.CurrentIdentity);
                if (current.ReadSecurity().GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.All)!=target.CurrentSecurity)
                    return Review("LEGACY-SECURITY-CHANGED");
                var hash = current.Hash();
                var originalMatch = hash==expected.Sha256 && current.Stream.Length==expected.Length;
                var currentMatch = hash==target.CurrentHash && current.Stream.Length==target.CurrentLength;
                allOriginal &= originalMatch;
                if (!originalMatch && !currentMatch) return Review("LEGACY-CURRENT-CONTENT-UNKNOWN");
            }
            install.Validate(); transaction.Validate(); receipt.Validate(); requestFile.Validate(); journalFile.Validate(); package.Validate();
            var recovery = InspectRecovery(installRoot);
            if (recovery.State==UpdateRecoveryState.RecoveryInProgress)
                return new(requestId,transactionId,LegacyApprovalState.RestorationInProgress,"RECOVERY-ACTIVE",journal.Stage.ToString(),true,false,false,false);
            if (journal.SchemaVersion==1)
                return new(requestId,transactionId,LegacyApprovalState.ApprovalReserved,"APPROVAL-CONSUMED-NEW-REQUEST-REQUIRED",journal.Stage.ToString(),true,false,true,false);
            if (journal.Stage==UpdateStage.Restored && allOriginal && journal.Files.All(f=>f.Restored))
                return new(requestId,transactionId,LegacyApprovalState.RestorationCommitted,"RESTORATION-BYTES-VERIFIED",journal.Stage.ToString(),true,false,false,true);
            if (journal.Stage is UpdateStage.RollbackPending or UpdateStage.RollingBack)
                return new(requestId,transactionId,LegacyApprovalState.RestorationInterrupted,"RESTORATION-REQUERY-RESUME-REQUIRED",journal.Stage.ToString(),true,true,false,false);
            return Review("LEGACY-RESTORATION-NOT-VERIFIED");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or NullReferenceException)
        { return Review("LEGACY-QUERY-REVIEW-REQUIRED"); }
    }
}
