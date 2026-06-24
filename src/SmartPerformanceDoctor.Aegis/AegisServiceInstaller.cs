using System.Diagnostics;
using System.Text.Json;

namespace SmartPerformanceDoctor.Aegis;

public static class AegisServiceInstaller
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AegisServiceRegistrationResult Install(string serviceExePath, string installRoot, string version)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new AegisServiceRegistrationResult { Success = false, Message = "Windows 전용 기능입니다." };
        }

        if (!File.Exists(serviceExePath))
        {
            return new AegisServiceRegistrationResult
            {
                Success = false,
                Message = $"복구 서비스 실행 파일 없음: {serviceExePath}"
            };
        }

        WriteServiceConfig(installRoot, version, serviceExePath);
        try
        {
            MigrateLegacyServiceIfPresent();

            if (IsInstalled())
            {
                RunSc($"stop {AegisProduct.RecoveryServiceName}");
                RunSc($"delete {AegisProduct.RecoveryServiceName}");
            }

            var binPath = $"\\\"{serviceExePath}\\\" --service";
            var create = RunSc(
                $"create {AegisProduct.RecoveryServiceName} binPath= \"{binPath}\" start= auto DisplayName= \"{AegisProduct.RecoveryServiceDisplayName}\"");
            if (create.ExitCode != 0)
            {
                return new AegisServiceRegistrationResult { Success = false, Message = create.Output };
            }

            RunSc($"description {AegisProduct.RecoveryServiceName} \"PC Care Pro recovery mirror background integrity watchdog\"");
            var start = RunSc($"start {AegisProduct.RecoveryServiceName}");
            return new AegisServiceRegistrationResult
            {
                Success = start.ExitCode == 0 || start.ExitCode == 1056,
                Message = start.Output,
                ServiceName = AegisProduct.RecoveryServiceName
            };
        }
        catch (Exception ex)
        {
            return new AegisServiceRegistrationResult { Success = false, Message = ex.Message };
        }
    }

    public static AegisServiceRegistrationResult Uninstall()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new AegisServiceRegistrationResult { Success = true, Message = "skipped" };
        }

        try
        {
            if (IsInstalled())
            {
                RunSc($"stop {AegisProduct.RecoveryServiceName}");
                var delete = RunSc($"delete {AegisProduct.RecoveryServiceName}");
                return new AegisServiceRegistrationResult
                {
                    Success = delete.ExitCode == 0,
                    Message = delete.Output,
                    ServiceName = AegisProduct.RecoveryServiceName
                };
            }

            return new AegisServiceRegistrationResult { Success = true, Message = "서비스 미설치" };
        }
        catch (Exception ex)
        {
            return new AegisServiceRegistrationResult { Success = false, Message = ex.Message };
        }
    }

    public static AegisServiceStatus GetStatus()
    {
        if (!OperatingSystem.IsWindows() || !IsInstalled())
        {
            return new AegisServiceStatus { Installed = false, Running = false };
        }

        try
        {
            var query = RunSc($"query {AegisProduct.RecoveryServiceName}");
            var running = query.Output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
            var status = running ? "Running" : "Stopped";
            if (query.Output.Contains("PAUSED", StringComparison.OrdinalIgnoreCase))
            {
                status = "Paused";
            }
            else if (query.Output.Contains("START_PENDING", StringComparison.OrdinalIgnoreCase))
            {
                status = "StartPending";
            }

            return new AegisServiceStatus
            {
                Installed = true,
                Running = running,
                Status = status
            };
        }
        catch
        {
            return new AegisServiceStatus { Installed = true, Running = false, Status = "unknown" };
        }
    }

    public static bool IsInstalled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var query = RunSc($"query {AegisProduct.RecoveryServiceName}");
            return query.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static AegisServiceConfig? ReadServiceConfig()
    {
        if (!File.Exists(AegisMirrorPaths.ServiceConfigFile))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AegisServiceConfig>(File.ReadAllText(AegisMirrorPaths.ServiceConfigFile), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static string ResolveServiceExePath(string installRoot)
    {
        var candidates = new[]
        {
            Path.Combine(installRoot, "engine", AegisProduct.RecoveryServiceExe),
            Path.Combine(installRoot, AegisProduct.RecoveryServiceExe)
        };
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static void WriteServiceConfig(string installRoot, string version, string serviceExePath)
    {
        Directory.CreateDirectory(AegisMirrorPaths.ProgramDataRoot);
        var config = new AegisServiceConfig
        {
            InstallRoot = installRoot,
            Version = version,
            ServiceExePath = serviceExePath,
            InstalledAt = DateTimeOffset.Now
        };
        File.WriteAllText(AegisMirrorPaths.ServiceConfigFile, JsonSerializer.Serialize(config, JsonOptions));
    }

    private static void MigrateLegacyServiceIfPresent()
    {
        if (!IsLegacyInstalled())
        {
            return;
        }

        RunSc($"stop {AegisProduct.LegacyRecoveryServiceName}");
        RunSc($"delete {AegisProduct.LegacyRecoveryServiceName}");
    }

    private static bool IsLegacyInstalled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var query = RunSc($"query {AegisProduct.LegacyRecoveryServiceName}");
            return query.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static (int ExitCode, string Output) RunSc(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.Trim());
    }
}

public sealed class AegisServiceConfig
{
    public string InstallRoot { get; set; } = "";
    public string Version { get; set; } = "";
    public string ServiceExePath { get; set; } = "";
    public DateTimeOffset InstalledAt { get; set; }
}

public sealed class AegisServiceStatus
{
    public bool Installed { get; init; }
    public bool Running { get; init; }
    public string Status { get; init; } = "";
}

public sealed class AegisServiceRegistrationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string ServiceName { get; init; } = "";
}