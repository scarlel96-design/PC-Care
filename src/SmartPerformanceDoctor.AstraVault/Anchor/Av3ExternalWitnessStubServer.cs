using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>In-process external witness stub (harness temp only; never calls network).</summary>
public sealed class Av3ExternalWitnessStubServer
{
    private readonly Dictionary<Guid, StubRecord> _records = new();

    public Av3ExternalWitnessStubContract.WitnessResponse Query(
        string vaultRoot,
        Av3ExternalWitnessStubContract.WitnessRequest request)
    {
        Av3TrustedAnchorHarnessScope.EnsureE13Root(vaultRoot);
        if (!TryLoadHarnessOverride(vaultRoot, out var overrideResponse))
        {
            if (!_records.TryGetValue(request.VaultId, out var record))
            {
                record = new StubRecord { Counter = request.ObservedGeneration };
                _records[request.VaultId] = record;
            }

            var digest = string.IsNullOrWhiteSpace(request.CurrentWitnessDigestHex)
                ? Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(record.Counter)))
                : request.CurrentWitnessDigestHex;

            return new Av3ExternalWitnessStubContract.WitnessResponse
            {
                MonotonicCounter = record.Counter,
                WitnessDigestHex = digest,
                SignatureHex = Sign(digest, record.Counter),
                ServerAvailable = record.ServerAvailable,
                ReplayDetected = record.ReplayDetected,
                SignatureValid = record.SignatureValid
            };
        }

        return overrideResponse;
    }

    public void Seed(Guid vaultId, ulong counter, string digestHex, bool serverAvailable = true, bool replay = false, bool signatureValid = true)
    {
        _records[vaultId] = new StubRecord
        {
            Counter = counter,
            DigestHex = digestHex,
            ServerAvailable = serverAvailable,
            ReplayDetected = replay,
            SignatureValid = signatureValid
        };
    }

    public void WriteHarnessOverride(string vaultRoot, Av3ExternalWitnessStubContract.WitnessResponse response)
    {
        var dir = Av3TrustedAnchorHarnessScope.ResolveTrustedStoreDirectory(vaultRoot);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Av3TrustedAnchorRuntimePolicy.ExternalWitnessStubFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(response));
    }

    private static bool TryLoadHarnessOverride(string vaultRoot, out Av3ExternalWitnessStubContract.WitnessResponse response)
    {
        response = new Av3ExternalWitnessStubContract.WitnessResponse();
        var path = Path.Combine(
            Av3TrustedAnchorHarnessScope.ResolveTrustedStoreDirectory(vaultRoot),
            Av3TrustedAnchorRuntimePolicy.ExternalWitnessStubFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var doc = JsonSerializer.Deserialize<Av3ExternalWitnessStubContract.WitnessResponse>(File.ReadAllText(path));
            if (doc is null)
            {
                return false;
            }

            response = doc;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Sign(string digestHex, ulong counter) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{digestHex}:{counter}")));

    private sealed class StubRecord
    {
        public ulong Counter { get; set; }

        public string DigestHex { get; set; } = string.Empty;

        public bool ServerAvailable { get; set; } = true;

        public bool ReplayDetected { get; set; }

        public bool SignatureValid { get; set; } = true;
    }
}