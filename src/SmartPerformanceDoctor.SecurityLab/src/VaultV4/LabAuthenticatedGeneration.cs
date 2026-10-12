using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>Explicit Lab v6 commit protocol. Preserve immutable generations and authenticate activation.</summary>
internal static class LabAuthenticatedGeneration
{
    internal sealed record Activation(string Id, string VaultId, long Generation, string HeaderHash, string MetadataHash, string Mac);
    internal sealed record Generation(Activation Activation, byte[] Header, byte[] Metadata, bool Previous);
    internal static string DirectoryPath(string root) => Path.Combine(root, "generations-v6");

    internal static Activation Prepare(string root, string vaultId, long generation, byte[] header, byte[] metadata,
        byte[] vmk, Action<string>? fault)
    {
        var directory = DirectoryPath(root);
        VaultExportWriter.ValidateAncestors(directory);
        using var parent = VaultFileBoundary.PinDirectory(directory, create: true);
        var id = Guid.NewGuid().ToString("N");
        WriteNew(directory, id + ".metadata", metadata);
        fault?.Invoke("GenerationMetadataWritten");
        WriteNew(directory, id + ".header", header);
        fault?.Invoke("GenerationHeaderWritten");
        var activation = new Activation(id, vaultId, generation, Hash(header), Hash(metadata), "");
        activation = activation with { Mac = Sign(activation, vmk) };
        WriteNew(directory, id + ".manifest", JsonSerializer.SerializeToUtf8Bytes(activation));
        Verify(root, activation, vmk);
        parent.Validate();
        fault?.Invoke("GenerationVerified");
        return activation;
    }

    internal static void Publish(string root, Activation activation, byte[] vmk, Action<string>? fault)
    {
        Verify(root, activation, vmk);
        var directory = DirectoryPath(root);
        using var parent = VaultFileBoundary.PinDirectory(directory);
        var temporary = activation.Id + ".activation.tmp";
        WriteNew(directory, temporary, JsonSerializer.SerializeToUtf8Bytes(activation));
        fault?.Invoke("ActivationBeforeReplace");
        var target = Path.Combine(directory, "active.json");
        parent.Validate();
        if (File.Exists(target))
            File.Replace(Path.Combine(directory, temporary), target, Path.Combine(directory, "previous.json"), false);
        else
            File.Move(Path.Combine(directory, temporary), target, false);
        fault?.Invoke("ActivationAfterReplace");
        parent.Validate();
        Verify(root, ReadPointer(directory, "active.json"), vmk);
    }

    internal static IEnumerable<Generation> Candidates(string root)
    {
        foreach (var name in new[] { "active.json", "previous.json" })
        {
            Generation? generation = null;
            try
            {
                var activation = ReadPointer(DirectoryPath(root), name);
                var header = Read(DirectoryPath(root), activation.Id + ".header", 64 * 1024);
                var metadata = Read(DirectoryPath(root), activation.Id + ".metadata", 64 * 1024 * 1024);
                if (Hash(header) != activation.HeaderHash || Hash(metadata) != activation.MetadataHash)
                    throw new CryptographicException("generation digest mismatch");
                generation = new(activation, header, metadata, name == "previous.json");
            }
            catch (Exception ex) when (ex is IOException or JsonException or CryptographicException or InvalidDataException) { }
            if (generation is not null) yield return generation;
        }
    }

    internal static void Verify(string root, Activation activation, byte[] vmk)
    {
        Validate(activation);
        if (!LabCryptoCompare.FixedTimeEqualsHex(activation.Mac, Sign(activation with { Mac = "" }, vmk)))
            throw new CryptographicException("generation activation authentication failed");
        var directory = DirectoryPath(root);
        if (Hash(Read(directory, activation.Id + ".header", 64 * 1024)) != activation.HeaderHash
            || Hash(Read(directory, activation.Id + ".metadata", 64 * 1024 * 1024)) != activation.MetadataHash)
            throw new CryptographicException("generation content authentication failed");
    }

    private static string Sign(Activation activation, byte[] vmk)
    {
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, vmk, 32, info: Encoding.UTF8.GetBytes("lab/v6/activation"));
        try { return Convert.ToHexString(HMACSHA256.HashData(key, JsonSerializer.SerializeToUtf8Bytes(activation))); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
    private static void Validate(Activation activation)
    {
        static bool Hex(string? value, int length) => value is not null && value.Length == length && value.All(Uri.IsHexDigit);
        if (!Hex(activation.Id, 32) || !Hex(activation.VaultId, 32) || activation.Generation < 1
            || !Hex(activation.Mac, 64) || !Hex(activation.HeaderHash, 64) || !Hex(activation.MetadataHash, 64))
            throw new InvalidDataException("invalid activation contract");
    }
    private static Activation ReadPointer(string directory, string name)
    {
        var result = JsonSerializer.Deserialize<Activation>(Read(directory, name, 4096))
            ?? throw new InvalidDataException("empty activation");
        Validate(result);
        return result;
    }
    private static byte[] Read(string directory, string name, int maximum)
    {
        using var file = VaultFileBoundary.OpenRead(directory, name);
        if (file.Stream.Length > maximum) throw new InvalidDataException("generation size exceeds budget");
        var data = new byte[checked((int)file.Stream.Length)];
        file.Stream.ReadExactly(data);
        file.Validate();
        return data;
    }
    private static void WriteNew(string directory, string name, byte[] data)
    {
        using var file = VaultFileBoundary.OpenWrite(directory, name, create: true);
        file.Stream.Write(data);
        file.Stream.Flush(true);
        file.Verify(data.Length, Hash(data));
    }
}
