using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>
/// Creates a locally tamper-evident, non-executable snapshot of every Purge gate input.
/// It is an audit artifact only: writing or verifying it never sends a disk command.
/// </summary>
public sealed class StoragePurgeAttestationBundleService
{
    private static readonly byte[] Entropy = "PC-Care-PurgeAttestation-v1"u8.ToArray();
    private readonly string _root;

    public StoragePurgeAttestationBundleService(string? root = null) => _root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartPerformanceDoctor", "purge", "attestations");

    public PurgeAttestationReceipt Write(
        StorageSanitizeDryRunResult plan,
        PurgeAuthorizationDecision authorization,
        PurgeReadOnlyInspection? inspection,
        NvmeIdentifyReadOnlyEvidence? identify,
        NvmeCommandEffectsReadOnlyEvidence? effects,
        NvmeSanitizeStatusReadOnlyEvidence? status,
        PurgePreExecutionAssessment assessment)
    {
        Directory.CreateDirectory(_root);
        var id = "purge-attestation-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var payload = new PurgeAttestationPayload
            {
                Id = id,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                OperationId = plan.OperationId,
                DeviceFingerprint = DeviceFingerprint.Create(plan.Device),
                AuthorizationFingerprint = authorization.DeviceFingerprint,
                IsAuthorizedForFutureExecutor = authorization.AuthorizedForFutureExecutor,
                IsWinPe = plan.IsWinPe,
                ExecutionEnabled = false,
                DryRunBlocks = plan.BlockingReasons.ToArray(),
                ReadOnlyInspectionState = inspection?.State.ToString() ?? "Missing",
                ReadOnlyInspectionAtUtc = inspection?.InspectedAtUtc,
                IdentifyReadSucceeded = identify?.ReadSucceeded ?? false,
                IdentifySha256 = identify?.IdentifySha256 ?? "",
                EffectsReadSucceeded = effects?.ReadSucceeded ?? false,
                EffectsLogSha256 = effects?.EffectsLogSha256 ?? "",
                SanitizeCommandSupported = effects?.SanitizeCommandSupported ?? false,
                StatusReadSucceeded = status?.ReadSucceeded ?? false,
                SanitizeState = status?.State.ToString() ?? "Missing",
                StatusLogSha256 = status?.StatusLogSha256 ?? "",
                FutureExecutorEligible = assessment.IsEligibleForFutureExecutor,
                GateBlocks = assessment.BlockingReasons.ToArray(),
                CrossValidatedCryptoErase = assessment.CrossCheck.CryptoEraseCrossValidated,
                CrossValidatedBlockErase = assessment.CrossCheck.BlockEraseCrossValidated,
                CrossValidatedOverwrite = assessment.CrossCheck.OverwriteCrossValidated
            };
            var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            var mac = HMACSHA256.HashData(key, payloadBytes);
            var envelope = new PurgeAttestationEnvelope { PayloadBase64 = Convert.ToBase64String(payloadBytes), MacHex = Convert.ToHexString(mac).ToLowerInvariant() };
            var path = Path.Combine(_root, id + ".pcattest");
            File.WriteAllText(path, JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(_root, id + ".key"), ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser));
            return new PurgeAttestationReceipt { Path = path, IsValid = Verify(path), ExecutionEnabled = false };
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public bool Verify(string path)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<PurgeAttestationEnvelope>(File.ReadAllText(path));
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.PayloadBase64) || string.IsNullOrWhiteSpace(envelope.MacHex)) return false;
            var payload = Convert.FromBase64String(envelope.PayloadBase64);
            var identity = JsonSerializer.Deserialize<PurgeAttestationPayload>(payload);
            if (identity is null || string.IsNullOrWhiteSpace(identity.Id) || identity.ExecutionEnabled) return false;
            var keyPath = Path.Combine(_root, identity.Id + ".key");
            if (!File.Exists(keyPath)) return false;
            var key = ProtectedData.Unprotect(File.ReadAllBytes(keyPath), Entropy, DataProtectionScope.CurrentUser);
            try { return CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, payload), Convert.FromHexString(envelope.MacHex)); }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch { return false; }
    }

    private sealed class PurgeAttestationEnvelope { public string PayloadBase64 { get; init; } = ""; public string MacHex { get; init; } = ""; }
    private sealed class PurgeAttestationPayload
    {
        public string Id { get; init; } = ""; public DateTimeOffset CreatedAtUtc { get; init; }
        public string OperationId { get; init; } = ""; public string DeviceFingerprint { get; init; } = ""; public string AuthorizationFingerprint { get; init; } = "";
        public bool IsAuthorizedForFutureExecutor { get; init; } public bool IsWinPe { get; init; } public bool ExecutionEnabled { get; init; }
        public string[] DryRunBlocks { get; init; } = Array.Empty<string>(); public string ReadOnlyInspectionState { get; init; } = ""; public DateTimeOffset? ReadOnlyInspectionAtUtc { get; init; }
        public bool IdentifyReadSucceeded { get; init; } public string IdentifySha256 { get; init; } = ""; public bool EffectsReadSucceeded { get; init; } public string EffectsLogSha256 { get; init; } = ""; public bool SanitizeCommandSupported { get; init; }
        public bool StatusReadSucceeded { get; init; } public string SanitizeState { get; init; } = ""; public string StatusLogSha256 { get; init; } = "";
        public bool FutureExecutorEligible { get; init; } public string[] GateBlocks { get; init; } = Array.Empty<string>();
        public bool CrossValidatedCryptoErase { get; init; } public bool CrossValidatedBlockErase { get; init; } public bool CrossValidatedOverwrite { get; init; }
    }
}

public sealed class PurgeAttestationReceipt
{
    public string Path { get; init; } = "";
    public bool IsValid { get; init; }
    public bool ExecutionEnabled { get; init; }
}