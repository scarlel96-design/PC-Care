using System.Text.Json;

namespace SmartPerformanceDoctor.Aegis;

public static class AegisWatchdogRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(10);

    public static AegisWatchdogResult RunIfDue(string installRoot, string version, bool force = false)
    {
        AegisRuntimeContext.SetInstallRoot(installRoot);
        AegisMirrorPaths.EnsureLayout();

        var state = ReadState();
        var due = force
            || state.LastRunAt is null
            || DateTimeOffset.Now - state.LastRunAt.Value >= DefaultInterval
            || AegisLaunchMarker.RequiresPreLaunchRepair();

        if (!due)
        {
            return new AegisWatchdogResult
            {
                Ran = false,
                Message = "다음 예정 검사까지 대기 중",
                NextDueAt = state.LastRunAt?.Add(DefaultInterval)
            };
        }

        return RunWatchdogCycle(installRoot, version);
    }

    public static AegisWatchdogResult RunWatchdogCycle(string installRoot, string version)
    {
        AegisRuntimeContext.SetInstallRoot(installRoot);
        AegisMirrorPaths.EnsureLayout();

        var operationId = $"aegis-watchdog-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        var verifier = new AegisIntegrityVerifier();
        var (manifest, signatureValid, _) = AegisManifestQuorum.TryLoadWithQuorum();
        if (manifest is null)
        {
            AegisBaselineService.RebuildBaseline(installRoot, version);
            WriteState(DateTimeOffset.Now, 0, "baseline-created");
            return new AegisWatchdogResult { Ran = true, Message = "매니페스트 없음 — 기준선 생성", RepairedFiles = 0 };
        }

        if (!signatureValid)
        {
            AegisAuditChain.Append(operationId, "watchdog", "manifest", "signature-failed");
            if (AegisSigningRuntime.IsSigningConfigured() || AegisTrustPolicy.AllowRelaxedMirrorTrust())
            {
                AegisBaselineService.RebuildBaseline(installRoot, version);
                (manifest, signatureValid, _) = AegisManifestQuorum.TryLoadWithQuorum();
                if (!signatureValid || manifest is null)
                {
                    return new AegisWatchdogResult
                    {
                        Ran = true,
                        Message = "매니페스트 서명 자동 재생성 실패",
                        IntegrityFailures = 1
                    };
                }

                AegisAuditChain.Append(operationId, "watchdog", "manifest", "signature-rebuilt");
            }
            else
            {
                return new AegisWatchdogResult
                {
                    Ran = true,
                    Message = "매니페스트 서명 실패 — 서명 키 없음",
                    IntegrityFailures = 1
                };
            }
        }

        var findings = verifier.VerifyAgainstManifest(manifest);
        var repaired = verifier.TryRestoreFromLastKnownGood(findings);
        findings = verifier.VerifyAgainstManifest(manifest);

        if (findings.Count > 0)
        {
            repaired += AegisSlotManager.RestoreFromBackupSlot(manifest);
            findings = verifier.VerifyAgainstManifest(manifest);
        }

        if (findings.Count > 0 && AegisRecoveryCapsule.VerifyCapsuleHash(manifest.CapsuleHash))
        {
            var staging = AegisStagingRestore.CreateOperationDirectory();
            if (AegisRecoveryCapsule.TryExtractToDirectory(staging, manifest)
                && AegisStagingRestore.VerifyStagingHashes(staging, manifest))
            {
                repaired += AegisStagingRestore.CommitStaging(staging, manifest);
                findings = verifier.VerifyAgainstManifest(manifest);
            }
        }

        if (repaired > 0)
        {
            AegisAuditChain.Append(operationId, "watchdog-repair", "files", "success", restoredHash: repaired.ToString());
        }

        AegisLaunchMarker.MarkLaunchSuccess(version);
        WriteState(DateTimeOffset.Now, repaired, repaired > 0 ? "repaired" : "ok");
        return new AegisWatchdogResult
        {
            Ran = true,
            Message = repaired > 0 ? $"자동 복구 {repaired}건 완료" : "무결성 정상",
            RepairedFiles = repaired,
            IntegrityFailures = findings.Count
        };
    }

    private static AegisWatchdogState ReadState()
    {
        if (!File.Exists(AegisMirrorPaths.WatchdogStateFile))
        {
            return new AegisWatchdogState();
        }

        try
        {
            return JsonSerializer.Deserialize<AegisWatchdogState>(File.ReadAllText(AegisMirrorPaths.WatchdogStateFile), JsonOptions)
                ?? new AegisWatchdogState();
        }
        catch
        {
            return new AegisWatchdogState();
        }
    }

    private static void WriteState(DateTimeOffset runAt, int repaired, string status)
    {
        var state = new AegisWatchdogState
        {
            LastRunAt = runAt,
            LastStatus = status,
            LastRepairedFiles = repaired
        };
        File.WriteAllText(AegisMirrorPaths.WatchdogStateFile, JsonSerializer.Serialize(state, JsonOptions));
    }
}

public sealed class AegisWatchdogState
{
    public DateTimeOffset? LastRunAt { get; set; }
    public string LastStatus { get; set; } = "";
    public int LastRepairedFiles { get; set; }
}

public sealed class AegisWatchdogResult
{
    public bool Ran { get; init; }
    public string Message { get; init; } = "";
    public int RepairedFiles { get; init; }
    public int IntegrityFailures { get; init; }
    public DateTimeOffset? NextDueAt { get; init; }
}