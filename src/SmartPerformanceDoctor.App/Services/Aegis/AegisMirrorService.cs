using System.Text.Json;
using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.App.Branding;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.Services.Aegis;

public sealed class AegisMirrorService
{
    public static AegisMirrorService Shared { get; } = new();

    private readonly AegisIntegrityVerifier _verifier = new();
    private readonly AegisRecoveryClient _recoveryClient = new();
    private DateTimeOffset? _lastCheckAt;
    private DateTimeOffset? _lastRepairAt;

    public AegisMirrorStatus RunStartupCheck(string version) =>
        RunCheckInternal(version, attemptRepair: true, writeReport: true);

    public AegisMirrorStatus RunManualCheck(string version, bool attemptRepair) =>
        RunCheckInternal(version, attemptRepair, writeReport: true);

    private void TryCreateBaseline(
        string version,
        string operationId,
        out AegisRecoveryManifest? manifest,
        out bool signatureValid,
        out string manifestSource)
    {
        AegisRuntimeContext.SetInstallRoot(RuntimePaths.InstallRoot);
        AegisBaselineService.RebuildBaseline(RuntimePaths.InstallRoot, version);
        (manifest, signatureValid, manifestSource) = AegisManifestQuorum.TryLoadWithQuorum();
        AegisAuditChain.Append(operationId, "create-baseline", "manifest", signatureValid ? "success" : "unsigned");
    }

    public void RebuildBaseline(string version)
    {
        AegisRuntimeContext.SetInstallRoot(RuntimePaths.InstallRoot);
        var manifest = _verifier.BuildBaselineManifest(version);
        _verifier.SaveManifest(manifest);
        _verifier.SnapshotLastKnownGood(manifest);
        var capsuleHash = AegisRecoveryCapsule.BuildFromLastKnownGood(manifest);
        manifest.CapsuleHash = capsuleHash;
        _verifier.SaveManifest(manifest, capsuleHash);
        AegisAuditChain.Append($"aegis-baseline-{DateTimeOffset.Now:yyyyMMddHHmmss}", "rebuild-baseline", "manifest", "success");
        _lastCheckAt = DateTimeOffset.Now;
    }

    public AegisMirrorStatus RunPostUpdateCheck(string version, bool updateSucceeded)
    {
        if (updateSucceeded)
        {
            RebuildBaseline(version);
            return RunCheckInternal(version, attemptRepair: false, writeReport: true);
        }

        return AttemptRollback(version, "update-failed");
    }

    private AegisMirrorStatus RunCheckInternal(string version, bool attemptRepair, bool writeReport)
    {
        AegisRuntimeContext.SetInstallRoot(RuntimePaths.InstallRoot);
        if (ProcessElevationService.IsAdministrator())
        {
            AegisProtectionProvisioner.EnsureFullStack(RuntimePaths.InstallRoot, version);
        }

        if (!AegisMirrorPaths.EnsureLayout())
        {
            return new AegisMirrorStatus
            {
                Message = "복구 미러 저장소를 준비하지 못했습니다. 앱을 관리자 권한으로 실행하거나 설치를 복구하세요.",
                Findings = ["mirror-layout-unavailable"],
                ProtectionLevel = 1,
                ManifestSource = "unavailable"
            };
        }

        WritePolicyIfMissing();
        var operationId = $"aegis-check-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        var auditValid = AegisAuditChain.VerifyChain();

        var (manifest, signatureValid, manifestSource) = AegisManifestQuorum.TryLoadWithQuorum();

        if (manifest is null || !signatureValid)
        {
            TryCreateBaseline(version, operationId, out manifest, out signatureValid, out manifestSource);
        }

        if (manifest is null)
        {
            TryCreateBaseline(version, operationId, out manifest, out signatureValid, out manifestSource);
        }

        var capsuleValid = manifest is not null && AegisRecoveryCapsule.VerifyCapsuleHash(manifest.CapsuleHash);
        if (manifest is not null && !capsuleValid && attemptRepair)
        {
            TryCreateBaseline(version, operationId, out manifest, out signatureValid, out manifestSource);
            capsuleValid = manifest is not null && AegisRecoveryCapsule.VerifyCapsuleHash(manifest.CapsuleHash);
        }

        if (manifest is null)
        {
            manifest = _verifier.BuildBaselineManifest(version);
            _verifier.SaveManifest(manifest);
            signatureValid = true;
            manifestSource = "auto-baseline";
            capsuleValid = false;
        }

        var findings = _verifier.VerifyAgainstManifest(manifest);
        _lastCheckAt = DateTimeOffset.Now;
        var repaired = 0;
        attemptRepair = attemptRepair && AegisTrustState.AllowAutoRepair;

        if (findings.Count > 0 && attemptRepair)
        {
            repaired += _verifier.TryRestoreFromLastKnownGood(findings);
            findings = _verifier.VerifyAgainstManifest(manifest);

            if (findings.Count > 0)
            {
                repaired += TryElevatedRestore(manifest, version, "restore-lkg", operationId);
                findings = _verifier.VerifyAgainstManifest(manifest);
            }

            if (findings.Count > 0)
            {
                repaired += AegisSlotManager.RestoreFromBackupSlot(manifest);
                findings = _verifier.VerifyAgainstManifest(manifest);
            }

            if (findings.Count > 0 && capsuleValid && AegisTrustState.AllowCapsuleApply)
            {
                repaired += TryRestoreFromCapsule(manifest, operationId);
                findings = _verifier.VerifyAgainstManifest(manifest);

                if (findings.Count > 0)
                {
                    repaired += TryElevatedRestore(manifest, version, "restore-capsule", operationId);
                    findings = _verifier.VerifyAgainstManifest(manifest);
                }
            }

            if (repaired > 0)
            {
                _lastRepairAt = DateTimeOffset.Now;
                AegisAuditChain.Append(operationId, "auto-repair", "files", "success", restoredHash: repaired.ToString());
            }
        }

        var level = ComputeProtectionLevel(signatureValid, capsuleValid, auditValid);
        var status = BuildStatus(
            manifest,
            signatureValid,
            capsuleValid,
            auditValid,
            findings,
            repaired,
            repaired > 0,
            level,
            null,
            _lastCheckAt,
            _lastRepairAt,
            manifestSource);

        if (writeReport && (repaired > 0 || findings.Count > 0 || !signatureValid))
        {
            var reportPath = AegisRecoveryReportWriter.Write(status, operationId);
            status = status with { RecoveryReportPath = reportPath };
        }

        AegisTrustState.Initialize(status);
        return status;
    }

    private static AegisMirrorStatus BuildSafeModeStatus(
        string version,
        string reason,
        string manifestSource,
        AegisRecoveryManifest? manifest = null)
    {
        _ = version;
        return new AegisMirrorStatus
        {
            ManifestReady = manifest is not null,
            ManifestSignatureValid = false,
            CapsuleReady = File.Exists(AegisMirrorPaths.CapsuleFile),
            CapsuleHashValid = false,
            AuditChainValid = AegisAuditChain.VerifyChain(),
            ProtectedFileCount = manifest?.Files.Count ?? 0,
            IntegrityFailures = manifest is null ? 1 : 0,
            Message = AegisTrustState.BuildSafeModeMessage(),
            Findings = [reason],
            LastCheckAt = DateTimeOffset.Now,
            ProtectionLevel = 1,
            ManifestSource = manifestSource,
            SafeModeActive = true,
            SafeModeReason = reason
        };
    }

    private AegisMirrorStatus AttemptRollback(string version, string reason)
    {
        AegisRuntimeContext.SetInstallRoot(RuntimePaths.InstallRoot);
        var operationId = $"aegis-rollback-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        var (manifest, signatureValid, manifestSource) = AegisManifestQuorum.TryLoadWithQuorum();
        if (manifest is null)
        {
            return new AegisMirrorStatus
            {
                Message = "롤백 매니페스트 없음 — 설치 복구를 실행하세요.",
                ProtectionLevel = 1
            };
        }

        var repaired = AegisSlotManager.RestoreFromBackupSlot(manifest);
        if (repaired == 0)
        {
            repaired = TryRestoreFromCapsule(manifest, operationId);
        }

        if (repaired == 0)
        {
            var findings = _verifier.VerifyAgainstManifest(manifest);
            repaired = _verifier.TryRestoreFromLastKnownGood(findings);
        }

        if (repaired == 0)
        {
            repaired += TryElevatedRestore(manifest, version, "restore-capsule", operationId);
        }

        var post = _verifier.VerifyAgainstManifest(manifest);
        AegisAuditChain.Append(operationId, "update-rollback", reason, post.Count == 0 ? "success" : "partial");
        _lastRepairAt = DateTimeOffset.Now;

        var status = BuildStatus(
            manifest,
            signatureValid,
            AegisRecoveryCapsule.VerifyCapsuleHash(manifest.CapsuleHash),
            AegisAuditChain.VerifyChain(),
            post,
            repaired,
            true,
            4,
            null,
            _lastCheckAt,
            _lastRepairAt,
            manifestSource);
        var reportPath = AegisRecoveryReportWriter.Write(status, operationId);
        return status with { RecoveryReportPath = reportPath };
    }

    public string ExportOfflineCapsule() => AegisOfflineCapsule.ExportLatestPack();

    private int TryRestoreFromCapsule(AegisRecoveryManifest manifest, string operationId)
    {
        var staging = AegisStagingRestore.CreateOperationDirectory();
        if (!AegisRecoveryCapsule.TryExtractToDirectory(staging, manifest))
        {
            return 0;
        }

        if (!AegisStagingRestore.VerifyStagingHashes(staging, manifest))
        {
            AegisAuditChain.Append(operationId, "staging-verify", "capsule", "failed");
            return 0;
        }

        var restored = AegisStagingRestore.CommitStaging(staging, manifest);
        if (restored > 0)
        {
            AegisAuditChain.Append(operationId, "capsule-restore", "staging", "success", restoredHash: restored.ToString());
        }

        return restored;
    }

    private int TryElevatedRestore(AegisRecoveryManifest manifest, string version, string action, string operationId)
    {
        try
        {
            var response = _recoveryClient.SendAsync(new AegisRecoveryRequest
            {
                Action = action,
                InstallRoot = RuntimePaths.InstallRoot,
                Version = version
            }, CancellationToken.None).GetAwaiter().GetResult();

            if (response.Restored > 0 && response.Status is "ok")
            {
                AegisAuditChain.Append(operationId, "elevated-repair", action, "success", restoredHash: response.Restored.ToString());
                return response.Restored;
            }
        }
        catch
        {
            // Fall back to in-process repair result.
        }

        return 0;
    }

    private static AegisMirrorStatus BuildStatus(
        AegisRecoveryManifest manifest,
        bool signatureValid,
        bool capsuleValid,
        bool auditValid,
        IReadOnlyList<AegisIntegrityFinding> findings,
        int repaired,
        bool repairAttempted,
        int level,
        string? reportPath,
        DateTimeOffset? lastCheckAt,
        DateTimeOffset? lastRepairAt,
        string manifestSource)
    {
        var service = AegisServiceInstaller.GetStatus();
        var messages = findings
            .Select(f => $"{f.RelativePath}: {f.Reason}")
            .Take(12)
            .ToList();

        var ok = findings.Count == 0 && signatureValid;
        var message = SmartProtectionDefaults.SilentConsumerMode
            ? (repaired > 0 ? $"자동 복구 {repaired}건 완료" : "정상")
            : ok
                ? (repairAttempted && repaired > 0
                    ? $"복구 미러 정상 · 자동 복구 {repaired}건 완료"
                    : "복구 미러 무결성 정상 · 상시 보호 활성")
                : !signatureValid
                    ? (AegisSigningRuntime.IsSigningConfigured()
                        ? "매니페스트 서명 재생성 중 — 잠시 후 자동 복구됩니다"
                        : "매니페스트 서명 검증 실패 — 서명 키를 확인하세요")
                    : $"복구 미러 손상 {findings.Count}건 · 자동 복구 {repaired}건";

        return new AegisMirrorStatus
        {
            ManifestReady = true,
            ManifestSignatureValid = signatureValid,
            SafeModeActive = false,
            SafeModeReason = "",
            CapsuleReady = File.Exists(AegisMirrorPaths.CapsuleFile),
            CapsuleHashValid = capsuleValid,
            AuditChainValid = auditValid,
            ProtectedFileCount = manifest.Files.Count,
            IntegrityFailures = findings.Count,
            RepairedFiles = repaired,
            RepairAttempted = repairAttempted,
            Message = message,
            Findings = messages,
            LastCheckAt = lastCheckAt ?? DateTimeOffset.Now,
            LastRepairAt = lastRepairAt,
            ProtectionLevel = level,
            RecoveryReportPath = reportPath,
            RecoveryServiceInstalled = service.Installed,
            RecoveryServiceRunning = service.Running,
            TpmAvailable = AegisKeyProtector.IsTpmAvailable(),
            KeyProtectionMode = AegisRecoveryCapsule.ReadKeyProtectionMode() ?? "dpapi-localmachine",
            OfflineCapsuleReady = AegisOfflineCapsule.LatestOfflinePackPath() is not null,
            BackupSlotReady = AegisSlotManager.BackupSlotReady,
            ManifestSource = manifestSource
        };
    }

    private static int ComputeProtectionLevel(bool signatureValid, bool capsuleValid, bool auditValid)
    {
        var service = AegisServiceInstaller.GetStatus();
        var offline = AegisOfflineCapsule.LatestOfflinePackPath() is not null;
        var backup = AegisSlotManager.BackupSlotReady;
        var mirrorReady = !AegisMirrorPaths.UsingUserFallback;

        if (signatureValid
            && capsuleValid
            && auditValid
            && service.Installed
            && service.Running
            && offline
            && backup
            && mirrorReady)
        {
            return 5;
        }

        if (signatureValid && capsuleValid && service.Installed && service.Running)
        {
            return 4;
        }

        if (signatureValid && capsuleValid)
        {
            return 3;
        }

        return signatureValid ? 2 : 1;
    }

    private static void WritePolicyIfMissing()
    {
        if (File.Exists(AegisMirrorPaths.PolicyFile))
        {
            return;
        }

        var policy = new
        {
            product = AstraCareBranding.Product,
            level = 5,
            excludes = new[]
            {
                "secure_vault",
                "user_documents",
                "secure_delete_audit",
                "vault_audit"
            },
            disclaimer = AstraCareBranding.AegisDisclaimer
        };
        File.WriteAllText(AegisMirrorPaths.PolicyFile, JsonSerializer.Serialize(policy, new JsonSerializerOptions { WriteIndented = true }));
    }
}