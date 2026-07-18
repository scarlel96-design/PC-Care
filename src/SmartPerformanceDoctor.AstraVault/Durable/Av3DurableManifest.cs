using System.Text.Json;

namespace SmartPerformanceDoctor.AstraVault.Durable;

public sealed class Av3DurableManifest
{
    public ulong PreviousGeneration { get; set; }
    public ulong TargetGeneration { get; set; }
    public List<string> FlushedRelativePaths { get; set; } = [];
    public List<string> ProgressSteps { get; set; } = [];
    public bool ActivationAuthenticated { get; set; }
    public bool MetadataAuthenticated { get; set; }
    public bool RereadSucceeded { get; set; }
    public bool CleanupCompleted { get; set; }
    public int HeaderCopyDurableCount { get; set; }
    public bool HeaderCopyConflict { get; set; }

    public static Av3DurableManifest Load(string vaultRoot)
    {
        var path = Path.Combine(vaultRoot, Av3DurableFileLayout.ManifestRelative);
        if (!File.Exists(path))
        {
            return new Av3DurableManifest();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Av3DurableManifest>(json) ?? new Av3DurableManifest();
    }

    public void Save(string vaultRoot)
    {
        var path = Path.Combine(vaultRoot, Av3DurableFileLayout.ManifestRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        writer.Write(JsonSerializer.Serialize(this));
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }
}