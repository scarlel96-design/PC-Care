using System.Text;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Deterministic E-12 vectors (test labels — no plaintext secrets).</summary>
public static class Av3XChaCha24VectorFactory
{
    public static Av3AeadVector BuildActivationPayloadVector()
    {
        var keyLabel = "e12_fixture_activation_key_label";
        var key = Av3AeadKeyMaterialPolicy.DeriveFixtureKey(Encoding.UTF8.GetBytes(keyLabel));
        var container = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var vault = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var metaDigest = new byte[32];
        Encoding.UTF8.GetBytes("e12_meta_digest_label").CopyTo(metaDigest);
        var aad = ActivationPayloadAad.Build(
            AstraFormatConstants.MajorVersion,
            container,
            vault,
            headerCopyId: 0,
            headerGeneration: 4,
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            metaDigest,
            ActivationPayloadAad.TargetMetadataRoot);
        var plain = HeaderActivationPayload.BuildPlaintext(4, metaDigest, 4, 3);
        var nonce = Av3AeadNoncePolicy.FixtureNonce(
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            "e12_activation_nonce_label"u8);
        var ct = Av3XChaCha24Aead.Instance.Encrypt(key, plain, aad);
        return new Av3AeadVector
        {
            VectorId = "e12_activation_payload",
            KeyLabel = keyLabel,
            SuiteId = Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            Nonce = ct.Nonce,
            Aad = aad,
            Plaintext = plain,
            Ciphertext = ct.Cipher,
            Tag = ct.Tag,
            Kind = "activation"
        };
    }

    public static Av3AeadVector BuildMetadataRootVector()
    {
        var keyLabel = "e12_fixture_metadata_key_label";
        var key = Av3AeadKeyMaterialPolicy.DeriveFixtureKey(Encoding.UTF8.GetBytes(keyLabel));
        var container = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var vault = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var activationDigest = new byte[32];
        var metaDigest = new byte[32];
        Encoding.UTF8.GetBytes("e12_activation_digest_label").CopyTo(activationDigest);
        Encoding.UTF8.GetBytes("e12_metadata_cipher_digest_label").CopyTo(metaDigest);
        var aad = MetadataRootAad.Build(
            AstraFormatConstants.MajorVersion,
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            container,
            vault,
            headerGeneration: 4,
            metadataRootGeneration: 4,
            metaDigest,
            activationDigest,
            MetadataRootReadOnlyReader.DefaultLogicalId,
            MetadataRootPlaintext.PlaintextSize);
        var plain = MetadataRootPlaintext.BuildCanonical(
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            4,
            3,
            metaDigest,
            metaDigest,
            metaDigest,
            metaDigest,
            metaDigest);
        var nonce = Av3AeadNoncePolicy.FixtureNonce(
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            "e12_metadata_nonce_label"u8);
        var ct = Av3XChaCha24Aead.Instance.Encrypt(key, plain, aad);
        return new Av3AeadVector
        {
            VectorId = "e12_metadata_root",
            KeyLabel = keyLabel,
            SuiteId = Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            Nonce = ct.Nonce,
            Aad = aad,
            Plaintext = plain,
            Ciphertext = ct.Cipher,
            Tag = ct.Tag,
            Kind = "metadata_root"
        };
    }

    public static Av3AeadVector BuildEmptyPlaintextVector()
    {
        var keyLabel = "e12_fixture_empty_key_label";
        var key = Av3AeadKeyMaterialPolicy.DeriveFixtureKey(Encoding.UTF8.GetBytes(keyLabel));
        var aad = Encoding.UTF8.GetBytes("e12_empty_aad_label");
        var nonce = Av3AeadNoncePolicy.FixtureNonce(
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            "e12_empty_nonce_label"u8);
        var ct = Av3XChaCha24Aead.Instance.Encrypt(key, [], aad);
        return new Av3AeadVector
        {
            VectorId = "e12_empty_plaintext",
            KeyLabel = keyLabel,
            SuiteId = Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            Nonce = ct.Nonce,
            Aad = aad,
            Plaintext = [],
            Ciphertext = ct.Cipher,
            Tag = ct.Tag,
            Kind = "empty"
        };
    }

    public static Av3AeadVector BuildMultiSegmentPlaintextVector()
    {
        var keyLabel = "e12_fixture_multiseg_key_label";
        var key = Av3AeadKeyMaterialPolicy.DeriveFixtureKey(Encoding.UTF8.GetBytes(keyLabel));
        var plain = Encoding.UTF8.GetBytes("e12_segment_a|e12_segment_b|e12_segment_c");
        var aad = Encoding.UTF8.GetBytes("e12_multiseg_aad_label");
        var nonce = Av3AeadNoncePolicy.FixtureNonce(
            Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            "e12_multiseg_nonce_label"u8);
        var ct = Av3XChaCha24Aead.Instance.Encrypt(key, plain, aad);
        return new Av3AeadVector
        {
            VectorId = "e12_multi_segment",
            KeyLabel = keyLabel,
            SuiteId = Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24,
            Nonce = ct.Nonce,
            Aad = aad,
            Plaintext = plain,
            Ciphertext = ct.Cipher,
            Tag = ct.Tag,
            Kind = "multi_segment"
        };
    }
}