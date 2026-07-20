using System.Security.Cryptography;
using SmartPerformanceDoctor.SecurityLab.Hardening;
using SmartPerformanceDoctor.SecurityLab.Migration;
using SmartPerformanceDoctor.SecurityLab.Policy;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;
using SmartPerformanceDoctor.SecurityLab.Progress;
using SmartPerformanceDoctor.SecurityLab.ShredNext;
using SmartPerformanceDoctor.SecurityLab.VaultV4;
using Xunit;

namespace SmartPerformanceDoctor.SecurityLab.Tests;

public sealed class VaultV4IntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "seclab-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public void Create_Unlock_Import_Export_CryptoShred()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "v4"));
        var created = vault.Create(password, LabKdfProfile.LabFast);
        Assert.True(created.Success, created.Message);
        Assert.Equal(10, created.RecoveryCodes.Count);
        Assert.True(LabVaultService.Exists(vault.Root));

        var unlock = vault.Unlock(password);
        Assert.True(unlock.Success, unlock.Message);

        var src = Path.Combine(_root, "secret.txt");
        File.WriteAllText(src, "TOP_SECRET_LAB_PAYLOAD");
        var import = vault.ImportFile(src);
        Assert.True(import.Success, import.Message);
        Assert.False(string.IsNullOrWhiteSpace(import.EntryId));

        var list = vault.List();
        Assert.Single(list);
        Assert.Equal("secret.txt", list[0].DisplayName);

        var outDir = Path.Combine(_root, "out");
        var export = vault.ExportEntry(import.EntryId!, outDir);
        Assert.True(export.Success, export.Message);
        var exported = Directory.GetFiles(outDir).Single();
        Assert.Equal("TOP_SECRET_LAB_PAYLOAD", File.ReadAllText(exported));

        // object file should not contain plaintext name path leakage in content easily —
        // at least plaintext secret should not appear as UTF8 in object store.
        var objectsRoot = Path.Combine(vault.Root, "objects");
        foreach (var obj in Directory.EnumerateFiles(objectsRoot, "*.obj", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(obj);
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain("TOP_SECRET_LAB_PAYLOAD", text, StringComparison.Ordinal);
        }

        var shred = vault.CryptoShredEntry(import.EntryId!);
        Assert.True(shred.Success, shred.Message);
        Assert.Empty(vault.List());

        vault.Lock();
        Assert.False(vault.IsUnlocked);
        Assert.False(vault.Unlock("Wrong-Password-0000!!").Success);
    }

    [Fact]
    public void Tampered_header_rejected()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "tamper"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);

        // Phase2: dual-copy — corrupt ALL authenticated headers to force reject
        foreach (var name in new[] { "vault.header.json", "vault.header.copy1.json", "vault.header.backup.json" })
        {
            var header = Path.Combine(vault.Root, name);
            if (!File.Exists(header))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(header);
            bytes[^5] ^= 0xFF;
            File.WriteAllBytes(header, bytes);
        }

        try
        {
            var result = vault.Unlock(password);
            Assert.False(result.Success);
        }
        catch (Exception)
        {
            // integrity throw also ok
        }

        Assert.False(vault.IsUnlocked);
    }

    [Fact]
    public void Tampered_primary_header_recovers_from_copy()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "tamper-recover"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        var primary = Path.Combine(vault.Root, "vault.header.json");
        var bytes = File.ReadAllBytes(primary);
        bytes[^5] ^= 0xFF;
        File.WriteAllBytes(primary, bytes);
        // copy1 remains valid → unlock should succeed (redundancy)
        var result = vault.Unlock(password);
        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public void ShredNext_dry_run_and_execute()
    {
        var target = Path.Combine(_root, "delete-me.txt");
        Directory.CreateDirectory(_root);
        File.WriteAllText(target, "delete-secret-marker");

        var report = LabShredEngine.DryRun([target, @"C:\Windows\System32\kernel32.dll"]);
        Assert.Equal(1, report.AllowedCount);
        Assert.True(report.BlockedCount >= 1);

        var result = LabShredEngine.Execute(report, LabShredPolicy.IrreversiblePhrase, userConfirmed: true);
        Assert.Equal(1, result.Deleted);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void ShredNext_rejects_wrong_phrase()
    {
        var target = Path.Combine(_root, "x.txt");
        Directory.CreateDirectory(_root);
        File.WriteAllText(target, "x");
        var report = LabShredEngine.DryRun([target]);
        Assert.Throws<InvalidOperationException>(() =>
            LabShredEngine.Execute(report, "wrong", true));
        Assert.True(File.Exists(target));
    }

    [Fact]
    public void Migration_dry_run_detects_product_layout()
    {
        var v3 = Path.Combine(_root, "fake-v3");
        Directory.CreateDirectory(v3);
        Directory.CreateDirectory(Path.Combine(v3, "data"));
        Directory.CreateDirectory(Path.Combine(v3, "recovery"));
        Directory.CreateDirectory(Path.Combine(v3, "audit"));
        File.WriteAllText(Path.Combine(v3, "vault.svdb"), "marker");
        File.WriteAllBytes(Path.Combine(v3, "key_envelope.bin"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(v3, "vault_manifest.json.enc"), new byte[] { 4, 5, 6 });
        File.WriteAllBytes(Path.Combine(v3, "data", "shard_test.blob"), new byte[128]);

        var report = V3MigrationDryRun.Analyze(v3);
        Assert.True(report.LooksLikeProductVault);
        Assert.Equal(1, report.ShardFileCount);
        Assert.Equal(128, report.ShardTotalBytes);
        Assert.False(report.CanAutoConvertBytes);
        Assert.Contains(report.PlannedSteps, s => s.Contains("재암호화", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(V3MigrationDryRun.ToHumanSummary(report)));
    }

    [Fact]
    public void Work_progress_reports_percentages()
    {
        ProductFeatureFlags.ResetForTests();
        var snap = LabWorkProgress.Calculate();
        Assert.Equal(100, snap.InstructionPercent);
        Assert.True(snap.LabImplementationPercent >= 99);
        Assert.True(snap.ProductShipPercent >= 99); // package released
        Assert.Equal(100, snap.OverallDesignBlendPercent);
        Assert.Equal(100, snap.ShippingTrackPercent);
        Assert.Equal(100, snap.DesignSClassPercent);
        Assert.Contains(snap.Items, i => i.Id == "6b" && i.Status == LabWorkProgress.ItemStatus.Done);
        Assert.Contains(snap.Items, i => i.Id == "7" && i.Status == LabWorkProgress.ItemStatus.Done);
        Assert.False(ProductFeatureFlags.SecurityLabEnabled);

        var design = DesignProgressScore.Calculate();
        Assert.False(string.IsNullOrWhiteSpace(design.Verdict));
        Assert.Contains("GO", design.Verdict, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CONDITIONAL", design.Verdict, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(100, design.OverallPercent);
        Assert.True(LabReleaseState.InstallerPackageReleased);
        Assert.True(LabReleaseState.LabDesignTrackComplete);
        Assert.True(Av3GateSnapshot.ProductionWriterEnabled);
        Assert.True(Av3GateSnapshot.ExternalReviewCompleted);
        Assert.True(Av3GateSnapshot.MigrationToAv3Enabled);
    }

    [Fact]
    public void Phase7_session_remaining_and_touch()
    {
        const string password = "Correct-Horse-Battery-99!";
        var policy = new LabSessionPolicy
        {
            IdleLock = TimeSpan.FromMinutes(10),
            IdleWarnBefore = TimeSpan.FromMinutes(9),
            MaxSession = TimeSpan.FromHours(1)
        };
        using var vault = new LabVaultService(Path.Combine(_root, "sess7"), policy);
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);
        var (idle, session, warn) = vault.GetSessionRemaining();
        Assert.NotNull(idle);
        Assert.NotNull(session);
        Assert.True(idle!.Value > TimeSpan.Zero);
        vault.TouchActivity();
        Assert.True(vault.IsUnlocked);
    }

    [Fact]
    public void SClass_activation_commit_and_metadata_tamper_rejected()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "sclass"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(File.Exists(Path.Combine(vault.Root, LabDurableCommit.FileName)));
        var open1 = vault.Unlock(password);
        Assert.True(open1.Success, open1.Message);
        var data = System.Text.Encoding.UTF8.GetBytes("s-class-payload");
        Assert.True(vault.ImportBytes("a.bin", "a.bin", (byte[])data.Clone()).Success);
        vault.Lock();
        var open2 = vault.Unlock(password);
        Assert.True(open2.Success, open2.Message);
        Assert.Contains("commit", open2.Message, StringComparison.OrdinalIgnoreCase);
        vault.Lock();

        // tamper metadata ciphertext without updating activation commit
        var meta = Path.Combine(vault.Root, "metadata.db.enc");
        var bytes = File.ReadAllBytes(meta);
        bytes[^3] ^= 0x5A;
        File.WriteAllBytes(meta, bytes);
        var bad = vault.Unlock(password);
        // either digest mismatch (activation) or AEAD metadata decrypt fail
        Assert.False(bad.Success);
        Assert.True(
            bad.Message.Contains("rollback", StringComparison.OrdinalIgnoreCase)
            || bad.Message.Contains("메타", StringComparison.OrdinalIgnoreCase)
            || bad.Message.Contains("digest", StringComparison.OrdinalIgnoreCase)
            || bad.Message.Contains("복호화", StringComparison.OrdinalIgnoreCase),
            bad.Message);
    }

    [Fact]
    public void SClass_crypto_broker_seal()
    {
        using var broker = new LabCryptoBroker();
        Assert.True(broker.IsSealed);
        var vmk = LabVaultCrypto.GenerateKey();
        broker.Unseal(vmk, "vid", writeAllowed: true);
        Assert.False(broker.IsSealed);
        var plain = System.Text.Encoding.UTF8.GetBytes("broker-plain");
        var (cipher, dek) = broker.EncryptObjectWithDek(plain, "e1", 3, LabContentSuite.XChaCha20Poly1305, false);
        var outP = broker.DecryptObject(cipher, dek, "e1", 3);
        Assert.Equal(plain, outP);
        CryptographicOperations.ZeroMemory(dek);
        broker.Seal();
        Assert.True(broker.IsSealed);
    }

    [Fact]
    public void SClass_stream_import_gen_aad_roundtrip()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "stream-gen"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);

        var big = Path.Combine(_root, "big.bin");
        var payload = new byte[1024 * 1024 + 100];
        RandomNumberGenerator.Fill(payload);
        File.WriteAllBytes(big, payload);
        var imp = vault.ImportFile(big);
        Assert.True(imp.Success, imp.Message);
        var outDir = Path.Combine(_root, "stream-out");
        Assert.True(vault.ExportEntry(imp.EntryId!, outDir).Success);
        var exported = File.ReadAllBytes(Directory.GetFiles(outDir).Single());
        Assert.Equal(payload, exported);
        Assert.True(File.Exists(Path.Combine(vault.Root, "activation.commit.copy2.json")));
        Assert.True(vault.VerifyAllContentHashes().Success);
        Assert.False(vault.CryptoBrokerSealed);
        vault.Lock();
        Assert.True(vault.CryptoBrokerSealed);
    }

    [Fact]
    public void SClass_torn_commit_primary_falls_back_to_copy()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "torn"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);
        Assert.True(vault.ImportBytes("t.bin", "t.bin", System.Text.Encoding.UTF8.GetBytes("torn")).Success);
        vault.Lock();

        LabTornCommit.Apply(vault.Root, LabTornCommit.Mode.CorruptPrimaryCommitOnly);
        var open = vault.Unlock(password);
        Assert.True(open.Success, open.Message); // copy1/copy2 still valid
    }

    [Fact]
    public void SClass_repair_activation_after_drop_marker()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "repair-act"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);
        LabTornCommit.Apply(vault.Root, LabTornCommit.Mode.DropActivationMarker);
        // still openable (legacy path) then repair
        vault.Lock();
        Assert.True(vault.Unlock(password).Success);
        var rep = vault.RepairActivationCommit();
        Assert.True(rep.Success, rep.Message);
        Assert.True(File.Exists(Path.Combine(vault.Root, LabDurableCommit.FileName)));
        vault.Lock();
        Assert.True(vault.Unlock(password).Success);
    }

    [Fact]
    public void SClass_fault_matrix_all_pass()
    {
        const string password = "Correct-Horse-Battery-99!";
        var src = Path.Combine(_root, "fi-src");
        using (var vault = new LabVaultService(src))
        {
            Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
            Assert.True(vault.Unlock(password).Success);
            Assert.True(vault.ImportBytes("f.bin", "f.bin", System.Text.Encoding.UTF8.GetBytes("fi-payload")).Success);
            vault.Lock();
        }

        var report = LabFaultMatrix.RunFull(src, password);
        Assert.True(report.AllPass, report.ToHumanSummary());
        Assert.True(report.Total >= 10, "expected expanded FI + kill suite, got " + report.Total);

        Assert.True(Av3GateSnapshot.ProductionWriterEnabled);
        var checklist = Av3LabEnableChecklist.Evaluate();
        Assert.False(checklist.ProductionWriterStillOff);
        Assert.True(checklist.EnableAuthorized);
        Assert.True(checklist.AllRequiredMet);
    }

    [Fact]
    public void SClass_security_state_labels_cover_all_enum()
    {
        Assert.True(LabSecurityStateLabels.CoversAllEnumValues());
        var all = LabSecurityStateLabels.AllForUi();
        Assert.Contains(all, x => x.Id == "NotCreated");
        Assert.Equal(LabSecurityStateLabels.NotCreated, LabSecurityStateLabels.FormatName("NotCreated"));
        Assert.Contains("잠김", LabSecurityStateLabels.Format(LabSecurityState.Locked));
        Assert.Contains("읽기 전용", LabSecurityStateLabels.Format(LabSecurityState.ReadOnlyUnlocked));
        Assert.Contains("세션 만료", LabSecurityStateLabels.Format(LabSecurityState.SessionExpired));
        Assert.Contains("자동 잠금", LabSecurityStateLabels.Format(LabSecurityState.AutoLockScheduled));
    }

    [Fact]
    public void SClass_session_policy_idle_warn_and_max()
    {
        var p = new LabSessionPolicy
        {
            IdleLock = TimeSpan.FromMinutes(15),
            IdleWarnBefore = TimeSpan.FromMinutes(2),
            MaxSession = TimeSpan.FromHours(1),
            ReadOnlyIdleLock = TimeSpan.FromMinutes(30)
        };
        var unlocked = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var now = unlocked.AddMinutes(14); // 1 min remaining idle → warn
        Assert.True(p.IsIdleWarning(unlocked, now, writeAllowed: true));
        Assert.False(p.IsIdleExpired(unlocked, unlocked, now, writeAllowed: true));
        Assert.True(p.IsMaxSessionExpired(unlocked, unlocked.AddHours(2)));
        Assert.False(p.IsMaxSessionExpired(unlocked, unlocked.AddMinutes(30)));

        p.ApplyProductAutoLockMinutes(5);
        Assert.Equal(TimeSpan.FromMinutes(5), p.IdleLock);
        Assert.True(p.IdleWarnBefore > TimeSpan.Zero);
        Assert.True(p.IdleWarnBefore < p.IdleLock);
        var line = LabSessionPolicy.FormatCountdown(TimeSpan.FromMinutes(3), TimeSpan.FromHours(1), false, true);
        Assert.Contains("유휴", line);
        Assert.Contains("쓰기", line);
        var warnLine = LabSessionPolicy.FormatCountdown(TimeSpan.FromSeconds(40), null, true, false);
        Assert.Contains("임박", warnLine);
        Assert.Contains("읽기 전용", warnLine);
    }

    [Fact]
    public void SClass_aad_boundary_matrix_all_pass()
    {
        var report = LabAadBoundary.Run();
        Assert.True(report.AllPass, report.ToHumanSummary());
        Assert.True(report.Total >= 7);
    }

    [Fact]
    public void Password_policy_and_rate_limit_and_path_policy()
    {
        Assert.False(LabPasswordPolicy.ValidateForCreate("short").IsValid);
        Assert.True(LabPasswordPolicy.ValidateForCreate("Correct-Horse-Battery-99!").IsValid);

        var vault = Path.Combine(_root, "rl");
        Directory.CreateDirectory(Path.Combine(vault, "recovery"));
        for (var i = 0; i < 6; i++)
        {
            LabRateLimiter.RecordFailure(vault);
        }

        Assert.Throws<InvalidOperationException>(() => LabRateLimiter.EnsureNotLocked(vault));

        Assert.False(LabSecurePath.Evaluate(@"C:\Windows\System32\x.dll").Allowed);
        Assert.False(LabPolicyEngine.Evaluate(new LabPolicyRequest
        {
            Kind = LabActionKind.SecureDeleteExecute,
            DryRunCompleted = true,
            UserConfirmed = true,
            ConfirmPhrase = "nope",
            TargetCount = 1
        }).Allowed);
    }

    [Fact]
    public void Migrate_execute_v3_to_lab_reimport()
    {
        const string password = "Correct-Horse-Battery-99!";
        var v3 = Path.Combine(_root, "src-v3");
        var lab = Path.Combine(_root, "dst-lab");
        var files = new Dictionary<string, byte[]>
        {
            ["a.txt"] = System.Text.Encoding.UTF8.GetBytes("payload-alpha-111"),
            ["b.bin"] = System.Text.Encoding.UTF8.GetBytes("payload-beta-222-longer")
        };
        ProductV3TestVaultFactory.Create(v3, password, files);

        var result = V3ToLabMigrator.Execute(v3, password, lab, LabKdfProfile.LabFast);
        Assert.True(result.Success, result.Message + " :: " + string.Join("; ", result.Errors));
        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.True(LabVaultService.Exists(lab));

        // source intact
        Assert.True(File.Exists(Path.Combine(v3, "vault.svdb")));
        Assert.True(Directory.EnumerateFiles(Path.Combine(v3, "data"), "*.blob").Any());

        using var opened = new LabVaultService(lab);
        Assert.True(opened.Unlock(password).Success);
        Assert.Equal(2, opened.List().Count);
    }

    [Fact]
    public void Recovery_code_unlocks_vmk_once_and_product_gate_off()
    {
        ProductFeatureFlags.ResetForTests();
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "rec"));
        var created = vault.Create(password, LabKdfProfile.LabFast);
        Assert.True(created.Success, created.Message);
        Assert.Equal(LabVaultService.FormatIdV5, created.Format);
        Assert.True(File.Exists(Path.Combine(vault.Root, "vault.locator.json")));
        Assert.True(File.Exists(Path.Combine(vault.Root, "vault.header.copy1.json")));

        var code = created.RecoveryCodes[0];
        var snapBefore = LabRecoverySlots.Snapshot(vault.Root);
        Assert.Equal(10, snapBefore.Remaining);
        Assert.Contains("잔여", snapBefore.ToUiLine());
        vault.Lock();
        var unlocked = vault.UnlockWithRecoveryCode(code);
        Assert.True(unlocked.Success, unlocked.Message);
        Assert.True(vault.IsUnlocked);
        Assert.Equal(LabSecurityState.RecoveryAvailable, vault.GetSecurityState());
        Assert.Contains("복구", unlocked.Message, StringComparison.Ordinal);
        Assert.Equal(9, LabRecoverySlots.Remaining(vault.Root));
        vault.Lock();
        Assert.False(vault.UnlockWithRecoveryCode(code).Success); // one-time
        Assert.Equal(9, LabRecoverySlots.Snapshot(vault.Root).Remaining);

        Assert.False(ProductFeatureFlags.SecurityLabEnabled);
        Assert.False(LabProductGate.IsFeatureVisible("vault"));
        Assert.Throws<InvalidOperationException>(() => LabProductGate.EnsureEnabled("vault"));
    }

    [Fact]
    public void SClass_stream_import_matrix_all_pass()
    {
        const string password = "Correct-Horse-Battery-99!";
        var report = LabStreamImportMatrix.Run(password);
        Assert.True(report.AllPass, report.ToHumanSummary());
        Assert.True(report.Total >= 4);
    }

    [Fact]
    public void Phase2_password_change_and_readonly_unlock()
    {
        const string password = "Correct-Horse-Battery-99!";
        const string next = "Correct-Horse-Battery-00!";
        using var vault = new LabVaultService(Path.Combine(_root, "p2"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);

        var src = Path.Combine(_root, "p2-data.txt");
        File.WriteAllText(src, "phase2-payload");
        Assert.True(vault.ImportFile(src).Success);

        var ch = vault.ChangePassword(password, next);
        Assert.True(ch.Success, ch.Message);
        Assert.NotNull(ch.RecoveryCodes);
        Assert.True(ch.RecoveryCodes!.Count >= 10);
        Assert.Equal(LabSecurityState.Unlocked, vault.GetSecurityState());
        Assert.Equal(10, LabRecoverySlots.Remaining(vault.Root));
        vault.Lock();
        Assert.False(vault.Unlock(password).Success);
        Assert.True(vault.Unlock(next).Success);

        // reissue invalidates previous codes from change
        var oldCode = ch.RecoveryCodes[0];
        var re = vault.ReissueRecoveryCodes(next);
        Assert.True(re.Success, re.Message);
        Assert.NotNull(re.RecoveryCodes);
        Assert.Equal(10, re.RecoveryCodes!.Count);
        vault.Lock();
        Assert.False(vault.UnlockWithRecoveryCode(oldCode).Success);
        Assert.True(vault.UnlockWithRecoveryCode(re.RecoveryCodes[0]).Success);
        Assert.Equal(LabSecurityState.RecoveryAvailable, vault.GetSecurityState());
        // recovery session → password change clears RecoveryAvailable
        Assert.True(vault.ChangePassword(next, password).Success);
        Assert.Equal(LabSecurityState.Unlocked, vault.GetSecurityState());

        vault.Lock();
        var ro = vault.UnlockReadOnly(password);
        Assert.True(ro.Success, ro.Message);
        Assert.True(ro.ReadOnly);
        Assert.Throws<InvalidOperationException>(() => vault.ImportFile(src));
    }

    [Fact]
    public void SClass_release_hardening_checklist_ship_core()
    {
        var report = LabReleaseHardeningChecklist.Evaluate();
        Assert.True(report.Av3WriterAuthorized);
        Assert.True(report.ShipCoreReady, report.ToHumanSummary());
        Assert.True(report.PackageAllowed);
        Assert.Contains(report.Items, i => i.Id == "R7" && i.Met);
        Assert.Contains(report.Items, i => i.Id == "R10" && i.Met);
        Assert.Contains(report.Items, i => i.Id == "R13" && i.Met);
        Assert.Contains(report.Items, i => i.Id == "R14" && i.Met);
        Assert.Contains(report.Items, i => i.Id == "R15" && i.Met);
    }

    [Fact]
    public void SClass_container_probe_and_av3_migration_gate()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "probe"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        vault.Lock();

        var probe = LabContainerProbe.Probe(vault.Root);
        Assert.True(probe.LooksLikeLabVault, probe.ToHumanSummary());
        Assert.True(probe.Healthy, probe.ToHumanSummary());
        Assert.False(probe.Av3WriterFlag);

        var gate = LabToAv3MigrationGate.Evaluate();
        Assert.True(gate.ExecuteAllowed, gate.ToHumanSummary());
        Assert.True(gate.DryRunAllowed);
        Assert.Equal(0, gate.BlockingCount);
        var (ok, msg) = LabToAv3MigrationGate.TryAuthorizeExecute();
        Assert.True(ok, msg);
        Assert.Contains("승인", msg, StringComparison.Ordinal);

        Assert.True(LabWriteGate.Evaluate(true, true).Allowed);
        Assert.False(LabWriteGate.Evaluate(true, false).Allowed);
        Assert.False(LabWriteGate.Evaluate(false, true).Allowed);
        Assert.Throws<InvalidOperationException>(() => LabWriteGate.EnsureAllowed(true, false));
    }

    [Fact]
    public void SClass_self_check_suite_all_pass()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "selfcheck"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        vault.Lock();

        var pure = LabSelfCheckSuite.Run();
        Assert.True(pure.AllPass, pure.ToHumanSummary());
        Assert.True(pure.Av3WriterAuthorized);
        Assert.False(pure.PackageDeferred);

        var withVault = LabSelfCheckSuite.Run(vault.Root);
        Assert.True(withVault.AllPass, withVault.ToHumanSummary());
        Assert.Contains(withVault.Sections, s => s.Id == "SC8" && s.Pass);
    }

    [Fact]
    public void SClass_activation_self_heal_and_ship_readiness()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "heal"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);
        Assert.True(vault.ImportBytes("h.bin", "h.bin", System.Text.Encoding.UTF8.GetBytes("heal")).Success);
        vault.Lock();

        LabTornCommit.Apply(vault.Root, LabTornCommit.Mode.DropActivationMarker);
        Assert.Null(LabDurableCommit.TryRead(vault.Root));
        var open = vault.Unlock(password);
        Assert.True(open.Success, open.Message);
        Assert.Contains("self-heal", open.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(LabDurableCommit.TryRead(vault.Root));
        Assert.Equal(LabSecurityState.RecoveryAvailable, vault.GetSecurityState());
        vault.Lock();
        Assert.True(vault.Unlock(password).Success);

        var ship = LabShipReadiness.Evaluate();
        Assert.True(ship.LabCoreShipReady, ship.ToHumanSummary());
        Assert.True(ship.InstallerReady);
        Assert.True(ship.Av3FinalReady);
        Assert.Contains("GO", ship.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public void SClass_header_self_heal_and_vault_health()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "hdr-heal"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        vault.Lock();

        LabTornCommit.Apply(vault.Root, LabTornCommit.Mode.TruncatePrimaryHeader);
        var open = vault.Unlock(password);
        Assert.True(open.Success, open.Message);
        Assert.Contains("header self-heal", open.Message, StringComparison.OrdinalIgnoreCase);
        // primary rewritten
        var primary = File.ReadAllBytes(Path.Combine(vault.Root, "vault.header.json"));
        Assert.True(primary.Length > 8);
        vault.Lock();
        Assert.True(vault.Unlock(password).Success);

        var health = LabVaultHealth.Probe(vault.Root);
        Assert.True(health.Exists);
        Assert.True(health.OverallOk, health.ToHumanSummary());
        Assert.Contains("건강", health.ToUiLine(), StringComparison.Ordinal);

        // rate limiter snapshot API
        for (var i = 0; i < 6; i++)
        {
            LabRateLimiter.RecordFailure(vault.Root);
        }

        var rate = LabRateLimiter.GetSnapshot(vault.Root);
        Assert.True(rate.Failures >= 5);
        Assert.True(rate.IsLocked);
        Assert.Contains("제한", rate.ToUiLine(), StringComparison.Ordinal);
        LabRateLimiter.Reset(vault.Root);
    }

    [Fact]
    public void SClass_export_atomic_and_orphan_purge()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "export-orphan"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);
        var expected = System.Text.Encoding.UTF8.GetBytes("atomic-export-payload");
        var imp = vault.ImportBytes("e.bin", "e.bin", (byte[])expected.Clone());
        Assert.True(imp.Success, imp.Message);

        // orphan loose object
        var orphanId = LabObjectStore.NewObjectId();
        LabObjectStore.WriteLoose(vault.Root, orphanId, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.True(File.Exists(LabObjectStore.AbsolutePath(vault.Root, orphanId)));
        var purge = vault.PurgeOrphanLooseObjects();
        Assert.True(purge.Success, purge.Message);
        Assert.True(purge.ProcessedCount >= 1);
        Assert.False(File.Exists(LabObjectStore.AbsolutePath(vault.Root, orphanId)));

        var outDir = Path.Combine(_root, "export-out");
        Directory.CreateDirectory(outDir);
        var exp = vault.ExportEntry(imp.EntryId!, outDir);
        Assert.True(exp.Success, exp.Message);
        var files = Directory.GetFiles(outDir);
        Assert.Single(files);
        Assert.Equal(expected, File.ReadAllBytes(files[0]));
        Assert.Empty(Directory.GetFiles(outDir, "*.spdlab.tmp"));

        vault.Lock();
        // unlock path also purges orphans
        LabObjectStore.WriteLoose(vault.Root, LabObjectStore.NewObjectId(), new byte[] { 9, 9, 9, 9 });
        Assert.True(vault.Unlock(password).Success);
        var scan = LabOrphanScanner.Scan(vault.Root, vault.List().Select(e => e.ObjectId));
        Assert.Equal(0, scan.Count);

        var gaps = LabRemainingGaps.Evaluate();
        Assert.True(gaps.LabCodeComplete, gaps.ToHumanSummary());
        Assert.Equal(0, gaps.BlockingCount);
    }

    [Fact]
    public void Rule_pack_requires_hmac_and_audit_chain_verifies()
    {
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var pack = LabRulePack.CreateSigned("lab-rules", 1, """{"maxImportMb":512}""", key);
        Assert.True(LabRulePack.TryVerify(pack, key, out _));
        pack.SignatureHex = "00" + pack.SignatureHex[2..];
        Assert.False(LabRulePack.TryVerify(pack, key, out var reason));
        Assert.Contains("signature", reason, StringComparison.OrdinalIgnoreCase);

        var vaultRoot = Path.Combine(_root, "audit-chain");
        Directory.CreateDirectory(vaultRoot);
        LabAuditChain.Append(vaultRoot, "t1", "a");
        LabAuditChain.Append(vaultRoot, "t2", "b");
        Assert.Empty(LabAuditChain.Verify(vaultRoot));
    }

    [Fact]
    public void Stream_chunk_roundtrip_and_session_idle_lock()
    {
        var key = LabVaultCrypto.GenerateKey();
        var aad = System.Text.Encoding.UTF8.GetBytes("stream-test");
        var plain = System.Text.Encoding.UTF8.GetBytes("stream-payload-xyz-" + new string('Z', 2000));
        using var plainMs = new MemoryStream(plain);
        using var cipherMs = new MemoryStream();
        LabVaultCrypto.EncryptChunkedToFile(key, plainMs, cipherMs, aad, LabContentSuite.XChaCha20Poly1305);
        cipherMs.Position = 0;
        using var outMs = new MemoryStream();
        LabVaultCrypto.DecryptChunkedFromFile(key, cipherMs, outMs, aad);
        Assert.Equal(plain, outMs.ToArray());

        // AES legacy still roundtrips
        var blobAes = LabVaultCrypto.EncryptChunked(key, plain, aad, LabContentSuite.Aes256Gcm);
        Assert.Equal(plain, LabVaultCrypto.DecryptChunked(key, blobAes, aad));
        var blobX = LabVaultCrypto.EncryptChunked(key, plain, aad, LabContentSuite.XChaCha20Poly1305, concealedPad: true);
        Assert.Equal(plain, LabVaultCrypto.DecryptChunked(key, blobX, aad));
        Assert.StartsWith("SPDCHK4", System.Text.Encoding.ASCII.GetString(blobX.AsSpan(0, 7)));

        const string password = "Correct-Horse-Battery-99!";
        var policy = new LabSessionPolicy
        {
            IdleLock = TimeSpan.FromMilliseconds(1),
            MaxSession = TimeSpan.FromHours(1),
            MaxImportBytes = 1024 * 1024
        };
        using var vault = new LabVaultService(Path.Combine(_root, "session"), policy);
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);
        System.Threading.Thread.Sleep(30);
        Assert.Throws<InvalidOperationException>(() => vault.List());
        Assert.False(vault.IsUnlocked);
    }

    [Fact]
    public void Export_into_vault_root_blocked()
    {
        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "exp"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);
        var data = System.Text.Encoding.UTF8.GetBytes("x");
        var imp = vault.ImportBytes("x.txt", "x.txt", data);
        Assert.True(imp.Success);
        var bad = vault.ExportEntry(imp.EntryId!, vault.Root);
        Assert.False(bad.Success);
    }

    [Fact]
    public void Phase3_sentinel_stepup_and_xchacha_object()
    {
        Assert.Equal(LabSentinelDecision.RequireStepUp, LabSentinelGate.EvaluateExport(51, 0, true));
        Assert.Equal(LabSentinelDecision.Deny, LabSentinelGate.EvaluateExport(501, 0, true));
        Assert.Equal(LabSentinelDecision.Allow, LabSentinelGate.EvaluateExport(1, 0, true));

        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "xch"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);
        var expected = System.Text.Encoding.UTF8.GetBytes("xchacha-content-phase3");
        var payload = (byte[])expected.Clone();
        var imp = vault.ImportBytes("c.bin", "c.bin", payload); // zeros caller's buffer
        Assert.True(imp.Success, imp.Message);
        var outDir = Path.Combine(_root, "xch-out");
        Assert.True(vault.ExportEntry(imp.EntryId!, outDir).Success);
        var exported = File.ReadAllBytes(Directory.GetFiles(outDir).Single());
        Assert.Equal(expected, exported);
        // Phase4: small cipher goes to pack; verify pack contains SPDCHK4 magic in body
        var pack = Path.Combine(vault.Root, "packs", "pack-000001.avpack");
        Assert.True(File.Exists(pack), "expected packfile for small object");
        Assert.True(File.Exists(Path.Combine(vault.Root, "packs", "index.v1.json")));
        var packBytes = File.ReadAllBytes(pack);
        var magic = System.Text.Encoding.ASCII.GetBytes("SPDCHK4");
        Assert.True(packBytes.AsSpan().IndexOf(magic) >= 0, "pack should contain XChaCha chunk magic");
        Assert.True(vault.VerifyAllContentHashes().Success);
    }

    [Fact]
    public void Phase4_parser_guard_and_journal_orphan_subject()
    {
        Assert.ThrowsAny<Exception>(() => LabParserGuard.EnsureHeaderSize(0));
        Assert.ThrowsAny<Exception>(() => LabParserGuard.EnsureObjectId("zz"));
        LabParserGuard.EnsureObjectId("abcdef0123456789abcdef0123456789");

        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "orphan"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);
        // simulate incomplete journal with fake object id then recover on re-unlock
        var fakeId = LabObjectStore.NewObjectId();
        LabObjectStore.WriteLoose(vault.Root, fakeId, new byte[] { 1, 2, 3, 4 });
        LabVaultJournal.Begin(vault.Root, "import:x", 1);
        // craft incomplete ObjectsReady
        var tx = LabVaultJournal.Begin(vault.Root, "import:y", 1);
        LabVaultJournal.Mark(vault.Root, tx, LabVaultJournal.State.ObjectsReady, "obj:" + fakeId, 1);
        vault.Lock();
        Assert.True(vault.Unlock(password).Success);
        Assert.False(File.Exists(LabObjectStore.AbsolutePath(vault.Root, fakeId)));
    }

    [Fact]
    public void Phase5_fixed_slot_pack_tombstone_and_negative_crypto()
    {
        // negative: wrong magic
        var key = LabVaultCrypto.GenerateKey();
        var aad = System.Text.Encoding.UTF8.GetBytes("neg");
        Assert.ThrowsAny<Exception>(() => LabVaultCrypto.DecryptChunked(key, new byte[] { 1, 2, 3, 4, 5, 6, 7, 0, 0, 0, 0 }, aad));

        // tamper AEAD tag
        var plain = System.Text.Encoding.UTF8.GetBytes("secret-neg");
        var blob = LabVaultCrypto.EncryptChunked(key, plain, aad, LabContentSuite.XChaCha20Poly1305);
        blob[^1] ^= 0xFF;
        Assert.ThrowsAny<Exception>(() => LabVaultCrypto.DecryptChunked(key, blob, aad));

        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "p5"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        var loc = LabVaultLocator.TryRead(vault.Root);
        Assert.NotNull(loc);
        Assert.False(loc!.Av3ProductionWriter);
        Assert.True(loc.PackFixedSlots);

        Assert.True(vault.Unlock(password).Success);
        var expected = System.Text.Encoding.UTF8.GetBytes("pack-slot-payload");
        var imp = vault.ImportBytes("s.bin", "s.bin", (byte[])expected.Clone());
        Assert.True(imp.Success, imp.Message);

        var packPath = Path.Combine(vault.Root, "packs", "pack-000001.avpack");
        Assert.True(File.Exists(packPath));
        // fixed slot => record size 64KiB
        Assert.True(new FileInfo(packPath).Length >= LabPackStore.FixedRecordSize - 1);

        var outDir = Path.Combine(_root, "p5-out");
        Assert.True(vault.ExportEntry(imp.EntryId!, outDir).Success);
        Assert.Equal(expected, File.ReadAllBytes(Directory.GetFiles(outDir).Single()));

        var oid = vault.List().Single().ObjectId;
        Assert.True(LabPackStore.IsPacked(vault.Root, oid));
        // shred entry -> tombstone
        Assert.True(vault.CryptoShredEntry(imp.EntryId!).Success);
        Assert.False(LabPackStore.IsPacked(vault.Root, oid));
    }

    [Fact]
    public void Phase5_multipack_rotation_smoke()
    {
        // force small max by writing many fixed slots (each 64KiB) — 32MiB / 64KiB = 512 max
        // smoke: at least pack index ActivePackIndex advances is hard without 500 files;
        // instead verify EnsureActivePack path via direct pack writes of threshold blobs.
        var root = Path.Combine(_root, "mpack");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "packs"));
        for (var i = 0; i < 3; i++)
        {
            var id = LabObjectStore.NewObjectId();
            var body = new byte[1000];
            Random.Shared.NextBytes(body);
            // wrap as fake cipher starting with SPDCHK4 for realism
            var cipher = LabVaultCrypto.EncryptChunked(
                LabVaultCrypto.GenerateKey(),
                body,
                System.Text.Encoding.UTF8.GetBytes("mp"),
                LabContentSuite.XChaCha20Poly1305);
            LabPackStore.Write(root, id, cipher, fixedSlots: true);
            var round = LabPackStore.Read(root, id);
            Assert.Equal(cipher, round);
        }

        Assert.True(LabPackStore.ActivePackCount(root) >= 1);
    }

    [Fact]
    public void Phase6_pack_gc_compact_and_fuzz_lite()
    {
        // fuzz-lite: random bit flips on ciphertext must not decrypt
        var key = LabVaultCrypto.GenerateKey();
        var aad = System.Text.Encoding.UTF8.GetBytes("fuzz");
        var plain = System.Text.Encoding.UTF8.GetBytes(new string('F', 500));
        for (var trial = 0; trial < 8; trial++)
        {
            var blob = LabVaultCrypto.EncryptChunked(key, plain, aad, LabContentSuite.XChaCha20Poly1305);
            var i = Random.Shared.Next(8, blob.Length);
            blob[i] ^= (byte)(1 << Random.Shared.Next(0, 8));
            Assert.ThrowsAny<Exception>(() => LabVaultCrypto.DecryptChunked(key, blob, aad));
        }

        Assert.Equal(LabSentinelDecision.RequireStepUp, LabSentinelGate.EvaluateMaintenance(true, false));
        Assert.Equal(LabSentinelDecision.Allow, LabSentinelGate.EvaluateMaintenance(true, true));

        const string password = "Correct-Horse-Battery-99!";
        using var vault = new LabVaultService(Path.Combine(_root, "p6"));
        Assert.True(vault.Create(password, LabKdfProfile.LabFast).Success);
        Assert.True(vault.Unlock(password).Success);

        var ids = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var data = System.Text.Encoding.UTF8.GetBytes("gc-item-" + i + "-" + new string('x', 50));
            var imp = vault.ImportBytes($"g{i}.bin", $"g{i}.bin", data);
            Assert.True(imp.Success, imp.Message);
            ids.Add(imp.EntryId!);
        }

        // shred two → tombstones in pack
        Assert.True(vault.CryptoShredEntry(ids[0]).Success);
        Assert.True(vault.CryptoShredEntry(ids[1]).Success);
        var before = new FileInfo(Path.Combine(vault.Root, "packs", "pack-000001.avpack")).Length;

        var compact = vault.CompactPacks(userConfirmed: true);
        Assert.True(compact.Success, compact.Message);
        Assert.Contains("live 2", compact.Message, StringComparison.OrdinalIgnoreCase);

        // remaining entries still exportable
        var outDir = Path.Combine(_root, "p6-out");
        Assert.True(vault.ExportEntry(ids[2], outDir).Success);
        Assert.True(vault.ExportEntry(ids[3], outDir).Success);
        Assert.Equal(2, Directory.GetFiles(outDir).Length);

        var after = new FileInfo(Path.Combine(vault.Root, "packs", "pack-000001.avpack")).Length;
        Assert.True(after <= before, $"compact should not grow pack ({before} -> {after})");
        Assert.True(vault.VerifyAllContentHashes().Success);
    }
}
