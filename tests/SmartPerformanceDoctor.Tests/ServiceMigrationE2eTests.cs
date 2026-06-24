using System.Security.Principal;
using SmartPerformanceDoctor.Aegis;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

/// <summary>
/// Requires elevation — migrates AstraCareAegisRecovery to PCCareAegisRecovery using real sc.exe.
/// </summary>
public sealed class ServiceMigrationE2eTests : IDisposable
{
    private readonly string? _previousSigningKeyPath;
    private readonly bool _isAdmin;
    private readonly string _installRoot;
    private readonly string _layoutRoot;

    public ServiceMigrationE2eTests()
    {
        _isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        _layoutRoot = ResolveLayoutRoot();
        _installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PCCare");

        const string signingKeyVariable = "AEGIS_SIGNING_KEY_PATH";
        _previousSigningKeyPath = Environment.GetEnvironmentVariable(signingKeyVariable);
        var devKey = ResolveDevSigningKey();
        if (devKey is not null)
        {
            Environment.SetEnvironmentVariable(signingKeyVariable, devKey);
        }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AEGIS_SIGNING_KEY_PATH", _previousSigningKeyPath);
    }

    [Fact]
    public void Service_migration_registers_PCCareAegisRecovery()
    {
        if (!_isAdmin)
        {
            return; // environment lacks elevation — run scripts\_elevated-rc-lock-runner.ps1 as admin
        }

        if (!Directory.Exists(_layoutRoot))
        {
            return;
        }

        var serviceExe = AegisServiceInstaller.ResolveServiceExePath(_layoutRoot);
        Assert.True(File.Exists(serviceExe), $"Missing service exe: {serviceExe}");

        var result = AegisServiceInstaller.Install(serviceExe, _installRoot, "50.0.0");
        Assert.True(result.Success, result.Message);
        Assert.Equal(AegisProduct.RecoveryServiceName, result.ServiceName);

        Assert.False(LegacyServiceExists());
        Assert.True(AegisServiceInstaller.IsInstalled());

        var status = AegisServiceInstaller.GetStatus();
        Assert.True(status.Installed);
    }

    private static bool LegacyServiceExists()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("sc.exe", $"query {AegisProduct.LegacyRecoveryServiceName}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = System.Diagnostics.Process.Start(psi)!;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveLayoutRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "artifacts", "installer", "layout");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return "";
    }

    private static string? ResolveDevSigningKey()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "artifacts", "signing", "aegis-dev-private.pem");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}