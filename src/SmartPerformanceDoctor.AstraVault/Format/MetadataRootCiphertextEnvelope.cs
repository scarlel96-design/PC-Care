using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Format;

/// <summary>metadata.root.enc on-disk layout: 256-byte descriptor + ciphertext bytes.</summary>
public sealed class MetadataRootCiphertextEnvelope
{
    public MetadataRootDescriptor Descriptor { get; init; } = null!;
    public byte[] Ciphertext { get; init; } = [];

    public static MetadataRootCiphertextEnvelope Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < MetadataRootDescriptor.DescriptorSize)
        {
            throw new CryptographicException("Metadata root envelope truncated.");
        }

        var descriptor = MetadataRootDescriptor.Parse(data.Slice(0, MetadataRootDescriptor.DescriptorSize));
        if (descriptor.CiphertextLength == 0)
        {
            throw new CryptographicException("Metadata root ciphertext length must be non-zero.");
        }

        if (descriptor.CiphertextLength > MetadataRootDescriptor.MaxCiphertextLength)
        {
            throw new CryptographicException("Metadata root ciphertext oversized.");
        }

        if (descriptor.CiphertextLength != MetadataRootPlaintext.PlaintextSize)
        {
            throw new CryptographicException("Metadata root ciphertext length invalid.");
        }

        var expectedTotal = MetadataRootDescriptor.DescriptorSize + (int)descriptor.CiphertextLength;
        if (data.Length != expectedTotal)
        {
            throw new CryptographicException("Metadata root envelope trailing bytes or truncation.");
        }

        return new MetadataRootCiphertextEnvelope
        {
            Descriptor = descriptor,
            Ciphertext = data.Slice(MetadataRootDescriptor.DescriptorSize, (int)descriptor.CiphertextLength).ToArray()
        };
    }
}