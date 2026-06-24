using System.Text.Json;

namespace SmartPerformanceDoctor.Aegis;

public static class AegisLaunchMarker
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void MarkLaunchSuccess(string version)
    {
        Write(new AegisLaunchState
        {
            LastSuccessfulLaunchAt = DateTimeOffset.Now,
            LastFailedLaunchAt = null,
            ConsecutiveFailures = 0,
            Version = version,
            RequiresPreLaunchRepair = false
        });
    }

    public static void MarkLaunchFailure(string version, string reason)
    {
        var current = Read();
        Write(new AegisLaunchState
        {
            LastSuccessfulLaunchAt = current.LastSuccessfulLaunchAt,
            LastFailedLaunchAt = DateTimeOffset.Now,
            ConsecutiveFailures = current.ConsecutiveFailures + 1,
            Version = version,
            LastFailureReason = reason,
            RequiresPreLaunchRepair = true
        });
    }

    public static bool RequiresPreLaunchRepair() => Read().RequiresPreLaunchRepair;

    public static AegisLaunchState Read()
    {
        if (!File.Exists(AegisMirrorPaths.LaunchStateFile))
        {
            return new AegisLaunchState();
        }

        try
        {
            return JsonSerializer.Deserialize<AegisLaunchState>(File.ReadAllText(AegisMirrorPaths.LaunchStateFile), JsonOptions)
                ?? new AegisLaunchState();
        }
        catch
        {
            return new AegisLaunchState();
        }
    }

    private static void Write(AegisLaunchState state)
    {
        if (!AegisMirrorPaths.EnsureLayout())
        {
            return;
        }

        try
        {
            File.WriteAllText(AegisMirrorPaths.LaunchStateFile, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
            // Launch marker must never throw from crash handlers.
        }
    }
}

public sealed class AegisLaunchState
{
    public DateTimeOffset? LastSuccessfulLaunchAt { get; set; }
    public DateTimeOffset? LastFailedLaunchAt { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string Version { get; set; } = "";
    public string LastFailureReason { get; set; } = "";
    public bool RequiresPreLaunchRepair { get; set; }
}