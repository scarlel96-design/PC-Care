using System.Security.Cryptography;
using System.Runtime.InteropServices;
using SmartPerformanceDoctor.SecurityLab.VaultV4;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

[Collection("VaultEnvironment")]
public sealed class WO14V5TransitionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wo14-vault-fixture-" + Guid.NewGuid().ToString("N"));
    private const string Password = "WO14-Synthetic-Only!";
    private string Source => Path.Combine(root, "source");
    private string Work => Path.Combine(root, "work-transition");
    public WO14V5TransitionTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "fixture.marker"), "WO14_SYNTHETIC_ONLY");
    }
    public void Dispose() => Directory.Delete(root, true);
    private void Seed()
    {
        using var vault = new LabVaultService(Source);
        Assert.True(vault.Create(Password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(Password).Success);
        Assert.True(vault.ImportBytes("한글.bin", "폴더/하위/한글.bin", [1,2,3]).Success);
        Assert.True(vault.ImportBytes("empty.bin", "empty.bin", []).Success);
    }
    private Dictionary<string,string> Hashes() => Directory.GetFiles(Source, "*", SearchOption.AllDirectories)
        .ToDictionary(p => Path.GetRelativePath(Source,p), p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
    private void AssertSource(Dictionary<string,string> before)
    {
        var after = Hashes(); Assert.Equal(before.Count, after.Count);
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }
    [Fact] public void Explicit_transition_preserves_every_source_file_and_reopens_both_formats()
    {
        Seed(); var before = Hashes();
        var result = LabV5Transition.ExecuteSynthetic(Source, Work, Password, true);
        Assert.True(result.Completed); Assert.Equal("VerifiedTestTransition", result.Status);
        Assert.Equal(2,result.ContentHashes.Count); Assert.Equal(10,result.FreshRecoveryCodes.Count); AssertSource(before);
        using var old = new LabVaultService(Source); Assert.True(old.UnlockReadOnly(Password).Success);
        using var converted = new LabVaultService(Path.Combine(Work,"candidate-v6"));
        Assert.True(converted.UnlockReadOnly(Password).Success); Assert.Equal(2,converted.List().Count);
        Assert.Contains(converted.List(), e => e.RelativePath == "폴더/하위/한글.bin");
    }
    [Fact] public void Consent_is_required_before_creating_workspace()
    {
        Seed(); Assert.Throws<InvalidOperationException>(() => LabV5Transition.ExecuteSynthetic(Source,Work,Password,false));
        Assert.False(Directory.Exists(Work));
    }
    [Fact] public void Production_path_is_rejected()
    {
        Seed(); Assert.Throws<InvalidOperationException>(() => LabV5Transition.ExecuteSynthetic(Source,Path.Combine(root,"production"),Password,true));
    }
    [Fact] public void Interrupted_workspace_is_never_reused_or_overwritten()
    {
        Seed(); Directory.CreateDirectory(Work); File.WriteAllText(Path.Combine(Work,"preserve.txt"),"evidence");
        Assert.Throws<IOException>(() => LabV5Transition.ExecuteSynthetic(Source,Work,Password,true));
        Assert.Equal("evidence",File.ReadAllText(Path.Combine(Work,"preserve.txt")));
    }
    [Fact] public void Wrong_password_preserves_source_and_reports_locked()
    {
        Seed(); var before = Hashes(); var result = LabV5Transition.ExecuteSynthetic(Source,Work,"Wrong-Synthetic!",true);
        Assert.False(result.Completed); Assert.Equal("PreservedButLocked",result.Status); AssertSource(before);
    }
    [Theory]
    [InlineData("TransitionSnapshotReady")]
    [InlineData("TransitionContentVerified")]
    [InlineData("GenerationMetadataWritten")]
    [InlineData("ActivationBeforeReplace")]
    [InlineData("ActivationAfterReplace")]
    public void Interrupted_transition_preserves_original(string stage)
    {
        Seed(); var before = Hashes();
        Assert.Throws<IOException>(() => LabV5Transition.ExecuteSynthetic(Source,Work,Password,true,
            fault: p => { if(p == stage) throw new IOException("injected"); }));
        AssertSource(before); using var original = new LabVaultService(Source); Assert.True(original.UnlockReadOnly(Password).Success);
    }
    [Fact] public void MetadataReady_copy_exports_authenticated_bytes_without_rewriting_commit()
    {
        Seed(); using(var vault = new LabVaultService(Source))
        {
            Assert.True(vault.Unlock(Password).Success);
            vault.FaultObserver = p => { if(p == "MetadataReady") throw new IOException("injected"); };
            Assert.False(vault.ImportBytes("new.bin","new.bin",[4]).Success);
        }
        var before = Hashes(); using(var reader = new LabVaultService(Source)) Assert.False(reader.UnlockReadOnly(Password).Success);
        // Normal opening records an audit failure. Snapshot after that attempt.
        before = Hashes(); var result = LabV5Transition.ExecuteSynthetic(Source,Work,Password,true,salvageReadOnly:true);
        Assert.Equal("ReadOnlyRecovery",result.Status); Assert.False(result.Completed); Assert.Equal(3,result.ContentHashes.Count);
        Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(Path.Combine(Work,"recovered","폴더","하위","한글.bin")));
        AssertSource(before);
        Assert.Equal(before["activation.commit.json"],Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Work,"candidate-v6","activation.commit.json")))));
    }
    [Fact] public void Cancellation_preserves_source()
    {
        Seed(); var before=Hashes(); using var stop = new CancellationTokenSource(); stop.Cancel();
        Assert.Throws<OperationCanceledException>(()=>LabV5Transition.ExecuteSynthetic(Source,Work,Password,true,stop.Token)); AssertSource(before);
    }
    [Fact] public void Held_snapshot_handles_block_source_replacement_and_mutation()
    {
        Seed(); var before = Hashes(); bool observed = false;
        var result = LabV5Transition.ExecuteSynthetic(Source, Work, Password, true, fault: stage =>
        {
            if (stage != "TransitionSnapshotReady") return;
            var header = Path.Combine(Source, "vault.header.json");
            Assert.Throws<IOException>(() => File.WriteAllText(header, "replaced"));
            Assert.Throws<IOException>(() => File.Move(header, header + ".moved"));
            Assert.Throws<IOException>(() => Directory.Move(Source, Source + ".moved"));
            observed = true;
        });
        Assert.True(observed); Assert.True(result.Completed); AssertSource(before);
    }
    [Fact] public void Concurrent_inventory_addition_is_visible_and_preserved()
    {
        Seed(); var added = Path.Combine(Source, "unapproved.bin");
        Assert.Throws<IOException>(() => LabV5Transition.ExecuteSynthetic(Source, Work, Password, true,
            fault: stage => { if (stage == "TransitionSnapshotReady") File.WriteAllBytes(added, [9]); }));
        Assert.Equal(new byte[]{9}, File.ReadAllBytes(added));
    }
    [Fact] public void Corrupt_authenticated_metadata_cannot_be_salvaged()
    {
        Seed(); var path = Directory.GetFiles(Source,"*.meta",SearchOption.AllDirectories).FirstOrDefault();
        path ??= Path.Combine(Source,"metadata.db.enc");
        var bytes=File.ReadAllBytes(path); bytes[^1]^=1; File.WriteAllBytes(path,bytes);
        var before=Hashes(); var result=LabV5Transition.ExecuteSynthetic(Source,Work,Password,true,salvageReadOnly:true);
        Assert.False(result.Completed); Assert.Empty(result.ContentHashes); AssertSource(before);
    }
    [Fact] public void Authenticated_recovery_store_rejects_used_bit_tampering()
    {
        Seed(); var result=LabV5Transition.ExecuteSynthetic(Source,Work,Password,true);
        var candidate=Path.Combine(Work,"candidate-v6"); var store=Path.Combine(candidate,"recovery","recovery_slots.v6.json");
        var text=File.ReadAllText(store); Assert.Contains("\"Used\":false",text);
        File.WriteAllText(store,text.Replace("\"Used\":false","\"Used\":true"));
        using var reader=new LabVaultService(candidate); Assert.False(reader.UnlockWithRecoveryCode(result.FreshRecoveryCodes[0]).Success);
        Assert.True(reader.UnlockReadOnly(Password).Success);
    }
    [Fact] public void Production_migration_gates_remain_closed()
    {
        Assert.False(SmartPerformanceDoctor.SecurityLab.ProductBridge.Av3GateSnapshot.ProductionWriterEnabled);
        Assert.False(SmartPerformanceDoctor.SecurityLab.ProductBridge.Av3GateSnapshot.ProductionEnableAuthorized);
        Assert.False(SmartPerformanceDoctor.SecurityLab.Migration.LabToAv3MigrationGate.Evaluate().ExecuteAllowed);
    }
    [Fact] public void Hardlinked_snapshot_file_is_rejected_before_copying()
    {
        Seed(); var header = Path.Combine(Source, "vault.header.json");
        var alias = Path.Combine(root, "header-alias.json");
        Assert.True(CreateHardLink(alias, header, IntPtr.Zero));
        var before = Hashes();
        Assert.Throws<IOException>(() => LabV5Transition.ExecuteSynthetic(Source, Work, Password, true));
        Assert.False(Directory.Exists(Work)); AssertSource(before);
    }
    [Fact] public void Locked_snapshot_file_is_rejected_without_changing_source()
    {
        Seed(); var before = Hashes();
        using var locked = new FileStream(Path.Combine(Source, "vault.header.json"), FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.Throws<IOException>(() => LabV5Transition.ExecuteSynthetic(Source, Work, Password, true));
        Assert.False(Directory.Exists(Work));
        locked.Dispose(); AssertSource(before);
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newName, string existing, IntPtr security);
}

