namespace SmartPerformanceDoctor.Setup;

internal sealed class InstallProgressReporter
{
    private readonly IProgress<(int percent, string detail)> _progress;
    private readonly int _throttleMs;
    private int _lastPercent = -1;
    private long _lastReportTicks;

    public InstallProgressReporter(IProgress<(int percent, string detail)> progress, int throttleMs = 40)
    {
        _progress = progress;
        _throttleMs = throttleMs;
    }

    public void Report(int percent, string detail, bool force = false)
    {
        percent = Math.Clamp(percent, 0, 100);
        var now = Environment.TickCount64;
        if (!force
            && percent < 100
            && percent == _lastPercent
            && now - _lastReportTicks < _throttleMs)
        {
            return;
        }

        _lastPercent = percent;
        _lastReportTicks = now;
        _progress.Report((percent, detail));
    }
}