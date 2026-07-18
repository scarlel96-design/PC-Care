using System.Text.Json;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Public bounded locator (design §4/§5). No secrets — vault discovery only.
/// </summary>
public static class LabVaultLocator
{
    public const string Magic = "AVLOC1";
    public const string FileName = "vault.locator.json";

    public sealed class Document
    {
        public string Magic { get; set; } = LabVaultLocator.Magic;
        public int Version { get; set; } = 5;
        public string Format { get; set; } = LabVaultService.FormatIdV5;
        public string VaultId { get; set; } = "";
        public string Suite { get; set; } = "Argon2id+XChaCha20-Poly1305+AES-GCM-wrap+HKDF";
        public int HeaderCopyCount { get; set; } = 2;
        public long CreatedUnix { get; set; }
        public string Bounds { get; set; } =
            "header<=64KiB;object<=512MiB;packSlot=64KiB;packMax=32MiB;entries<=50000";
        public bool PackFixedSlots { get; set; } = true;
        public bool Av3ProductionWriter { get; set; } // always false until separate gate
    }

    public static void Write(string vaultRoot, Document doc)
    {
        var path = Path.Combine(vaultRoot, FileName);
        File.WriteAllText(path, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static Document? TryRead(string vaultRoot)
    {
        var path = Path.Combine(vaultRoot, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
            if (doc is null || !string.Equals(doc.Magic, Magic, StringComparison.Ordinal))
            {
                return null;
            }

            return doc;
        }
        catch
        {
            return null;
        }
    }
}
