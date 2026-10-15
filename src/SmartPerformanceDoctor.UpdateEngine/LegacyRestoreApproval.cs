using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record LegacyApprovalIntent(string RequestId, DateTimeOffset RequestedAt, bool UserConsent);
public sealed record LegacyApprovalScope(string RequestId, string TransactionId, string PackageIdentity,
    string PlanBinding, string RequestIdentity, string RequestHash, DateTimeOffset ApprovedAt,
    string AdministratorSid, string AdministratorSession, IReadOnlyList<LegacyRecoveryTarget> Targets);

public static class LegacyApprovalPolicy
{
    public static bool IsCurrent(LegacyApprovalIntent? intent, DateTimeOffset now) => intent is not null &&
        intent.UserConsent && Guid.TryParseExact(intent.RequestId, "N", out _) &&
        intent.RequestedAt <= now && now - intent.RequestedAt <= TimeSpan.FromMinutes(10);
}

public sealed partial class UpdateTransaction
{
    private readonly AsyncLocal<string?> _approvedLegacyBinding = new();

    /// <summary>Read the pinned request again. Record a single-use approval before restoration.</summary>
    public TransactionResult ExecuteLegacyRecoveryRequest(string installRoot, string requestPath,
        string expectedRequestHash, Func<LegacyRecoveryPlan, bool> confirm,
        Action<LegacyRestoreFaultPoint>? fault = null)
    {
        using var requestFile = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(requestPath))!, Path.GetFileName(requestPath));
        if (requestFile.Stream.Length > 1024 * 1024 || requestFile.Hash() != expectedRequestHash)
            throw new InvalidDataException("LEGACY-REQUEST-CHANGED");
        using var document = JsonDocument.Parse(requestFile.Stream);
        ValidateJsonProperties(document.RootElement);
        var request = document.RootElement.Deserialize<LegacyRestoreRequest>(new JsonSerializerOptions
            { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidDataException("LEGACY-REQUEST");
        if (!LegacyApprovalPolicy.IsCurrent(request.Approval, DateTimeOffset.UtcNow))
            return new(UpdateStage.RecoveryRequired, "LEGACY-APPROVAL-EXPIRED-OR-MISSING", request.Plan.TransactionId, 0);
        if (_strictAcl && !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            throw new UnauthorizedAccessException("LEGACY-ADMIN-REQUIRED");
        var fresh = PlanLegacyRecovery(installRoot, request.Plan.TransactionId, request.OriginalPackage, request.PackagePreviousVersion);
        if (fresh.ConsentBinding != request.Plan.ConsentBinding || fresh.Status != "ManualReviewRequired" ||
            fresh.Targets.Count == 0 || fresh.Targets.Any(t => t.Status != "BackupVerified" ||
                t.CurrentContentState is not ("OriginalVerified" or "UpdatedVerified")))
            return new(UpdateStage.RecoveryRequired, "LEGACY-APPROVAL-SCOPE-CHANGED", request.Plan.TransactionId, 0);
        if (!confirm(fresh))
            return new(UpdateStage.RecoveryRequired, "LEGACY-APPROVAL-CANCELLED", request.Plan.TransactionId, 0);
        if (!LegacyApprovalPolicy.IsCurrent(request.Approval, DateTimeOffset.UtcNow))
            return new(UpdateStage.RecoveryRequired, "LEGACY-APPROVAL-EXPIRED", request.Plan.TransactionId, 0);
        requestFile.Verify(requestFile.Stream.Length, expectedRequestHash);
        if (_strictAcl) VerifyProtectedAcl(_workRoot);
        using var root = WindowsFileBoundary.PinDirectory(_workRoot);
        // CreateNew prevents reuse after success, failure, or an interrupted approval.
        using var receipt = WindowsFileBoundary.OpenExclusive(_workRoot, "legacy-approval-" + request.Approval!.RequestId + ".json", true);
        VerifyProtectedFile(receipt, _workRoot);
        var scope = new LegacyApprovalScope(request.Approval.RequestId, fresh.TransactionId, fresh.PackageIdentity,
            fresh.ConsentBinding, requestFile.Identity, expectedRequestHash, DateTimeOffset.UtcNow,
            WindowsIdentity.GetCurrent().User!.Value, Guid.NewGuid().ToString("N"), fresh.Targets);
        receipt.Stream.Write(JsonSerializer.SerializeToUtf8Bytes(scope));
        receipt.Stream.Flush(true);
        root.Validate(); requestFile.Validate(); receipt.Validate();
        _approvedLegacyBinding.Value = fresh.ConsentBinding;
        try
        {
            return ExecuteLegacyRecovery(installRoot, fresh, request.OriginalPackage, request.PackagePreviousVersion,
                fresh.ConsentBinding, true, fault);
        }
        finally { _approvedLegacyBinding.Value = null; }
    }
}
