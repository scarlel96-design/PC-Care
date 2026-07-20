using System.Security.Cryptography;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>Temp files with best-effort secure wipe on dispose.</summary>
public sealed class LabSecureTempDir : IDisposable
{
    public string Path { get; }

    public LabSecureTempDir(string? prefix = null)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            (prefix ?? "seclab") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        try
        {
            if (!Directory.Exists(Path))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                SecureWipeFile(file);
            }

            Directory.Delete(Path, true);
        }
        catch
        {
            // best effort
        }
    }

    public static void SecureWipeFile(string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                return;
            }

            var len = new FileInfo(file).Length;
            if (len > 0)
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.None);
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

            File.Delete(file);
        }
        catch
        {
            try { File.Delete(file); } catch { /* ignore */ }
        }
    }
}
