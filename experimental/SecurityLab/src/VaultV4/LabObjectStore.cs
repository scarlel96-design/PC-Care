using System.Security.Cryptography;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>Random object IDs under objects/ab/….obj + pack fallback via LabPackStore.</summary>
public static class LabObjectStore
{
    public static string NewObjectId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static string AbsolutePath(string vaultRoot, string objectId)
    {
        LabParserGuard.EnsureObjectId(objectId);
        return Path.Combine(vaultRoot, "objects", objectId[..2], objectId + ".obj");
    }

    public static void Write(string vaultRoot, string objectId, byte[] bytes)
    {
        LabParserGuard.EnsureObjectId(objectId);
        LabParserGuard.EnsureObjectSize(bytes.LongLength);
        // Phase4: small blobs go to packfile (concealed intro)
        LabPackStore.Write(vaultRoot, objectId, bytes);
    }

    /// <summary>Force loose object write (tests / migration).</summary>
    public static void WriteLoose(string vaultRoot, string objectId, byte[] bytes)
    {
        LabParserGuard.EnsureObjectId(objectId);
        LabParserGuard.EnsureObjectSize(bytes.LongLength);
        var path = AbsolutePath(vaultRoot, objectId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    public static byte[] Read(string vaultRoot, string objectId)
    {
        LabParserGuard.EnsureObjectId(objectId);
        if (LabPackStore.IsPacked(vaultRoot, objectId))
        {
            return LabPackStore.Read(vaultRoot, objectId);
        }

        return ReadLooseOrThrow(vaultRoot, objectId);
    }

    public static byte[] ReadLooseOrThrow(string vaultRoot, string objectId)
    {
        var path = AbsolutePath(vaultRoot, objectId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("object missing", path);
        }

        var data = File.ReadAllBytes(path);
        LabParserGuard.EnsureObjectSize(data.LongLength);
        return data;
    }

    public static bool TryDelete(string vaultRoot, string objectId)
    {
        try
        {
            var path = AbsolutePath(vaultRoot, objectId);
            if (File.Exists(path))
            {
                SecureShredLoose(path);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void DeleteEverywhere(string vaultRoot, string objectId)
    {
        LabPackStore.Delete(vaultRoot, objectId);
        TryDelete(vaultRoot, objectId);
    }

    private static void SecureShredLoose(string path)
    {
        try
        {
            var len = new FileInfo(path).Length;
            if (len > 0)
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
                var buf = new byte[Math.Min(8192, (int)Math.Min(int.MaxValue, len))];
                RandomNumberGenerator.Fill(buf);
                long rem = len;
                fs.Position = 0;
                while (rem > 0)
                {
                    var n = (int)Math.Min(buf.Length, rem);
                    fs.Write(buf, 0, n);
                    rem -= n;
                }

                fs.SetLength(0);
                fs.Flush(true);
                CryptographicOperations.ZeroMemory(buf);
            }

            File.Delete(path);
        }
        catch
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }
}
