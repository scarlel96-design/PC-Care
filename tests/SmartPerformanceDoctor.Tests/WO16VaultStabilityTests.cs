using System.Security.Cryptography;
using System.Text.Json.Nodes;
using SmartPerformanceDoctor.SecurityLab.VaultV4;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

[Collection("VaultEnvironment")]
public sealed class WO16VaultStabilityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wo15-vault-fixture-" + Guid.NewGuid().ToString("N"));
    private const string Password = "WO15-Synthetic-Only!";
    private const string NewPassword = "WO15-New-Synthetic-Only!";
    private string VaultPath => Path.Combine(root, "vault");
    public WO16VaultStabilityTests()
    { Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "fixture.marker"), "WO15_SYNTHETIC_ONLY"); }
    public void Dispose() => Directory.Delete(root, true);
    private LabLocalTrustedState Store() => new(Path.Combine(root, "anchor"), "test-instance");
    private (LabVaultService Vault, string Code, LabLocalTrustedState Store) Seed(bool legacy = false)
    {
        var vault = new LabVaultService(VaultPath, crashSafeTestFormat: !legacy);
        var created = vault.Create(Password, LabKdfProfile.LabFast); Assert.True(created.Success);
        Assert.True(vault.Unlock(Password).Success);
        Assert.True(vault.ImportBytes("seed.bin", "seed.bin", [1, 2, 3]).Success);
        var store = Store(); if (!legacy) store.Initialize(vault.CaptureFreshnessEvidence());
        return (vault, created.RecoveryCodes[0], store);
    }
    private void VerifyBytes(LabVaultService vault)
    {
        var output = Path.Combine(root, "export-" + Guid.NewGuid().ToString("N"));
        Assert.True(vault.ExportEntry(vault.List().Single(e => e.DisplayName == "seed.bin").EntryId, output, true).Success);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(output, "seed.bin")));
    }
    [Theory]
    [InlineData("TrustedAfterConsumption")]
    [InlineData("AnchorBeforeReplace:2")]
    public void Consumed_operation_reconciles_without_consuming_again(string stage)
    {
        var setup = Seed(); using var vault = setup.Vault; vault.Lock(); var id = Guid.NewGuid(); var count = 0;
        vault.FaultObserver = p => { if (p == stage) throw new IOException("injected"); };
        setup.Store.FaultObserver = p => { if (p == "AnchorBeforeReplace" && ++count == 2 && stage.EndsWith(":2")) throw new IOException("injected"); };
        Assert.Throws<IOException>(() => vault.UnlockWithTrustedRecovery(setup.Code, id, setup.Store));
        Assert.Equal(RecoveryReceiptState.Interrupted, Store().Query(id)!.State);
        vault.FaultObserver = null; setup.Store.FaultObserver = null;
        Assert.True(vault.UnlockReadOnly(Password).Success);
        Assert.Equal(RecoveryReceiptState.ReceiptPending, vault.ReconcileTrustedRecovery(id, setup.Code, Store()).State);
        var evidence = vault.CaptureFreshnessEvidence(); VerifyBytes(vault);
        Assert.Equal(RecoveryReceiptState.ReceiptAcknowledged, vault.AcknowledgeTrustedRecovery(id, Store()).State);
        Assert.Equal(RecoveryReceiptState.ReceiptAcknowledged, vault.ReconcileTrustedRecovery(id, setup.Code, Store()).State);
        Assert.Equal(evidence, vault.CaptureFreshnessEvidence());
    }
    [Fact]
    public void Before_consumption_remains_review_required_and_preserves_code()
    {
        var setup = Seed(); using var vault = setup.Vault; vault.Lock(); var id = Guid.NewGuid();
        vault.FaultObserver = p => { if (p == "TrustedBeforeConsumption") throw new OperationCanceledException(); };
        var slots = File.ReadAllBytes(Path.Combine(VaultPath, "recovery", "recovery_slots.v6.json"));
        Assert.Throws<OperationCanceledException>(() => vault.UnlockWithTrustedRecovery(setup.Code, id, setup.Store));
        vault.FaultObserver = null; Assert.True(vault.UnlockReadOnly(Password).Success);
        Assert.Throws<InvalidDataException>(() => vault.ReconcileTrustedRecovery(id, setup.Code, Store()));
        Assert.Equal(slots, File.ReadAllBytes(Path.Combine(VaultPath, "recovery", "recovery_slots.v6.json")));
        VerifyBytes(vault);
    }
    [Theory][InlineData("wrong-code")][InlineData("missing-backup")][InlineData("changed-backup")]
    public void Unproven_consumption_is_not_committed(string condition)
    {
        var setup = Seed(); using var vault = setup.Vault; vault.Lock(); var id = Guid.NewGuid();
        vault.FaultObserver = p => { if (p == "TrustedAfterConsumption") throw new IOException("injected"); };
        Assert.Throws<IOException>(() => vault.UnlockWithTrustedRecovery(setup.Code, id, setup.Store));
        vault.FaultObserver = null;
        var backup = Path.Combine(VaultPath, "recovery", "recovery_slots.v6.json.previous");
        if (condition == "missing-backup") File.Delete(backup);
        if (condition == "changed-backup") { var bytes = File.ReadAllBytes(backup); bytes[^2] ^= 1; File.WriteAllBytes(backup, bytes); }
        Assert.True(vault.UnlockReadOnly(Password).Success);
        Assert.ThrowsAny<Exception>(() => vault.ReconcileTrustedRecovery(id, condition == "wrong-code" ? "invalid" : setup.Code, Store()));
        Assert.Equal(RecoveryReceiptState.Interrupted, Store().Query(id)!.State); VerifyBytes(vault);
    }
    [Fact] public void Normal_import_commits_exact_next_generation()
    {
        var setup = Seed(); using var vault = setup.Vault;
        var result = vault.ImportWithTrustedAnchor("next.bin", [4, 5], setup.Store);
        Assert.Equal(TrustedGenerationStatus.Completed, result.Status);
        Assert.Equal(result.Previous.Generation + 1, result.Next!.Generation);
        Assert.Equal(RecoveryPosture.FreshnessVerified, Store().Verify(result.Next).Posture); VerifyBytes(vault);
    }
    [Fact] public void Password_change_commits_anchor_and_reopens_bytes()
    {
        var setup = Seed(); using var vault = setup.Vault;
        var result = vault.ChangePasswordWithTrustedAnchor(Password, NewPassword, setup.Store);
        Assert.Equal(TrustedGenerationStatus.Completed, result.Status); vault.Lock();
        Assert.True(vault.UnlockReadOnly(NewPassword).Success); VerifyBytes(vault);
        Assert.Equal(RecoveryPosture.FreshnessVerified, Store().Verify(vault.CaptureFreshnessEvidence()).Posture);
    }
    [Fact] public void Anchor_write_failure_preserves_committed_vault_and_reports_pending()
    {
        var setup = Seed(); using var vault = setup.Vault;
        setup.Store.FaultObserver = p => { if (p == "AnchorBeforeReplace") throw new IOException("injected"); };
        var result = vault.ImportWithTrustedAnchor("next.bin", [4, 5], setup.Store);
        Assert.Equal(TrustedGenerationStatus.AnchorCommitPending, result.Status); Assert.True(result.VaultResult.Success);
        Assert.Equal(RecoveryPosture.AuthenticButStale, Store().Verify(result.Next!).Posture);
        vault.Lock(); Assert.True(vault.UnlockReadOnly(Password).Success); Assert.Equal(2, vault.List().Count); VerifyBytes(vault);
    }
    [Fact] public void Unresolved_reservation_blocks_normal_mutation_before_import()
    {
        var setup = Seed(); using var vault = setup.Vault; var previous = vault.CaptureFreshnessEvidence();
        setup.Store.Reserve(previous, Guid.NewGuid(), new string('D', 64), Guid.NewGuid());
        Assert.Throws<InvalidDataException>(() => vault.ImportWithTrustedAnchor("next.bin", [4], setup.Store));
        Assert.Equal(previous, vault.CaptureFreshnessEvidence()); Assert.Single(vault.List());
    }
    [Theory][InlineData(0)][InlineData(2)][InlineData(3)]
    public void Invalid_generation_sequence_is_rejected(int delta)
    {
        var setup = Seed(); using var vault = setup.Vault; var evidence = vault.CaptureFreshnessEvidence();
        Assert.Throws<InvalidDataException>(() => setup.Store.CommitGeneration(new(evidence,
            evidence with { Generation = evidence.Generation + delta, GenerationBinding = new string('D', 64) })));
        Assert.Equal(RecoveryPosture.FreshnessVerified, Store().Verify(evidence).Posture);
    }
    [Theory][InlineData("LegacyPasswordHeadersWritten")][InlineData("LegacyPasswordSlotsWritten")]
    public void V5_authenticated_backup_recovers_bytes_only_in_independent_copy(string stage)
    {
        var setup = Seed(true); using var vault = setup.Vault;
        var backup = Path.Combine(root, "authenticated-header.json"); File.Copy(Path.Combine(VaultPath, "vault.header.json"), backup);
        vault.FaultObserver = p => { if (p == stage) throw new IOException("injected"); };
        Assert.Throws<IOException>(() => vault.ReproduceLegacyPasswordChange(Password, NewPassword)); vault.Lock();
        Assert.False(vault.UnlockReadOnly(Password).Success); Assert.False(vault.UnlockReadOnly(NewPassword).Success); vault.Lock();
        var before = Hashes();
        var result = LabV5Transition.ExecuteSynthetic(VaultPath, Path.Combine(root, "work-recovery"), Password, true,
            salvageReadOnly: true, authenticatedHeaderBackup: backup);
        Assert.Equal("ReadOnlyRecovery", result.Status); Assert.False(result.Completed);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(root, "work-recovery", "recovered", "seed.bin")));
        Assert.Equal(before, Hashes());
    }
    [Fact] public void V5_tampered_backup_is_not_used_as_authenticated_data()
    {
        var setup = Seed(true); using var vault = setup.Vault; vault.Lock();
        var backup = Path.Combine(root, "bad-header.json");
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(VaultPath, "vault.header.json")))!;
        node["HeaderMacHex"] = new string('0', 64); File.WriteAllText(backup, node.ToJsonString());
        var before = Hashes();
        var result = LabV5Transition.ExecuteSynthetic(VaultPath, Path.Combine(root, "work-rejected"), Password, true,
            salvageReadOnly: true, authenticatedHeaderBackup: backup);
        Assert.Equal("PreservedButLocked", result.Status); Assert.Empty(result.ContentHashes); Assert.Equal(before, Hashes());
    }
    [Fact] public void Cancelled_snapshot_preserves_source_and_creates_no_output()
    {
        var setup = Seed(true); using var vault = setup.Vault; vault.Lock();
        var backup = Path.Combine(root, "header.json"); File.Copy(Path.Combine(VaultPath, "vault.header.json"), backup);
        var before = Hashes(); using var stop = new CancellationTokenSource(); stop.Cancel();
        Assert.Throws<OperationCanceledException>(() => LabV5Transition.ExecuteSynthetic(VaultPath,
            Path.Combine(root, "work-cancelled"), Password, true, stop.Token, salvageReadOnly: true, authenticatedHeaderBackup: backup));
        Assert.Equal(before, Hashes()); Assert.False(Directory.Exists(Path.Combine(root, "work-cancelled")));
    }
    private string[] Hashes() => Directory.GetFiles(VaultPath, "*", SearchOption.AllDirectories).Order()
        .Select(p => Path.GetRelativePath(VaultPath, p) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))).ToArray();
    [Theory]
    [InlineData("invalid-state")][InlineData("early-ack")][InlineData("invalid-code-id")]
    [InlineData("reservation-vault")][InlineData("duplicate-operation")][InlineData("invalid-posture")]
    public void Invalid_receipt_data_is_visible_and_preserved(string mutation)
    {
        var setup = Seed(); using var vault = setup.Vault;
        var evidence = vault.CaptureFreshnessEvidence();
        setup.Store.Reserve(evidence, Guid.NewGuid(), new string('D', 64), Guid.NewGuid());
        var path = Path.Combine(root, "anchor", "state.dpapi");
        var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        var node = JsonNode.Parse(plain)!; CryptographicOperations.ZeroMemory(plain);
        var receipt = node["Receipts"]![0]!;
        switch (mutation)
        {
            case "invalid-state": receipt["State"] = 999; break;
            case "early-ack": receipt["AcknowledgedAt"] = DateTimeOffset.UtcNow; break;
            case "invalid-code-id": receipt["ProtectedCodeIdentifier"] = "invalid"; break;
            case "reservation-vault": receipt["ReservedEvidence"]!["VaultId"] = "wrong-vault"; break;
            case "duplicate-operation": node["Receipts"]!.AsArray().Add(receipt.DeepClone()); break;
            case "invalid-posture": receipt["AnchorResult"] = 999; break;
        }
        var changed = ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, changed);
        Assert.Equal(RecoveryPosture.AnchorCorrupted, Store().Verify(evidence).Posture);
        Assert.Equal(changed, File.ReadAllBytes(path)); VerifyBytes(vault);
    }
    [Fact] public void Unresolved_operation_blocks_another_code_reservation()
    {
        var setup = Seed(); using var vault = setup.Vault; var evidence = vault.CaptureFreshnessEvidence();
        setup.Store.Reserve(evidence, Guid.NewGuid(), new string('D', 64), Guid.NewGuid());
        Assert.Throws<InvalidDataException>(() => setup.Store.Reserve(evidence, Guid.NewGuid(), new string('E', 64), Guid.NewGuid()));
        Assert.Equal(RecoveryPosture.FreshnessVerified, Store().Verify(evidence).Posture); VerifyBytes(vault);
    }
}
