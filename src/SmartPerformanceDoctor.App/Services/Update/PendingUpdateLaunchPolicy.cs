namespace SmartPerformanceDoctor.App.Services.Update;

/// <summary>
/// Prevents a normal application launch from unexpectedly showing UAC and
/// closing the visible window. Automatic finalization is reserved for an
/// explicit, out-of-process update handoff.
/// </summary>
public static class PendingUpdateLaunchPolicy
{
    public const string ExplicitStartupFinalizeVariable = "PCCARE_APPLY_PENDING_ON_STARTUP";

    public static bool AllowsAutomaticFinalize() =>
        AllowsAutomaticFinalize(Environment.GetEnvironmentVariable(ExplicitStartupFinalizeVariable));

    public static bool AllowsAutomaticFinalize(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
