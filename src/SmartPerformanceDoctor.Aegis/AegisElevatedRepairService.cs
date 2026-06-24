namespace SmartPerformanceDoctor.Aegis;

public static class AegisElevatedRepairService
{
    public static (int Restored, string Status, string Message) Execute(
        string action,
        string installRoot,
        string? stagingDirectory,
        string version)
    {
        if (string.IsNullOrWhiteSpace(installRoot) || !Directory.Exists(installRoot))
        {
            return (0, "invalid-install-root", "설치 경로가 유효하지 않습니다.");
        }

        AegisRuntimeContext.SetInstallRoot(installRoot);
        AegisMirrorPaths.EnsureLayout();

        var verifier = new AegisIntegrityVerifier();
        var (manifest, signatureValid) = verifier.TryLoadSignedManifest();
        if (manifest is null)
        {
            if (string.Equals(action, "rebuild-baseline", StringComparison.OrdinalIgnoreCase))
            {
                AegisBaselineService.RebuildBaseline(installRoot, version);
                return (0, "ok", "복구 미러 기준선이 재생성되었습니다.");
            }

            return (0, "manifest-missing", "복구 매니페스트를 찾을 수 없습니다.");
        }

        if (!signatureValid)
        {
            return (0, "signature-invalid", "매니페스트 서명 검증에 실패했습니다.");
        }

        var operationId = $"aegis-elevated-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";

        return action.ToLowerInvariant() switch
        {
            "rebuild-baseline" => RunRebuildBaseline(installRoot, version),
            "restore-lkg" => RunRestoreLkg(verifier, manifest, operationId),
            "restore-capsule" => RunRestoreCapsule(manifest, operationId),
            "commit-staging" => RunCommitStaging(manifest, stagingDirectory, operationId),
            _ => (0, "unknown-action", $"지원하지 않는 작업: {action}")
        };
    }

    private static (int, string, string) RunRebuildBaseline(string installRoot, string version)
    {
        AegisBaselineService.RebuildBaseline(installRoot, version);
        return (0, "ok", "복구 미러 기준선이 재생성되었습니다.");
    }

    private static (int, string, string) RunRestoreLkg(
        AegisIntegrityVerifier verifier,
        AegisRecoveryManifest manifest,
        string operationId)
    {
        var findings = verifier.VerifyAgainstManifest(manifest);
        var restored = verifier.TryRestoreFromLastKnownGood(findings);
        if (restored > 0)
        {
            AegisAuditChain.Append(operationId, "elevated-restore-lkg", "files", "success", restoredHash: restored.ToString());
        }

        var remaining = verifier.VerifyAgainstManifest(manifest).Count;
        return restored > 0
            ? (restored, "ok", $"Last Known Good에서 {restored}건 복구 (남은 이슈 {remaining}건)")
            : (0, remaining > 0 ? "partial" : "noop", remaining > 0 ? "복구 가능한 LKG 파일이 없거나 적용 실패" : "복구할 항목 없음");
    }

    private static (int, string, string) RunRestoreCapsule(AegisRecoveryManifest manifest, string operationId)
    {
        var staging = AegisStagingRestore.CreateOperationDirectory();
        if (!AegisRecoveryCapsule.TryExtractToDirectory(staging, manifest))
        {
            return (0, "capsule-extract-failed", "복구 캡슐 추출에 실패했습니다.");
        }

        if (!AegisStagingRestore.VerifyStagingHashes(staging, manifest))
        {
            AegisAuditChain.Append(operationId, "elevated-staging-verify", "capsule", "failed");
            return (0, "staging-verify-failed", "스테이징 해시 검증에 실패했습니다.");
        }

        var restored = AegisStagingRestore.CommitStaging(staging, manifest);
        if (restored > 0)
        {
            AegisAuditChain.Append(operationId, "elevated-capsule-restore", "staging", "success", restoredHash: restored.ToString());
        }

        return restored > 0
            ? (restored, "ok", $"캡슐에서 {restored}건 복구 완료")
            : (0, "noop", "캡슐 복구 대상 없음");
    }

    private static (int, string, string) RunCommitStaging(
        AegisRecoveryManifest manifest,
        string? stagingDirectory,
        string operationId)
    {
        if (string.IsNullOrWhiteSpace(stagingDirectory) || !Directory.Exists(stagingDirectory))
        {
            return (0, "invalid-staging", "스테이징 경로가 유효하지 않습니다.");
        }

        if (!AegisStagingRestore.VerifyStagingHashes(stagingDirectory, manifest))
        {
            return (0, "staging-verify-failed", "스테이징 해시 검증에 실패했습니다.");
        }

        var restored = AegisStagingRestore.CommitStaging(stagingDirectory, manifest);
        if (restored > 0)
        {
            AegisAuditChain.Append(operationId, "elevated-commit-staging", "staging", "success", restoredHash: restored.ToString());
        }

        return restored > 0
            ? (restored, "ok", $"스테이징에서 {restored}건 적용 완료")
            : (0, "noop", "적용할 스테이징 파일 없음");
    }
}