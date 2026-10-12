using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Recovery slots: each one-time code can unwrap a recovery root which unwraps VMK (design §5 Recovery).
/// Codes are stored as hashes only; new wraps use a 128-bit random code and
/// product-grade Argon2id parameters per slot. Existing slots retain their
/// persisted parameters for backward-compatible recovery.
/// </summary>
public static class LabRecoverySlots
{
    private sealed class Store
    {
        public int SchemaVersion { get; set; }
        public string VaultId { get; set; } = "";
        public string MacHex { get; set; } = "";
        public string SaltHex { get; set; } = "";
        public string VmkWrapNonceHex { get; set; } = "";
        public string VmkWrapTagHex { get; set; } = "";
        public string VmkWrapCipherHex { get; set; } = "";
        public List<Slot> Slots { get; set; } = new();
    }

    private sealed class Slot
    {
        public string HashHex { get; set; } = "";
        public string SaltHex { get; set; } = "";
        public int Iterations { get; set; }
        public int MemoryKb { get; set; }
        public int Parallelism { get; set; }
        public string NonceHex { get; set; } = "";
        public string TagHex { get; set; } = "";
        public string CipherHex { get; set; } = "";
        public bool Used { get; set; }
    }

    public static (IReadOnlyList<string> Codes, string Path) GenerateAndWrap(string vaultRoot, string vaultId, byte[] vmk,
        bool authenticatedTestFormat = false)
    {
        var recoveryRoot = LabVaultCrypto.GenerateKey();
        try
        {
            var aadVmk = Encoding.UTF8.GetBytes("rec-vmk:" + vaultId);
            var vmkWrap = Wrap(recoveryRoot, vmk, aadVmk);
            var salt = LabVaultCrypto.GenerateSalt(16);
            var store = new Store
            {
                SchemaVersion = authenticatedTestFormat ? 6 : 0,
                VaultId = authenticatedTestFormat ? vaultId : "",
                SaltHex = Convert.ToHexString(salt),
                VmkWrapNonceHex = Convert.ToHexString(vmkWrap.Nonce),
                VmkWrapTagHex = Convert.ToHexString(vmkWrap.Tag),
                VmkWrapCipherHex = Convert.ToHexString(vmkWrap.Cipher)
            };

            var codes = new List<string>();
            var kdf = LabKdfParams.FromProfile(LabKdfProfile.Balanced);
            for (var i = 0; i < 10; i++)
            {
                var raw = RandomNumberGenerator.GetBytes(16);
                var code = string.Join("-", Enumerable.Range(0, 8)
                    .Select(offset => BitConverter.ToUInt16(raw, offset * 2).ToString("X4")));
                CryptographicOperations.ZeroMemory(raw);
                codes.Add(code);
                var slotSalt = LabVaultCrypto.GenerateSalt(16);
                var kek = LabVaultCrypto.DeriveArgon2id(code, slotSalt, kdf.Iterations, kdf.MemoryKb, kdf.Parallelism);
                try
                {
                    var aad = Encoding.UTF8.GetBytes($"rec-slot:{vaultId}:{i}");
                    var wrapped = Wrap(kek, recoveryRoot, aad);
                    store.Slots.Add(new Slot
                    {
                        HashHex = HashCode(salt, code),
                        SaltHex = Convert.ToHexString(slotSalt),
                        Iterations = kdf.Iterations,
                        MemoryKb = kdf.MemoryKb,
                        Parallelism = kdf.Parallelism,
                        NonceHex = Convert.ToHexString(wrapped.Nonce),
                        TagHex = Convert.ToHexString(wrapped.Tag),
                        CipherHex = Convert.ToHexString(wrapped.Cipher),
                        Used = false
                    });
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(kek);
                }
            }

            var path = PathOf(vaultRoot, authenticatedTestFormat);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (authenticatedTestFormat)
            {
                store.MacHex = SignStore(store, vmk);
                WriteAuthenticatedStore(path, store, vmk);
            }
            else File.WriteAllText(path, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));
            return (codes, path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recoveryRoot);
        }
    }

    /// <summary>Try one-time recovery unlock → returns VMK copy (caller zeroizes).</summary>
    public static bool TryUnwrapVmk(string vaultRoot, string vaultId, string code, out byte[]? vmk, out string message,
        bool consume = true, Action<string>? faultObserver = null, bool requireAuthenticated = false)
    {
        vmk = null;
        message = "";
        var authenticated = requireAuthenticated || Directory.Exists(LabAuthenticatedGeneration.DirectoryPath(vaultRoot));
        var path = PathOf(vaultRoot, authenticated);
        if (!File.Exists(path))
        {
            message = "복구 슬롯 없음";
            return false;
        }

        VaultExportWriter.ValidateAncestors(Path.GetDirectoryName(path)!);
        using var recoveryLease = VaultFileBoundary.OpenExclusive(Path.GetDirectoryName(path)!, "consume.lock",
            !File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "consume.lock")));
        Store store;
        try
        {
            using var file = VaultFileBoundary.OpenRead(Path.GetDirectoryName(path)!, Path.GetFileName(path));
            if (file.Stream.Length > 1024 * 1024) throw new InvalidDataException("recovery store exceeds budget");
            store = JsonSerializer.Deserialize<Store>(file.Stream)
                    ?? throw new InvalidOperationException("parse");
            ValidateStore(store, authenticated, vaultId);
            file.Validate();
        }
        catch
        {
            message = "복구 저장소 손상";
            return false;
        }

        byte[] masterSalt;
        try
        {
            masterSalt = Convert.FromHexString(store.SaltHex);
        }
        catch
        {
            message = "복구 salt 손상";
            return false;
        }

        var codeNorm = (code ?? "").Trim();
        if (codeNorm.Length == 0)
        {
            message = "복구 코드 비어 있음";
            return false;
        }

        var candidate = HashCode(masterSalt, codeNorm);
        var match = -1;
        for (var i = 0; i < store.Slots.Count; i++)
        {
            var s = store.Slots[i];
            if (s.Used)
            {
                _ = LabCryptoCompare.FixedTimeEqualsHex(s.HashHex, candidate);
                continue;
            }

            if (LabCryptoCompare.FixedTimeEqualsHex(s.HashHex, candidate))
            {
                match = i;
            }
        }

        if (match < 0)
        {
            message = "복구 코드가 올바르지 않거나 이미 사용됨";
            return false;
        }

        var slot = store.Slots[match];
        byte[] slotSalt;
        try
        {
            slotSalt = Convert.FromHexString(slot.SaltHex);
        }
        catch
        {
            message = "슬롯 salt 손상";
            return false;
        }

        var kek = LabVaultCrypto.DeriveArgon2id(
            codeNorm,
            slotSalt,
            slot.Iterations,
            slot.MemoryKb,
            slot.Parallelism);
        try
        {
            var aad = Encoding.UTF8.GetBytes($"rec-slot:{vaultId}:{match}");
            var recoveryRoot = Unwrap(
                kek,
                Convert.FromHexString(slot.NonceHex),
                Convert.FromHexString(slot.TagHex),
                Convert.FromHexString(slot.CipherHex),
                aad);
            try
            {
                var aadVmk = Encoding.UTF8.GetBytes("rec-vmk:" + vaultId);
                vmk = Unwrap(
                    recoveryRoot,
                    Convert.FromHexString(store.VmkWrapNonceHex),
                    Convert.FromHexString(store.VmkWrapTagHex),
                    Convert.FromHexString(store.VmkWrapCipherHex),
                    aadVmk);
                if (authenticated) VerifyStore(store, vmk);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(recoveryRoot);
            }
        }
        catch (CryptographicException)
        {
            if (vmk is not null) CryptographicOperations.ZeroMemory(vmk);
            vmk = null;
            message = "복구 슬롯 복호화 실패";
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(slotSalt);
        }

        try
        {
        if (consume)
        {
            store.Slots[match].Used = true;
            if (authenticated)
            {
                store.MacHex = SignStore(store, vmk!);
                WriteAuthenticatedStore(path, store, vmk!, faultObserver);
            }
            else
            {
            var directory = Path.GetDirectoryName(path)!;
            using var parent = VaultFileBoundary.PinDirectory(directory);
            var temporary = Guid.NewGuid().ToString("N") + ".recovery.tmp";
            using (var file = VaultFileBoundary.OpenWrite(directory, temporary, true))
            {
                file.Stream.Write(JsonSerializer.SerializeToUtf8Bytes(store)); file.Stream.Flush(true); file.Validate();
            }
            parent.Validate();
            File.Replace(Path.Combine(directory, temporary), path, path + ".previous", false);
            parent.Validate();
            }
        }
        }
        catch
        {
            if (vmk is not null) CryptographicOperations.ZeroMemory(vmk);
            vmk = null;
            throw;
        }
        message = "복구 코드로 금고 키를 복원했습니다(일회용).";
        return true;
    }

    public static int Remaining(string vaultRoot)
    {
        var snap = Snapshot(vaultRoot);
        return snap.Remaining;
    }

    /// <summary>S-class recovery posture for UI / session labels.</summary>
    public static RecoverySnapshot Snapshot(string vaultRoot)
    {
        var authenticated = Directory.Exists(LabAuthenticatedGeneration.DirectoryPath(vaultRoot));
        var path = PathOf(vaultRoot, authenticated);
        if (!File.Exists(path))
        {
            if (authenticated) return new RecoverySnapshot { StorePresent = true, Format = "corrupt" };
            var legacy = LabRecoveryCodes.Remaining(vaultRoot);
            return new RecoverySnapshot
            {
                StorePresent = legacy > 0,
                Format = legacy > 0 ? "legacy-v4-hash" : "none",
                TotalSlots = legacy,
                Remaining = legacy,
                Used = 0
            };
        }

        try
        {
            var store = JsonSerializer.Deserialize<Store>(File.ReadAllText(path));
            if (store?.Slots is null || store.Slots.Count == 0)
            {
                return new RecoverySnapshot { StorePresent = true, Format = "v5-slots", TotalSlots = 0, Remaining = 0, Used = 0 };
            }

            var used = store.Slots.Count(s => s.Used);
            var rem = store.Slots.Count - used;
            return new RecoverySnapshot
            {
                StorePresent = true,
                Format = authenticated ? "v6-authenticated-slots-unverified" : "v5-slots",
                TotalSlots = store.Slots.Count,
                Remaining = rem,
                Used = used
            };
        }
        catch
        {
            return new RecoverySnapshot
            {
                StorePresent = true,
                Format = "corrupt",
                TotalSlots = 0,
                Remaining = 0,
                Used = 0
            };
        }
    }

    public sealed class RecoverySnapshot
    {
        public bool StorePresent { get; init; }
        public string Format { get; init; } = "none";
        public int TotalSlots { get; init; }
        public int Remaining { get; init; }
        public int Used { get; init; }
        public bool LowRemaining => Remaining is > 0 and <= 3;
        public bool Exhausted => StorePresent && Remaining == 0 && Format != "none";
        public bool Healthy => Remaining >= 4 && Format == "v5-slots";

        public string ToUiLine()
        {
            if (!StorePresent || Format == "none")
            {
                return "복구코드 없음 · 생성 시 10개 발급";
            }

            if (Format == "corrupt")
            {
                return "복구 저장소 손상 · 비밀번호로만 열기";
            }

            if (Format == "legacy-v4-hash")
            {
                return $"레거시 복구 증명 {Remaining}개 (VMK 해제 불가)";
            }

            if (Format == "v6-authenticated-slots-unverified")
                return $"인증형 복구코드 {Remaining}/{TotalSlots} · 잠금 해제 시 인증 확인";

            if (Exhausted)
            {
                return $"복구코드 소진 ({Used}/{TotalSlots}) · 명시적 복구 코드 재발급 필요";
            }

            if (LowRemaining)
            {
                return $"복구코드 잔여 {Remaining}/{TotalSlots} · 부족 · 복구 코드 정책 확인";
            }

            return $"복구코드 잔여 {Remaining}/{TotalSlots}";
        }
    }

    private static string PathOf(string vaultRoot, bool authenticated = false) =>
        Path.Combine(vaultRoot, "recovery", authenticated ? "recovery_slots.v6.json" : "recovery_slots.v5.json");

    private static bool IsHex(string? value, int length) =>
        value is not null && value.Length == length && value.All(Uri.IsHexDigit);

    private static void ValidateStore(Store store, bool authenticated, string vaultId)
    {
        if (store.Slots is null || store.Slots.Count != 10 || !IsHex(store.SaltHex, 32)
            || !IsHex(store.VmkWrapNonceHex, 24) || !IsHex(store.VmkWrapTagHex, 32)
            || !IsHex(store.VmkWrapCipherHex, 64))
            throw new InvalidDataException("invalid recovery slot count or wrapping contract");
        if (authenticated && (store.SchemaVersion != 6 || store.VaultId != vaultId || !IsHex(store.MacHex, 64)))
            throw new InvalidDataException("invalid authenticated recovery identity");
        var balanced = LabKdfParams.FromProfile(LabKdfProfile.Balanced);
        foreach (var slot in store.Slots)
        {
            if (slot is null || !IsHex(slot.HashHex, 64) || !IsHex(slot.SaltHex, 32) || !IsHex(slot.NonceHex, 24)
                || !IsHex(slot.TagHex, 32) || !IsHex(slot.CipherHex, 64)
                || slot.Iterations is < 1 or > 20 || slot.MemoryKb is < 8 or > 1048576
                || slot.Parallelism is < 1 or > 64)
                throw new InvalidDataException("invalid recovery slot contract");
            // Bound unauthenticated KDF input before deriving the authentication key.
            if (authenticated && (slot.Iterations != balanced.Iterations || slot.MemoryKb != balanced.MemoryKb
                || slot.Parallelism != balanced.Parallelism))
                throw new InvalidDataException("unsupported authenticated recovery KDF");
        }
    }

    private static string SignStore(Store store, byte[] vmk)
    {
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, vmk, 32, info: "lab/v6/recovery-slots"u8.ToArray());
        var mac = store.MacHex;
        try
        {
            store.MacHex = "";
            return Convert.ToHexString(HMACSHA256.HashData(key, JsonSerializer.SerializeToUtf8Bytes(store)));
        }
        finally { store.MacHex = mac; CryptographicOperations.ZeroMemory(key); }
    }

    private static void VerifyStore(Store store, byte[] vmk)
    {
        if (!LabCryptoCompare.FixedTimeEqualsHex(store.MacHex, SignStore(store, vmk)))
            throw new CryptographicException("recovery store authentication failed");
    }

    private static void WriteAuthenticatedStore(string path, Store store, byte[] vmk, Action<string>? fault = null)
    {
        var directory = Path.GetDirectoryName(path)!;
        using var parent = VaultFileBoundary.PinDirectory(directory);
        VerifyStore(store, vmk);
        var temporary = Guid.NewGuid().ToString("N") + ".recovery.tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(store);
        using (var file = VaultFileBoundary.OpenWrite(directory, temporary, true))
        {
            fault?.Invoke("RecoveryTemporaryCreated");
            file.Stream.Write(bytes);
            fault?.Invoke("RecoveryDataWritten");
            file.Stream.Flush(true);
            file.Verify(bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        fault?.Invoke("RecoveryBeforeReplace");
        parent.Validate();
        if (File.Exists(path)) File.Replace(Path.Combine(directory, temporary), path, path + ".previous", false);
        else File.Move(Path.Combine(directory, temporary), path, false);
        fault?.Invoke("RecoveryAfterReplace");
        parent.Validate();
        using var published = VaultFileBoundary.OpenRead(directory, Path.GetFileName(path));
        if (published.Stream.Length > 1024 * 1024) throw new InvalidDataException("recovery store exceeds budget");
        var verified = JsonSerializer.Deserialize<Store>(published.Stream) ?? throw new InvalidDataException("empty recovery store");
        ValidateStore(verified, true, store.VaultId);
        VerifyStore(verified, vmk); published.Validate();
    }

    private static string HashCode(byte[] salt, string code)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        h.AppendData("pccare/lab/recovery/v5"u8.ToArray());
        h.AppendData(salt);
        h.AppendData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant()));
        return Convert.ToHexString(h.GetHashAndReset());
    }

    private static (byte[] Nonce, byte[] Tag, byte[] Cipher) Wrap(byte[] key, byte[] plain, byte[] aad)
    {
        var nonce = RandomNumberGenerator.GetBytes(LabVaultCrypto.NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[LabVaultCrypto.TagSize];
        using var gcm = new AesGcm(key, LabVaultCrypto.TagSize);
        gcm.Encrypt(nonce, plain, cipher, tag, aad);
        return (nonce, tag, cipher);
    }

    private static byte[] Unwrap(byte[] key, byte[] nonce, byte[] tag, byte[] cipher, byte[] aad)
    {
        var plain = new byte[cipher.Length];
        using var gcm = new AesGcm(key, LabVaultCrypto.TagSize);
        try { gcm.Decrypt(nonce, cipher, tag, plain, aad); return plain; }
        catch { CryptographicOperations.ZeroMemory(plain); throw; }
    }
}
