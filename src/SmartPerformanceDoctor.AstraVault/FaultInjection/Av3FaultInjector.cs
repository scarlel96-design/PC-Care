namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

/// <summary>Injects deterministic faults at a single commit step.</summary>
public sealed class Av3FaultInjectionScenario
{
    public Av3FaultPoint FaultPoint { get; init; }
    public bool FailFlush { get; init; }
    public bool FailReread { get; init; }
    public bool FailAuthentication { get; init; }
    public bool AbortCleanup { get; init; }
}

public sealed class Av3FaultInjector
{
    private readonly Av3FaultInjectionScenario? _scenario;

    public Av3FaultInjector(Av3FaultInjectionScenario? scenario = null)
    {
        _scenario = scenario;
    }

    public void MaybeFault(Av3FaultPoint currentStep, Av3WriteTrace trace)
    {
        if (_scenario is null || _scenario.FaultPoint != currentStep)
        {
            return;
        }

        if (_scenario.FailFlush && IsPreFlushStep(currentStep))
        {
            return;
        }

        if (_scenario.FailReread && currentStep == Av3FaultPoint.AfterActivationFlushBeforeReread)
        {
            return;
        }

        if (_scenario.FailAuthentication && currentStep == Av3FaultPoint.AfterRereadBeforeAuthentication)
        {
            return;
        }

        trace.MarkFault(currentStep);
        throw new Av3SimulatedFaultException(currentStep);
    }

    private static bool IsPreFlushStep(Av3FaultPoint point) =>
        point is Av3FaultPoint.AfterObjectWriteBeforeFlush
            or Av3FaultPoint.AfterMetadataWriteBeforeFlush
            or Av3FaultPoint.AfterJournalWriteBeforeFlush
            or Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush;

    public bool ShouldFailFlush(Av3FaultPoint flushStep) =>
        _scenario?.FailFlush == true && _scenario.FaultPoint == flushStep;

    public bool ShouldFailReread() => _scenario?.FailReread == true;

    public bool ShouldFailAuthentication() => _scenario?.FailAuthentication == true;

    public bool ShouldAbortCleanup() =>
        _scenario?.AbortCleanup == true && _scenario.FaultPoint == Av3FaultPoint.DuringCleanup;

    public void ArmPostFlushTruncation(Av3FaultPoint currentStep, Av3TestStorage storage)
    {
        if (_scenario?.FaultPoint == currentStep && _scenario.FailReread)
        {
            storage.TruncateRelativePathOnNextFlush = true;
        }
    }
}

public sealed class Av3SimulatedFaultException : Exception
{
    public Av3FaultPoint FaultPoint { get; }

    public Av3SimulatedFaultException(Av3FaultPoint faultPoint)
        : base("Simulated AV3 harness fault.")
    {
        FaultPoint = faultPoint;
    }
}