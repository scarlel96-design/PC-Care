using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>Creates locally verifiable evidence for a non-destructive Purge Dry Run.</summary>
public sealed class StoragePurgeEvidenceService
{
    private static readonly byte[] Entropy = "PC-Care-PurgeEvidence-v1"u8.ToArray();
    private readonly string _root;

    public StoragePurgeEvidenceService(string? root = null) => _root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartPerformanceDoctor", "purge", "evidence");

    public PurgeEvidenceReceipt Write(StorageSanitizeDryRunResult plan)
    {
        Directory.CreateDirectory(_root);
        var evidenceId = plan.OperationId + "-" + Guid.NewGuid().ToString("N")[..8];
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var payload = new PurgeEvidencePayload
            {
                EvidenceId = evidenceId,
                OperationId = plan.OperationId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                DeviceFingerprint = DeviceFingerprint.Create(plan.Device),
                IsSystemDisk = plan.IsSystemDisk,
                RequiresWinPe = plan.RequiresWinPe,
                IsWinPe = plan.IsWinPe,
                ExecutionEnabled = plan.ExecutionEnabled,
                CapabilityStates = plan.Capabilities.Select(item => item.Code + "=" + item.Supported).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                BlockingReasonCount = plan.BlockingReasons.Count
            };
            var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            var signature = HMACSHA256.HashData(key, payloadBytes);
            var envelope = new PurgeEvidenceEnvelope
            {
                PayloadBase64 = Convert.ToBase64String(payloadBytes),
                SignatureHex = Convert.ToHexString(signature).ToLowerInvariant()
            };
            var evidencePath = Path.Combine(_root, evidenceId + ".pcevidence");
            var keyPath = Path.Combine(_root, evidenceId + ".key");
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.WriteAllBytes(keyPath, ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser));
            return new PurgeEvidenceReceipt { EvidencePath = evidencePath, IsValid = Verify(evidencePath), ExecutionEnabled = false };
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public bool Verify(string evidencePath)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<PurgeEvidenceEnvelope>(File.ReadAllText(evidencePath));
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.PayloadBase64) || string.IsNullOrWhiteSpace(envelope.SignatureHex)) return false;
            var payload = Convert.FromBase64String(envelope.PayloadBase64);
            var identity = JsonSerializer.Deserialize<PurgeEvidencePayload>(payload);
            if (identity is null || string.IsNullOrWhiteSpace(identity.EvidenceId)) return false;
            var keyPath = Path.Combine(_root, identity.EvidenceId + ".key");
            if (!File.Exists(keyPath)) return false;
            var key = ProtectedData.Unprotect(File.ReadAllBytes(keyPath), Entropy, DataProtectionScope.CurrentUser);
            try
            {
                var expected = HMACSHA256.HashData(key, payload);
                var actual = Convert.FromHexString(envelope.SignatureHex);
                return CryptographicOperations.FixedTimeEquals(expected, actual) && !identity.ExecutionEnabled;
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch { return false; }
    }

    private sealed class PurgeEvidenceEnvelope
    {
        public string PayloadBase64 { get; init; } = "";
        public string SignatureHex { get; init; } = "";
    }

    private sealed class PurgeEvidencePayload
    {
        public string EvidenceId { get; init; } = "";
        public string OperationId { get; init; } = "";
        public DateTimeOffset CreatedAtUtc { get; init; }
        public string DeviceFingerprint { get; init; } = "";
        public bool IsSystemDisk { get; init; }
        public bool RequiresWinPe { get; init; }
        public bool IsWinPe { get; init; }
        public bool ExecutionEnabled { get; init; }
        public string[] CapabilityStates { get; init; } = Array.Empty<string>();
        public int BlockingReasonCount { get; init; }
    }
}

public sealed class PurgeEvidenceReceipt
{
    public string EvidencePath { get; init; } = "";
    public bool IsValid { get; init; }
    public bool ExecutionEnabled { get; init; }
}