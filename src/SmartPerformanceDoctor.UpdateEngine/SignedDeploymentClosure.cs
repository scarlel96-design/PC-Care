using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record DeploymentInputFile(string Path, long Length, string Sha256, bool IsPe, string? Publisher = null);
public sealed record DeploymentInputManifest(string Format, string Version, string Publisher, IReadOnlyList<DeploymentInputFile> Files);
public sealed record DeploymentInputObservation(string Path, long Length, string Sha256, string FileIdentity, bool IsPe);

/// <summary>Bind isolated deployment inputs. This contract does not change production publisher trust.</summary>
public sealed class SignedDeploymentClosure : IDisposable
{
    private readonly List<WindowsFileBoundary.FileLease> _files = [];
    private readonly List<WindowsFileBoundary.DirectoryPin> _directories = [];
    public IReadOnlyList<DeploymentInputObservation> Files { get; private set; } = [];
    public bool AuthenticodeVerified { get; private set; }
    private string _root = "";
    private bool _strictAcl;
    private readonly Dictionary<string, string> _publishers = new(StringComparer.OrdinalIgnoreCase);
    private SignedDeploymentClosure() { }
    public static SignedDeploymentClosure Bind(string root, byte[] manifestBytes, byte[] signature, byte[] publicKey,
        string pinnedPublicKeyHash, bool strictAcl = true)
    {
        if (manifestBytes.Length > 2 * 1024 * 1024 || signature.Length != 64 || publicKey.Length > 4096 ||
            !Convert.ToHexString(SHA256.HashData(publicKey)).Equals(pinnedPublicKeyHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("DEPLOYMENT-TRUST-INPUT");
        using var key = ECDsa.Create(); key.ImportSubjectPublicKeyInfo(publicKey, out var consumed);
        if (consumed != publicKey.Length || key.KeySize != 256 ||
            !key.VerifyData(manifestBytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new InvalidDataException("DEPLOYMENT-SIGNATURE");
        using var json = JsonDocument.Parse(manifestBytes); RejectDuplicateProperties(json.RootElement);
        var manifest = json.RootElement.Deserialize<DeploymentInputManifest>(new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })!;
        if (manifest.Format != "pccare-test-deployment-v1" || !Version.TryParse(manifest.Version, out _) ||
            manifest.Publisher.Length != 40 || !manifest.Publisher.All(Uri.IsHexDigit) || manifest.Files.Count is < 1 or > 4096 ||
            manifest.Files.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Count)
            throw new InvalidDataException("DEPLOYMENT-MANIFEST");
        var result = new SignedDeploymentClosure { _root = Path.GetFullPath(root), _strictAcl = strictAcl };
        try
        {
            result._directories.Add(WindowsFileBoundary.PinDirectory(root));
            if (strictAcl) UpdateTransaction.ValidateProtectedDirectory(root);
            var observations = new List<DeploymentInputObservation>();
            foreach (var item in manifest.Files)
            {
                if (!SignedPackage.SafeRelativePath("payload/" + item.Path) || item.Length < 0 || item.Length > SignedPackage.MaxEntryLength || item.Sha256.Length != 64 || !item.Sha256.All(Uri.IsHexDigit))
                    throw new InvalidDataException("DEPLOYMENT-PATH");
                var file = WindowsFileBoundary.OpenRead(root, item.Path); result._files.Add(file);
                if (strictAcl) UpdateTransaction.ValidateProtectedSecurity(file.ReadSecurity(), false);
                file.Verify(item.Length, item.Sha256);
                file.Stream.Position = 0; var mz = file.Stream.ReadByte() == 0x4d && file.Stream.ReadByte() == 0x5a;
                var executable = Path.GetExtension(item.Path).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(item.Path).Equals(".dll", StringComparison.OrdinalIgnoreCase);
                if (item.IsPe != mz || executable && !item.IsPe) throw new InvalidDataException("DEPLOYMENT-PE-CLASSIFICATION");
                result._publishers[item.Path] = item.Publisher ?? manifest.Publisher;
                observations.Add(new(item.Path, item.Length, item.Sha256, file.Identity, item.IsPe));
            }
            result.Files = observations.AsReadOnly();
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<(string Path, int Depth)>(); pending.Push((root, 0));
            while (pending.TryPop(out var item))
            {
                if (item.Depth > 32) throw new InvalidDataException("DEPLOYMENT-LIMIT");
                result._directories.Add(WindowsFileBoundary.PinDirectory(item.Path));
                if (strictAcl) UpdateTransaction.ValidateProtectedDirectory(item.Path);
                foreach (var path in Directory.EnumerateFiles(item.Path))
                { if (actual.Count >= 4096) throw new InvalidDataException("DEPLOYMENT-LIMIT"); actual.Add(Path.GetRelativePath(root, path).Replace('\\', '/')); }
                foreach (var child in Directory.EnumerateDirectories(item.Path)) pending.Push((child, item.Depth + 1));
            }
            if (!actual.SetEquals(manifest.Files.Select(x => x.Path))) throw new InvalidDataException("DEPLOYMENT-CLOSURE-MISMATCH");
            foreach (var file in observations.Where(x => x.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                var bound = result._files[observations.IndexOf(file)];
                if (bound.Stream.Length > 8 * 1024 * 1024) throw new InvalidDataException("DEPLOYMENT-JSON-LIMIT");
                bound.Stream.Position = 0; using var config = JsonDocument.Parse(bound.Stream); RejectDuplicateProperties(config.RootElement);
                if (file.Path.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) && config.RootElement.TryGetProperty("targets", out var targets))
                    foreach (var target in targets.EnumerateObject()) foreach (var library in target.Value.EnumerateObject())
                        foreach (var kind in new[] { "runtime", "native", "runtimeTargets" })
                            if (library.Value.TryGetProperty(kind, out var assets)) foreach (var asset in assets.EnumerateObject())
                            {
                                if (asset.Name.EndsWith("/_._", StringComparison.Ordinal)) continue;
                                var name = Path.GetFileName(asset.Name);
                                if (!observations.Any(x => Path.GetFileName(x.Path).Equals(name, StringComparison.OrdinalIgnoreCase)))
                                    throw new InvalidDataException("DEPLOYMENT-DEPENDENCY-MISSING");
                            }
            }
            result.Validate(); return result;
        }
        catch { result.Dispose(); throw; }
    }
    public void VerifyAuthenticode()
    {
        if (!_strictAcl) throw new InvalidDataException("DEPLOYMENT-PROTECTED-INPUT-REQUIRED");
        foreach (var item in Files.Where(x => x.IsPe))
        {
            var signature = AuthenticodeBoundary.Verify(Path.Combine(_root, item.Path));
            if (signature.Code != 0 || !string.Equals(signature.Thumbprint, _publishers[item.Path], StringComparison.OrdinalIgnoreCase) ||
                signature.Identity != item.FileIdentity || !string.Equals(signature.Hash, item.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("DEPLOYMENT-PE-SIGNATURE");
        }
        if (!Files.Any(x => x.IsPe)) throw new InvalidDataException("DEPLOYMENT-PE-MISSING");
        Validate(); AuthenticodeVerified = true;
    }
    public void Validate() { foreach (var file in _files) file.Validate(); foreach (var directory in _directories) directory.Validate(); }
    public void Dispose() { foreach (var file in _files) file.Dispose(); foreach (var directory in _directories) directory.Dispose(); }
    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        { var names = new HashSet<string>(StringComparer.Ordinal); foreach (var property in value.EnumerateObject()) { if (!names.Add(property.Name)) throw new InvalidDataException("DEPLOYMENT-DUPLICATE-PROPERTY"); RejectDuplicateProperties(property.Value); } }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item);
    }
}
