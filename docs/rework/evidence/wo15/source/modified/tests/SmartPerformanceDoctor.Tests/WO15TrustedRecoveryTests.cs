using System.Security.Cryptography;
using SmartPerformanceDoctor.SecurityLab.VaultV4;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

[Collection("VaultEnvironment")]
public sealed class WO15TrustedRecoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wo15-vault-fixture-" + Guid.NewGuid().ToString("N"));
    private const string Password = "WO15-Synthetic-Only!";
    private string Vault => Path.Combine(root,"vault");
    private string Anchor => Path.Combine(root,"anchor");
    public WO15TrustedRecoveryTests() { Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root,"fixture.marker"),"WO15_SYNTHETIC_ONLY"); }
    public void Dispose() => Directory.Delete(root,true);
    private static FreshnessEvidence Evidence => new("synthetic-vault",1,new string('A',64),new string('B',64));
    private LabLocalTrustedState Store() => new(Anchor,"test-instance");
    private (LabVaultService Vault,string Code,LabLocalTrustedState Store) Seed()
    {
        var vault=new LabVaultService(Vault,crashSafeTestFormat:true);
        var created=vault.Create(Password,LabKdfProfile.LabFast); Assert.True(created.Success);
        Assert.True(vault.Unlock(Password).Success); Assert.True(vault.ImportBytes("seed.bin","seed.bin",[1,2,3]).Success);
        var store=Store(); store.Initialize(vault.CaptureFreshnessEvidence()); vault.Lock(); return(vault,created.RecoveryCodes[0],store);
    }
    [Fact] public void Production_root_cannot_enroll_test_anchor() => Assert.Throws<InvalidOperationException>(()=>new LabLocalTrustedState(Path.GetTempPath(),"instance"));
    [Fact] public void Missing_anchor_is_not_automatically_enrolled() { Assert.Equal(RecoveryPosture.AnchorMissing,Store().Verify(Evidence).Posture); Assert.False(File.Exists(Path.Combine(Anchor,"state.dpapi"))); }
    [Fact] public void Dpapi_anchor_reopens_and_detects_changed_binding()
    { var store=Store();store.Initialize(Evidence); Assert.Equal(RecoveryPosture.FreshnessVerified,Store().Verify(Evidence).Posture); Assert.Equal(RecoveryPosture.AuthenticButStale,store.Verify(Evidence with {RecoveryBinding=new string('C',64)}).Posture); }
    [Fact] public void Corrupt_anchor_is_visible_and_preserved()
    { var store=Store();store.Initialize(Evidence);var path=Path.Combine(Anchor,"state.dpapi");var data=File.ReadAllBytes(path);data[^1]^=1;File.WriteAllBytes(path,data); Assert.Equal(RecoveryPosture.AnchorCorrupted,store.Verify(Evidence).Posture);Assert.Equal(data,File.ReadAllBytes(path)); }
    [Fact] public void Wrong_instance_is_reset() { Store().Initialize(Evidence);Assert.Equal(RecoveryPosture.AnchorReset,new LabLocalTrustedState(Anchor,"other").Verify(Evidence).Posture); }
    [Fact] public void Deleted_anchor_is_missing() { Store().Initialize(Evidence);File.Delete(Path.Combine(Anchor,"state.dpapi"));Assert.Equal(RecoveryPosture.AnchorMissing,Store().Verify(Evidence).Posture); }
    [Fact] public void Exclusive_anchor_lock_reports_unavailable()
    { var store=Store();store.Initialize(Evidence);using var locked=new FileStream(Path.Combine(Anchor,"state.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);Assert.Equal(RecoveryPosture.AnchorUnavailable,store.Verify(Evidence).Posture); }
    [Fact] public void Reservation_survives_restart_and_prevents_duplicate_code()
    { var store=Store();store.Initialize(Evidence);var id=Guid.NewGuid();store.Reserve(Evidence,id,new string('D',64),Guid.NewGuid());Assert.Equal(RecoveryReceiptState.Interrupted,Store().Query(id)!.State);Assert.Throws<InvalidDataException>(()=>store.Reserve(Evidence,Guid.NewGuid(),new string('D',64),Guid.NewGuid())); }
    [Fact] public void Commit_and_acknowledgement_are_queryable_and_idempotent()
    { var store=Store();store.Initialize(Evidence);var id=Guid.NewGuid();store.Reserve(Evidence,id,new string('D',64),Guid.NewGuid());var next=Evidence with {RecoveryBinding=new string('C',64)};store.Commit(new(Evidence,next),id);Assert.Equal(RecoveryReceiptState.ReceiptPending,Store().Query(id)!.State);var ack=store.Acknowledge(id);Assert.Equal(ack,Store().Acknowledge(id));Assert.Equal(RecoveryPosture.FreshnessVerified,store.Verify(next).Posture); }
    [Fact] public void Reservation_cannot_be_acknowledged_as_completed()
    { var store=Store();store.Initialize(Evidence);var id=Guid.NewGuid();store.Reserve(Evidence,id,new string('D',64),Guid.NewGuid());Assert.Throws<InvalidDataException>(()=>store.Acknowledge(id)); }
    [Theory][InlineData("vault")][InlineData("generation")][InlineData("unchanged")]
    public void Invalid_commit_is_rejected(string mutation)
    { var store=Store();store.Initialize(Evidence);var id=Guid.NewGuid();store.Reserve(Evidence,id,new string('D',64),Guid.NewGuid());var next=Evidence with {RecoveryBinding=new string('C',64)};next=mutation switch {"vault"=>next with {VaultId="other"},"generation"=>next with {Generation=2},_=>Evidence};Assert.Throws<InvalidDataException>(()=>store.Commit(new(Evidence,next),id));Assert.Equal(RecoveryPosture.FreshnessVerified,store.Verify(Evidence).Posture); }
    [Theory][InlineData("AnchorBeforeReplace")][InlineData("AnchorAfterReplace")]
    public void Atomic_anchor_failure_preserves_old_or_new_valid_record(string stage)
    { var store=Store();store.Initialize(Evidence);var id=Guid.NewGuid();store.FaultObserver=p=>{if(p==stage)throw new IOException("injected");};Assert.Throws<IOException>(()=>store.Reserve(Evidence,id,new string('D',64),Guid.NewGuid()));Assert.Equal(RecoveryPosture.FreshnessVerified,Store().Verify(Evidence).Posture);Assert.Equal(stage=="AnchorAfterReplace",Store().Query(id)!=null); }
    [Fact] public void Trusted_recovery_exports_original_bytes_and_returns_receipt()
    { var setup=Seed();using var vault=setup.Vault;var id=Guid.NewGuid();Assert.Equal(RecoveryReceiptState.ReceiptPending,vault.UnlockWithTrustedRecovery(setup.Code,id,setup.Store).State);Assert.False(vault.IsWriteAllowed);Assert.True(vault.ExportEntry(Assert.Single(vault.List()).EntryId,Path.Combine(root,"export"),true).Success);Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(Path.Combine(root,"export","seed.bin")));Assert.Equal(RecoveryReceiptState.ReceiptAcknowledged,vault.AcknowledgeTrustedRecovery(id,setup.Store).State); }
    [Fact] public void Historical_recovery_store_replay_is_blocked_by_external_anchor()
    { var setup=Seed();using var vault=setup.Vault;var path=Path.Combine(Vault,"recovery","recovery_slots.v6.json");var original=File.ReadAllBytes(path);vault.UnlockWithTrustedRecovery(setup.Code,Guid.NewGuid(),setup.Store);vault.Lock();File.WriteAllBytes(path,original);Assert.Throws<InvalidDataException>(()=>vault.UnlockWithTrustedRecovery(setup.Code,Guid.NewGuid(),setup.Store));Assert.False(vault.IsUnlocked);Assert.True(vault.UnlockReadOnly(Password).Success);Assert.False(vault.IsWriteAllowed);Assert.Equal(RecoveryPosture.AuthenticButStale,setup.Store.Verify(vault.CaptureFreshnessEvidence()).Posture); }
    [Fact] public void Session_collision_does_not_consume_code()
    { var setup=Seed();using var vault=setup.Vault;Assert.True(vault.Unlock(Password).Success);using var second=new LabVaultService(Vault);Assert.Throws<IOException>(()=>second.UnlockWithTrustedRecovery(setup.Code,Guid.NewGuid(),setup.Store));vault.Lock();Assert.Equal(RecoveryReceiptState.ReceiptPending,second.UnlockWithTrustedRecovery(setup.Code,Guid.NewGuid(),setup.Store).State); }
    [Fact] public void Anchor_inside_vault_is_rejected()
    { var setup=Seed();using var vault=setup.Vault;Assert.Throws<InvalidOperationException>(()=>vault.UnlockWithTrustedRecovery(setup.Code,Guid.NewGuid(),new LabLocalTrustedState(Path.Combine(Vault,"anchor"),"test"))); }
    [Fact] public void Legacy_password_change_preserves_authenticated_files()
    { using var vault=new LabVaultService(Vault);Assert.True(vault.Create(Password,LabKdfProfile.LabFast).Success);Assert.True(vault.Unlock(Password).Success);var header=File.ReadAllBytes(Path.Combine(Vault,"vault.header.json"));Assert.False(vault.ChangePassword(Password,"WO15-New-Synthetic-Only!").Success);Assert.Equal(header,File.ReadAllBytes(Path.Combine(Vault,"vault.header.json")));vault.Lock();Assert.True(vault.UnlockReadOnly(Password).Success); }
    [Fact] public void Null_receipt_is_corrupt_instead_of_crashing_verification()
    {
        var store=Store();store.Initialize(Evidence);var path=Path.Combine(Anchor,"state.dpapi");
        var plain=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);
        var node=System.Text.Json.Nodes.JsonNode.Parse(plain)!;node["Receipts"]=new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null);
        CryptographicOperations.ZeroMemory(plain);
        File.WriteAllBytes(path,ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()),null,DataProtectionScope.CurrentUser));
        Assert.Equal(RecoveryPosture.AnchorCorrupted,store.Verify(Evidence).Posture);
    }
    [Fact] public void Invalid_reservation_cannot_corrupt_existing_anchor()
    {
        var store=Store();store.Initialize(Evidence);
        Assert.Throws<InvalidDataException>(()=>store.Reserve(Evidence,Guid.NewGuid(),new string('D',64),Guid.Empty));
        Assert.Equal(RecoveryPosture.FreshnessVerified,store.Verify(Evidence).Posture);
    }
    [Fact] public void Historical_activation_is_authentic_but_stale()
    {
        var setup=Seed();using var vault=setup.Vault;
        Assert.True(vault.Unlock(Password).Success);
        var old=File.ReadAllBytes(Path.Combine(Vault,"generations-v6","active.json"));
        Assert.True(vault.ChangePassword(Password,"WO15-New-Synthetic-Only!").Success);
        var changed=vault.CaptureFreshnessEvidence();
        var currentStore=new LabLocalTrustedState(Path.Combine(root,"new-anchor"),"after-password");currentStore.Initialize(changed);
        vault.Lock();File.WriteAllBytes(Path.Combine(Vault,"generations-v6","active.json"),old);
        Assert.True(vault.UnlockReadOnly(Password).Success);
        Assert.Equal(RecoveryPosture.AuthenticButStale,currentStore.Verify(vault.CaptureFreshnessEvidence()).Posture);
    }
}
