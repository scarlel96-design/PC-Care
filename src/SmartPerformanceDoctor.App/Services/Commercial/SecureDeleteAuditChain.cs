using System.Security.Cryptography;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using SmartPerformanceDoctor.UpdateEngine;

namespace SmartPerformanceDoctor.App.Services.Commercial;

internal static class SecureDeleteAuditChain
{
    private static readonly byte[] KeyMagic = "SPDSDAK1"u8.ToArray();

    public static byte[] EnsureOperationKey(string operationId, string? storageRoot = null)
    {
        var defaultRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PC-Care",
            "secure_delete");
        var keyDir = Path.Combine(storageRoot ?? defaultRoot, "keys");
        PrepareDirectory(keyDir);
        var keyPath = Path.Combine(keyDir, $"{operationId}.key");

        if (File.Exists(keyPath))
        {
            return ReadKey(keyDir, Path.GetFileName(keyPath));
        }

        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            using var file = WindowsFileBoundary.OpenExclusive(keyDir, Path.GetFileName(keyPath), true);
            file.Stream.Write(Protect(key)); file.Stream.Flush(true); file.Validate();
            return key;
        }
        catch { CryptographicOperations.ZeroMemory(key); throw; }
    }

    public static string Append(
        string auditPath,
        string operationId,
        string pathHash,
        string contentHash,
        string protocol,
        string result,
        string previousHash,
        byte[] hmacKey)
    {
        var line = $"{DateTimeOffset.Now:o}|{operationId}|{pathHash}|{contentHash}|{protocol}|{result}|{previousHash}";
        using var hmac = new HMACSHA256(hmacKey);
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(line))).ToLowerInvariant();
        var root = Path.GetDirectoryName(Path.GetFullPath(auditPath))!;
        using var file = WindowsFileBoundary.OpenExclusive(root, Path.GetFileName(auditPath), !File.Exists(auditPath));
        file.Stream.Position = file.Stream.Length;
        file.Stream.Write(Encoding.UTF8.GetBytes($"{line}|{signature}\n")); file.Stream.Flush(true); file.Validate();
        return signature;
    }


    /// <summary>Stores an operation-keyed fingerprint rather than the source file's plain SHA-256.</summary>
    public static string ProtectContentFingerprint(string contentHash, byte[] hmacKey)
    {
        using var hmac = new HMACSHA256(hmacKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(contentHash))).ToLowerInvariant();
    }
    public static bool Verify(string auditPath, byte[] hmacKey)
    {
        if (!File.Exists(auditPath))
        {
            return false;
        }

        using var file = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(Path.GetFullPath(auditPath))!, Path.GetFileName(auditPath));
        if (file.Stream.Length is <= 0 or > 16 * 1024 * 1024) return false;
        using var reader = new StreamReader(file.Stream, Encoding.UTF8, leaveOpen: true);
        var text = reader.ReadToEnd(); file.Validate();
        if (!text.EndsWith('\n')) return false;
        var previous = "GENESIS";
        foreach (var rawLine in text.Split('\n')[..^1])
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                return false;
            }

            var parts = line.Split('|');
            if (parts.Length != 8)
            {
                return false;
            }

            var signature = parts[^1];
            var chainInput = string.Join('|', parts[..^1]);
            if (!string.Equals(parts[^2], previous, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using var hmac = new HMACSHA256(hmacKey);
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(chainInput))).ToLowerInvariant();
            if (!string.Equals(signature, expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            previous = signature;
        }

        return true;
    }

    public static byte[] LoadOperationKey(string operation, string root)
    {
        SecureDeleteJournal.ValidateOperation(operation);
        return ReadKey(Path.Combine(root, "keys"), operation + ".key");
    }

    private static byte[] ReadKey(string root, string name)
    {
        using var file = WindowsFileBoundary.OpenRead(root, name);
        if (file.Stream.Length is <= 0 or > 4096) throw new InvalidDataException("AuditKeySize");
        using var data = new MemoryStream(); file.Stream.CopyTo(data); file.Validate();
        var key = Unprotect(data.ToArray());
        if (key.Length != 32) { CryptographicOperations.ZeroMemory(key); throw new InvalidDataException("AuditKeyLength"); }
        return key;
    }

    public static void PrepareDirectory(string root)
    {
        var created = !Directory.Exists(root);
        var directory = Directory.CreateDirectory(root);
        using var pin = WindowsFileBoundary.PinDirectory(root);
        var sid = WindowsIdentity.GetCurrent().User ?? throw new UnauthorizedAccessException("AuditSidMissing");
        if (created)
        {
            var acl = new DirectorySecurity(); acl.SetOwner(sid); acl.SetAccessRuleProtection(true, false);
            foreach (var principal in new[] {sid, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)})
                acl.AddAccessRule(new FileSystemAccessRule(principal, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            directory.SetAccessControl(acl);
        }
        else
        {
            var acl = directory.GetAccessControl();
            if (acl.GetOwner(typeof(SecurityIdentifier)) != sid) throw new UnauthorizedAccessException("AuditOwnerMismatch");
            foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                if (rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference != sid
                    && rule.IdentityReference.Value is not ("S-1-5-18" or "S-1-5-32-544")
                    && (rule.FileSystemRights & (FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership)) != 0)
                    throw new UnauthorizedAccessException("AuditWritableByOtherUser");
        }
        pin.Validate();
    }

    private static byte[] Protect(byte[] data) =>
        ProtectedData.Protect(data, KeyMagic, DataProtectionScope.CurrentUser);

    private static byte[] Unprotect(byte[] data) =>
        ProtectedData.Unprotect(data, KeyMagic, DataProtectionScope.CurrentUser);
}
