using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

if (args.Length != 2 || (args[0] is not ("prepare" or "prepare-self" or "hold" or "verify-updated" or "apply" or "apply-kill" or "recover-kill" or "recover" or "verify" or "verify-orphan") && !args[0].StartsWith("kill:", StringComparison.Ordinal))) return 2;
var root = Path.GetFullPath(args[1]);
var install = Path.Combine(root, "install");
var work = Path.Combine(root, "work");
var packagePath = Path.Combine(root, "update.spdup");
var publicKeyPath = Path.Combine(root, "test-public-key.bin");
try
{
    if (args[0] == "hold")
    {
        File.WriteAllText(Path.Combine(root, "holder.pid"), Environment.ProcessId.ToString());
        for (var index = 0; index < 600; index++) await Task.Delay(100);
        return 0;
    }
    if (args[0] is "prepare" or "prepare-self")
    {
        if (Directory.Exists(root)) return 3;
        Directory.CreateDirectory(install);
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(install, "app.txt"), "old");
        File.WriteAllText(Path.Combine(install, "synthetic-vault.txt"), "vault-marker");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllBytes(publicKeyPath, key.ExportSubjectPublicKeyInfo());
        var payload = "new"u8.ToArray();
        var selfPayloads = new Dictionary<string, byte[]>();
        if (args[0] == "prepare-self")
        {
            var engine = Path.Combine(install, "engine");
            Directory.CreateDirectory(engine);
            foreach (var file in Directory.GetFiles(AppContext.BaseDirectory))
                File.Copy(file, Path.Combine(engine, Path.GetFileName(file)));
            foreach (var name in new[] { "SmartPerformanceDoctor.UpdateRecoveryProbe.exe", "SmartPerformanceDoctor.UpdateRecoveryProbe.dll" })
            {
                var original = File.ReadAllBytes(Path.Combine(engine, name));
                selfPayloads["payload/engine/" + name] = original.Concat(new byte[] { 0x57 }).ToArray();
            }
        }
        var manifest = new SignedManifest
        {
            Publisher = "WO04-Isolated-Test", KeyId = "ephemeral", PackageId = Guid.NewGuid().ToString("N"),
            FromVersion = "51.0.4", ToVersion = "51.0.5", MinimumSupportedVersion = "51.0.0",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
            Files = [new SignedFile { Path = "payload/app.txt", Length = payload.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(payload)) },
                new SignedFile { Path = "payload/new.txt", Length = payload.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(payload)) }]
        };
        manifest.Files.AddRange(selfPayloads.Select(item => new SignedFile { Path = item.Key,
            Length = item.Value.LongLength, Sha256 = Convert.ToHexString(SHA256.HashData(item.Value)) }));
        var signature = key.SignData(SignedPackage.CanonicalBytes(manifest), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        using var stream = File.Create(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        Add(archive, SignedPackage.ManifestName, JsonSerializer.SerializeToUtf8Bytes(manifest));
        Add(archive, SignedPackage.SignatureName,
            JsonSerializer.SerializeToUtf8Bytes(new SignatureEnvelope { Signature = Convert.ToBase64String(signature) }));
        Add(archive, "payload/app.txt", payload);
        Add(archive, "payload/new.txt", payload);
        foreach (var item in selfPayloads) Add(archive, item.Key, item.Value);
        Console.WriteLine("PREPARED old vault-marker");
        return 0;
    }
    var trust = new PublisherTrustPolicy([new TrustedPublisherKey("WO04-Isolated-Test", "ephemeral", "stable",
        File.ReadAllBytes(publicKeyPath))]);
    var transaction = new UpdateTransaction(trust, work, strictAcl: false);
    if (args[0] == "verify-updated")
    {
        var verified = SignedPackage.Verify(packagePath, "51.0.4", trust);
        if (!verified.CanApply || verified.Manifest is null) return 8;
        foreach (var file in verified.Manifest.Files)
        {
            var path = Path.Combine(install, file.Path[8..].Replace('/', Path.DirectorySeparatorChar));
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) != file.Sha256) return 8;
        }
        var preserved = File.ReadAllText(Path.Combine(install, "synthetic-vault.txt")) == "vault-marker";
        Console.WriteLine("DETACHED_IMAGE_UPDATE=HASH-MATCH; DATA=" + (preserved ? "PRESERVED" : "CHANGED"));
        return preserved ? 0 : 8;
    }
    if (args[0] == "apply")
    {
        var result = transaction.Apply(packagePath, install, "51.0.4");
        Console.WriteLine($"RESULT={result.Stage}:{result.Code} FAILURE={result.Failure}");
        return result.Stage == UpdateStage.Committed ? 0 : 5;
    }
    if (args[0].StartsWith("kill:", StringComparison.Ordinal))
    {
        if (!Enum.TryParse<UpdateFaultPoint>(args[0][5..], out var target) || !Enum.IsDefined(target)) return 2;
        var armed = false;
        transaction.Apply(packagePath, install, "51.0.4", faultInjector: point =>
        {
            if (point == UpdateFaultPoint.AfterApplyingJournal) armed = true;
            if (armed && point == target) Crash("WO06 fixture process termination: " + point);
        });
        return 4;
    }
    if (args[0] == "apply-kill")
    {
        transaction.Apply(packagePath, install, "51.0.4", faultInjector: point =>
        {
            if (point == UpdateFaultPoint.AfterFirstReplacement)
                Crash("WO04 isolated synthetic process termination");
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
    if (args[0] == "recover-kill")
    {
        transaction.Recover(install, faultInjector: point =>
        {
            if (point == UpdateFaultPoint.DuringRollbackCopy)
                Crash("WO05 synthetic termination during rollback copy");
        });
        return 4;
    }
    var app = File.ReadAllText(Path.Combine(install, "app.txt"));
    var vault = File.ReadAllText(Path.Combine(install, "synthetic-vault.txt"));
    var status = transaction.InspectRecovery(install);
    if (args[0] == "verify-orphan")
    {
        var orphans = Directory.GetFiles(install, ".pccare-*.pending").Length;
        Console.WriteLine($"APP={app} VAULT={vault} STATUS={status.State} ORPHANS={orphans}");
        return app == "old" && vault == "vault-marker" && status.State == UpdateRecoveryState.RecoveryFailed &&
            orphans == 1 && !File.Exists(Path.Combine(install, "new.txt")) ? 0 : 6;
    }
    Console.WriteLine($"APP={app} VAULT={vault} STATUS={status.State}");
    return app == "old" && vault == "vault-marker" && !File.Exists(Path.Combine(install, "new.txt")) && status.State == UpdateRecoveryState.RecoveryCompleted
        ? 0 : 6;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
    return 7;
}

static void Crash(string message)
{
    Console.Error.WriteLine(message);
    System.Diagnostics.Process.GetCurrentProcess().Kill();
    Thread.Sleep(Timeout.Infinite);
}

static void Add(ZipArchive archive, string name, byte[] data)
{
    using var output = archive.CreateEntry(name).Open();
    output.Write(data);
}
