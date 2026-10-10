using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

if (args.Length != 2 || args[0] is not ("prepare" or "apply-kill" or "recover" or "verify")) return 2;
var root = Path.GetFullPath(args[1]);
var install = Path.Combine(root, "install");
var work = Path.Combine(root, "work");
var packagePath = Path.Combine(root, "update.spdup");
var publicKeyPath = Path.Combine(root, "test-public-key.bin");
try
{
    if (args[0] == "prepare")
    {
        if (Directory.Exists(root)) return 3;
        Directory.CreateDirectory(install);
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(install, "app.txt"), "old");
        File.WriteAllText(Path.Combine(install, "synthetic-vault.txt"), "vault-marker");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllBytes(publicKeyPath, key.ExportSubjectPublicKeyInfo());
        var payload = "new"u8.ToArray();
        var manifest = new SignedManifest
        {
            Publisher = "WO04-Isolated-Test", KeyId = "ephemeral", PackageId = Guid.NewGuid().ToString("N"),
            FromVersion = "51.0.4", ToVersion = "51.0.5", MinimumSupportedVersion = "51.0.0",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
            Files = [new SignedFile { Path = "payload/app.txt", Length = payload.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(payload)) }]
        };
        var signature = key.SignData(SignedPackage.CanonicalBytes(manifest), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        using var stream = File.Create(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        Add(archive, SignedPackage.ManifestName, JsonSerializer.SerializeToUtf8Bytes(manifest));
        Add(archive, SignedPackage.SignatureName,
            JsonSerializer.SerializeToUtf8Bytes(new SignatureEnvelope { Signature = Convert.ToBase64String(signature) }));
        Add(archive, "payload/app.txt", payload);
        Console.WriteLine("PREPARED old vault-marker");
        return 0;
    }
    var trust = new PublisherTrustPolicy([new TrustedPublisherKey("WO04-Isolated-Test", "ephemeral", "stable",
        File.ReadAllBytes(publicKeyPath))]);
    var transaction = new UpdateTransaction(trust, work, strictAcl: false);
    if (args[0] == "apply-kill")
    {
        transaction.Apply(packagePath, install, "51.0.4", faultInjector: point =>
        {
            if (point == UpdateFaultPoint.AfterFirstReplacement)
                Environment.FailFast("WO04 isolated synthetic process termination");
        });
        return 4;
    }
    if (args[0] == "recover")
    {
        var before = transaction.InspectRecovery(install);
        var result = transaction.Recover(install);
        Console.WriteLine($"BEFORE={before.State}:{before.Code} RESULT={result.Stage}:{result.Code}");
        return result.Stage == UpdateStage.Restored ? 0 : 5;
    }
    var app = File.ReadAllText(Path.Combine(install, "app.txt"));
    var vault = File.ReadAllText(Path.Combine(install, "synthetic-vault.txt"));
    var status = transaction.InspectRecovery(install);
    Console.WriteLine($"APP={app} VAULT={vault} STATUS={status.State}");
    return app == "old" && vault == "vault-marker" && status.State == UpdateRecoveryState.RecoveryCompleted
        ? 0 : 6;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
    return 7;
}

static void Add(ZipArchive archive, string name, byte[] data)
{
    using var output = archive.CreateEntry(name).Open();
    output.Write(data);
}
