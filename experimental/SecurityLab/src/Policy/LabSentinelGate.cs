namespace SmartPerformanceDoctor.SecurityLab.Policy;

/// <summary>
/// Deterministic Sentinel-style gate (design §9). No AI, no key access.
/// Closed enum decisions only.
/// </summary>
public enum LabSentinelDecision
{
    Allow,
    RequireStepUp,
    ForceReadOnly,
    DelayAndRateLimit,
    LockSession,
    QuarantineOperation,
    Deny,
    LogOnly
}

public static class LabSentinelGate
{
    public static LabSentinelDecision EvaluateExport(int fileCount, int recentUnlockFailures, bool writeAllowed)
    {
        if (recentUnlockFailures >= 5)
        {
            return LabSentinelDecision.DelayAndRateLimit;
        }

        // vault size risk: step-up then hard deny
        if (fileCount > 500)
        {
            return LabSentinelDecision.Deny;
        }

        if (fileCount > 50)
        {
            return LabSentinelDecision.RequireStepUp;
        }

        _ = writeAllowed; // RO may still export; write gate is separate
        return LabSentinelDecision.Allow;
    }

    public static LabSentinelDecision EvaluateImport(long bytes, bool writeAllowed)
    {
        if (!writeAllowed)
        {
            return LabSentinelDecision.ForceReadOnly;
        }

        if (bytes > 512L * 1024 * 1024)
        {
            return LabSentinelDecision.Deny;
        }

        return LabSentinelDecision.Allow;
    }

    /// <summary>Destructive maintenance (pack GC) — requires write session + user confirm at UI.</summary>
    public static LabSentinelDecision EvaluateMaintenance(bool writeAllowed, bool userConfirmed)
    {
        if (!writeAllowed)
        {
            return LabSentinelDecision.ForceReadOnly;
        }

        if (!userConfirmed)
        {
            return LabSentinelDecision.RequireStepUp;
        }

        return LabSentinelDecision.Allow;
    }

    public static bool IsBlocking(LabSentinelDecision d) =>
        d is LabSentinelDecision.Deny
            or LabSentinelDecision.DelayAndRateLimit
            or LabSentinelDecision.ForceReadOnly
            or LabSentinelDecision.QuarantineOperation
            or LabSentinelDecision.LockSession;
}
