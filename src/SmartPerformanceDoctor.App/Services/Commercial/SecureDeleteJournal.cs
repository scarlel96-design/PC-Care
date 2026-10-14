using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed record DeleteHandleEvidence(string Identity, long Length, string ContentHash, string Owner);
public sealed record DeleteJournalEntry(int Schema, long Sequence, string OperationId, string StoreIdentity,
    int TargetIndex, string State, string PreviousMac, string ProtectedPath, string FileIdentity,
    long Length, string ContentFingerprint, string Protocol, string Media, string ErrorCode)
{
    public string OwnerFingerprint { get; init; } = "";
    public string VolumeId => FileIdentity.Split(':')[0];
    public string FileId => FileIdentity.Contains(':') ? FileIdentity.Split(':')[1] : "";
}
public sealed record DeleteReconciliationItem(int TargetIndex, string State, string Observation, bool MutationPossible);
public sealed record DeleteReconciliationResult(string OperationId, string Status, bool JournalAuthenticated,
    bool AuditVerified, bool RequiresManualReview, IReadOnlyList<DeleteReconciliationItem> Items);

/// <summary>Authenticate ordered checkpoints. Reconciliation grants no mutation authority.</summary>
public sealed class SecureDeleteJournal : IDisposable
{
    private sealed record Envelope(string Data, string Mac);
    private readonly WindowsFileBoundary.FileLease _file;
    private readonly byte[] _key;
    private readonly string _operation;
    private readonly string _storeIdentity;
    private readonly Dictionary<int, string> _states = [];
    private bool _writeFailed;
    private long _sequence;
    private string _previous = "GENESIS";
    public string Path { get; }
    public Action<string>? Checkpoint { get; set; }

    public SecureDeleteJournal(string root, string operation, byte[] key, Action<string>? checkpoint = null)
    {
        ValidateOperation(operation);
        Checkpoint = checkpoint;
        _operation = operation; _key = key; root = System.IO.Path.GetFullPath(root);
        using var pin = WindowsFileBoundary.PinDirectory(root);
        _storeIdentity = pin.Identity;
        Path = System.IO.Path.Combine(root, operation + ".journal");
        _file = WindowsFileBoundary.OpenExclusive(root, operation + ".journal", true);
        Append(-1, "Planned");
    }

    public static void ValidateOperation(string operation)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(operation, @"^secure-delete-\d{8}-\d{6}-[0-9a-f]{32}$"))
            throw new InvalidDataException("InvalidOperationId");
    }

    public static bool Allowed(string? previous, string next, int target)
    {
        if (target is < -1 or > 511) return false;
        if (previous is null) return next == "Planned";
        if (target == -1) return (previous, next) is ("Planned", "AuditCommitted") or ("AuditCommitted", "Completed");
        if (next == "RecoveryReviewRequired") return previous is not ("Completed" or "CancelledBeforeMutation" or "RecoveryReviewRequired");
        if (next == "CancelledBeforeMutation") return previous is "Planned" or "PreflightVerified" or "TargetHandleBound";
        return (previous, next) is ("Planned", "PreflightVerified") or ("PreflightVerified", "TargetHandleBound")
            or ("TargetHandleBound", "DestructiveWriteIntent") or ("DestructiveWriteIntent", "WriteInProgress")
            or ("WriteInProgress", "WriteFinished") or ("WriteFinished", "VerificationFinished")
            or ("VerificationFinished", "DeleteRequested") or ("DeleteRequested", "DeleteDispositionAccepted")
            or ("DeleteDispositionAccepted", "DeletionObserved") or ("DeletionObserved", "AuditCommitted")
            or ("AuditCommitted", "Completed");
    }

    public void Append(int target, string state, string path = "", DeleteHandleEvidence? evidence = null,
        string protocol = "", string media = "", string error = "")
    {
        if (_writeFailed) throw new IOException("JournalWritePreviouslyFailed");
        try { AppendCore(target, state, path, evidence, protocol, media, error); }
        catch { _writeFailed = true; throw; }
    }

    private void AppendCore(int target, string state, string path, DeleteHandleEvidence? evidence,
        string protocol, string media, string error)
    {
        _states.TryGetValue(target, out var prior);
        if (!Allowed(prior, state, target)) throw new InvalidDataException("InvalidDeleteStateTransition");
        var token = path.Length == 0 ? "" : Convert.ToHexString(ProtectedData.Protect(Encoding.UTF8.GetBytes(path),
            Encoding.UTF8.GetBytes(_operation), DataProtectionScope.CurrentUser));
        var content = evidence is null ? "" : SecureDeleteAuditChain.ProtectContentFingerprint(evidence.ContentHash, _key);
        var entry = new DeleteJournalEntry(1, _sequence + 1, _operation, _storeIdentity, target, state, _previous,
            token, evidence?.Identity ?? "", evidence?.Length ?? 0, content, protocol, media, error)
        { OwnerFingerprint = evidence is null ? "" : SecureDeleteAuditChain.ProtectContentFingerprint(evidence.Owner, _key) };
        var data = JsonSerializer.Serialize(entry);
        var mac = Sign(data, _key);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Envelope(data, mac)) + "\n");
        if (bytes.Length > 65536 || _sequence >= 9999) throw new InvalidDataException("JournalRecordBudget");
        Checkpoint?.Invoke("JournalBeforeWrite:" + state);
        _file.Validate(); _file.Stream.Position = _file.Stream.Length;
        _file.Stream.Write(bytes); Checkpoint?.Invoke("JournalBeforeFlush:" + state);
        _file.Stream.Flush(true); _file.Validate();
        _states[target] = state; _previous = mac; _sequence++;
        Checkpoint?.Invoke("JournalAfterFlush:" + state);
    }

    private static string Sign(string data, byte[] key) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data)));

    internal static IReadOnlyList<DeleteJournalEntry> Read(string root, string operation, byte[] key)
    {
        ValidateOperation(operation);
        using var pin = WindowsFileBoundary.PinDirectory(root);
        using var file = WindowsFileBoundary.OpenRead(root, operation + ".journal");
        if (file.Stream.Length is <= 0 or > 16 * 1024 * 1024) throw new InvalidDataException("JournalSize");
        using var reader = new StreamReader(file.Stream, new UTF8Encoding(false, true), leaveOpen: true);
        var text = reader.ReadToEnd(); file.Validate();
        if (!text.EndsWith('\n')) throw new InvalidDataException("JournalTruncated");
        var records = new List<DeleteJournalEntry>(); var states = new Dictionary<int, string>();
        var previous = "GENESIS";
        foreach (var line in text.Split('\n')[..^1])
        {
            if (line.Length is 0 or > 65536 || records.Count >= 10000) throw new InvalidDataException("JournalRecordSize");
            var envelope = JsonSerializer.Deserialize<Envelope>(line) ?? throw new InvalidDataException("JournalEnvelope");
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(envelope.Mac), Convert.FromHexString(Sign(envelope.Data, key))))
                throw new InvalidDataException("JournalAuthentication");
            var entry = JsonSerializer.Deserialize<DeleteJournalEntry>(envelope.Data) ?? throw new InvalidDataException("JournalRecord");
            states.TryGetValue(entry.TargetIndex, out var prior);
            if (entry.Schema != 1 || entry.OperationId != operation || entry.StoreIdentity != pin.Identity
                || entry.Sequence != records.Count + 1 || entry.PreviousMac != previous
                || !Allowed(prior, entry.State, entry.TargetIndex)) throw new InvalidDataException("JournalSequenceOrState");
            states[entry.TargetIndex] = entry.State; previous = envelope.Mac; records.Add(entry);
        }
        return records;
    }

    public static DeleteReconciliationResult Reconcile(string root, string operation, Action<string>? checkpoint = null)
    {
        ValidateOperation(operation);
        var results = new List<DeleteReconciliationItem>(); byte[]? key = null; var authenticated = false;
        try
        {
            if (!File.Exists(System.IO.Path.Combine(root, operation + ".journal")))
                return new(operation, File.Exists(System.IO.Path.Combine(root, operation + ".log.started")) ? "RecoveryReviewRequired" : "NotStarted", false, false, File.Exists(System.IO.Path.Combine(root, operation + ".log.started")), []);
            key = SecureDeleteAuditChain.LoadOperationKey(operation, root);
            var records = Read(root, operation, key); authenticated = true; checkpoint?.Invoke("ReconciliationRead");
            foreach (var group in records.Where(e => e.TargetIndex >= 0).GroupBy(e => e.TargetIndex))
            {
                var last = group.Last(); var bound = group.LastOrDefault(e => e.FileIdentity.Length > 0);
                var mutation = group.Any(e => e.State == "DestructiveWriteIntent");
                var observation = mutation ? "MutationPossible" : "NotStarted";
                if (bound is not null)
                {
                    var path = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromHexString(bound.ProtectedPath),
                        Encoding.UTF8.GetBytes(operation), DataProtectionScope.CurrentUser));
                    if (!File.Exists(path))
                        observation = last.State == "Completed" && group.Any(e => e.State == "DeleteDispositionAccepted")
                            ? "DeletionConfirmedByAvailableEvidence" : "TargetAbsentUnconfirmed";
                    else
                    {
                        using var file = WindowsFileBoundary.OpenRead(System.IO.Path.GetDirectoryName(path)!, System.IO.Path.GetFileName(path));
                        if (file.Identity != bound.FileIdentity) observation = "TargetIdentityChanged";
                        else if (file.Stream.Length > 64 * 1024 * 1024) observation = "MutationPossible";
                        else if (file.Stream.Length == bound.Length && SecureDeleteAuditChain.ProtectContentFingerprint(file.Hash(), key) == bound.ContentFingerprint)
                            observation = mutation ? "FileStillPresent" : "NoMutationVerified";
                        else observation = mutation ? group.Any(e => e.State == "WriteFinished") ? "FileStillPresent" : "PartialWriteObserved" : "TargetIdentityChanged";
                    }
                }
                results.Add(new(group.Key, last.State, observation, mutation));
                checkpoint?.Invoke("ReconciliationTarget");
            }
            var audit = SecureDeleteAuditChain.Verify(System.IO.Path.Combine(root, operation + ".log"), key);
            var completed = records.Last().TargetIndex == -1 && records.Last().State == "Completed";
            var resultPath = System.IO.Path.Combine(root, operation + ".log.result.json");
            byte[]? resultBytes = null;
            if (completed && File.Exists(resultPath))
            {
                using var resultFile = WindowsFileBoundary.OpenRead(root, operation + ".log.result.json");
                if (resultFile.Stream.Length is <= 0 or > 4 * 1024 * 1024) throw new InvalidDataException("ResultSize");
                using var resultBuffer = new MemoryStream();
                resultFile.Stream.CopyTo(resultBuffer); resultFile.Validate(); resultBytes = resultBuffer.ToArray();
            }
            var sealedResult = resultBytes is not null && records.Last().ErrorCode == "ResultSha:" +
                Convert.ToHexString(SHA256.HashData(resultBytes));
            var review = !sealedResult || !audit || results.Any(r => r.Observation != "DeletionConfirmedByAvailableEvidence"
                && r.State != "CancelledBeforeMutation");
            var cancelled = sealedResult && JsonSerializer.Deserialize<SecureDeleteExecutionResult>(resultBytes!)?.WasCancellationRequested == true;
            return new(operation, review ? "RecoveryReviewRequired" : results.All(r => r.State == "CancelledBeforeMutation")
                ? "NoMutationVerified" : cancelled ? "CancellationOutcomeVerified" : "DeletionConfirmedByAvailableEvidence", true, audit, review, results);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException
            or JsonException or FormatException or InvalidDataException or ArgumentException)
        { return new(operation, "RecoveryReviewRequired", authenticated, false, true, results); }
        finally { if (key is not null) CryptographicOperations.ZeroMemory(key); }
    }

    public void Dispose() => _file.Dispose();
}
