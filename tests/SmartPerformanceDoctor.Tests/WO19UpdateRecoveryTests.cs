using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO19UpdateRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyPlanRequiresIndependentPublisherEvidence(bool trusted)
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        File.WriteAllText(Path.Combine(f.Install, "second.txt"), "old-second");
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        var path = Directory.GetFiles(f.Work, "journal.json", SearchOption.AllDirectories).Single();
        var journal = JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path))!;
        journal.SchemaVersion = 1; journal.Stage = UpdateStage.Applying;
        journal.InstallIdentity = journal.TransactionIdentity = "";
        foreach (var file in journal.Files) file.OriginalIdentity = file.BackupIdentity = file.InstalledIdentity = "";
        File.WriteAllText(path, JsonSerializer.Serialize(journal));
        var transaction = trusted ? f.Transaction : new UpdateTransaction(PublisherTrustPolicy.Production, f.Work, false);
        var before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var plan = transaction.PlanLegacyRecovery(f.Install, journal.TransactionId, f.Package("original-version"), "51.0.3");
        Assert.False(plan.CanExecute);
        Assert.Equal(trusted ? "ManualReviewRequired" : "RecoveryDenied", plan.Status);
        if (trusted) Assert.All(plan.Targets, target => Assert.Equal("BackupVerified", target.Status));
        Assert.Equal(before, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
    }

    [Fact]
    public void ProtectedStagingHandlesPathsLongerThanLegacyMaxPath()
    {
        using var f = new SignedUpdateTests.Fixture();
        var deep = Path.Combine(f.Work, new string('a', 110), new string('b', 110));
        Directory.CreateDirectory(deep);
        using var file = WindowsFileBoundary.OpenWrite(deep, "new.bin", true, true);
        file.Stream.Write(new byte[] { 9 }); file.Stream.Flush(true);
        file.PublishOpenedFile(f.Install, "new.bin");
        file.Verify(1, Convert.ToHexString(SHA256.HashData(new byte[] { 9 })), file.Identity);
    }

    [Fact]
    public void SnapshotProtocolReadsMetadataWithoutLoadingAssembly()
    {
        var helper = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../../src/SmartPerformanceDoctor.UpdateHelper/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.UpdateHelper.dll"));
        Assert.True(UpdateBootstrap.SupportsSnapshotProtocol(helper));
        using var f = new SignedUpdateTests.Fixture();
        var malformed = Path.Combine(f.Work, "invalid.dll");
        File.WriteAllText(malformed, "invalid PE");
        Assert.False(UpdateBootstrap.SupportsSnapshotProtocol(malformed));
        Assert.False(UpdateBootstrap.SupportsSnapshotProtocol(Path.Combine(f.Work, "missing.dll")));
    }

    [Theory]
    [InlineData(UpdateFaultPoint.AfterNewFileIntent)]
    [InlineData(UpdateFaultPoint.AfterNewFileCreated)]
    [InlineData(UpdateFaultPoint.AfterNewFileIdentity)]
    [InlineData(UpdateFaultPoint.DuringNewFileContent)]
    [InlineData(UpdateFaultPoint.BeforeNewFileVerification)]
    [InlineData(UpdateFaultPoint.AfterNewFileVerification)]
    [InlineData(UpdateFaultPoint.BeforeNewFileRename)]
    [InlineData(UpdateFaultPoint.AfterNewFileRename)]
    [InlineData(UpdateFaultPoint.BeforeCommittedJournal)]
    public void NewFileInterruptionsRestoreAndPermitReapplication(UpdateFaultPoint point)
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        File.WriteAllText(Path.Combine(f.Install, "user-settings.json"), "synthetic-settings");
        var fired = false;
        var package = f.Package();
        var result = f.Transaction.Apply(package, f.Install, "51.0.4", faultInjector: p =>
        { if (p == point && !fired) { fired = true; throw new IOException("WO19 interruption"); } });
        Assert.True(fired);
        Assert.Equal(UpdateStage.Restored, result.Stage);
        Assert.Equal(UpdateStage.Restored, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.Equal("synthetic-settings", File.ReadAllText(Path.Combine(f.Install, "user-settings.json")));
        Assert.False(File.Exists(Path.Combine(f.Install, "second.txt")));
        Assert.Empty(Directory.GetFiles(f.Install, ".pccare-*.pending", SearchOption.AllDirectories));
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(package, f.Install, "51.0.4").Stage);
        Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(f.Install, "second.txt")));
        Assert.Equal("synthetic-settings", File.ReadAllText(Path.Combine(f.Install, "user-settings.json")));
    }

    [Fact]
    public void PublicationKeepsFileIdentityAndRejectsCollision()
    {
        using var f = new SignedUpdateTests.Fixture();
        using var source = WindowsFileBoundary.OpenWrite(f.Work, "new.bin", true, true);
        source.Stream.Write(new byte[] { 1, 2, 3 }); source.Stream.Flush(true);
        var identity = source.Identity; var hash = source.Hash();
        source.PublishOpenedFile(f.Install, "nested/new.bin");
        source.Verify(3, hash, identity);
        Assert.False(File.Exists(Path.Combine(f.Work, "new.bin")));
        var content = new byte[3]; source.Stream.ReadExactly(content);
        Assert.Equal(new byte[] { 1, 2, 3 }, content);
        using var second = WindowsFileBoundary.OpenWrite(f.Work, "second.bin", true, true);
        Assert.Throws<IOException>(() => second.PublishOpenedFile(f.Install, "nested/new.bin"));
        source.Stream.Position = 0; source.Stream.ReadExactly(content);
        Assert.Equal(new byte[] { 1, 2, 3 }, content);
    }

    [Fact]
    public void StagingOrphanWithUnknownIdentityIsPreserved()
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        Assert.Equal(UpdateStage.Restored, f.Transaction.Apply(f.Package(), f.Install, "51.0.4", faultInjector: p =>
        { if (p == UpdateFaultPoint.AfterNewFileCreated) throw new IOException("orphan"); }).Stage);
        var orphan = Assert.Single(Directory.GetFiles(f.Work, ".pccare-*.pending", SearchOption.AllDirectories));
        File.WriteAllText(orphan, "unknown-staging-content");
        var before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(orphan)));
        Assert.Equal(UpdateStage.Restored, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal(before, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(orphan))));
    }

    [Fact]
    public void RecoveryExaminesAllPendingTransactions()
    {
        using var f = new SignedUpdateTests.Fixture();
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        var paths = Directory.GetFiles(f.Work, "journal.json", SearchOption.AllDirectories).Order().ToArray();
        foreach (var path in paths)
        {
            var journal = JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path))!;
            journal.Stage = UpdateStage.Applying;
            File.WriteAllText(path, JsonSerializer.Serialize(journal));
        }
        var before = Directory.GetFiles(f.Install).ToDictionary(path => path,
            path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var result = f.Transaction.Recover(f.Install);
        Assert.Equal(UpdateStage.RecoveryRequired, result.Stage);
        Assert.Equal("MULTIPLE-PENDING-TRANSACTIONS", result.Code);
        Assert.All(paths, path => Assert.Equal(UpdateStage.Applying,
            JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(path))!.Stage));
        foreach (var file in before)
            Assert.Equal(file.Value, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file.Key))));
    }
}
