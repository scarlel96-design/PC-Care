using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

try
{
    if (args.Length == 2 && args[0] == "diagnose-legacy")
    {
        UpdateBootstrap.ValidateInstallRoot(args[1]);
        var reports = new UpdateTransaction(BuildPublisherTrust.Current, UpdateBootstrap.WorkRoot, true).DiagnoseLegacy(args[1]);
        Console.WriteLine(JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 2 && args[0] == "fixture-vault")
    {
        var directory = Path.GetFullPath(args[1]);
        if (Directory.Exists(directory)) return 3;
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "synthetic.txt");
        File.WriteAllText(source, "WO06-SYNTHETIC-CLI");
        using var vault = new SmartPerformanceDoctor.SecurityLab.VaultV4.LabVaultService(Path.Combine(directory, "vault"));
        const string fixturePassword = "WO06-Synthetic-Only-42!";
        if (!vault.Create(fixturePassword, SmartPerformanceDoctor.SecurityLab.VaultV4.LabKdfProfile.LabFast).Success ||
            !vault.Unlock(fixturePassword).Success || !vault.ImportFile(source).Success) return 10;
        Console.WriteLine("ENCRYPTED_LAB_FIXTURE=CREATED; SOURCE_SHA256=" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
        return 0;
    }
    if (args.Length == 3 && args[0] is "fixture-profile" or "vm-profile")
    {
        var directory = Path.GetFullPath(args[1]);
        if (Directory.Exists(directory)) return 3;
        if (args[0] == "vm-profile" && !File.Exists(Path.Combine(Path.GetDirectoryName(directory)!, ".wo05-disposable-vm"))) return 4;
        if (args[2].Length != 40 || !args[2].All(Uri.IsHexDigit)) return 5;
        Directory.CreateDirectory(directory);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = key.ExportSubjectPublicKeyInfo();
        File.WriteAllBytes(Path.Combine(directory, "test-public.bin"), publicKey);
        if (args[0] == "vm-profile") File.WriteAllBytes(Path.Combine(directory, "test-private.p8"), key.ExportPkcs8PrivateKey());
        var source = "namespace SmartPerformanceDoctor.UpdateEngine; internal static class TestVmTrustData { public const string SignerThumbprint = \"" + args[2] + "\"; public static byte[] PublicKey => System.Convert.FromBase64String(\"" + Convert.ToBase64String(publicKey) + "\"); }";
        File.WriteAllText(Path.Combine(directory, "TestVmTrust.g.cs"), source);
        Console.WriteLine("PROFILE=WO05-VM-TEST; PRIVATE_KEY=" + (args[0] == "vm-profile" ? "VM-LOCAL-ONLY" : "NOT-SAVED"));
        return 0;
    }
    if (args.Length == 2 && args[0] == "audit-production")
    {
        var assembly = Assembly.LoadFile(Path.GetFullPath(args[1]));
        if (assembly.GetType("SmartPerformanceDoctor.UpdateEngine.TestVmTrustData") is not null ||
            assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Any(a => a.Key == "PC-Care.BuildProfile")) return 6;
        var type = assembly.GetType("SmartPerformanceDoctor.UpdateEngine.BuildPublisherTrust");
        if (type is not null && (bool)type.GetField("IsVmTest")!.GetRawConstantValue()!) return 7;
        Console.WriteLine("PRODUCTION_PROFILE=PASS; TEST_ROOT=ABSENT");
        return 0;
    }
    if (args.Length == 6 && args[0] == "sign-package")
    {
        var payloadRoot = Path.GetFullPath(args[1]);
        var keyPath = Path.GetFullPath(args[2]);
        if (!File.Exists(Path.Combine(Directory.GetParent(Path.GetDirectoryName(keyPath)!)!.FullName, ".wo05-disposable-vm"))) return 4;
        var payloads = Directory.GetFiles(payloadRoot, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => (Path: "payload/" + Path.GetRelativePath(payloadRoot, p).Replace('\\', '/'), Bytes: File.ReadAllBytes(p))).ToArray();
        if (payloads.Any(p => !SignedPackage.SafeRelativePath(p.Path))) return 8;
        var manifest = new SignedManifest { Publisher = "WO05-VM-TEST", KeyId = "wo05-ephemeral", PackageId = Guid.NewGuid().ToString("N"),
            FromVersion = args[4], ToVersion = args[5], MinimumSupportedVersion = args[4],
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
            Files = payloads.Select(p => new SignedFile { Path = p.Path, Length = p.Bytes.LongLength, Sha256 = Convert.ToHexString(SHA256.HashData(p.Bytes)) }).ToList() };
        using var key = ECDsa.Create();
        var secret = File.ReadAllBytes(keyPath);
        try { key.ImportPkcs8PrivateKey(secret, out _); }
        finally { CryptographicOperations.ZeroMemory(secret); }
        var signature = key.SignData(SignedPackage.CanonicalBytes(manifest), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        using var file = new FileStream(args[3], FileMode.CreateNew);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        Add(zip, SignedPackage.ManifestName, JsonSerializer.SerializeToUtf8Bytes(manifest));
        Add(zip, SignedPackage.SignatureName, JsonSerializer.SerializeToUtf8Bytes(new SignatureEnvelope { Signature = Convert.ToBase64String(signature) }));
        foreach (var payload in payloads) Add(zip, payload.Path, payload.Bytes);
        Console.WriteLine($"SIGNED_TEST_PACKAGE files={payloads.Length}; OFFICIAL=false");
        return 0;
    }
    return 2;
}
catch (Exception ex) { Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message); return 9; }

static void Add(ZipArchive zip, string path, byte[] bytes) { using var file = zip.CreateEntry(path).Open(); file.Write(bytes); }
