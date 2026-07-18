using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Durable;
using SmartPerformanceDoctor.AstraVault.Experimental;

namespace SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

/// <summary>Windows child-process kill FI (test-only).</summary>
public static class Av3ChildProcessKillHarness
{
    public static Av3KillSupportStatus SupportStatus => Av3KillChildRunner.GetSupportStatus();

    public static bool IsSupported => SupportStatus == Av3KillSupportStatus.Supported;

    public static Av3KillScenarioResult Run(Av3KillScenario scenario, TimeSpan? timeout = null)
    {
        var status = SupportStatus;
        if (status != Av3KillSupportStatus.Supported)
        {
            return new Av3KillScenarioResult
            {
                SupportStatus = status,
                KillMarker = scenario.KillMarker,
                Classification = Av3RecoveryClassification.UnknownFailClosed,
                VaultRootToken = "blocked"
            };
        }

        var vaultRoot = Path.Combine(Path.GetTempPath(), Av3KillChildRunner.VaultRootPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vaultRoot);
        try
        {
            var plan = BuildPlan(scenario);
            var ctx = Av3HarnessCommitContext.Generate(plan);
            var planPath = Av3KillPlanFixture.WriteToTempFile(plan, ctx.Vmk, ctx.HarnessObjectPlaintext);
            try
            {
                var (killed, markerReached, _) = Av3KillChildRunner.RunAndKillAtMarker(
                    vaultRoot,
                    scenario.KillMarker,
                    planPath,
                    timeout ?? TimeSpan.FromSeconds(120));

                var snapshot = Av3DurableStorageHarness.BuildSnapshotFromManifest(vaultRoot);
                ApplyProgressAuthHint(vaultRoot, snapshot);
                ApplyPostAuthCleanupHint(vaultRoot, snapshot, scenario.KillMarker, markerReached);
                if (markerReached)
                {
                    snapshot.FaultPoint = scenario.KillMarker;
                    if (scenario.KillMarker == Av3FaultPoint.DuringCleanup)
                    {
                        snapshot.CleanupFailed = true;
                        snapshot.CleanupCompleted = false;
                    }
                }

                return new Av3KillScenarioResult
                {
                    SupportStatus = Av3KillSupportStatus.Supported,
                    KillMarker = scenario.KillMarker,
                    Classification = Av3RecoveryClassifier.Classify(snapshot),
                    ChildKilled = killed,
                    MarkerReached = markerReached,
                    VaultRootToken = Path.GetFileName(vaultRoot)
                };
            }
            finally
            {
                try
                {
                    File.Delete(planPath);
                }
                catch
                {
                    // best-effort
                }
            }
        }
        finally
        {
            try
            {
                Directory.Delete(vaultRoot, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    public static Av3RecoveryClassification ClassifyAfterKill(string vaultRoot, Av3FaultPoint marker)
    {
        var snapshot = Av3DurableStorageHarness.BuildSnapshotFromManifest(vaultRoot);
        snapshot.FaultPoint = marker;
        return Av3RecoveryClassifier.Classify(snapshot);
    }

    private static void ApplyProgressAuthHint(string vaultRoot, Av3CommitSnapshot snapshot)
    {
        var manifest = Av3DurableManifest.Load(vaultRoot);
        if (manifest.ProgressSteps.Contains("authenticate", StringComparer.Ordinal))
        {
            snapshot.ActivationAuthenticated = manifest.ActivationAuthenticated || snapshot.ActivationAuthenticated;
            snapshot.MetadataAuthenticated = manifest.MetadataAuthenticated || snapshot.MetadataAuthenticated;
        }

        var progressPath = Path.Combine(vaultRoot, Durable.Av3DurableFileLayout.ProgressRelative);
        if (!File.Exists(progressPath))
        {
            return;
        }

        var step = File.ReadAllText(progressPath);
        if (step.Contains("authenticate", StringComparison.Ordinal)
            || step.Contains("classified_committed", StringComparison.Ordinal))
        {
            snapshot.ActivationAuthenticated = true;
            snapshot.MetadataAuthenticated = true;
        }
    }

    private static void ApplyPostAuthCleanupHint(
        string vaultRoot,
        Av3CommitSnapshot snapshot,
        Av3FaultPoint killMarker,
        bool markerReached)
    {
        if (killMarker != Av3FaultPoint.DuringCleanup || !markerReached)
        {
            return;
        }

        var manifest = Av3DurableManifest.Load(vaultRoot);
        if (manifest.ActivationAuthenticated && manifest.MetadataAuthenticated)
        {
            snapshot.ActivationAuthenticated = true;
            snapshot.MetadataAuthenticated = true;
        }
        else if (manifest.RereadSucceeded && manifest.ProgressSteps.Contains("authenticate", StringComparer.Ordinal))
        {
            snapshot.ActivationAuthenticated = true;
            snapshot.MetadataAuthenticated = true;
        }
    }

    private static Av3WritePlan BuildPlan(Av3KillScenario scenario) => new()
    {
        ContainerId = Guid.NewGuid(),
        TransactionId = Guid.NewGuid(),
        PreviousGeneration = scenario.PreviousGeneration,
        TargetGeneration = scenario.TargetGeneration,
        PreviousMetadataRootDigest = RandomNumberGenerator.GetBytes(32),
        Objects = new Av3ObjectWriteSet { ObjectWriteSetDigest = RandomNumberGenerator.GetBytes(32) },
        Metadata = new Av3MetadataWriteSet
        {
            MetadataWriteDigest = RandomNumberGenerator.GetBytes(32),
            TargetMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32)
        }
    };
}