using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// S-class stream import matrix: ≥1 MiB streaming path, size reject, export hash, mid-import kill.
/// </summary>
public static class LabStreamImportMatrix
{
    public sealed class CaseResult
    {
        public required string Name { get; init; }
        public required bool ExpectedPass { get; init; }
        public required bool ActualPass { get; init; }
        public string Message { get; init; } = "";
        public bool Pass => ExpectedPass == ActualPass;
    }

    public sealed class Report
    {
        public required IReadOnlyList<CaseResult> Cases { get; init; }
        public int Passed => Cases.Count(c => c.Pass);
        public int Total => Cases.Count;
        public bool AllPass => Cases.All(c => c.Pass);

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"LabStreamImportMatrix {Passed}/{Total} pass");
            foreach (var c in Cases)
            {
                sb.AppendLine($"  {(c.Pass ? "OK" : "FAIL")} {c.Name} expectedPass={c.ExpectedPass} actual={c.ActualPass} · {c.Message}");
            }

            return sb.ToString();
        }
    }

    public static Report Run(string password)
    {
        var cases = new List<CaseResult>
        {
            CaseStreamImportRoundtrip(password),
            CaseMaxSizeReject(password),
            CaseJustUnderStreamThresholdBytesPath(password),
            CaseMidStreamKillRecovery(password)
        };
        return new Report { Cases = cases };
    }

    private static CaseResult CaseStreamImportRoundtrip(string password)
    {
        var root = Path.Combine(Path.GetTempPath(), "lab-stream-" + Guid.NewGuid().ToString("N"));
        // Export must be outside vault root (path policy)
        var work = Path.Combine(Path.GetTempPath(), "lab-stream-work-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(work);
            using var vault = new LabVaultService(root, new Hardening.LabSessionPolicy
            {
                MaxImportBytes = 8L * 1024 * 1024
            });
            if (!vault.Create(password, LabKdfProfile.LabFast).Success
                || !vault.Unlock(password).Success)
            {
                return Fail("stream-import-roundtrip", "create/unlock failed");
            }

            // Force streaming path (>= 1 MiB)
            var src = Path.Combine(work, "_src_1_5m.bin");
            var size = (1024 * 1024) + (512 * 1024);
            WritePatternFile(src, size);
            var expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(src)));
            var imp = vault.ImportFile(src);
            if (!imp.Success)
            {
                return Fail("stream-import-roundtrip", imp.Message);
            }

            var outDir = Path.Combine(work, "_out");
            Directory.CreateDirectory(outDir);
            var exp = vault.ExportEntry(imp.EntryId!, outDir);
            if (!exp.Success)
            {
                return Fail("stream-import-roundtrip", exp.Message);
            }

            var exported = Directory.GetFiles(outDir).Select(File.ReadAllBytes).Single();
            var actualHash = Convert.ToHexString(SHA256.HashData(exported));
            var ok = string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase)
                     && exported.Length == size;
            return R("stream-import-roundtrip", true, ok, ok ? $"stream {size} ok" : "hash/size mismatch");
        }
        catch (Exception ex)
        {
            return Fail("stream-import-roundtrip", ex.Message);
        }
        finally
        {
            SafeDelete(root);
            SafeDelete(work);
        }
    }

    private static CaseResult CaseMaxSizeReject(string password)
    {
        var root = Path.Combine(Path.GetTempPath(), "lab-stream-max-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var vault = new LabVaultService(root, new Hardening.LabSessionPolicy
            {
                MaxImportBytes = 64 * 1024 // tiny ceiling
            });
            if (!vault.Create(password, LabKdfProfile.LabFast).Success
                || !vault.Unlock(password).Success)
            {
                return Fail("max-size-reject", "create/unlock failed");
            }

            var src = Path.Combine(root, "_big.bin");
            WritePatternFile(src, 128 * 1024);
            var imp = vault.ImportFile(src);
            var ok = !imp.Success && imp.Message.Contains("한도", StringComparison.Ordinal);
            return R("max-size-reject", true, ok, imp.Message);
        }
        catch (Exception ex)
        {
            return Fail("max-size-reject", ex.Message);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    private static CaseResult CaseJustUnderStreamThresholdBytesPath(string password)
    {
        var root = Path.Combine(Path.GetTempPath(), "lab-stream-small-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var vault = new LabVaultService(root);
            if (!vault.Create(password, LabKdfProfile.LabFast).Success
                || !vault.Unlock(password).Success)
            {
                return Fail("under-1mib-bytes-path", "create/unlock failed");
            }

            // < 1 MiB uses ImportBytes path (not streaming file)
            var src = Path.Combine(root, "_small.bin");
            WritePatternFile(src, 200 * 1024);
            var imp = vault.ImportFile(src);
            if (!imp.Success)
            {
                return Fail("under-1mib-bytes-path", imp.Message);
            }

            var verify = vault.VerifyAllContentHashes();
            return R("under-1mib-bytes-path", true, verify.Success, verify.Message);
        }
        catch (Exception ex)
        {
            return Fail("under-1mib-bytes-path", ex.Message);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    /// <summary>Simulate crash after stream object write (ObjectsReady) before metadata commit.</summary>
    private static CaseResult CaseMidStreamKillRecovery(string password)
    {
        var root = Path.Combine(Path.GetTempPath(), "lab-stream-kill-" + Guid.NewGuid().ToString("N"));
        string? fakeId = null;
        try
        {
            using (var v = new LabVaultService(root))
            {
                if (!v.Create(password, LabKdfProfile.LabFast).Success
                    || !v.Unlock(password).Success)
                {
                    return Fail("mid-stream-kill-recovery", "create/unlock failed");
                }

                fakeId = LabObjectStore.NewObjectId();
                // partial stream cipher stub
                LabObjectStore.WriteLoose(root, fakeId, new byte[64 * 1024]);
                var tx = LabVaultJournal.Begin(root, "stream-import:killed.bin", 1);
                LabVaultJournal.Mark(root, tx, LabVaultJournal.State.ObjectsReady, "obj:" + fakeId, 1);
                v.Lock();
            }

            using var reopen = new LabVaultService(root);
            var open = reopen.Unlock(password);
            var incomplete = LabVaultJournal.ListIncomplete(root);
            var orphanGone = fakeId is not null && !File.Exists(LabObjectStore.AbsolutePath(root, fakeId));
            var pass = open.Success && incomplete.Count == 0 && orphanGone;
            return R(
                "mid-stream-kill-recovery",
                true,
                pass,
                open.Message + " incomplete=" + incomplete.Count + " orphanGone=" + orphanGone);
        }
        catch (Exception ex)
        {
            return Fail("mid-stream-kill-recovery", ex.Message);
        }
        finally
        {
            SafeDelete(root);
        }
    }

    private static void WritePatternFile(string path, int size)
    {
        var buf = new byte[Math.Min(size, 64 * 1024)];
        for (var i = 0; i < buf.Length; i++)
        {
            buf[i] = (byte)(i * 31 + 7);
        }

        using var fs = File.Create(path);
        var left = size;
        while (left > 0)
        {
            var n = Math.Min(left, buf.Length);
            fs.Write(buf, 0, n);
            left -= n;
        }
    }

    private static CaseResult Fail(string name, string msg) =>
        R(name, true, false, msg);

    private static CaseResult R(string name, bool expected, bool actual, string msg) =>
        new() { Name = name, ExpectedPass = expected, ActualPass = actual, Message = msg };

    private static void SafeDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
        catch
        {
            // ignore
        }
    }
}
