namespace SmartPerformanceDoctor.Diagnostics;

public sealed record WorkloadPolicy(string Mode, int Seconds = 10, int Threads = 1, int MemoryMiB = 64)
{
    public void Validate()
    {
        if (Mode is not ("cpu" or "peak" or "memory" or "sequential-read" or "sequential-write" or "random-read" or "random-write" or "mixed")
            || Seconds is < 2 or > 15 || Threads is < 1 or > 4 || MemoryMiB is < 16 or > 128)
            throw new ArgumentOutOfRangeException(nameof(WorkloadPolicy));
    }
}
