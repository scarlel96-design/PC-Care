using SmartPerformanceDoctor.Aegis;

namespace SmartPerformanceDoctor.AegisRecoveryService;

public sealed class AegisWatchdogWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = AegisServiceInstaller.ReadServiceConfig();
                if (config is not null && Directory.Exists(config.InstallRoot))
                {
                    _ = AegisWatchdogRunner.RunIfDue(config.InstallRoot, config.Version);
                }
            }
            catch
            {
                // Service must stay alive.
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}