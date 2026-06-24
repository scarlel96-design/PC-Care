using System.Text;
using SmartPerformanceDoctor.App.Models.Security;
using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;
using SmartPerformanceDoctor.App.Services.Security;

namespace SmartPerformanceDoctor.Tests;

/// <summary>
/// Isolated attack simulations for Secure Vault and Secure Delete.
/// Uses a temporary LOCALAPPDATA — never touches the user's live vault.
/// </summary>
public sealed class SecurityAttackSimulationTests : IDisposable
{
    private readonly string _originalLocalAppData;
    private readonly string _tempRoot;

    private readonly string _vaultRoot;

    public SecurityAttackSimulationTests()
    {
        _originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
        _tempRoot = Path.Combine(Path.GetTempPath(), "spd-attack-sim-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _vaultRoot = Path.Combine(_tempRoot, "vault-isolated");
        Directory.CreateDirectory(_vaultRoot);
        Environment.SetEnvironmentVariable("SPD_TEST_VAULT_ROOT", _vaultRoot);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SPD_TEST_VAULT_ROOT", null);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _originalLocalAppData);
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }
        catch
        {
            // Best-effort cleanup after simulations.
        }
    }

    [Fact]
    public void Vault_WrongPassword_RejectsBruteForceAttempts()
    {
        using var vault = new SecureVaultService();
        const string password = "CorrectHorseBattery99!";
        Assert.True(vault.CreateVault(password).Success);

        for (var i = 0; i < 12; i++)
        {
            var attempt = vault.Unlock($"wrong-password-{i}");
            Assert.False(attempt.Success);
            Assert.Equal(SecureVaultState.Locked, vault.State);
        }
    }

    [Fact]
    public async Task Vault_EncryptedStore_ContainsNoPlaintextPayload()
    {
        const string secret = "TOP_SECRET_PAYLOAD_ATTACK_SIM_2026";
        const string password = "VaultTestPass123!";

        var source = Path.Combine(_tempRoot, "plain-source.txt");
        await File.WriteAllTextAsync(source, secret);

        using var vault = new SecureVaultService();
        Assert.True(vault.CreateVault(password).Success);
        Assert.True(vault.Unlock(password).Success);

        var add = await vault.AddFileAsync(source, sealOrigin: false);
        Assert.True(add.Success);

        foreach (var file in Directory.EnumerateFiles(SecureVaultPaths.Root, "*", SearchOption.AllDirectories))
        {
            var bytes = await File.ReadAllBytesAsync(file);
            var text = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain(secret, text);
        }
    }

    [Fact]
    public void Vault_TamperedManifest_RejectedOnUnlock()
    {
        const string password = "TamperManifest99!";
        using var vault = new SecureVaultService();
        Assert.True(vault.CreateVault(password).Success);
        Assert.True(vault.Unlock(password).Success);
        vault.Lock();

        var manifest = File.ReadAllBytes(SecureVaultPaths.ManifestFile);
        manifest[manifest.Length / 2] ^= 0xFF;
        File.WriteAllBytes(SecureVaultPaths.ManifestFile, manifest);

        var unlock = vault.Unlock(password);
        Assert.False(unlock.Success);
        Assert.Contains("손상", unlock.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Vault_TamperedKeyEnvelope_RejectedOnUnlock()
    {
        const string password = "TamperEnvelope99!";
        using var vault = new SecureVaultService();
        Assert.True(vault.CreateVault(password).Success);

        var envelope = File.ReadAllBytes(SecureVaultPaths.KeyEnvelopeFile);
        envelope[^1] ^= 0x55;
        File.WriteAllBytes(SecureVaultPaths.KeyEnvelopeFile, envelope);

        var unlock = vault.Unlock(password);
        Assert.False(unlock.Success);
    }

    [Fact]
    public async Task Vault_ValidSession_ExportRoundTrip()
    {
        const string password = "RoundTripVault99!";
        const string content = "round-trip-content-verification";

        var source = Path.Combine(_tempRoot, "export-me.txt");
        await File.WriteAllTextAsync(source, content);

        using var vault = new SecureVaultService();
        Assert.True(vault.CreateVault(password).Success);
        Assert.True(vault.Unlock(password).Success);
        Assert.True((await vault.AddFileAsync(source, sealOrigin: false)).Success);

        var entry = vault.Entries.FirstOrDefault();
        Assert.NotNull(entry);

        var dest = Path.Combine(_tempRoot, "export-out");
        Directory.CreateDirectory(dest);
        var exported = await vault.ExportEntryAsync(entry!.EntryId, dest);
        Assert.True(exported.Success);

        var exportedFile = Directory.GetFiles(dest, "*", SearchOption.AllDirectories).FirstOrDefault();
        Assert.NotNull(exportedFile);
        Assert.Equal(content, await File.ReadAllTextAsync(exportedFile!));
    }

    [Fact]
    public void SecureDelete_BlocksSystemAndTraversalPaths()
    {
        var service = new ProfessionalSecureDeleteService();
        var candidates = new[]
        {
            @"C:\Windows\System32\kernel32.dll",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            @"C:\"
        };

        foreach (var path in candidates.Where(p => File.Exists(p) || Directory.Exists(p)))
        {
            var plan = service.PlanDryRun([path]);
            Assert.Empty(plan.Targets);
            Assert.Single(plan.BlockedTargets);
        }
    }

    [Fact]
    public async Task SecureDelete_WrongConfirmation_Rejected()
    {
        var target = Path.Combine(_tempRoot, "delete-wrong-confirm.txt");
        await File.WriteAllTextAsync(target, "delete-me");

        var service = new ProfessionalSecureDeleteService();
        var plan = service.PlanDryRun([target]);
        Assert.Single(plan.Targets);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyAsync(plan, "잘못된 확인 문구", cancellationToken: CancellationToken.None));
        Assert.True(File.Exists(target));
    }

    [Fact]
    public async Task SecureDelete_ValidConfirmation_RemovesFileAndContent()
    {
        const string secret = "SECURE_DELETE_SECRET_MARKER_XYZ";
        var target = Path.Combine(_tempRoot, "delete-valid.txt");
        await File.WriteAllTextAsync(target, secret);

        var service = new ProfessionalSecureDeleteService();
        var plan = service.PlanDryRun([target]);
        var (deleted, failed, auditPath, auditValid) = await service.ApplyAsync(plan, "보안 삭제에 동의합니다");

        Assert.Equal(1, deleted);
        Assert.Equal(0, failed);
        Assert.False(File.Exists(target));
        Assert.True(File.Exists(auditPath));

        foreach (var file in Directory.EnumerateFiles(_tempRoot, "*", SearchOption.AllDirectories))
        {
            var text = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain(secret, text);
        }
    }

    [Fact]
    public void PathSafetyGuard_BlocksWindowsPrefixAttack()
    {
        var (allowed, reason) = PathSafetyGuard.Evaluate(@"C:\Windows\Temp\fake-user-file.txt");
        Assert.False(allowed);
        Assert.Contains("시스템", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordPolicy_RejectsWeakNewVaultPassword()
    {
        var result = SecureVaultPasswordPolicy.ValidateForNewVault("short1!");
        Assert.False(result.IsValid);
        Assert.Contains("12자", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordPolicy_AcceptsStrongPassword()
    {
        var result = SecureVaultPasswordPolicy.ValidateForNewVault("CorrectHorseBattery99!");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Vault_RateLimiter_LocksAfterRepeatedFailures()
    {
        using var vault = new SecureVaultService();
        const string password = "CorrectHorseBattery99!";
        var created = vault.CreateVault(password);
        Assert.True(created.Success);

        for (var i = 0; i < 6; i++)
        {
            _ = vault.Unlock($"WrongAttempt{i}!!Aa");
        }

        var locked = vault.Unlock(password);
        Assert.False(locked.Success);
        Assert.Contains("너무 많습니다", locked.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Vault_RecoveryKey_UnlocksWithoutPassword()
    {
        using var vault = new SecureVaultService();
        const string password = "CorrectHorseBattery99!";
        var created = vault.CreateVault(password);
        Assert.True(created.Success);
        Assert.False(string.IsNullOrWhiteSpace(created.RecoveryKey));

        vault.Lock();
        var passwordUnlock = vault.Unlock("WrongPassWord!!11");
        Assert.False(passwordUnlock.Success);

        var recoveryUnlock = vault.UnlockWithRecoveryKey(created.RecoveryKey!);
        Assert.True(recoveryUnlock.Success, recoveryUnlock.Message);
        Assert.Equal(SecureVaultState.Unlocked, vault.State);
    }

    [Fact]
    public void Vault_ChangePassword_RekeysAndIssuesNewRecoveryKey()
    {
        using var vault = new SecureVaultService();
        const string oldPassword = "CorrectHorseBattery99!";
        const string newPassword = "NewVaultPassword88!";

        var created = vault.CreateVault(oldPassword);
        Assert.True(created.Success);
        Assert.True(vault.Unlock(oldPassword).Success);

        var changed = vault.ChangeMasterPassword(oldPassword, newPassword);
        Assert.True(changed.Success);
        Assert.False(string.IsNullOrWhiteSpace(changed.RecoveryKey));

        vault.Lock();
        Assert.False(vault.Unlock(oldPassword).Success);
        var newUnlock = vault.Unlock(newPassword);
        Assert.True(newUnlock.Success, newUnlock.Message);
    }

    [Fact]
    public async Task Vault_ExportToSystemPath_Blocked()
    {
        using var vault = new SecureVaultService();
        const string password = "CorrectHorseBattery99!";
        Assert.True(vault.CreateVault(password).Success);
        Assert.True(vault.Unlock(password).Success);

        var source = Path.Combine(_tempRoot, "export-blocked.txt");
        await File.WriteAllTextAsync(source, "blocked-export-test");
        Assert.True((await vault.AddFileAsync(source, sealOrigin: false)).Success);

        var entry = vault.Entries.FirstOrDefault();
        Assert.NotNull(entry);

        var result = await vault.ExportEntryAsync(entry!.EntryId, @"C:\Windows\Temp");
        Assert.False(result.Success);
        Assert.Contains("시스템", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vault_IntegrityCheck_DetectsCorruptPrimaryShard()
    {
        const string password = "IntegrityDetect99!";
        const string content = "integrity-detect-primary-corruption";

        var source = Path.Combine(_tempRoot, "integrity-detect.txt");
        await File.WriteAllTextAsync(source, content);

        using var vault = new SecureVaultService();
        Assert.True(vault.CreateVault(password).Success);
        Assert.True(vault.Unlock(password).Success);
        Assert.True((await vault.AddFileAsync(source, sealOrigin: false)).Success);

        var entry = vault.Entries.FirstOrDefault();
        Assert.NotNull(entry);
        Assert.False(string.IsNullOrWhiteSpace(entry!.ShardName));

        var primary = Path.Combine(SecureVaultPaths.DataDirectory, entry.ShardName);
        var redundant = Path.Combine(SecureVaultPaths.RedundantDataDirectory, entry.ShardName);
        Assert.True(File.Exists(primary));
        Assert.True(File.Exists(redundant));

        var bytes = await File.ReadAllBytesAsync(primary);
        bytes[bytes.Length / 3] ^= 0xAA;
        await File.WriteAllBytesAsync(primary, bytes);

        var verifyOnly = vault.VerifyIntegrity();
        Assert.False(verifyOnly.Success);
        Assert.Equal(1, verifyOnly.FailedEntries);
        Assert.Contains(verifyOnly.Issues, i =>
            i.Kind == SecureVaultIntegrityIssueKind.CorruptShard && i.Repairable);
    }

    [Fact]
    public async Task Vault_RepairIntegrity_RestoresCorruptPrimaryFromRedundant()
    {
        const string password = "IntegrityRepair99!";
        const string content = "integrity-repair-from-redundant-copy";

        var source = Path.Combine(_tempRoot, "integrity-repair.txt");
        await File.WriteAllTextAsync(source, content);

        using var vault = new SecureVaultService();
        Assert.True(vault.CreateVault(password).Success);
        Assert.True(vault.Unlock(password).Success);
        Assert.True((await vault.AddFileAsync(source, sealOrigin: false)).Success);

        var entry = vault.Entries.FirstOrDefault();
        Assert.NotNull(entry);

        var primary = Path.Combine(SecureVaultPaths.DataDirectory, entry!.ShardName);
        var bytes = await File.ReadAllBytesAsync(primary);
        bytes[^2] ^= 0x55;
        await File.WriteAllBytesAsync(primary, bytes);

        var repaired = vault.RepairIntegrity();
        Assert.True(repaired.Success, repaired.Message);
        Assert.True(repaired.RepairedEntries >= 1);
        Assert.Equal(0, repaired.FailedEntries);
        Assert.True(repaired.ManifestIntegrityValid);
        Assert.True(repaired.AuditChainValid);

        var dest = Path.Combine(_tempRoot, "integrity-repair-out");
        Directory.CreateDirectory(dest);
        var exported = await vault.ExportEntryAsync(entry.EntryId, dest);
        Assert.True(exported.Success, exported.Message);

        var exportedFile = Directory.GetFiles(dest, "*", SearchOption.AllDirectories).Single();
        Assert.Equal(content, await File.ReadAllTextAsync(exportedFile));
    }

    [Fact]
    public async Task Vault_RepairIntegrity_BackfillsMissingRedundantCopy()
    {
        const string password = "RedundantBackfill99!";
        const string content = "redundant-backfill-on-integrity";

        var source = Path.Combine(_tempRoot, "redundant-backfill.txt");
        await File.WriteAllTextAsync(source, content);

        using var vault = new SecureVaultService();
        Assert.True(vault.CreateVault(password).Success);
        Assert.True(vault.Unlock(password).Success);
        Assert.True((await vault.AddFileAsync(source, sealOrigin: false)).Success);

        var entry = vault.Entries.FirstOrDefault();
        Assert.NotNull(entry);

        var redundant = Path.Combine(SecureVaultPaths.RedundantDataDirectory, entry!.ShardName);
        File.Delete(redundant);
        Assert.False(File.Exists(redundant));

        var repaired = vault.RepairIntegrity();
        Assert.True(repaired.Success, repaired.Message);
        Assert.True(repaired.RepairedEntries >= 1);
        Assert.True(File.Exists(redundant));
    }

    [Fact]
    public async Task Vault_RepairIntegrity_FailsWhenBothCopiesCorrupt()
    {
        const string password = "BothCorruptFail99!";
        const string content = "both-shard-copies-corrupted";

        var source = Path.Combine(_tempRoot, "both-corrupt.txt");
        await File.WriteAllTextAsync(source, content);

        using var vault = new SecureVaultService();
        Assert.True(vault.CreateVault(password).Success);
        Assert.True(vault.Unlock(password).Success);
        Assert.True((await vault.AddFileAsync(source, sealOrigin: false)).Success);

        var entry = vault.Entries.FirstOrDefault();
        Assert.NotNull(entry);

        foreach (var shardPath in new[]
                 {
                     Path.Combine(SecureVaultPaths.DataDirectory, entry!.ShardName),
                     Path.Combine(SecureVaultPaths.RedundantDataDirectory, entry.ShardName)
                 })
        {
            var bytes = await File.ReadAllBytesAsync(shardPath);
            bytes[0] ^= 0xFF;
            bytes[^1] ^= 0x0F;
            await File.WriteAllBytesAsync(shardPath, bytes);
        }

        var repaired = vault.RepairIntegrity();
        Assert.False(repaired.Success);
        Assert.Equal(1, repaired.FailedEntries);
        Assert.Contains(repaired.Issues, i =>
            i.Kind == SecureVaultIntegrityIssueKind.CorruptShard && !i.Repairable);
    }

    [Fact]
    public void SecureDelete_DryRun_ReportsForensicFullChain()
    {
        var target = Path.Combine(_tempRoot, "chain-preview.txt");
        File.WriteAllText(target, "chain-preview");

        var service = new ProfessionalSecureDeleteService();
        var plan = service.PlanDryRun([target], SecureDeleteSecurityLevel.Maximum);

        Assert.Single(plan.Targets);
        Assert.Contains("ADS", plan.ChainSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VSS", plan.ChainSummary, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(plan.Limitations));
        Assert.True(plan.RecoveryResistanceLevel >= 3);
    }

    [Fact]
    public async Task Vault_SealedFolder_RestoreToOrigin_RoundTrip()
    {
        const string password = "RestoreFolderVault99!";
        var folder = Path.Combine(_tempRoot, "sealed-restore");
        var nested = Path.Combine(folder, "nested");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(folder, "root.txt"), "root-content");
        await File.WriteAllTextAsync(Path.Combine(nested, "child.txt"), "child-content");

        using var vault = new SecureVaultService();
        Assert.True(vault.CreateVault(password).Success);
        Assert.True(vault.Unlock(password).Success);
        Assert.True((await vault.AddFolderAsync(folder, sealOrigin: true)).Success);

        Assert.False(File.Exists(Path.Combine(folder, "root.txt")));
        Assert.False(File.Exists(Path.Combine(nested, "child.txt")));
        Assert.True(File.Exists(Path.Combine(folder, ".spd_vault_sealed")));

        var root = vault.Entries.First(e => e.Kind == SecureVaultEntryKind.FolderRoot);
        var restored = await vault.RestoreToOriginAsync(root.EntryId);
        Assert.True(restored.Success, restored.Message);

        Assert.Equal("root-content", await File.ReadAllTextAsync(Path.Combine(folder, "root.txt")));
        Assert.Equal("child-content", await File.ReadAllTextAsync(Path.Combine(nested, "child.txt")));
        Assert.False(File.Exists(Path.Combine(folder, ".spd_vault_sealed")));
        Assert.DoesNotContain(vault.Entries, e => e.BundleId == root.BundleId);
    }
}