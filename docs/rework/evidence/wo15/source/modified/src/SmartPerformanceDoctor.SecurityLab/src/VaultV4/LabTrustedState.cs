using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

internal enum RecoveryPosture
{
    FreshnessVerified, AuthenticButStale, AnchorUnavailable, AnchorMissing,
    AnchorCorrupted, AnchorReset, RecoveryReviewRequired, UnsupportedProvider
}
internal enum RecoveryReceiptState
{
    Unused, Reserved, Authenticated, SessionEstablished, ConsumptionCommitted,
    ReceiptPending, ReceiptAcknowledged, Interrupted, RecoveryReviewRequired
}
internal sealed record FreshnessAnchorDescriptor(string Provider, string ThreatBoundary, bool ProductionApproved);
internal sealed record FreshnessEvidence(string VaultId, long Generation, string GenerationBinding, string RecoveryBinding);
internal sealed record AnchorCommitRequest(FreshnessEvidence Previous, FreshnessEvidence Next);
internal sealed record AnchorVerificationResult(RecoveryPosture Posture, string Reason);
internal sealed record FreshnessPolicy(bool RequireAnchorForRecovery, bool AllowAuthenticatedReadOnly);
internal sealed record RecoveryReceipt
{
    public required string VaultId { get; init; }
    public required Guid RecoveryOperationId { get; init; }
    public required string ProtectedCodeIdentifier { get; init; }
    public required long AuthenticatedGeneration { get; init; }
    public required DateTimeOffset ReservedAt { get; init; }
    public DateTimeOffset? CommittedAt { get; init; }
    public required Guid SessionConnection { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public RecoveryReceiptState State { get; init; }
    public RecoveryPosture AnchorResult { get; init; }
}
internal interface TrustedStateProvider
{
    FreshnessAnchorDescriptor Descriptor { get; }
    AnchorVerificationResult Verify(FreshnessEvidence evidence);
    RecoveryReceipt? Query(Guid operationId);
}

/// <summary>Test-only DPAPI store. The attacker can replace vault files but cannot replace this store.</summary>
internal sealed class LabLocalTrustedState : TrustedStateProvider
{
    private sealed record State(string InstanceId, FreshnessEvidence Evidence, List<RecoveryReceipt> Receipts);
    private readonly string _root;
    private readonly string _instanceId;
    internal string Root => _root;
    internal Action<string>? FaultObserver { get; set; }
    public FreshnessAnchorDescriptor Descriptor => new("DPAPI-CurrentUser-Test", "Vault-only rollback; anchor rollback is not covered", false);
    internal LabLocalTrustedState(string root, string instanceId)
    {
        _root = Path.GetFullPath(root); _instanceId = instanceId;
        ValidateFixture(_root);
    }
    internal static void ValidateFixture(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current is not null && !current.Name.StartsWith("wo15-vault-fixture-", StringComparison.Ordinal)) current = current.Parent;
        if (current is null || File.ReadAllText(Path.Combine(current.FullName, "fixture.marker")) != "WO15_SYNTHETIC_ONLY")
            throw new InvalidOperationException("WO15-SYNTHETIC-FIXTURE-REQUIRED");
        using var pin = VaultFileBoundary.PinDirectory(current.FullName); pin.Validate();
    }
    internal void Initialize(FreshnessEvidence evidence)
    {
        using var gate = Gate();
        if (File.Exists(Path.Combine(_root, "state.dpapi"))) throw new IOException("ANCHOR-ALREADY-ENROLLED");
        // Enrollment is explicit. Missing anchors are never initialized during recovery.
        Write(new State(_instanceId, evidence, []));
    }
    private VaultFileBoundary.FileLease Gate()
    {
        using var pin = VaultFileBoundary.PinDirectory(_root, true);
        var name = Path.Combine(_root, "state.lock");
        if (!File.Exists(name)) { try { using var created = VaultFileBoundary.OpenWrite(_root, "state.lock", true); } catch (IOException) { } }
        return VaultFileBoundary.OpenExclusive(_root, "state.lock", false);
    }
    private State Read()
    {
        using var file = VaultFileBoundary.OpenRead(_root, "state.dpapi");
        if (file.Stream.Length is <= 0 or > 1048576) throw new InvalidDataException("ANCHOR-SIZE");
        var ciphertext = new byte[(int)file.Stream.Length]; file.Stream.ReadExactly(ciphertext); file.Validate();
        var plain = ProtectedData.Unprotect(ciphertext, null, DataProtectionScope.CurrentUser);
        try
        {
            var state = JsonSerializer.Deserialize<State>(plain) ?? throw new InvalidDataException("ANCHOR-JSON");
            if (state.InstanceId != _instanceId) throw new InvalidDataException("ANCHOR-RESET");
            if (state.Evidence is null || state.Receipts is null || string.IsNullOrWhiteSpace(state.Evidence.VaultId) ||
                state.Evidence.Generation < 1 || state.Evidence.GenerationBinding?.Length != 64 || state.Evidence.RecoveryBinding?.Length != 64 ||
                state.Receipts.Count > 1000 || state.Receipts.Select(r => r?.RecoveryOperationId).Distinct().Count() != state.Receipts.Count ||
                state.Receipts.Any(r => r is null || r.VaultId != state.Evidence.VaultId || r.RecoveryOperationId == Guid.Empty || r.SessionConnection == Guid.Empty ||
                    string.IsNullOrEmpty(r.ProtectedCodeIdentifier) || !Enum.IsDefined(r.State)))
                throw new InvalidDataException("ANCHOR-RECEIPTS");
            return state;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    private void Write(State state)
    {
        using var parent = VaultFileBoundary.PinDirectory(_root);
        var plain = JsonSerializer.SerializeToUtf8Bytes(state);
        byte[] protectedBytes;
        try { protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        var temporary = "pending-" + Guid.NewGuid().ToString("N");
        using (var file = VaultFileBoundary.OpenWrite(_root, temporary, true))
        {
            file.Stream.Write(protectedBytes); file.Stream.Flush(true); file.Verify(protectedBytes.Length, Convert.ToHexString(SHA256.HashData(protectedBytes)));
        }
        FaultObserver?.Invoke("AnchorBeforeReplace");
        var target = Path.Combine(_root, "state.dpapi");
        if (File.Exists(target)) { using (var old = VaultFileBoundary.OpenRead(_root, "state.dpapi")) old.Validate(); File.Replace(Path.Combine(_root, temporary), target, null); }
        else File.Move(Path.Combine(_root, temporary), target);
        FaultObserver?.Invoke("AnchorAfterReplace"); parent.Validate();
        if (JsonSerializer.Serialize(Read()) != JsonSerializer.Serialize(state)) throw new InvalidDataException("ANCHOR-READBACK");
    }
    private static AnchorVerificationResult Compare(State state, FreshnessEvidence evidence) =>
        state.Evidence == evidence ? new(RecoveryPosture.FreshnessVerified, "Authenticated binding matches the enrolled anchor") :
        new(RecoveryPosture.AuthenticButStale, "Binding mismatch; stale or unrecorded state requires review");
    public AnchorVerificationResult Verify(FreshnessEvidence evidence)
    {
        try { using var gate = Gate(); if (!File.Exists(Path.Combine(_root, "state.dpapi"))) return new(RecoveryPosture.AnchorMissing, "Enrolled anchor is missing"); return Compare(Read(), evidence); }
        catch (FileNotFoundException) { return new(RecoveryPosture.AnchorMissing, "Enrolled anchor is missing"); }
        catch (InvalidDataException ex) { return new(ex.Message == "ANCHOR-RESET" ? RecoveryPosture.AnchorReset : RecoveryPosture.AnchorCorrupted, "Invalid anchor"); }
        catch (CryptographicException) { return new(RecoveryPosture.AnchorCorrupted, "DPAPI authentication failed"); }
        catch (JsonException) { return new(RecoveryPosture.AnchorCorrupted, "Invalid anchor JSON"); }
        catch (UnauthorizedAccessException) { return new(RecoveryPosture.AnchorUnavailable, "Anchor access denied"); }
        catch (IOException) { return new(RecoveryPosture.AnchorUnavailable, "Anchor cannot be read or locked"); }
    }
    internal RecoveryReceipt Reserve(FreshnessEvidence evidence, Guid operationId, string codeId, Guid session)
    {
        using var gate = Gate(); var state = Read();
        if (operationId == Guid.Empty || session == Guid.Empty || codeId?.Length != 64 || state.Receipts.Count >= 1000) throw new InvalidDataException("RECEIPT-BUDGET-OR-ID");
        if (state.Receipts.Any(r => r.RecoveryOperationId == operationId || r.ProtectedCodeIdentifier == codeId))
            throw new InvalidDataException("RECOVERY-ALREADY-RESERVED-QUERY-RECEIPT");
        if (Compare(state, evidence).Posture != RecoveryPosture.FreshnessVerified) throw new InvalidDataException("RECOVERY-FRESHNESS-REVIEW");
        var receipt = new RecoveryReceipt { VaultId = evidence.VaultId, RecoveryOperationId = operationId,
            ProtectedCodeIdentifier = codeId, AuthenticatedGeneration = evidence.Generation, ReservedAt = DateTimeOffset.UtcNow,
            SessionConnection = session, State = RecoveryReceiptState.Reserved, AnchorResult = RecoveryPosture.FreshnessVerified };
        state.Receipts.Add(receipt); Write(state); return receipt;
    }
    internal RecoveryReceipt Commit(AnchorCommitRequest request, Guid operationId)
    {
        using var gate = Gate(); var state = Read();
        if (Compare(state, request.Previous).Posture != RecoveryPosture.FreshnessVerified ||
            request.Next.VaultId != request.Previous.VaultId || request.Next.Generation != request.Previous.Generation ||
            request.Next.GenerationBinding != request.Previous.GenerationBinding ||
            request.Next.RecoveryBinding == request.Previous.RecoveryBinding) throw new InvalidDataException("ANCHOR-CAS");
        var index = state.Receipts.FindIndex(r => r.RecoveryOperationId == operationId && r.State == RecoveryReceiptState.Reserved);
        if (index < 0) throw new InvalidDataException("RECEIPT-TRANSITION");
        var receipt = state.Receipts[index] with { State = RecoveryReceiptState.ReceiptPending, CommittedAt = DateTimeOffset.UtcNow };
        state.Receipts[index] = receipt; Write(state with { Evidence = request.Next }); return receipt;
    }
    public RecoveryReceipt? Query(Guid operationId)
    {
        using var gate = Gate(); var receipt = Read().Receipts.SingleOrDefault(r => r.RecoveryOperationId == operationId);
        return receipt?.State == RecoveryReceiptState.Reserved ? receipt with { State = RecoveryReceiptState.Interrupted } : receipt;
    }
    internal RecoveryReceipt Acknowledge(Guid operationId)
    {
        using var gate = Gate(); var state = Read(); var index = state.Receipts.FindIndex(r => r.RecoveryOperationId == operationId);
        if (index < 0) throw new InvalidDataException("RECEIPT-NOT-FOUND");
        var receipt = state.Receipts[index];
        if (receipt.State == RecoveryReceiptState.ReceiptAcknowledged) return receipt;
        if (receipt.State != RecoveryReceiptState.ReceiptPending) throw new InvalidDataException("RECEIPT-REVIEW-REQUIRED");
        receipt = receipt with { State = RecoveryReceiptState.ReceiptAcknowledged, AcknowledgedAt = DateTimeOffset.UtcNow };
        state.Receipts[index] = receipt; Write(state); return receipt;
    }
}
