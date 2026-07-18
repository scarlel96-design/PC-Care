using System.Buffers.Binary;
using System.Text;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Canonical AAD for password-slot VMK unwrap (context binding).</summary>
public static class VmkUnwrapAad
{
    public const ushort AadKindVmkUnwrap = 1;
    public static ReadOnlySpan<byte> DomainLabel => "astra-vmk-unwrap"u8;

    public static byte[] Build(ushort formatVersion, Guid containerId, ushort slotId, ulong generation)
    {
        var domain = Encoding.UTF8.GetBytes("astra-vmk-unwrap");
        var buf = new byte[2 + 2 + 2 + 2 + 8 + 16 + domain.Length];
        var span = buf.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span, formatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(2), AadKindVmkUnwrap);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4), slotId);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(6), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(8), generation);
        containerId.TryWriteBytes(span.Slice(16, 16));
        domain.CopyTo(span.Slice(32));
        return buf;
    }
}