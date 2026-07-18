namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Inject flush success/failure at named commit stages (test-only).</summary>
public static class Av3FlushFaultHarness
{
    public static void RequireFlushOrAbort(
        Av3TestStorage storage,
        string relativePath,
        Av3FaultInjector injector,
        Av3FaultPoint flushStep,
        Av3CommitSnapshot snapshot)
    {
        if (injector.ShouldFailFlush(flushStep))
        {
            if (flushStep is not Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush)
            {
                snapshot.Aborted = true;
            }

            snapshot.FlushFailureStep = flushStep;
            throw new Av3SimulatedFaultException(flushStep);
        }

        storage.MarkFlushed(relativePath);
    }
}