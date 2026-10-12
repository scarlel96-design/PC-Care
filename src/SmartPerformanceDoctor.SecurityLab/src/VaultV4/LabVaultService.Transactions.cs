using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

public sealed partial class LabVaultService
{
    private readonly object _operationGate = new();
    private IDisposable EnterOperation()
    {
        Monitor.Enter(_operationGate);
        return new OperationLease(_operationGate);
    }
    private sealed class OperationLease(object gate) : IDisposable
    {
        public void Dispose() => Monitor.Exit(gate);
    }

    private void WriteGenerationObject(string objectId, byte[] bytes)
    {
        var path = LabObjectStore.AbsolutePath(_root, objectId);
        using var parent = VaultFileBoundary.PinDirectory(Path.GetDirectoryName(path)!, create: true);
        using var file = VaultFileBoundary.OpenExclusive(Path.GetDirectoryName(path)!, Path.GetFileName(path), true);
        file.Stream.Write(bytes); file.Stream.Flush(true); file.Validate(); parent.Validate();
    }

    private void DecryptEntryToStream(LabMetaEntry entry, Stream output, CancellationToken token)
    {
        var key = _broker.UnwrapDek(Convert.FromHexString(entry.DekNonceHex), Convert.FromHexString(entry.DekTagHex),
            Convert.FromHexString(entry.DekCipherHex), entry.ObjectId);
        try
        {
            void Decrypt(Stream input)
            {
                var generation = entry.ContentGeneration;
                var aad = generation > 0 ? LabCryptoBroker.BuildObjectAad(_header!.VaultId, entry.EntryId, generation)
                    : Encoding.UTF8.GetBytes($"lab-obj:{_header!.VaultId}:{entry.EntryId}");
                try { LabVaultCrypto.DecryptChunkedFromFile(key, input, output, aad, token); }
                catch (CryptographicException) when (generation > 0 && _header!.Version != 6)
                {
                    input.Position = 0; output.SetLength(0); output.Position = 0;
                    LabVaultCrypto.DecryptChunkedFromFile(key, input, output,
                        Encoding.UTF8.GetBytes($"lab-obj:{_header.VaultId}:{entry.EntryId}"), token);
                }
            }
            if (LabPackStore.IsPacked(_root, entry.ObjectId))
            {
                var blob = LabPackStore.Read(_root, entry.ObjectId, LabPackStore.PackThresholdBytes);
                if (blob.Length > LabPackStore.PackThresholdBytes) throw new CryptographicException("packed export exceeds budget");
                using var input = new MemoryStream(blob, false); Decrypt(input);
            }
            else
            {
                var path = LabObjectStore.AbsolutePath(_root, entry.ObjectId);
                using var parent = VaultFileBoundary.PinDirectory(Path.GetDirectoryName(path)!);
                using var file = VaultFileBoundary.OpenRead(Path.GetDirectoryName(path)!, Path.GetFileName(path));
                Decrypt(file.Stream); file.Validate(); parent.Validate();
            }
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private void CommitGeneration()
    {
        if (_header!.Version != 6)
        {
            LabDurableCommit.WriteCommitted(_root, _header.VaultId, _metadata!.Generation);
            return;
        }
        if (_preparedGeneration is null) return;
        LabAuthenticatedGeneration.Publish(_root, _preparedGeneration, _vmk!, FaultObserver);
        _selectedGeneration = LabAuthenticatedGeneration.Candidates(_root).First();
        _preparedGeneration = null;
    }

    private LabVaultOperationResult ChangeGenerationPassword(string currentPassword, string newPassword)
    {
        var policy = LabPasswordPolicy.ValidateForCreate(newPassword);
        if (!policy.IsValid) return FailOp(policy.Message);
        var header = _header!;
        var salt = Convert.FromHexString(header.SaltHex);
        var oldKek = LabVaultCrypto.DeriveArgon2id(currentPassword, salt, header.KdfIterations, header.KdfMemoryKb, header.KdfParallelism);
        try
        {
            var check = UnwrapKey(oldKek, new Wrapped(Convert.FromHexString(header.WrappedVmkNonceHex),
                Convert.FromHexString(header.WrappedVmkTagHex), Convert.FromHexString(header.WrappedVmkCipherHex)),
                Encoding.UTF8.GetBytes("vmk:" + header.VaultId));
            try { if (!LabCryptoCompare.FixedTimeEquals(check, _vmk!)) return FailOp("현재 비밀번호가 올바르지 않습니다."); }
            finally { CryptographicOperations.ZeroMemory(check); }
        }
        catch (CryptographicException) { return FailOp("현재 비밀번호가 올바르지 않습니다."); }
        finally { CryptographicOperations.ZeroMemory(oldKek); CryptographicOperations.ZeroMemory(salt); }

        FaultObserver?.Invoke("PasswordKdfPrepared");
        var newSalt = LabVaultCrypto.GenerateSalt();
        FaultObserver?.Invoke("PasswordSaltPrepared");
        var newKek = LabVaultCrypto.DeriveArgon2id(newPassword, newSalt, header.KdfIterations, header.KdfMemoryKb, header.KdfParallelism);
        var saved = JsonSerializer.SerializeToUtf8Bytes(header);
        try
        {
            var wrap = WrapKey(newKek, _vmk!, Encoding.UTF8.GetBytes("vmk:" + header.VaultId));
            FaultObserver?.Invoke("PasswordWrapPrepared");
            header.SaltHex = Convert.ToHexString(newSalt);
            header.WrappedVmkNonceHex = Convert.ToHexString(wrap.Nonce);
            header.WrappedVmkTagHex = Convert.ToHexString(wrap.Tag);
            header.WrappedVmkCipherHex = Convert.ToHexString(wrap.Cipher);
            FaultObserver?.Invoke("PasswordHeaderPrepared");
            WriteMetadata(_metadata!, _metaKey!);
            CommitGeneration();
            FaultObserver?.Invoke("PasswordCommitted");
            // Recovery slots wrap the unchanged VMK. Keep them until an explicit rotation transaction exists.
            return new LabVaultOperationResult { Success = true, Message = "비밀번호가 변경되었습니다. 기존 복구 코드는 유지됩니다." };
        }
        catch
        {
            _header = JsonSerializer.Deserialize<LabVaultHeader>(saved)!;
            _writeAllowed = false;
            _securityState = LabSecurityState.RecoveryAvailable;
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(newKek); CryptographicOperations.ZeroMemory(newSalt); }
    }
}
