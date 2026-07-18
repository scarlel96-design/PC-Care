using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.Aegis;

public sealed class AegisProtectionBackgroundService : IDisposable
{
    public static AegisProtectionBackgroundService Shared { get; } = new();

    private readonly object _gate = new();
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _started;
    private bool _cycleRunning;

    public bool IsRunning { get; private set; }
    public DateTimeOffset? LastCycleAt { get; private set; }
    public DateTimeOffset? NextCycleAt { get; private set; }
    public string LastMessage { get; private set; } = "자동 보호 대기 중";
    public AegisMirrorStatus? LastStatus { get; private set; }

    public event Action? StatusChanged;

    public void Start(string version)
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(AegisWatchdogRunner.DefaultInterval);
            IsRunning = true;
            LastMessage = "상시 자동 보호 활성";
            _loopTask = RunLoopAsync(version, _cts.Token);
            NotifyChanged();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_started)
            {
                return;
            }

            _cts?.Cancel();
            _started = false;
            IsRunning = false;
            LastMessage = "자동 보호 중지됨";
            NotifyChanged();
        }
    }

    public async Task<AegisMirrorStatus?> RunImmediateCycleAsync(string version, CancellationToken cancellationToken = default)
    {
        if (!await TryEnterCycleAsync(cancellationToken).ConfigureAwait(false))
        {
            return LastStatus;
        }

        try
        {
            return await Task.Run(() => RunProtectionCycle(version), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ReleaseCycle();
        }
    }

    private async Task RunLoopAsync(string version, CancellationToken cancellationToken)
    {
        try
        {
            await RunImmediateCycleAsync(version, cancellationToken).ConfigureAwait(false);

            while (await _timer!.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await RunImmediateCycleAsync(version, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // App shutdown.
        }
        catch (Exception ex)
        {
            LastMessage = $"자동 보호 오류: {ex.GetBaseException().Message}";
            NotifyChanged();
        }
    }

    private AegisMirrorStatus RunProtectionCycle(string version)
    {
        AegisRuntimeContext.SetInstallRoot(RuntimePaths.InstallRoot);
        if (ProcessElevationService.IsAdministrator())
        {
            AegisProtectionProvisioner.EnsureFullStack(RuntimePaths.InstallRoot, version);
        }

        var watchdog = AegisWatchdogRunner.RunWatchdogCycle(RuntimePaths.InstallRoot, version);
        var status = AegisMirrorService.Shared.RunManualCheck(version, attemptRepair: true);

        LastCycleAt = DateTimeOffset.Now;
        NextCycleAt = LastCycleAt.Value.Add(AegisWatchdogRunner.DefaultInterval);
        LastStatus = status;
        var repairedTotal = watchdog.RepairedFiles + status.RepairedFiles;
        LastMessage = repairedTotal > 0
            ? $"자동 복구 {repairedTotal}건"
            : "정상";
        if (repairedTotal > 0)
        {
            TrayIconService.Shared.EnsureInitialized();
            TrayIconService.Shared.ShowBalloon(
                AppInfo.ProductName,
                $"백그라운드 보호: 손상된 파일 {repairedTotal}개를 복구했습니다.");
        }

        NotifyChanged();
        return status;
    }

    private async Task<bool> TryEnterCycleAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (!_cycleRunning)
                {
                    _cycleRunning = true;
                    return true;
                }
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
    }

    private void ReleaseCycle()
    {
        lock (_gate)
        {
            _cycleRunning = false;
        }
    }

    private void NotifyChanged() => StatusChanged?.Invoke();

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _timer?.Dispose();
    }
}