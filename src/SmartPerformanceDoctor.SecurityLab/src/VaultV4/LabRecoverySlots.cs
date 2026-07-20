using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Recovery slots: each one-time code can unwrap a recovery root which unwraps VMK (design §5 Recovery).
/// Codes are stored as hashes only; wraps use Argon2id(LabFast) per slot.
/// </summary>
public static class LabRecoverySlots
{
    private sealed class Store
    {
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

    public static (IReadOnlyList<string> Codes, string Path) GenerateAndWrap(string vaultRoot, string vaultId, byte[] vmk)
    {
        var recoveryRoot = LabVaultCrypto.GenerateKey();
        try
        {
            var aadVmk = Encoding.UTF8.GetBytes("rec-vmk:" + vaultId);
            var vmkWrap = Wrap(recoveryRoot, vmk, aadVmk);
            var salt = LabVaultCrypto.GenerateSalt(16);
            var store = new Store
            {
                SaltHex = Convert.ToHexString(salt),
                VmkWrapNonceHex = Convert.ToHexString(vmkWrap.Nonce),
                VmkWrapTagHex = Convert.ToHexString(vmkWrap.Tag),
                VmkWrapCipherHex = Convert.ToHexString(vmkWrap.Cipher)
            };

            var codes = new List<string>();
            var kdf = LabKdfParams.FromProfile(LabKdfProfile.LabFast);
            for (var i = 0; i < 10; i++)
            {
                var raw = RandomNumberGenerator.GetBytes(8);
                var code =
                    $"{BitConverter.ToUInt16(raw, 0):X4}-{BitConverter.ToUInt16(raw, 2):X4}-{BitConverter.ToUInt16(raw, 4):X4}-{BitConverter.ToUInt16(raw, 6):X4}";
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

            var path = PathOf(vaultRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));
            return (codes, path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recoveryRoot);
        }
    }

    /// <summary>Try one-time recovery unlock → returns VMK copy (caller zeroizes).</summary>
    public static bool TryUnwrapVmk(string vaultRoot, string vaultId, string code, out byte[]? vmk, out string message)
    {
        vmk = null;
        message = "";
        var path = PathOf(vaultRoot);
        if (!File.Exists(path))
        {
            message = "복구 슬롯 없음";
            return false;
        }

        Store store;
        try
        {
            store = JsonSerializer.Deserialize<Store>(File.ReadAllText(path))
                    ?? throw new InvalidOperationException("parse");
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
            }
            finally
            {
                CryptographicOperations.ZeroMemory(recoveryRoot);
            }
        }
        catch (CryptographicException)
        {
            message = "복구 슬롯 복호화 실패";
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(slotSalt);
        }

        store.Slots[match].Used = true;
        File.WriteAllText(path, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));
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
        var path = PathOf(vaultRoot);
        if (!File.Exists(path))
        {
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
                Format = "v5-slots",
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

            if (Exhausted)
            {
                return $"복구코드 소진 ({Used}/{TotalSlots}) · 비밀번호 변경으로 재발급";
            }

            if (LowRemaining)
            {
                return $"복구코드 잔여 {Remaining}/{TotalSlots} · 부족 · 비밀번호 변경 권고";
            }

            return $"복구코드 잔여 {Remaining}/{TotalSlots}";
        }
    }

    private static string PathOf(string vaultRoot) =>
        Path.Combine(vaultRoot, "recovery", "recovery_slots.v5.json");

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
        gcm.Decrypt(nonce, cipher, tag, plain, aad);
        return plain;
    }
}
