using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO06RecoveryTests
{
    [Theory]
    [InlineData(UpdateFaultPoint.AfterNewFileIntent, UpdateStage.Restored)]
    [InlineData(UpdateFaultPoint.AfterNewFileCreated, UpdateStage.RecoveryRequired)]
    [InlineData(UpdateFaultPoint.AfterNewFileIdentity, UpdateStage.Restored)]
    [InlineData(UpdateFaultPoint.BeforeNewFileRename, UpdateStage.Restored)]
    [InlineData(UpdateFaultPoint.AfterNewFileRename, UpdateStage.Restored)]
    public void NewFileInterruptionPreservesOriginal(UpdateFaultPoint point, UpdateStage expected)
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var fired = false;
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4", faultInjector: current =>
        { if (current == point && !fired) { fired = true; throw new IOException("WO06 injected interruption"); } });
        Assert.True(fired);
        Assert.Equal(expected, result.Stage);
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.False(File.Exists(Path.Combine(f.Install, "second.txt")));
        if (expected == UpdateStage.RecoveryRequired)
        {
            Assert.Single(Directory.GetFiles(f.Install, ".pccare-*.pending"));
            Assert.Equal(expected, f.Transaction.Recover(f.Install).Stage);
        }
        else Assert.Empty(Directory.GetFiles(f.Install, ".pccare-*.pending"));
    }

    [Theory]
    [InlineData(UpdateFaultPoint.JournalTempCreated)]
    [InlineData(UpdateFaultPoint.JournalPartialWrite)]
    [InlineData(UpdateFaultPoint.JournalFlushed)]
    [InlineData(UpdateFaultPoint.JournalBeforeReplace)]
    [InlineData(UpdateFaultPoint.JournalAfterReplace)]
    public void JournalWriteFailureHasRecoverablePreviousGeneration(UpdateFaultPoint point)
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var armed = false; var fired = false;
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4", faultInjector: current =>
        {
            if (current == UpdateFaultPoint.AfterApplyingJournal) armed = true;
            if (armed && !fired && current == point) { fired = true; throw new IOException("WO06 journal interruption"); }
        });
        Assert.True(fired);
        Assert.Equal(UpdateStage.Restored, result.Stage);
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.Equal(UpdateStage.Restored, f.Transaction.Recover(f.Install).Stage);
        Assert.True(File.Exists(Path.Combine(Directory.GetDirectories(f.Work, "tx-*").Single(), "journal.previous.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TruncatedOrMissingPrimaryRecoversUsingPreviousGeneration(bool missing)
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        var path = JournalPath(f);
        if (missing) File.Delete(path); else File.WriteAllText(path, "{\"Stage\":");
        Assert.Equal(UpdateStage.Restored, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.False(File.Exists(Path.Combine(f.Install, "second.txt")));
    }

    [Fact]
    public void ValidJsonWithForgedIdentityDoesNotUsePreviousGeneration()
    {
        using var f = new SignedUpdateTests.Fixture();
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        var node = JsonNode.Parse(File.ReadAllText(JournalPath(f)))!;
        node["InstallIdentity"] = new string('0', 16) + ":" + new string('0', 32);
        File.WriteAllText(JournalPath(f), node.ToJsonString());
        Assert.Equal(UpdateStage.RecoveryRequired, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
    }

    [Theory]
    [InlineData(UpdateStage.Committed)]
    [InlineData(UpdateStage.Restored)]
    public void LegacyTerminalReceiptPreservesOriginalAndAllowsLaterUpdates(UpdateStage terminal)
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4", faultInjector: point =>
        { if (terminal == UpdateStage.Restored && point == UpdateFaultPoint.BeforeCommittedJournal) throw new IOException("restore fixture"); });
        Assert.Equal(terminal, result.Stage);
        MakeLegacy(f, terminal);
        var originalHash = Hash(JournalPath(f));
        Assert.Equal(UpdateStage.Restored, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal(originalHash, Hash(JournalPath(f)));
        var receipt = Path.Combine(Path.GetDirectoryName(JournalPath(f))!, "legacy-terminal.receipt");
        Assert.True(File.Exists(receipt));
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        Assert.Equal(originalHash, Hash(Directory.GetFiles(f.Work, "journal.json", SearchOption.AllDirectories)
            .Single(path => Path.GetDirectoryName(path) == Path.GetDirectoryName(receipt))));
    }

    [Theory]
    [InlineData(UpdateStage.Prepared)]
    [InlineData(UpdateStage.Applying)]
    [InlineData(UpdateStage.RollbackPending)]
    [InlineData(UpdateStage.RollingBack)]
    public void LegacyIncompleteJournalRemainsReadOnly(UpdateStage stage)
    {
        using var f = new SignedUpdateTests.Fixture();
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        MakeLegacy(f, stage);
        var hash = Hash(JournalPath(f));
        Assert.Equal(UpdateRecoveryState.RecoveryRequired, f.Transaction.InspectRecovery(f.Install).State);
        Assert.Equal(UpdateStage.RecoveryRequired, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal(hash, Hash(JournalPath(f)));
        Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
    }

    [Fact]
    public void LegacyReceiptRejectsChangedJournal()
    {
        using var f = new SignedUpdateTests.Fixture();
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        MakeLegacy(f, UpdateStage.Committed);
        Assert.Equal(UpdateStage.Restored, f.Transaction.Recover(f.Install).Stage);
        File.AppendAllText(JournalPath(f), " ");
        Assert.Equal(UpdateStage.RecoveryRequired, f.Transaction.Recover(f.Install).Stage);
    }

    [Fact]
    public void NewFileNameCollisionPreservesUserFile()
    {
        using var f = new SignedUpdateTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4", faultInjector: point =>
        { if (point == UpdateFaultPoint.BeforeNewFileRename) File.WriteAllText(Path.Combine(f.Install, "second.txt"), "user-created"); });
        Assert.Equal(UpdateStage.RecoveryRequired, result.Stage);
        Assert.Equal("user-created", File.ReadAllText(Path.Combine(f.Install, "second.txt")));
        Assert.Equal(UpdateStage.RecoveryRequired, f.Transaction.Recover(f.Install).Stage);
    }

    [Theory]
    [InlineData("null-identity")]
    [InlineData("foreign-root")]
    [InlineData("null-file")]
    public void MalformedLegacyTerminalIsRejectedWithoutChangingFiles(string mutation)
    {
        using var f = new SignedUpdateTests.Fixture();
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        MakeLegacy(f, UpdateStage.Committed);
        var node = JsonNode.Parse(File.ReadAllText(JournalPath(f)))!;
        if (mutation == "null-identity") node["InstallIdentity"] = null;
        if (mutation == "foreign-root") node["InstallRoot"] = f.Work;
        if (mutation == "null-file") node["Files"]!.AsArray()[0] = null;
        File.WriteAllText(JournalPath(f), node.ToJsonString());
        var before = Hash(JournalPath(f));
        Assert.Equal(UpdateStage.RecoveryRequired, f.Transaction.Recover(f.Install).Stage);
        Assert.Equal(before, Hash(JournalPath(f)));
        Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
    }

    private static string JournalPath(SignedUpdateTests.Fixture f) =>
        Path.Combine(Directory.GetDirectories(f.Work, "tx-*").Single(), "journal.json");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void MakeLegacy(SignedUpdateTests.Fixture f, UpdateStage stage)
    {
        var node = JsonNode.Parse(File.ReadAllText(JournalPath(f)))!;
        node["SchemaVersion"] = 1; node["Stage"] = (int)stage;
        node.AsObject().Remove("InstallIdentity"); node.AsObject().Remove("TransactionIdentity");
        foreach (var file in node["Files"]!.AsArray())
            foreach (var field in new[] { "OriginalIdentity", "BackupIdentity", "InstalledIdentity", "TemporaryPath", "TemporaryReady" })
                file!.AsObject().Remove(field);
        File.WriteAllText(JournalPath(f), node.ToJsonString());
    }
}
