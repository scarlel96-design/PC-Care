using SmartPerformanceDoctor.App.Models.Update;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class UpdateProgressReporter
{
    private readonly IProgress<UpdateProgressReport>? _progress;
    private readonly DateTimeOffset _started = DateTimeOffset.Now;

    public UpdateProgressReporter(IProgress<UpdateProgressReport>? progress)
    {
        _progress = progress;
    }

    public void Report(
        int percent,
        int stepIndex,
        int stepCount,
        string phase,
        string action,
        string detail,
        int? fileIndex = null,
        int? fileTotal = null,
        string? currentFile = null)
    {
        _progress?.Report(new UpdateProgressReport
        {
            Percent = percent,
            StepIndex = stepIndex,
            StepCount = stepCount,
            Phase = phase,
            Action = action,
            Detail = detail,
            FileIndex = fileIndex,
            FileTotal = fileTotal,
            CurrentFile = currentFile,
            Elapsed = DateTimeOffset.Now - _started
        });
    }
}