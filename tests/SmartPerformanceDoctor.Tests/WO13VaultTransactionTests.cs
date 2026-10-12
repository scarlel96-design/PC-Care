using System.Security.Cryptography;
using SmartPerformanceDoctor.SecurityLab.VaultV4;
using SmartPerformanceDoctor.SecurityLab.Hardening;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

[Collection("VaultEnvironment")]
public sealed class WO13VaultTransactionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wo13-tests-" + Guid.NewGuid().ToString("N"));
    private const string Password = "Synthetic-Vault-13!";
    public WO13VaultTransactionTests() => Directory.CreateDirectory(root);
    public void Dispose() { Directory.Delete(root, true); if (Directory.Exists(root + "-output")) Directory.Delete(root + "-output", true); }
    [Fact]
    public void Lab_mirror_denies_production_writer_and_migration()
    {
        Assert.False(SmartPerformanceDoctor.SecurityLab.ProductBridge.Av3GateSnapshot.ProductionWriterEnabled);
        Assert.False(SmartPerformanceDoctor.SecurityLab.ProductBridge.Av3GateSnapshot.ExternalReviewCompleted);
        Assert.False(SmartPerformanceDoctor.SecurityLab.ProductBridge.Av3GateSnapshot.ProductionEnableAuthorized);
        Assert.False(SmartPerformanceDoctor.SecurityLab.Migration.LabToAv3MigrationGate.Evaluate().ExecuteAllowed);
    }
    private LabVaultService Seed()
    {
        var vault = new LabVaultService(root, crashSafeTestFormat: true);
        Assert.True(vault.Create(Password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(Password).Success);
        Assert.True(vault.ImportBytes("seed.bin", "seed.bin", [1,2,3]).Success);
        return vault;
    }
    private void VerifySeed(LabVaultService vault)
    {
        var entry = Assert.Single(vault.List(), x => x.DisplayName == "seed.bin");
        var exported = vault.ExportEntry(entry.EntryId, root + "-output", true); Assert.True(exported.Success, exported.Message);
        Assert.Equal(new byte[] {1,2,3}, File.ReadAllBytes(Path.Combine(root + "-output", "seed.bin")));
    }
    [Theory]
    [InlineData("GenerationMetadataWritten")]
    [InlineData("GenerationHeaderWritten")]
    [InlineData("MetadataReady")]
    [InlineData("ActivationBeforeReplace")]
    public void Uncommitted_generation_preserves_authenticated_seed(string stage)
    {
        using (var vault = Seed())
        {
            vault.FaultObserver = point => { if (point == stage) throw new IOException("injected"); };
            Assert.False(vault.ImportBytes("new.bin", "new.bin", [4]).Success);
            Assert.False(vault.IsWriteAllowed);
        }
        using var reader = new LabVaultService(root);
        Assert.True(reader.Unlock(Password).Success); Assert.Single(reader.List()); VerifySeed(reader);
    }
    [Fact] public void Corrupt_active_pointer_recovers_previous_read_only()
    {
        using (var vault = Seed()) Assert.True(vault.ImportBytes("new.bin", "new.bin", [4]).Success);
        File.WriteAllText(Path.Combine(root,"generations-v6","active.json"), "broken");
        using var reader = new LabVaultService(root);
        Assert.True(reader.Unlock(Password).Success); Assert.False(reader.IsWriteAllowed); VerifySeed(reader);
    }
    [Fact] public void Both_pointers_corrupt_block_access_and_preserve_objects()
    {
        using (var vault = Seed()) { }
        var objects = Directory.GetFiles(root,"*.obj",SearchOption.AllDirectories).ToDictionary(x=>x,File.ReadAllBytes);
        foreach(var name in new[]{"active.json","previous.json"}) File.WriteAllText(Path.Combine(root,"generations-v6",name),"broken");
        using var reader = new LabVaultService(root); Assert.False(reader.Unlock(Password).Success);
        foreach(var pair in objects) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
    }
    [Fact] public void Missing_generation_directory_cannot_use_stale_root_header()
    {
        using (var vault = Seed()) { }
        Directory.Move(Path.Combine(root,"generations-v6"),Path.Combine(root,"preserved"));
        using var reader = new LabVaultService(root); Assert.False(reader.Unlock(Password).Success);
    }
    [Fact] public void Journal_write_lock_prevents_success_and_preserves_seed()
    {
        using (var vault = Seed())
        using (var locked = new FileStream(Path.Combine(root,"journal","writer.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Assert.NotNull(Record.Exception(()=>vault.ImportBytes("new.bin","new.bin",[4])));
        using var reader = new LabVaultService(root); Assert.True(reader.Unlock(Password).Success); VerifySeed(reader);
    }
    [Theory]
    [InlineData("{")]
    [InlineData("{\"TxId\":\"bad\",\"State\":1}")]
    [InlineData("{\"TxId\":\"0123456789abcdef\",\"State\":999}")]
    public void Corrupt_journal_is_visible_and_does_not_authorize_writes(string content)
    {
        using (var vault = Seed()) { }
        File.WriteAllText(Path.Combine(root,"journal","tx.avj"),content+"\n");
        using var reader = new LabVaultService(root);
        Assert.True(reader.Unlock(Password).Success); Assert.False(reader.IsWriteAllowed); VerifySeed(reader);
    }
    [Fact] public void Duplicate_and_invalid_journal_transitions_are_rejected()
    {
        var id=LabVaultJournal.Begin(root,"synthetic",1);
        Assert.Throws<InvalidDataException>(()=>LabVaultJournal.Mark(root,id,LabVaultJournal.State.Prepared,"synthetic",1));
        Assert.Throws<InvalidDataException>(()=>LabVaultJournal.Mark(root,id,LabVaultJournal.State.Committed,"synthetic",1));
    }
    [Fact] public void Password_change_keeps_content_and_recovery_key()
    {
        using var vault = new LabVaultService(root,crashSafeTestFormat:true);
        var created=vault.Create(Password,LabKdfProfile.LabFast); Assert.True(created.Success);
        Assert.True(vault.Unlock(Password).Success); Assert.True(vault.ImportBytes("seed.bin","seed.bin",[1,2,3]).Success);
        Assert.True(vault.ChangePassword(Password,"New-Synthetic-Vault-13!").Success);
        vault.Lock(); Assert.False(vault.Unlock(Password).Success);
        Assert.True(vault.Unlock("New-Synthetic-Vault-13!").Success); vault.Lock();
        Assert.True(vault.UnlockWithRecoveryCode(created.RecoveryCodes[0]).Success); VerifySeed(vault);
    }
    [Fact] public void Session_conflict_does_not_consume_recovery_code()
    {
        using var first = new LabVaultService(root,crashSafeTestFormat:true);
        var created=first.Create(Password,LabKdfProfile.LabFast); Assert.True(created.Success); Assert.True(first.Unlock(Password).Success);
        using var second = new LabVaultService(root);
        Assert.False(second.UnlockWithRecoveryCode(created.RecoveryCodes[0]).Success);
        first.Lock(); Assert.True(second.UnlockWithRecoveryCode(created.RecoveryCodes[0]).Success);
        second.Lock(); Assert.False(second.UnlockWithRecoveryCode(created.RecoveryCodes[0]).Success);
    }
    [Fact] public async Task Lock_waits_for_active_import_in_same_instance()
    {
        using var vault=Seed(); using var reached=new ManualResetEventSlim(); using var release=new ManualResetEventSlim();
        vault.FaultObserver=stage=>{if(stage=="ObjectsReady"){reached.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(10)));}};
        var importing=Task.Run(()=>vault.ImportBytes("new.bin","new.bin",[4]));
        Assert.True(reached.Wait(TimeSpan.FromSeconds(10))); var locking=Task.Run(()=>vault.Lock());
        Assert.NotSame(locking, await Task.WhenAny(locking, Task.Delay(100))); release.Set(); Assert.True((await importing).Success); await locking;
        Assert.False(vault.IsUnlocked); vault.FaultObserver=null; Assert.True(vault.Unlock(Password).Success); VerifySeed(vault);
    }
    [Fact] public void Streaming_authentication_failure_publishes_no_plaintext()
    {
        using var vault=Seed(); var entry=Assert.Single(vault.List());
        var file=Directory.GetFiles(root,"*.obj",SearchOption.AllDirectories).Single(); var bytes=File.ReadAllBytes(file); bytes[^1]^=1;File.WriteAllBytes(file,bytes);
        Assert.False(vault.ExportEntry(entry.EntryId,root + "-output",true).Success);
        Assert.True(!Directory.Exists(root + "-output") || Directory.GetFiles(root + "-output").Length == 0);
    }
    [Fact] public void Streaming_callback_wrong_length_is_not_published()
    {
        Assert.Throws<CryptographicException>(()=>VaultExportWriter.WriteNew(Path.Combine(root,"out.bin"),2,
            Convert.ToHexString(SHA256.HashData(new byte[]{1,2})),stream=>stream.WriteByte(1)));
        Assert.False(File.Exists(Path.Combine(root,"out.bin"))); Assert.Empty(Directory.GetFiles(root));
    }
    [Fact] public void Default_format_stays_v5_without_implicit_conversion()
    {
        using var vault = new LabVaultService(root);
        Assert.True(vault.Create(Password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(Password).Success);
        Assert.Equal("spd-vault-v5-lab", vault.AuthenticatedFormat);
        Assert.False(Directory.Exists(Path.Combine(root, "generations-v6")));
    }
    [Fact] public void Unauthenticated_activation_cannot_authorize_new_generation()
    {
        using (var vault = Seed()) Assert.True(vault.ImportBytes("new.bin", "new.bin", [4]).Success);
        var pointer = Path.Combine(root, "generations-v6", "active.json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(pointer))!;
        node["Mac"] = new string('0', 64); File.WriteAllText(pointer, node.ToJsonString());
        using var reader = new LabVaultService(root);
        Assert.True(reader.Unlock(Password).Success); Assert.False(reader.IsWriteAllowed);
        Assert.Single(reader.List()); VerifySeed(reader);
    }
    [Fact] public void Changed_generation_metadata_uses_authenticated_previous_copy()
    {
        using (var vault = Seed()) Assert.True(vault.ImportBytes("new.bin", "new.bin", [4]).Success);
        var directory = Path.Combine(root, "generations-v6");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "active.json")))!;
        var metadata = Path.Combine(directory, node["Id"]!.GetValue<string>() + ".metadata");
        var bytes = File.ReadAllBytes(metadata); bytes[^1] ^= 1; File.WriteAllBytes(metadata, bytes);
        using var reader = new LabVaultService(root);
        Assert.True(reader.Unlock(Password).Success); Assert.False(reader.IsWriteAllowed); VerifySeed(reader);
    }
    [Fact] public void Export_collision_preserves_existing_destination_bytes()
    {
        using var vault = Seed(); Directory.CreateDirectory(root + "-output");
        var target = Path.Combine(root + "-output", "seed.bin"); File.WriteAllBytes(target, [9,8,7]);
        Assert.False(vault.ExportEntry(Assert.Single(vault.List()).EntryId, root + "-output", true).Success);
        Assert.Equal(new byte[]{9,8,7}, File.ReadAllBytes(target));
    }
    [Fact] public void Cancelled_export_publishes_no_file_and_can_retry()
    {
        using var vault = Seed(); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var entry = Assert.Single(vault.List());
        Assert.Throws<OperationCanceledException>(() => vault.ExportEntry(entry.EntryId, root + "-output", true, cancelled.Token));
        Assert.True(!Directory.Exists(root + "-output") || Directory.GetFiles(root + "-output").Length == 0);
        VerifySeed(vault);
    }
    [Fact] public void Forged_journal_cannot_delete_unreferenced_object()
    {
        using (var vault = Seed()) { }
        var objectPath = Directory.GetFiles(root, "*.obj", SearchOption.AllDirectories).Single();
        var before = File.ReadAllBytes(objectPath);
        File.WriteAllText(Path.Combine(root, "journal", "tx.avj"), "{invalid-json}\n");
        using var reader = new LabVaultService(root);
        Assert.True(reader.Unlock(Password).Success); Assert.False(reader.IsWriteAllowed);
        Assert.Equal(before, File.ReadAllBytes(objectPath)); VerifySeed(reader);
    }
    [Fact] public void Stream_parser_rejects_truncated_and_trailing_ciphertext()
    {
        var key = LabVaultCrypto.GenerateKey(); var aad = new byte[]{1,2};
        var encrypted = LabVaultCrypto.EncryptChunked(key, new byte[]{3,4}, aad);
        foreach (var malformed in new[]{encrypted[..^1], encrypted.Concat(new byte[]{0}).ToArray()})
        {
            using var input = new MemoryStream(malformed); using var output = new MemoryStream();
            Assert.Throws<CryptographicException>(() => LabVaultCrypto.DecryptChunkedFromFile(key,input,output,aad));
        }
        CryptographicOperations.ZeroMemory(key);
    }
    [Fact] public void HChaCha_matches_published_draft_vector()
    {
        Assert.Equal("82413B4227B27BFED30E42508A877D73A0F9E4D58A74A853C12EC41326D3ECDC",
            Convert.ToHexString(LabXChaCha20Poly1305.HChaCha20(
                Convert.FromHexString("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F"),
                Convert.FromHexString("000000090000004A0000000031415927"))));
    }

    [Theory]
    [InlineData("Used")]
    [InlineData("MacHex")]
    [InlineData("VaultId")]
    [InlineData("MemoryKb")]
    [InlineData("NullSalt")]
    public void Authenticated_recovery_tampering_rejects_without_unlock(string field)
    {
        using var vault = new LabVaultService(root, crashSafeTestFormat: true);
        var created = vault.Create(Password, LabKdfProfile.LabFast); Assert.True(created.Success);
        Assert.True(vault.Unlock(Password).Success);
        Assert.True(vault.ImportBytes("seed.bin", "seed.bin", [1,2,3]).Success); vault.Lock();
        var path = Path.Combine(root, "recovery", "recovery_slots.v6.json");
        if (field == "Used") { Assert.True(vault.UnlockWithRecoveryCode(created.RecoveryCodes[0]).Success); vault.Lock(); }
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        switch (field)
        {
            case "Used": node["Slots"]![0]!["Used"] = false; break;
            case "MacHex": node["MacHex"] = new string('0',64); break;
            case "VaultId": node["VaultId"] = "different"; break;
            case "MemoryKb": node["Slots"]![0]!["MemoryKb"] = 1048576; break;
            case "NullSalt": node["Slots"]![0]!["SaltHex"] = null; break;
        }
        File.WriteAllText(path, node.ToJsonString());
        Assert.False(vault.UnlockWithRecoveryCode(created.RecoveryCodes[0]).Success);
        Assert.False(vault.IsUnlocked);
        Assert.True(vault.Unlock(Password).Success); VerifySeed(vault);
    }
    [Theory]
    [InlineData("RecoveryTemporaryCreated",false)]
    [InlineData("RecoveryDataWritten",false)]
    [InlineData("RecoveryBeforeReplace",false)]
    [InlineData("RecoveryAfterReplace",true)]
    public void Recovery_write_failure_reports_failure_and_preserves_authenticated_data(string stage, bool consumed)
    {
        using var vault = new LabVaultService(root, crashSafeTestFormat: true);
        var created = vault.Create(Password, LabKdfProfile.LabFast); Assert.True(created.Success);
        Assert.True(vault.Unlock(Password).Success);
        Assert.True(vault.ImportBytes("seed.bin", "seed.bin", [1,2,3]).Success); vault.Lock();
        vault.FaultObserver = point => { if (point == stage) throw new IOException("injected"); };
        Assert.False(vault.UnlockWithRecoveryCode(created.RecoveryCodes[0]).Success);
        Assert.False(vault.IsUnlocked); vault.FaultObserver = null;
        Assert.Equal(!consumed, vault.UnlockWithRecoveryCode(created.RecoveryCodes[0]).Success);
        vault.Lock(); Assert.True(vault.Unlock(Password).Success); VerifySeed(vault);
    }
    [Fact] public void Missing_authenticated_recovery_store_does_not_use_legacy_store()
    {
        using var vault = new LabVaultService(root, crashSafeTestFormat: true);
        var created = vault.Create(Password, LabKdfProfile.LabFast); Assert.True(created.Success);
        var path = Path.Combine(root,"recovery","recovery_slots.v6.json");
        File.Move(path,Path.Combine(root,"recovery","recovery_slots.v5.json"));
        Assert.False(vault.UnlockWithRecoveryCode(created.RecoveryCodes[0]).Success);
        Assert.Equal("corrupt", LabRecoverySlots.Snapshot(root).Format);
        Assert.True(vault.Unlock(Password).Success);
    }
}
