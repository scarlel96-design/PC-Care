using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>
/// Writes a local tamper-evident execution record before dispatch and after a controller-status result.
/// It is evidence for operator review only; it is not a remote witness or a proof of unrecoverability.
/// </summary>
public interface IStoragePurgeExecutionEvidence
{
    PurgeExecutionEvidenceSession Begin(WinPePurgeExecutionRequest request);
    PurgeExecutionEvidenceReceipt Complete(PurgeExecutionEvidenceSession session, WinPePurgeExecutionResult result);
}

public sealed class StoragePurgeExecutionEvidenceService : IStoragePurgeExecutionEvidence
{
    private static readonly byte[] Entropy = "PC-Care-PurgeExecutionEvidence-v1"u8.ToArray();
    private readonly string _root;

    public StoragePurgeExecutionEvidenceService(string? root = null) =>
        _root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartPerformanceDoctor", "purge", "execution-evidence");

    public PurgeExecutionEvidenceSession Begin(WinPePurgeExecutionRequest request)
    {
        Directory.CreateDirectory(_root);
        var id = "purge-execution-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
        var path = Path.Combine(_root, id + ".pcpurge");
        var keyPath = Path.Combine(_root, id + ".key");
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var payload = new PurgeExecutionEvidencePayload
            {
                Id = id,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                DeviceFingerprint = DeviceFingerprint.Create(request.Plan.Device),
                DiskNumber = request.Plan.Device.DiskNumber,
                BusType = request.Plan.Device.BusType,
                Method = request.Method.ToString(),
                Stage = "pre-dispatch",
                ConfirmationBound = string.Equals(request.ConfirmationPhrase?.Trim(), WinPeStoragePurgeExecutor.RequiredConfirmation(request.Method, request.Plan.Device.DiskNumber), StringComparison.Ordinal),
                IsWinPePlan = request.Plan.IsWinPe
            };
            Write(path, key, payload);
            File.WriteAllBytes(keyPath, ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser));
            return new PurgeExecutionEvidenceSession(id, path);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public PurgeExecutionEvidenceReceipt Complete(PurgeExecutionEvidenceSession session, WinPePurgeExecutionResult result)
    {
        var key = ReadKey(session.Id);
        try
        {
            var payload = ReadPayload(session, key);
            var final = payload with
            {
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Stage = result.DispatchState switch
                {
                    PurgeDispatchState.SubmissionUncertain => "dispatch-submission-uncertain",
                    PurgeDispatchState.NotAttempted => "dispatch-not-attempted",
                    _ when result.Completed => "controller-completed",
                    _ => "controller-incomplete"
                },
                CommandIssued = result.CommandIssued,
                DispatchState = result.DispatchState.ToString(),
                ControllerCompleted = result.Completed,
                ControllerState = result.FinalStatus?.State.ToString() ?? "Unavailable",
                ControllerStatusHash = result.FinalStatus?.StatusLogSha256 ?? "",
                ResultMessage = result.Message
            };
            Write(session.Path, key, final);
            return new PurgeExecutionEvidenceReceipt(session.Path, Verify(session.Path));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public bool Verify(string path)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<PurgeExecutionEvidenceEnvelope>(File.ReadAllText(path));
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.PayloadBase64) || string.IsNullOrWhiteSpace(envelope.MacHex)) return false;
            var payloadBytes = Convert.FromBase64String(envelope.PayloadBase64);
            var payload = JsonSerializer.Deserialize<PurgeExecutionEvidencePayload>(payloadBytes);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Id) || string.IsNullOrWhiteSpace(payload.DeviceFingerprint)) return false;
            var key = ReadKey(payload.Id);
            try { return CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, payloadBytes), Convert.FromHexString(envelope.MacHex)); }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch { return false; }
    }

    private void Write(string path, byte[] key, PurgeExecutionEvidencePayload payload)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var envelope = new PurgeExecutionEvidenceEnvelope
        {
            PayloadBase64 = Convert.ToBase64String(bytes),
            MacHex = Convert.ToHexString(HMACSHA256.HashData(key, bytes)).ToLowerInvariant()
        };
        File.WriteAllText(path, JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    }

    private PurgeExecutionEvidencePayload ReadPayload(PurgeExecutionEvidenceSession session, byte[] key)
    {
        PurgeExecutionEvidenceEnvelope envelope;
        byte[] payloadBytes;
        try
        {
            envelope = JsonSerializer.Deserialize<PurgeExecutionEvidenceEnvelope>(File.ReadAllText(session.Path))
                ?? throw new IOException("실행 증적을 읽지 못했습니다.");
            payloadBytes = Convert.FromBase64String(envelope.PayloadBase64);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw new IOException("명령 전 실행 증적 형식이 손상되었습니다.", exception);
        }
        if (!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, payloadBytes), Convert.FromHexString(envelope.MacHex)))
        {
            throw new IOException("명령 전 실행 증적의 무결성 검증에 실패했습니다.");
        }
        var payload = JsonSerializer.Deserialize<PurgeExecutionEvidencePayload>(payloadBytes)
            ?? throw new IOException("실행 증적 형식이 올바르지 않습니다.");
        if (!string.Equals(payload.Id, session.Id, StringComparison.Ordinal)) throw new IOException("실행 증적 식별자가 일치하지 않습니다.");
        return payload;
    }

    private byte[] ReadKey(string id) => ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(_root, id + ".key")), Entropy, DataProtectionScope.CurrentUser);

    private sealed class PurgeExecutionEvidenceEnvelope { public string PayloadBase64 { get; init; } = ""; public string MacHex { get; init; } = ""; }
    private sealed record PurgeExecutionEvidencePayload
    {
        public string Id { get; init; } = ""; public DateTimeOffset CreatedAtUtc { get; init; } public DateTimeOffset? CompletedAtUtc { get; init; }
        public string DeviceFingerprint { get; init; } = ""; public int? DiskNumber { get; init; } public string BusType { get; init; } = ""; public string Method { get; init; } = "";
        public string Stage { get; init; } = ""; public bool ConfirmationBound { get; init; } public bool IsWinPePlan { get; init; }
        public bool CommandIssued { get; init; } public string DispatchState { get; init; } = "NotAttempted"; public bool ControllerCompleted { get; init; } public string ControllerState { get; init; } = ""; public string ControllerStatusHash { get; init; } = ""; public string ResultMessage { get; init; } = "";
    }
}

public sealed record PurgeExecutionEvidenceSession(string Id, string Path);
public sealed record PurgeExecutionEvidenceReceipt(string Path, bool IsValid);
