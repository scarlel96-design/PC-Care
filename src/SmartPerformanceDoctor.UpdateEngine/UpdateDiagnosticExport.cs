using System.Text.Json;
namespace SmartPerformanceDoctor.UpdateEngine;

public static class UpdateDiagnosticExport
{
    /// <summary>Export a fixed allowlist. Never copy free-form failure messages or machine identifiers.</summary>
    public static string Serialize(TransactionResult result) => JsonSerializer.Serialize(new
    {
        Stage = result.Stage.ToString(),
        result.FilesApplied,
        result.AnyMutationStarted,
        result.WasCancellationRequested,
        Disposition = result.RecoveryDisposition is "PreMutationRejected" or "OriginalStateVerified" or "RecoveryCompleted" or "RecoveryDeferred"
            ? result.RecoveryDisposition : "ManualReviewRequired",
        result.OriginalBytesVerified,
        Primary = Numeric(result.Diagnostic),
        Recovery = Numeric(result.RecoveryDiagnostic)
    });

    public static string Serialize(RecoveryReconciliation value) => JsonSerializer.Serialize(new
    {
        State = value.State is "RecoveryDeferred" or "RecoveryRequired" or "NoPendingTransaction" or "NotStarted" ? value.State : "ManualReviewRequired",
        value.OriginalBytesVerified,
        RecordedTargets = value.Files.Count,
        ExecutionAuthorized = false
    });

    private static object? Numeric(UpdateFailureDiagnostic? value) => value is null ? null : new
    {
        ExceptionType = value.ExceptionType is "IOException" or "UnauthorizedAccessException" or "InvalidDataException" or "JsonException" or "Win32Exception" ? value.ExceptionType : "Exception",
        value.NativeErrorCode,
        JournalStage = value.JournalStage.ToString(),
        value.AnyMutationStarted,
        value.StrictAclChecksRequired,
        ShareModeSource = "RequestedByBoundary; not the external holder mode",
        DeletePending = "NOT OBSERVED"
    };
}
