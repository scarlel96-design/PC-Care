using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WindowsFileBoundaryTests
{
    [Fact]
    public void VerifiedInputPackageCannotBeSwappedBeforeStaging()
    {
        using var f = new SignedUpdateTests.Fixture();
        var package = f.Package();
        var reached = false;
        var result = f.Transaction.Apply(package, f.Install, "51.0.4", faultInjector: point =>
        {
            if (point != UpdateFaultPoint.AfterPackageVerification) return;
            reached = true;
            Assert.Throws<IOException>(() => File.Move(package, package + ".replaced"));
            Assert.Throws<IOException>(() => File.WriteAllText(package, "replacement"));
        });
        Assert.True(reached);
        Assert.Equal(UpdateStage.Committed, result.Stage);
    }

    [Fact]
    public void LockedLaterDestinationStopsBeforeExistingContentChanges()
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var later = Path.Combine(f.Install, "second.txt");
        File.WriteAllText(later, "old-second");
        using (var locked = new FileStream(later, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4");
            Assert.Equal(UpdateStage.RecoveryRequired, result.Stage);
            Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
            Assert.Equal("old-second", File.ReadAllText(later));
        }
        Assert.Equal(UpdateStage.Restored, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.Equal("old-second", File.ReadAllText(later));
    }

    [Fact]
    public void CommittedPackageCleanupPreservesBackupAndJournal()
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        var transaction = Directory.GetDirectories(f.Work, "tx-*").Single();
        Assert.False(File.Exists(Path.Combine(transaction, "package.spdup")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(transaction, "backup", "app.txt")));
        Assert.True(File.Exists(Path.Combine(transaction, "journal.json")));
    }

    [Fact]
    public void FileAclIsReadFromTheRetainedHandle()
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        using var file = WindowsFileBoundary.OpenRead(f.Install, "app.txt");
        var actual = file.ReadSecurity().GetSecurityDescriptorBinaryForm();
        var expected = new FileInfo(Path.Combine(f.Install, "app.txt")).GetAccessControl().GetSecurityDescriptorBinaryForm();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OpenHandleBlocksWriteDeleteAndParentRename()
    {
        using var f = new SignedUpdateTests.Fixture();
        var path = Path.Combine(f.Install, "app.txt");
        File.WriteAllText(path, "old");
        using var file = WindowsFileBoundary.OpenRead(f.Install, "app.txt");
        Assert.Throws<IOException>(() => File.WriteAllText(path, "attack"));
        Assert.Throws<IOException>(() => File.Delete(path));
        Assert.Throws<IOException>(() => Directory.Move(f.Install, f.Install + "-moved"));
        Assert.Equal(49, file.Identity.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData("old"u8)), file.Hash());
    }

    [Fact]
    public void ExistingFileKeepsIdentityThroughApplyAndRecovery()
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        string before;
        using (var file = WindowsFileBoundary.OpenRead(f.Install, "app.txt")) before = file.Identity;
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        using (var file = WindowsFileBoundary.OpenRead(f.Install, "app.txt")) Assert.Equal(before, file.Identity);
        Rewrite(f, journal => journal.Stage = UpdateStage.Applying);
        Assert.Equal(UpdateStage.Restored, f.Transaction.Recover(f.Install).Stage);
        using (var file = WindowsFileBoundary.OpenRead(f.Install, "app.txt")) Assert.Equal(before, file.Identity);
        Assert.Equal("NO-PENDING", f.Transaction.Recover(f.Install).Code);
    }

    [Fact]
    public void SameContentDifferentFileIdCannotDriveRestore()
    {
        using var f = Applied();
        var path = Path.Combine(f.Install, "app.txt");
        File.Move(path, path + ".saved");
        File.WriteAllText(path, "new");
        Assert.Equal(UpdateStage.RecoveryRequired, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal("new", File.ReadAllText(path));
        Assert.Equal("second", File.ReadAllText(Path.Combine(f.Install, "second.txt")));
    }

    [Fact]
    public void NewFileIdentitySwapCannotDeleteUserFile()
    {
        using var f = Applied();
        var path = Path.Combine(f.Install, "second.txt");
        File.Move(path, path + ".saved");
        File.WriteAllText(path, "second");
        Assert.Equal(UpdateStage.RecoveryRequired, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.Equal("second", File.ReadAllText(path));
    }

    [Fact]
    public void SameIdUserContentChangeStopsAllRestoreOperations()
    {
        using var f = Applied();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "user edit");
        Assert.Equal(UpdateStage.RecoveryRequired, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal("user edit", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.True(File.Exists(Path.Combine(f.Install, "second.txt")));
    }

    [Fact]
    public void BackupSameHashDifferentIdIsRejected()
    {
        using var f = Applied();
        var path = Path.Combine(Directory.GetDirectories(f.Work, "tx-*").Single(), "backup", "app.txt");
        File.Move(path, path + ".saved");
        File.WriteAllText(path, "old");
        Assert.Equal(UpdateStage.RecoveryRequired, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
    }

    [Fact]
    public void BackupAndTargetsRemainLockedUntilCommit()
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var reached = false;
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4", count =>
        {
            if (count != 1) return;
            reached = true;
            var backup = Path.Combine(Directory.GetDirectories(f.Work, "tx-*").Single(), "backup", "app.txt");
            Assert.Throws<IOException>(() => File.Delete(backup));
            Assert.Throws<IOException>(() => File.WriteAllText(backup, "attack"));
            Assert.Throws<IOException>(() => File.Delete(Path.Combine(f.Install, "app.txt")));
        });
        Assert.True(reached);
        Assert.Equal(UpdateStage.Committed, result.Stage);
    }

    [Theory]
    [InlineData("InstallIdentity")]
    [InlineData("TransactionIdentity")]
    [InlineData("OriginalIdentity")]
    [InlineData("BackupIdentity")]
    [InlineData("legacy")]
    [InlineData("duplicate-property")]
    [InlineData("unknown-property")]
    [InlineData("truncated")]
    public void IdentityAndJsonJournalTamperingFailsClosed(string mutation)
    {
        using var f = Applied();
        var path = Path.Combine(Directory.GetDirectories(f.Work, "tx-*").Single(), "journal.json");
        var content = File.ReadAllText(path);
        var journal = JsonSerializer.Deserialize<UpdateJournal>(content)!;
        switch (mutation)
        {
            case "InstallIdentity": journal.InstallIdentity = new string('0', 16) + ":" + new string('0', 32); break;
            case "TransactionIdentity": journal.TransactionIdentity = ""; break;
            case "OriginalIdentity": journal.Files[0].OriginalIdentity = ""; break;
            case "BackupIdentity": journal.Files[0].BackupIdentity = ""; break;
            case "legacy": journal.SchemaVersion = 1; break;
            case "duplicate-property": File.WriteAllText(path, content[..^1] + ",\"Stage\":4}"); break;
            case "unknown-property": File.WriteAllText(path, content[..^1] + ",\"DeleteOutside\":true}"); break;
            case "truncated": File.WriteAllText(path, "{\"Stage\":"); break;
        }
        if (mutation is not ("duplicate-property" or "unknown-property" or "truncated"))
            File.WriteAllText(path, JsonSerializer.Serialize(journal));
        Assert.Equal(mutation == "truncated" ? UpdateStage.Restored : UpdateStage.RecoveryRequired, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal(mutation == "truncated" ? "old" : "new", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
    }

    [Fact]
    public void PrimaryAndFailureJournalErrorsAreBothPreserved()
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4",
            afterFileApplied: _ => throw new IOException("PRIMARY-ERROR"),
            faultInjector: point => { if (point == UpdateFaultPoint.BeforeFailureJournal) throw new IOException("JOURNAL-ERROR"); });
        Assert.Equal(UpdateStage.RecoveryRequired, result.Stage);
        Assert.Contains("PRIMARY-ERROR", result.Failure);
        Assert.Contains("JOURNAL-ERROR", result.Failure);
        Assert.Equal(UpdateStage.Restored, f.Transaction.Recover(f.Install).Stage);
    }

    [Fact]
    public void RollbackAndFailureJournalErrorsAreBothPreserved()
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var rollbackReached = false;
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4",
            afterFileApplied: _ => throw new IOException("PRIMARY-ERROR"), faultInjector: point =>
            {
                if (point == UpdateFaultPoint.AfterRollbackStarted) { rollbackReached = true; throw new IOException("ROLLBACK-ERROR"); }
                if (point == UpdateFaultPoint.BeforeFailureJournal && rollbackReached) throw new IOException("JOURNAL-ERROR");
            });
        Assert.Contains("PRIMARY-ERROR", result.Failure);
        Assert.Contains("ROLLBACK-ERROR", result.Failure);
        Assert.Contains("JOURNAL-ERROR", result.Failure);
        Assert.Equal(UpdateStage.RecoveryRequired, result.Stage);
        Assert.Equal(UpdateStage.Restored, f.Transaction.Recover(f.Install).Stage);
    }

    [Fact]
    public void AncestorJunctionCannotEscapeBoundary()
    {
        using var f = new SignedUpdateTests.Fixture();
        var link = Path.Combine(f.Install, "junction");
        using var command = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{f.Work}\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        command.WaitForExit();
        Assert.Equal(0, command.ExitCode);
        try { Assert.Throws<IOException>(() => WindowsFileBoundary.PinDirectory(link)); }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void HardlinkLeafIsRejectedBeforeWriting()
    {
        using var f = new SignedUpdateTests.Fixture();
        var outside = Path.Combine(f.Work, "outside.txt");
        File.WriteAllText(outside, "outside");
        Assert.True(CreateHardLink(Path.Combine(f.Install, "linked.txt"), outside, IntPtr.Zero));
        Assert.Throws<IOException>(() => WindowsFileBoundary.OpenWrite(f.Install, "linked.txt"));
        Assert.Equal("outside", File.ReadAllText(outside));
    }

    private static SignedUpdateTests.Fixture Applied()
    {
        var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        Rewrite(f, journal => journal.Stage = UpdateStage.Applying);
        return f;
    }

    private static void Rewrite(SignedUpdateTests.Fixture f, Action<UpdateJournal> change)
    {
        var path = Path.Combine(Directory.GetDirectories(f.Work, "tx-*").Single(), "journal.json");
        var journal = JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path))!;
        change(journal);
        File.WriteAllText(path, JsonSerializer.Serialize(journal));
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newName, string existing, IntPtr security);
}
