using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class CommercialPackTrustResult
{
    public bool ChecksumValid { get; init; }
    public bool SignatureValid { get; init; }
    public bool IsTrusted => ChecksumValid && SignatureValid;
    public string PackFile { get; init; } = "";
    public string Checksum { get; init; } = "";
    public string ExpectedChecksum { get; init; } = "";
    public string Message { get; init; } = "";
}

public static class CommercialPackTrustState
{
    private static bool _initialized;
    private static bool _rulesTrusted;
    private static bool _protocolsTrusted;
    private static string _message = "";

    public static bool RulesTrusted => _initialized && _rulesTrusted;
    public static bool ProtocolsTrusted => _initialized && _protocolsTrusted;
    public static bool IsFullyTrusted => RulesTrusted && ProtocolsTrusted;
    public static string Message => _message;

    public static void Initialize(string commercialRoot)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var rules = CommercialPackTrust.VerifyPack(commercialRoot, "rules.pack.json");
        var protocols = CommercialPackTrust.VerifyPack(commercialRoot, "protocols.pack.json");
        _rulesTrusted = rules.IsTrusted;
        _protocolsTrusted = protocols.IsTrusted;
        _message = rules.IsTrusted && protocols.IsTrusted
            ? "Rule/Protocol Pack trust verified"
            : $"Pack trust degraded: {rules.Message}; {protocols.Message}";
    }
}

internal static class CommercialPackTrust
{
    private const string PublicKeyPem =
        """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEH0srZcMD6tbrRQE6hW7L9LDEQaim
        jLiwPZqFgaWIg57Vq9aYMeXvarn0P43JtkwF+W2zs07ab64oOwJPqOYaTw==
        -----END PUBLIC KEY-----
        """;

    public static CommercialPackTrustResult VerifyPack(string commercialRoot, string packFileName)
    {
        var packPath = Path.Combine(commercialRoot, packFileName);
        if (!File.Exists(packPath))
        {
            return new CommercialPackTrustResult
            {
                PackFile = packFileName,
                Message = $"{packFileName} not found"
            };
        }

        var json = File.ReadAllText(packPath);
        var expectedChecksum = ReadChecksumField(json);
        var actualChecksum = ComputeCanonicalChecksum(json);
        var checksumValid = !string.IsNullOrWhiteSpace(expectedChecksum)
            && string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase);

        var sigPath = packPath + ".sig";
        var signatureValid = false;
        if (File.Exists(sigPath) && checksumValid)
        {
            signatureValid = VerifySignatureFile(sigPath, actualChecksum);
        }

        return new CommercialPackTrustResult
        {
            PackFile = packFileName,
            ChecksumValid = checksumValid,
            SignatureValid = signatureValid,
            Checksum = actualChecksum,
            ExpectedChecksum = expectedChecksum,
            Message = checksumValid && signatureValid
                ? "checksum+signature OK"
                : !checksumValid
                    ? "checksum mismatch or missing"
                    : "signature missing or invalid"
        };
    }

    public static string ComputeCanonicalChecksum(string json)
    {
        using var doc = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalObject(writer, doc.RootElement, omitChecksum: true);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static string ReadChecksumField(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("checksum", out var node)
            ? node.GetString() ?? ""
            : "";
    }

    private static bool VerifySignatureFile(string sigPath, string checksumHex)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(sigPath));
            var root = doc.RootElement;
            if (!root.TryGetProperty("signature", out var sigNode))
            {
                return false;
            }

            var signatureBase64 = sigNode.GetString();
            if (string.IsNullOrWhiteSpace(signatureBase64))
            {
                return false;
            }

            if (root.TryGetProperty("checksum", out var checksumNode))
            {
                var signedChecksum = checksumNode.GetString() ?? "";
                if (!string.Equals(signedChecksum, checksumHex, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            var signature = Convert.FromBase64String(signatureBase64);
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(PublicKeyPem);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(checksumHex));
            return verifier.VerifyHash(hash, signature);
        }
        catch
        {
            return false;
        }
    }

    private static void WriteCanonicalObject(Utf8JsonWriter writer, JsonElement element, bool omitChecksum)
    {
        writer.WriteStartObject();
        foreach (var prop in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (omitChecksum && prop.NameEquals("checksum"))
            {
                writer.WriteString("checksum", "");
                continue;
            }

            writer.WritePropertyName(prop.Name);
            WriteValue(writer, prop.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                WriteCanonicalObject(writer, value, omitChecksum: false);
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteValue(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}