using System.Text;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// S-class fault-injection matrix (design §14/§16 subset).
/// Runs deterministic torn/corrupt + kill/crash scenarios and records expected vs actual open outcomes.
/// </summary>
public static class LabFaultMatrix
{
    public sealed class CaseResult
    {
        public required string Name { get; init; }
        public required bool ExpectedOpen { get; init; }
        public required bool ActualOpen { get; init; }
        public string Message { get; init; } = "";
        public bool Pass => ExpectedOpen == ActualOpen;
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
            sb.AppendLine($"LabFaultMatrix {Passed}/{Total} pass");
            foreach (var c in Cases)
            {
                sb.AppendLine($"  {(c.Pass ? "OK" : "FAIL")} {c.Name} expectedOpen={c.ExpectedOpen} actual={c.ActualOpen} · {c.Message}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Clone vault files to temp, apply fault, try unlock. Source vault must be closed.
    /// </summary>
    public static Report Run(string sourceVaultRoot, string password)
    {
        var results = new List<CaseResult>();
        foreach (var (name, mode, expectedOpen) in Scenarios())
        {
            var tmp = Path.Combine(Path.GetTempPath(), "lab-fi-" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyVault(sourceVaultRoot, tmp);
                LabTornCommit.Apply(tmp, mode);
                using var v = new LabVaultService(tmp);
                var open = v.Unlock(password);
                results.Add(new CaseResult
                {
                    Name = name,
                    ExpectedOpen = expectedOpen,
                    ActualOpen = open.Success,
                    Message = open.Message
                });
            }
            catch (Exception ex)
            {
                results.Add(new CaseResult
                {
                    Name = name,
                    ExpectedOpen = expectedOpen,
                    ActualOpen = false,
                    Message = "exception: " + ex.Message
                });
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tmp))
                    {
                        Directory.Delete(tmp, true);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        return new Report { Cases = results };
    }

    /// <summary>Full S-class FI: static scenarios + kill/crash suite.</summary>
    public static Report RunFull(string sourceVaultRoot, string password)
    {
        var staticReport = Run(sourceVaultRoot, password);
        var kill = RunKillCrashSuite(password);
        return new Report { Cases = staticReport.Cases.Concat(kill.Cases).ToList() };
    }

    private static IEnumerable<(string Name, LabTornCommit.Mode Mode, bool ExpectedOpen)> Scenarios()
    {
        yield return ("corrupt-meta-keep-commit", LabTornCommit.Mode.CorruptMetadataKeepCommit, false);
        yield return ("drop-activation-marker", LabTornCommit.Mode.DropActivationMarker, true);
        yield return ("corrupt-primary-commit-only", LabTornCommit.Mode.CorruptPrimaryCommitOnly, true);
        yield return ("corrupt-all-commits", LabTornCommit.Mode.CorruptAllCommits, true); // TryRead null → legacy allow
        yield return ("truncate-header-primary", LabTornCommit.Mode.TruncatePrimaryHeader, true); // dual header recover
        yield return ("zero-object-pack-index", LabTornCommit.Mode.CorruptPackIndex, true); // vault still opens
        yield return ("truncate-all-headers", LabTornCommit.Mode.TruncateAllHeaders, false); // fail-closed
        yield return ("corrupt-loose-object-cipher", LabTornCommit.Mode.CorruptLooseObjectCipher, true); // open OK
        yield return ("corrupt-marker-digest-field", LabTornCommit.Mode.CorruptSideDigestOnly, false); // digest mismatch
    }

    /// <summary>
    /// Kill/crash simulation suite: leave incomplete journal at various stages, unlock recovers.
    /// </summary>
    public static Report RunKillCrashSuite(string password)
    {
        var cases = new List<CaseResult>
        {
            RunMidImportKillRecovery(password),
            RunPreparedOnlyKillRecovery(password),
            RunMetadataReadyKillRecovery(password)
        };
        return new Report { Cases = cases };
    }

    /// <summary>
    /// Kill/crash simulation: leave incomplete ObjectsReady journal + orphan loose object, then unlock should recover.
    /// </summary>
    public static CaseResult RunMidImportKillRecovery(string password)
    {
        var root = Path.Combine(Path.GetTempPath(), "lab-kill-" + Guid.NewGuid().ToString("N"));
        string? fakeId = null;
        try
        {
            using (var v = new LabVaultService(root))
            {
                var created = v.Create(password, LabKdfProfile.LabFast);
                if (!created.Success)
                {
                    return FailCase("mid-import-kill-recovery", created.Message);
                }

                var unlock = v.Unlock(password);
                if (!unlock.Success)
                {
                    return FailCase("mid-import-kill-recovery", unlock.Message);
                }

                fakeId = LabObjectStore.NewObjectId();
                LabObjectStore.WriteLoose(root, fakeId, new byte[] { 9, 9, 9, 9 });
                var tx = LabVaultJournal.Begin(root, "import:killed", 1);
                LabVaultJournal.Mark(root, tx, LabVaultJournal.State.ObjectsReady, "obj:" + fakeId, 1);
                v.Lock();
            }

            using var reopen = new LabVaultService(root);
            var open = reopen.Unlock(password);
            var incomplete = LabVaultJournal.ListIncomplete(root);
            var orphanGone = fakeId is not null && !File.Exists(LabObjectStore.AbsolutePath(root, fakeId));
            var pass = open.Success && incomplete.Count == 0 && orphanGone;
            return new CaseResult
            {
                Name = "mid-import-kill-recovery",
                ExpectedOpen = true,
                ActualOpen = pass,
                Message = open.Message + " incomplete=" + incomplete.Count + " orphanGone=" + orphanGone
            };
        }
        finally
        {
            SafeDelete(root);
        }
    }

    /// <summary>Crash after TX Prepared only (no object yet) — unlock aborts incomplete TX.</summary>
    public static CaseResult RunPreparedOnlyKillRecovery(string password)
    {
        var root = Path.Combine(Path.GetTempPath(), "lab-kill-prep-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var v = new LabVaultService(root))
            {
                if (!v.Create(password, LabKdfProfile.LabFast).Success
                    || !v.Unlock(password).Success)
                {
                    return FailCase("prepared-only-kill-recovery", "create/unlock failed");
                }

                LabVaultJournal.Begin(root, "import:prepared-kill", 1);
                v.Lock();
            }

            using var reopen = new LabVaultService(root);
            var open = reopen.Unlock(password);
            var incomplete = LabVaultJournal.ListIncomplete(root);
            var pass = open.Success && incomplete.Count == 0;
            return new CaseResult
            {
                Name = "prepared-only-kill-recovery",
                ExpectedOpen = true,
                ActualOpen = pass,
                Message = open.Message + " incomplete=" + incomplete.Count
            };
        }
        finally
        {
            SafeDelete(root);
        }
    }

    /// <summary>
    /// Crash after MetadataReady (object present, not committed) — orphan object shredded + TX aborted.
    /// </summary>
    public static CaseResult RunMetadataReadyKillRecovery(string password)
    {
        var root = Path.Combine(Path.GetTempPath(), "lab-kill-meta-" + Guid.NewGuid().ToString("N"));
        string? fakeId = null;
        try
        {
            using (var v = new LabVaultService(root))
            {
                if (!v.Create(password, LabKdfProfile.LabFast).Success
                    || !v.Unlock(password).Success)
                {
                    return FailCase("metadata-ready-kill-recovery", "create/unlock failed");
                }

                fakeId = LabObjectStore.NewObjectId();
                LabObjectStore.WriteLoose(root, fakeId, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
                var tx = LabVaultJournal.Begin(root, "import:meta-ready-kill", 1);
                LabVaultJournal.Mark(root, tx, LabVaultJournal.State.ObjectsReady, "obj:" + fakeId, 1);
                LabVaultJournal.Mark(root, tx, LabVaultJournal.State.MetadataReady, "entry:pending", 1);
                v.Lock();
            }

            using var reopen = new LabVaultService(root);
            var open = reopen.Unlock(password);
            var incomplete = LabVaultJournal.ListIncomplete(root);
            var orphanGone = fakeId is not null && !File.Exists(LabObjectStore.AbsolutePath(root, fakeId));
            var pass = open.Success && incomplete.Count == 0 && orphanGone;
            return new CaseResult
            {
                Name = "metadata-ready-kill-recovery",
                ExpectedOpen = true,
                ActualOpen = pass,
                Message = open.Message + " incomplete=" + incomplete.Count + " orphanGone=" + orphanGone
            };
        }
        finally
        {
            SafeDelete(root);
        }
    }

    private static CaseResult FailCase(string name, string message) =>
        new()
        {
            Name = name,
            ExpectedOpen = true,
            ActualOpen = false,
            Message = message
        };

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

    private static void CopyVault(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            var target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
