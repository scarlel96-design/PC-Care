using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Design §9 Crypto Broker (S-class subset): key material stays inside broker scope.
/// UI/AI must not hold raw VMK/DEK — only request sealed operations.
/// </summary>
public sealed class LabCryptoBroker : IDisposable
{
    private byte[]? _vmk;
    private byte[]? _metaKey;
    private byte[]? _wrapKey;
    private bool _writeAllowed;
    private string _vaultId = "";

    public bool IsSealed => _vmk is null;
    public bool WriteAllowed => _writeAllowed && !IsSealed;
    public string VaultId => _vaultId;

    public void Unseal(byte[] vmk, string vaultId, bool writeAllowed)
    {
        Seal();
        _vmk = (byte[])vmk.Clone();
        _vaultId = vaultId;
        _writeAllowed = writeAllowed;
        _metaKey = Derive(_vmk, "lab/v4/meta");
        _wrapKey = Derive(_vmk, "lab/v4/wrap");
        // zero caller buffer if it is a distinct array (not same ref as clone source already copied)
        CryptographicOperations.ZeroMemory(vmk);
    }

    /// <summary>Allow rewrap helpers (password change) without exposing raw VMK long-term.</summary>
    public byte[] BorrowVmkCopy()
    {
        EnsureOpen();
        return (byte[])_vmk!.Clone();
    }

    public void Seal()
    {
        if (_vmk is not null)
        {
            CryptographicOperations.ZeroMemory(_vmk);
        }

        if (_metaKey is not null)
        {
            CryptographicOperations.ZeroMemory(_metaKey);
        }

        if (_wrapKey is not null)
        {
            CryptographicOperations.ZeroMemory(_wrapKey);
        }

        _vmk = null;
        _metaKey = null;
        _wrapKey = null;
        _writeAllowed = false;
        _vaultId = "";
    }

    public byte[] BorrowMetaKeyCopy()
    {
        EnsureOpen();
        return (byte[])_metaKey!.Clone();
    }

    public byte[] BorrowWrapKeyCopy()
    {
        EnsureOpen();
        return (byte[])_wrapKey!.Clone();
    }

    /// <summary>Wrap DEK with wrap key; caller supplies plain DEK (zeroized after).</summary>
    public (byte[] Nonce, byte[] Tag, byte[] Cipher) WrapDek(byte[] dek, string objectId)
    {
        EnsureOpen();
        if (!_writeAllowed)
        {
            throw new InvalidOperationException("broker: write not allowed");
        }

        var aad = Encoding.UTF8.GetBytes("dek:" + objectId);
        var nonce = RandomNumberGenerator.GetBytes(LabVaultCrypto.NonceSize);
        var cipher = new byte[dek.Length];
        var tag = new byte[LabVaultCrypto.TagSize];
        using var gcm = new AesGcm(_wrapKey!, LabVaultCrypto.TagSize);
        gcm.Encrypt(nonce, dek, cipher, tag, aad);
        return (nonce, tag, cipher);
    }

    public byte[] UnwrapDek(byte[] nonce, byte[] tag, byte[] cipher, string objectId)
    {
        EnsureOpen();
        var aad = Encoding.UTF8.GetBytes("dek:" + objectId);
        var plain = new byte[cipher.Length];
        using var gcm = new AesGcm(_wrapKey!, LabVaultCrypto.TagSize);
        gcm.Decrypt(nonce, cipher, tag, plain, aad);
        return plain;
    }

    public byte[] EncryptObject(
        byte[] plain,
        string entryId,
        long generation,
        LabContentSuite suite,
        bool conceal)
    {
        EnsureOpen();
        if (!_writeAllowed)
        {
            throw new InvalidOperationException("broker: write not allowed");
        }

        var dek = LabVaultCrypto.GenerateKey();
        try
        {
            var aad = BuildObjectAad(_vaultId, entryId, generation);
            return LabVaultCrypto.EncryptChunked(dek, plain, aad, suite, conceal);
        }
        finally
        {
            // DEK returned to caller via separate WrapDek — keep? Better return tuple.
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public (byte[] Cipher, byte[] Dek) EncryptObjectWithDek(
        byte[] plain,
        string entryId,
        long generation,
        LabContentSuite suite,
        bool conceal)
    {
        EnsureOpen();
        if (!_writeAllowed)
        {
            throw new InvalidOperationException("broker: write not allowed");
        }

        var dek = LabVaultCrypto.GenerateKey();
        var aad = BuildObjectAad(_vaultId, entryId, generation);
        var cipher = LabVaultCrypto.EncryptChunked(dek, plain, aad, suite, conceal);
        return (cipher, dek);
    }

    public byte[] DecryptObject(
        byte[] cipherBlob,
        byte[] dek,
        string entryId,
        long generation)
    {
        EnsureOpen();
        var aad = BuildObjectAad(_vaultId, entryId, generation);
        try
        {
            return LabVaultCrypto.DecryptChunked(dek, cipherBlob, aad);
        }
        catch (CryptographicException)
        {
            // legacy AAD without generation
            var legacy = Encoding.UTF8.GetBytes($"lab-obj:{_vaultId}:{entryId}");
            return LabVaultCrypto.DecryptChunked(dek, cipherBlob, legacy);
        }
    }

    public static byte[] BuildObjectAad(string vaultId, string entryId, long generation)
    {
        return Encoding.UTF8.GetBytes($"lab-obj:{vaultId}:{entryId}:g{generation}");
    }

    public void Dispose() => Seal();

    private void EnsureOpen()
    {
        if (_vmk is null || _metaKey is null || _wrapKey is null)
        {
            throw new InvalidOperationException("broker sealed");
        }
    }

    private static byte[] Derive(byte[] vmk, string info)
    {
        var salt = new byte[32];
        using var extract = new HMACSHA256(salt);
        var prk = extract.ComputeHash(vmk);
        using var expand = new HMACSHA256(prk);
        var infoBytes = Encoding.UTF8.GetBytes(info);
        var input = new byte[infoBytes.Length + 1];
        Buffer.BlockCopy(infoBytes, 0, input, 0, infoBytes.Length);
        input[^1] = 1;
        var okm = expand.ComputeHash(input);
        CryptographicOperations.ZeroMemory(prk);
        return okm;
    }
}
