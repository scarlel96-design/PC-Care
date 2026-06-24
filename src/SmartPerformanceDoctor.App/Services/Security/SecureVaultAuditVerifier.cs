using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.App.Services.Security;

public sealed class SecureVaultAuditVerificationResult
{
    public bool IsValid { get; init; }
    public int VerifiedEntries { get; init; }
    public int BrokenAtIndex { get; init; } = -1;
    public string Message { get; init; } = "";
}

internal static class SecureVaultAuditVerifier
{
    private const int RecordOverhead = SecureVaultCrypto.NonceSize + SecureVaultCrypto.TagSize;

    public static SecureVaultAuditVerificationResult Verify(byte[] metadataKey, byte[] macKey)
    {
        if (!File.Exists(SecureVaultPaths.AuditLogFile))
        {
            return new SecureVaultAuditVerificationResult
            {
                IsValid = true,
                VerifiedEntries = 0,
                Message = "감사 로그 없음"
            };
        }

        var bytes = File.ReadAllBytes(SecureVaultPaths.AuditLogFile);
        if (bytes.Length == 0)
        {
            return new SecureVaultAuditVerificationResult
            {
                IsValid = true,
                VerifiedEntries = 0,
                Message = "감사 로그 비어 있음"
            };
        }

        var offset = 0;
        var index = 0;
        var previousHash = "GENESIS";

        while (offset + RecordOverhead < bytes.Length)
        {
            var nonce = bytes.AsSpan(offset, SecureVaultCrypto.NonceSize).ToArray();
            offset += SecureVaultCrypto.NonceSize;
            var tag = bytes.AsSpan(offset, SecureVaultCrypto.TagSize).ToArray();
            offset += SecureVaultCrypto.TagSize;

            int cipherLength;
            if (offset + 4 <= bytes.Length)
            {
                var prefixedLength = BitConverter.ToInt32(bytes, offset);
                if (prefixedLength > 0 && offset + 4 + prefixedLength <= bytes.Length)
                {
                    offset += 4;
                    cipherLength = prefixedLength;
                }
                else
                {
                    cipherLength = bytes.Length - offset;
                }
            }
            else
            {
                cipherLength = bytes.Length - offset;
            }

            if (cipherLength <= 0)
            {
                return Broken(index, "감사 레코드 형식 오류");
            }

            var cipher = bytes.AsSpan(offset, cipherLength).ToArray();
            offset += cipherLength;

            try
            {
                var plaintext = SecureVaultCrypto.Decrypt(metadataKey, new EncryptedBlob(cipher, nonce, tag));
                var line = Encoding.UTF8.GetString(plaintext);
                var parts = line.Split('|');
                if (parts.Length < 5)
                {
                    return Broken(index, "감사 레코드 필드 부족");
                }

                var embeddedHash = parts[^1];
                var chainInput = string.Join('|', parts[..^1]);
                using var hmac = new HMACSHA256(macKey);
                var expectedHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(chainInput))).ToLowerInvariant();

                if (!string.Equals(embeddedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return Broken(index, "HMAC 불일치");
                }

                var previousInRecord = parts[^2];
                if (!string.Equals(previousInRecord, previousHash, StringComparison.OrdinalIgnoreCase))
                {
                    return Broken(index, "체인 연결 끊김");
                }

                previousHash = embeddedHash;
                index++;
            }
            catch
            {
                return Broken(index, "복호화 실패");
            }
        }

        return new SecureVaultAuditVerificationResult
        {
            IsValid = true,
            VerifiedEntries = index,
            Message = $"감사 체인 {index}건 검증 완료"
        };
    }

    private static SecureVaultAuditVerificationResult Broken(int index, string reason) =>
        new()
        {
            IsValid = false,
            VerifiedEntries = index,
            BrokenAtIndex = index,
            Message = $"감사 체인 손상 (레코드 {index}): {reason}"
        };
}