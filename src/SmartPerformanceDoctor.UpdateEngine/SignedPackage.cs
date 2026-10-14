using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartPerformanceDoctor.UpdateEngine;

public sealed class SignedManifest
{
    public string Format { get; set; } = "spd-update-v2";
    public string Product { get; set; } = "PC-Care";
    public string Publisher { get; set; } = "";
    public string KeyId { get; set; } = "";
    public string Algorithm { get; set; } = "ECDSA-P256-SHA256-P1363";
    public string Channel { get; set; } = "stable";
    public string PackageId { get; set; } = "";
    public string FromVersion { get; set; } = "";
    public string ToVersion { get; set; } = "";
    public string MinimumSupportedVersion { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public List<SignedFile> Files { get; set; } = [];
}

public sealed class SignedFile
{
    public string Path { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class SignatureEnvelope
{
    public string Signature { get; set; } = "";
}

public enum PublisherVerification { PublisherVerified, ContentVerifiedOnly, Unverified, VerificationFailed }
public sealed record PackageVerification(PublisherVerification Trust, string Code, string Message, SignedManifest? Manifest)
{
    public bool CanApply => Trust == PublisherVerification.PublisherVerified;
}

public sealed record TrustedPublisherKey(string Publisher, string KeyId, string Channel, byte[] SubjectPublicKeyInfo);

public sealed class PublisherTrustPolicy
{
    private readonly IReadOnlyList<TrustedPublisherKey> _keys;
    public PublisherTrustPolicy(IEnumerable<TrustedPublisherKey> keys) => _keys = keys.ToArray();
    // No development key is embedded in production. Provisioning a publisher root is a separate release gate.
    public static PublisherTrustPolicy Production { get; } = new([]);
    public TrustedPublisherKey? Find(SignedManifest manifest) => _keys.SingleOrDefault(k =>
        k.Publisher == manifest.Publisher && k.KeyId == manifest.KeyId && k.Channel == manifest.Channel);
}

public static class SignedPackage
{
    public const string ManifestName = "update.manifest.json";
    public const string SignatureName = "update.signature.json";
    public const long MaxEntryLength = 512L * 1024 * 1024;
    public const long MaxTotalLength = 2L * 1024 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static byte[] CanonicalBytes(SignedManifest manifest)
    {
        // A fixed binary encoding avoids JSON whitespace, order and Unicode normalization ambiguity.
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        Write(writer, "PC-CARE-SIGNED-UPDATE-V2\0");
        foreach (var value in new[] { manifest.Format, manifest.Product, manifest.Publisher, manifest.KeyId,
                     manifest.Algorithm, manifest.Channel, manifest.PackageId, manifest.FromVersion, manifest.ToVersion,
                     manifest.MinimumSupportedVersion }) Write(writer, value);
        writer.Write(manifest.CreatedAt.UtcTicks);
        writer.Write(manifest.ExpiresAt.UtcTicks);
        writer.Write(manifest.Files.Count);
        foreach (var file in manifest.Files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            Write(writer, file.Path);
            writer.Write(file.Length);
            Write(writer, file.Sha256.ToLowerInvariant());
        }
        writer.Flush();
        return output.ToArray();
    }

    private static void Write(BinaryWriter writer, string value)
    {
        var data = Encoding.UTF8.GetBytes(value);
        writer.Write(data.Length);
        writer.Write(data);
    }

    public static PackageVerification Verify(string packagePath, string currentVersion,
        PublisherTrustPolicy policy, DateTimeOffset? now = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = Path.GetFullPath(packagePath);
            using var lease = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(path)!, Path.GetFileName(path));
            return Verify(lease.Stream, currentVersion, policy, now, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Fail("VERIFY-FAIL", ex.GetType().Name); }
    }

    public static PackageVerification Verify(Stream file, string currentVersion,
        PublisherTrustPolicy policy, DateTimeOffset? now = null, CancellationToken cancellationToken = default)
    {
        try
        {
            file.Position = 0;
            if (file.Length > MaxTotalLength) return Fail("PACKAGE-LIMIT");
            using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count is < 3 or > 10_002) return Fail("ENTRY-LIMIT");
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.FullName.EndsWith('/') || entry.FullName.Contains('\\') || !entries.TryAdd(entry.FullName, entry))
                    return Fail("ENTRY-PATH");
                if (entry.Length > MaxEntryLength || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                    return Fail("ENTRY-TYPE");
            }
            if (!entries.TryGetValue(ManifestName, out var manifestEntry) || manifestEntry.FullName != ManifestName ||
                !entries.TryGetValue(SignatureName, out var signatureEntry) || signatureEntry.FullName != SignatureName ||
                manifestEntry.Length > 1024 * 1024 || signatureEntry.Length > 4096) return Fail("METADATA");
            var manifestBytes = ReadBounded(manifestEntry, 1024 * 1024);
            var signatureBytes = ReadBounded(signatureEntry, 4096);
            CheckDuplicateProperties(manifestBytes);
            CheckDuplicateProperties(signatureBytes);
            var manifest = JsonSerializer.Deserialize<SignedManifest>(manifestBytes, JsonOptions) ?? throw new InvalidDataException();
            var envelope = JsonSerializer.Deserialize<SignatureEnvelope>(signatureBytes, JsonOptions) ?? throw new InvalidDataException();
            if (manifest.Format != "spd-update-v2" || manifest.Product != "PC-Care" ||
                manifest.Algorithm != "ECDSA-P256-SHA256-P1363" || manifest.Channel != "stable" ||
                !Guid.TryParseExact(manifest.PackageId, "N", out var packageId) || packageId == Guid.Empty ||
                string.IsNullOrWhiteSpace(manifest.Publisher) || string.IsNullOrWhiteSpace(manifest.KeyId) ||
                !Version.TryParse(manifest.FromVersion, out var from) ||
                !Version.TryParse(manifest.ToVersion, out var to) ||
                !Version.TryParse(manifest.MinimumSupportedVersion, out var minimum) ||
                !Version.TryParse(currentVersion, out var current) ||
                current < minimum || current < from || current >= to ||
                manifest.CreatedAt >= manifest.ExpiresAt ||
                (now ?? DateTimeOffset.UtcNow) < manifest.CreatedAt ||
                (now ?? DateTimeOffset.UtcNow) > manifest.ExpiresAt) return Fail("POLICY");
            if (manifest.Files is null || manifest.Files.Count == 0 || manifest.Files.Count > 10_000 ||
                entries.Count != manifest.Files.Count + 2) return Fail("ALLOWLIST");
            long total = 0;
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var expected in manifest.Files)
            {
                if (!SafeRelativePath(expected.Path) || !paths.Add(expected.Path) ||
                    expected.Length < 0 || expected.Length > MaxEntryLength ||
                    expected.Sha256.Length != 64 || !expected.Sha256.All(Uri.IsHexDigit) ||
                    !entries.TryGetValue(expected.Path, out var entry) || entry.FullName != expected.Path ||
                    entry.Length != expected.Length || total > MaxTotalLength - entry.Length) return Fail("ALLOWLIST");
                total += entry.Length;
                using var stream = entry.Open();
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[128 * 1024];
                long read = 0;
                int size;
                while ((size = stream.Read(buffer)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    read += size;
                    if (read > expected.Length) return Fail("CONTENT-LENGTH");
                    sha.AppendData(buffer, 0, size);
                }
                if (read != expected.Length || !Convert.ToHexString(sha.GetHashAndReset()).Equals(expected.Sha256,
                    StringComparison.OrdinalIgnoreCase)) return Fail("CONTENT-HASH");
            }
            byte[] signature;
            try { signature = Convert.FromBase64String(envelope.Signature); }
            catch (FormatException) { return Fail("SIGNATURE-FORMAT"); }
            if (signature.Length != 64) return Fail("SIGNATURE-FORMAT");
            var key = policy.Find(manifest);
            if (key is null) return new(PublisherVerification.ContentVerifiedOnly, "UNKNOWN-PUBLISHER",
                "파일 내용은 확인됐지만 공식 발행자 키가 등록되지 않았습니다.", manifest);
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(key.SubjectPublicKeyInfo, out _);
            if (ecdsa.KeySize != 256 || !ecdsa.VerifyData(CanonicalBytes(manifest), signature,
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) return Fail("SIGNATURE-FAIL");
            return new(PublisherVerification.PublisherVerified, "PUBLISHER-VERIFIED", "발행자와 파일 내용 검증 완료", manifest);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return Fail("VERIFY-FAIL", ex.GetType().Name); }
    }

    /// <summary>Verify a historical signed declaration. Check installed file content separately.</summary>
    public static bool VerifyDeclaration(SignedManifest manifest, string encodedSignature, PublisherTrustPolicy policy)
    {
        try
        {
            if(manifest.Format!="spd-update-v2"||manifest.Product!="PC-Care"||manifest.Channel!="stable"||manifest.Algorithm!="ECDSA-P256-SHA256-P1363"||
                manifest.Files is null || manifest.Files.Count is <1 or >10000 || manifest.Files.Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=manifest.Files.Count ||
                manifest.Files.Any(f=>!SafeRelativePath(f.Path)||f.Length<0||f.Sha256.Length!=64||!f.Sha256.All(Uri.IsHexDigit)))return false;
            var key=policy.Find(manifest);if(key is null)return false;
            var signature=Convert.FromBase64String(encodedSignature);if(signature.Length!=64)return false;
            using var ecdsa=ECDsa.Create();ecdsa.ImportSubjectPublicKeyInfo(key.SubjectPublicKeyInfo,out _);
            return ecdsa.KeySize==256&&ecdsa.VerifyData(CanonicalBytes(manifest),signature,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch{return false;}
    }

    private static PackageVerification Fail(string code, string? detail = null) =>
        new(PublisherVerification.VerificationFailed, code, detail ?? code, null);

    private static byte[] ReadBounded(ZipArchiveEntry entry, int limit)
    {
        using var stream = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            if (output.Length > limit - read) throw new InvalidDataException("metadata limit");
            output.Write(buffer, 0, read);
        }
        if (output.Length > limit || output.Length != entry.Length) throw new InvalidDataException("metadata length");
        return output.ToArray();
    }

    private static void CheckDuplicateProperties(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        Walk(document.RootElement);
        static void Walk(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new InvalidDataException("duplicate JSON property");
                    Walk(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var child in element.EnumerateArray()) Walk(child);
        }
    }

    public static bool SafeRelativePath(string path)
    {
        if (!path.StartsWith("payload/", StringComparison.Ordinal) || path.Contains('\\') || path.Contains(':') ||
            path.Any(char.IsControl)) return false;
        var parts = path.Split('/');
        if (parts.Length < 2) return false;
        foreach (var part in parts)
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith(' ') || part.EndsWith('.') ||
                part.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0) return false;
            var stem = part.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
                    (stem[3] is >= '1' and <= '9' or '\u00b9' or '\u00b2' or '\u00b3'))) return false;
        }
        return true;
    }
}
