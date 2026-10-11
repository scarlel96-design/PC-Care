using System.Security.Cryptography;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.SecurityLab.Hardening;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;
using SmartPerformanceDoctor.SecurityLab.VaultV4;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

[Collection("VaultEnvironment")]
public sealed class WO12VaultReliabilityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wo12-tests-" + Guid.NewGuid().ToString("N"));
    private const string Password = "Synthetic-Vault-12!";
    public WO12VaultReliabilityTests() { Directory.CreateDirectory(root); Environment.SetEnvironmentVariable("SPD_TEST_VAULT_ROOT", Path.Combine(root,"vault")); }
    public void Dispose() { ProductFeatureFlags.ResetForTests(); Environment.SetEnvironmentVariable("SPD_TEST_VAULT_ROOT",null); Directory.Delete(root,true); }
    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(257)] [InlineData(1048577)]
    public void Export_has_exact_bytes_and_no_owned_temporary_files(int size)
    {
        var bytes = new byte[size]; new Random(12).NextBytes(bytes);
        var target = Path.Combine(root,"out","한글 Ω..bin");
        VaultExportWriter.WriteNew(target,bytes,Hash(bytes));
        Assert.Equal(bytes,File.ReadAllBytes(target));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(target)!,".pccare-export-*.tmp"));
    }
    [Fact] public void Existing_export_and_foreign_temporary_files_are_preserved()
    {
        var target=Path.Combine(root,"a.bin"); var sentinel=new byte[]{9,8,7}; File.WriteAllBytes(target,sentinel);
        var foreign=target+".spdlab.tmp"; File.WriteAllBytes(foreign,sentinel);
        Assert.Throws<IOException>(()=>VaultExportWriter.WriteNew(target,new byte[]{1},Hash(new byte[]{1})));
        Assert.Equal(sentinel,File.ReadAllBytes(target)); Assert.Equal(sentinel,File.ReadAllBytes(foreign));
    }
    [Fact] public void Hash_failure_preserves_destination_and_cleans_only_owned_temporary_file()
    {
        var target=Path.Combine(root,"out.bin");
        Assert.Throws<CryptographicException>(()=>VaultExportWriter.WriteNew(target,new byte[]{1},new string('0',64)));
        Assert.False(File.Exists(target)); Assert.Empty(Directory.GetFiles(root,".pccare-export-*.tmp"));
    }
    [Fact] public void Cancelled_export_creates_no_plaintext()
    {
        using var token=new CancellationTokenSource(); token.Cancel();
        Assert.Throws<OperationCanceledException>(()=>VaultExportWriter.WriteNew(Path.Combine(root,"out.bin"),new byte[]{1},Hash(new byte[]{1}),token.Token));
        Assert.Empty(Directory.GetFiles(root));
    }
    [Fact] public void Locked_destination_is_preserved()
    {
        var target=Path.Combine(root,"locked.bin");File.WriteAllBytes(target,new byte[]{5});
        using var file=new FileStream(target,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        Assert.Throws<IOException>(()=>VaultExportWriter.WriteNew(target,new byte[]{1},Hash(new byte[]{1})));
        Assert.Equal(5,file.ReadByte());
    }
    [Fact] public void Parent_directory_identity_is_pinned()
    {
        var directory=Path.Combine(root,"pinned");Directory.CreateDirectory(directory);
        using var pin=VaultFileBoundary.PinDirectory(directory);
        var error = Record.Exception(() => Directory.Move(directory,directory+"-other"));
        if (error is IOException) pin.Validate();
        else Assert.Throws<IOException>(() => pin.Validate());
    }
    [Fact] public async Task Lab_folder_and_multiselection_roundtrip_preserves_sources_and_hierarchy()
    {
        using(var lab=new LabVaultService(SecureVaultPaths.Root)) Assert.True(lab.Create(Password,LabKdfProfile.LabFast).Success);
        ProductFeatureFlags.EnableProductHost(); using var vault=new SecureVaultService(); Assert.True(vault.Unlock(Password).Success);
        Assert.Equal(LabVaultService.FormatIdV5,vault.VaultFormatDescription);
        var input=Path.Combine(root,"자료"); Directory.CreateDirectory(Path.Combine(input,"a")); Directory.CreateDirectory(Path.Combine(input,"ab"));
        var data=new Dictionary<string,byte[]> { ["빈.txt"]=[],["a/같은.txt"]=[1,2,3],["ab/같은.txt"]=[1,2,3],["a/Ω.txt"]=[4,5] };
        foreach(var pair in data) File.WriteAllBytes(Path.Combine(input,pair.Key),pair.Value);
        Assert.True((await vault.AddFolderAsync(input)).Success);
        var items=vault.GetBrowsableItems(null,""); Assert.Single(items);
        var output=Path.Combine(root,"extracted");
        var result=await vault.ExportBrowsableItemsAsync(items,output,stepUpConfirmed:true); Assert.True(result.Success,result.Message);
        foreach(var pair in data) { Assert.Equal(pair.Value,File.ReadAllBytes(Path.Combine(output,"자료",pair.Key))); Assert.Equal(pair.Value,File.ReadAllBytes(Path.Combine(input,pair.Key))); }
        Assert.Equal(data.Count,Directory.GetFiles(output,"*",SearchOption.AllDirectories).Length);
        var sentinel=new byte[]{9};File.WriteAllBytes(Path.Combine(output,"자료","빈.txt"),sentinel);
        Assert.False((await vault.ExportBrowsableItemsAsync(items,output,stepUpConfirmed:true)).Success);
        Assert.Equal(sentinel,File.ReadAllBytes(Path.Combine(output,"자료","빈.txt")));
        vault.Lock(); Assert.True(vault.Unlock(Password).Success); Assert.Equal(4,vault.Entries.Count);
    }
    [Fact] public async Task Legacy_default_import_preserves_source_and_export_collision()
    {
        ProductFeatureFlags.ResetForTests(); using var vault=new SecureVaultService(); Assert.True(vault.CreateVault(Password).Success);
        var input=Path.Combine(root,"legacy.bin");File.WriteAllBytes(input,new byte[]{1,2,3});
        Assert.True((await vault.AddFileAsync(input)).Success); Assert.True(File.Exists(input));
        var output=Path.Combine(root,"out");Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"legacy.bin"),new byte[]{7});
        var result=await vault.ExportEntryAsync(vault.Entries.Single().EntryId,output);Assert.False(result.Success);
        Assert.Equal(new byte[]{7},File.ReadAllBytes(Path.Combine(output,"legacy.bin")));
    }
    [Fact] public void Lab_wrong_password_recovery_and_password_change_preserve_content()
    {
        using var vault=new LabVaultService(Path.Combine(root,"direct")); var created=vault.Create(Password,LabKdfProfile.LabFast);Assert.True(created.Success);Assert.True(vault.Unlock(Password).Success);
        var import=vault.ImportBytes("data.bin","data.bin",new byte[]{1,2,3});Assert.True(import.Success);
        vault.Lock();Assert.False(vault.Unlock("Wrong-Synthetic-12!").Success); Assert.True(vault.UnlockWithRecoveryCode(created.RecoveryCodes[0]).Success);
        Assert.True(vault.ChangePassword(Password,"New-Synthetic-Vault-12!").Success);
        vault.Lock();Assert.False(vault.Unlock(Password).Success);Assert.True(vault.Unlock("New-Synthetic-Vault-12!").Success);
        Assert.True(vault.ExportEntry(import.EntryId!,Path.Combine(root,"changed"),true).Success);Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(Path.Combine(root,"changed","data.bin")));
    }
    [Fact] public void Source_directory_scan_cancellation_is_not_a_success()
    {
        var input=Path.Combine(root,"input");Directory.CreateDirectory(input); using var token=new CancellationTokenSource();token.Cancel();
        Assert.Throws<OperationCanceledException>(()=>VaultImportPathValidator.EnumerateValidatedFiles(input,token.Token));
    }
    [Fact] public void Unauthenticated_journal_never_deletes_live_or_unreferenced_objects()
    {
        var id = LabObjectStore.NewObjectId(); var bytes = new byte[]{1,2,3};
        LabObjectStore.WriteLoose(root,id,bytes);
        var tx=LabVaultJournal.Begin(root,"synthetic",1);
        LabVaultJournal.Mark(root,tx,LabVaultJournal.State.ObjectsReady,"obj:"+id,1);
        Assert.Equal(0,LabVaultJournal.RecoverOrphans(root,1));
        Assert.Equal(0,LabVaultJournal.RecoverOrphans(root,1,Array.Empty<string>()));
        Assert.Equal(bytes,LabObjectStore.Read(root,id));
        Assert.Single(LabVaultJournal.ListIncomplete(root));
        Assert.Equal(1,LabVaultJournal.RecoverOrphans(root,1,new[]{id}));
        Assert.Equal(bytes,LabObjectStore.Read(root,id));
        Assert.Empty(LabVaultJournal.ListIncomplete(root));
    }
    [Fact] public void All_AV3_production_gates_remain_closed()
    {
        Assert.False(Av3PhaseGate.ProductionWriterEnabled);Assert.False(Av3PhaseGate.JournalWriterEnabled);Assert.False(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3PhaseGate.WriterEnableReady);Assert.False(Av3PhaseGate.ExternalReviewCompleted);Assert.False(Av3PhaseGate.ProductionEnableAuthorized);
    }
    [Fact] public void Duplicate_sessions_fail_closed_and_release_on_lock()
    {
        var path=Path.Combine(root,"sessions");
        using var first=new LabVaultService(path);using var second=new LabVaultService(path);
        Assert.True(first.Create(Password,LabKdfProfile.LabFast).Success);Assert.True(first.Unlock(Password).Success);
        Assert.False(second.Unlock(Password).Success);Assert.False(second.IsUnlocked);
        first.Lock();Assert.True(second.Unlock(Password).Success);
    }
    [Fact] public void Readonly_unlock_preserves_incomplete_journal_and_authenticated_content()
    {
        var path=Path.Combine(root,"readonly");using var vault=new LabVaultService(path);
        Assert.True(vault.Create(Password,LabKdfProfile.LabFast).Success);Assert.True(vault.Unlock(Password).Success);
        Assert.True(vault.ImportBytes("data.bin","data.bin",new byte[]{1,2}).Success);
        var entry=vault.List().Single();var journal=LabVaultJournal.Begin(path,"interrupted",1);
        vault.Lock();var before=Directory.GetFiles(path,"*",SearchOption.AllDirectories)
            .Where(p=>p.EndsWith(".enc") || p.EndsWith(".avpack") || p.Contains("journal"))
            .ToDictionary(p=>p,p=>Hash(File.ReadAllBytes(p)));
        Assert.True(vault.UnlockReadOnly(Password).Success);Assert.False(vault.IsWriteAllowed);
        Assert.Single(LabVaultJournal.ListIncomplete(path));
        foreach(var pair in before) Assert.Equal(pair.Value,Hash(File.ReadAllBytes(pair.Key)));
        Assert.Throws<InvalidOperationException>(()=>vault.ImportBytes("blocked","blocked",new byte[]{3}));
        Assert.True(vault.ExportEntry(entry.EntryId,Path.Combine(root,"readonly-export"),true).Success);
    }
}
