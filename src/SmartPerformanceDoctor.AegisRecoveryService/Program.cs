using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.AegisRecoveryService;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Contains("--install-service"))
    {
        var rootIndex = Array.IndexOf(args, "--install-root");
        var versionIndex = Array.IndexOf(args, "--version");
        if (rootIndex >= 0 && rootIndex + 1 < args.Length && versionIndex >= 0 && versionIndex + 1 < args.Length)
        {
            var exe = Environment.ProcessPath ?? AppContext.BaseDirectory;
            var result = AegisServiceInstaller.Install(exe, args[rootIndex + 1], args[versionIndex + 1]);
            Console.WriteLine(result.Message);
            return result.Success ? 0 : 1;
        }

        Console.Error.WriteLine("usage: --install-service --install-root <path> --version <ver>");
        return 2;
    }

    if (args.Contains("--uninstall-service"))
    {
        var result = AegisServiceInstaller.Uninstall();
        Console.WriteLine(result.Message);
        return result.Success ? 0 : 1;
    }

    if (args.Contains("--watchdog-once"))
    {
        var config = AegisServiceInstaller.ReadServiceConfig();
        if (config is null)
        {
            Console.Error.WriteLine("service config missing");
            return 3;
        }

        var result = AegisWatchdogRunner.RunIfDue(config.InstallRoot, config.Version, force: true);
        Console.WriteLine(result.Message);
        return 0;
    }

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = AegisProduct.RecoveryServiceName;
    });
    builder.Services.AddHostedService<AegisWatchdogWorker>();
    var host = builder.Build();
    await host.RunAsync();
    return 0;
}