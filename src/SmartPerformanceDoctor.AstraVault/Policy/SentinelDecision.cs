namespace SmartPerformanceDoctor.AstraVault.Policy;

public enum SentinelDecision
{
    Allow,
    RequireStepUp,
    ForceReadOnly,
    DelayAndRateLimit,
    LockSession,
    QuarantineOperation,
    Deny,
    LogOnly,
    ModelUnavailable
}

public static class VaultPolicyEngine
{
    public static SentinelDecision EvaluateExport(int fileCount, int recentUnlockFailures, bool modelAvailable)
    {
        if (!modelAvailable)
        {
            return SentinelDecision.ModelUnavailable;
        }

        if (recentUnlockFailures >= 5)
        {
            return SentinelDecision.DelayAndRateLimit;
        }

        if (fileCount > 500)
        {
            return SentinelDecision.RequireStepUp;
        }

        return SentinelDecision.Allow;
    }
}