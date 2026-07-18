using System.Diagnostics;

namespace SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

public static class Av3KillChildRunner
{
    public const string VaultRootPrefix = "av3-e3-kill-";

    public static Av3KillSupportStatus GetSupportStatus()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Av3KillSupportStatus.UnsupportedPlatform;
        }

        return ResolveWorkerDll() is null
            ? Av3KillSupportStatus.WorkerNotFound
            : Av3KillSupportStatus.Supported;
    }

    public static string? ResolveWorkerDll()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "killworker", "SmartPerformanceDoctor.AstraVault.KillWorker.dll"),
            Path.Combine(baseDir, "SmartPerformanceDoctor.AstraVault.KillWorker.dll")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public static (bool Killed, bool MarkerReached, int ExitCode) RunAndKillAtMarker(
        string vaultRoot,
        Av3FaultPoint marker,
        string planPath,
        TimeSpan timeout)
    {
        var worker = ResolveWorkerDll();
        if (worker is null)
        {
            return (false, false, -1);
        }

        var dotnet = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "", "dotnet.exe");
        if (!File.Exists(dotnet))
        {
            dotnet = "dotnet";
        }

        var workerDir = Path.GetDirectoryName(worker)!;
        var psi = new ProcessStartInfo
        {
            FileName = dotnet,
            Arguments = $"exec \"{worker}\" \"{vaultRoot}\" \"{(int)marker}\" \"{planPath}\"",
            WorkingDirectory = workerDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start kill worker.");
        var markerPath = Path.Combine(vaultRoot, Durable.Av3DurableFileLayout.KillMarkerReachedRelative);
        var sw = Stopwatch.StartNew();
        var markerReached = false;
        while (sw.Elapsed < timeout && !process.HasExited)
        {
            if (File.Exists(markerPath))
            {
                markerReached = true;
                break;
            }

            Thread.Sleep(25);
        }

        var killed = false;
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            killed = true;
        }

        process.WaitForExit(5000);
        if (!markerReached)
        {
            markerReached = TryReadReachedMarker(vaultRoot, marker);
        }

        return (killed, markerReached, process.ExitCode);
    }

    private static bool TryReadReachedMarker(string vaultRoot, Av3FaultPoint expected)
    {
        var markerPath = Path.Combine(vaultRoot, Durable.Av3DurableFileLayout.KillMarkerReachedRelative);
        if (!File.Exists(markerPath))
        {
            return false;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var text = File.ReadAllText(markerPath).Trim();
                return int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
                       && value == (int)expected;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(15);
            }
        }

        return false;
    }
}