using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.Contracts.Models.Installation;
using SmartPerformanceDoctor.Contracts.Services.Installation;

namespace SmartPerformanceDoctor.Setup;

internal sealed class RepairRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<InstallReport> RepairAsync(
        string sourceLayout,
        string targetDir,
        InstalledFeaturesManifest? manifest,
        IProgress<(int percent, string detail)> progress)
    {
        if (!Directory.Exists(targetDir))
        {
            throw new DirectoryNotFoundException($"설치 폴더를 찾을 수 없습니다: {targetDir}");
        }

        manifest ??= LoadManifest() ?? FeatureCatalog.CreateAllEnabled(InstallerPaths.ProductVersion);
        var files = Directory.EnumerateFiles(sourceLayout, "*", SearchOption.AllDirectories)
            .Where(f => !ShouldSkipLayoutFile(f, sourceLayout))
            .ToArray();
        var total = Math.Max(files.Length, 1);
        var repaired = 0;
        var mismatches = 0;

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(sourceLayout, file);
            var dest = Path.Combine(targetDir, relative);
            var needsCopy = !File.Exists(dest);
            if (!needsCopy)
            {
                var srcHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file))).ToLowerInvariant();
                var dstHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dest))).ToLowerInvariant();
                needsCopy = !srcHash.Equals(dstHash, StringComparison.OrdinalIgnoreCase);
                if (needsCopy)
                {
                    mismatches++;
                }
            }

            if (needsCopy)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, true);
            }

            repaired++;
            progress.Report(((int)(repaired * 100.0 / total), $"복구 중: {relative}"));
            await Task.Yield();
        }

        var programData = InstallerPaths.ProgramDataRoot;
        Directory.CreateDirectory(programData);
        File.WriteAllText(Path.Combine(programData, "installed_features.json"), JsonSerializer.Serialize(manifest, JsonOptions));

        var report = new InstallReport
        {
            Version = manifest.Version,
            TargetDirectory = targetDir,
            InstalledAt = DateTimeOffset.Now.ToString("o"),
            InstallMode = "repair",
            FeatureCount = manifest.Features.Count(f => f.Value),
            Success = true,
            RepairedFiles = mismatches
        };
        WriteRepairArtifacts(report, manifest);
        _ = TryFinalizeAegis(targetDir, manifest.Version);
        progress.Report((100, $"복구 완료 (교체 {mismatches}개)"));
        return report;
    }

    private static AegisInstallStatus TryFinalizeAegis(string targetDir, string version)
    {
        try
        {
            return AegisPostInstall.FinalizeRepair(targetDir, version);
        }
        catch
        {
            return new AegisInstallStatus();
        }
    }

    private static bool ShouldSkipLayoutFile(string file, string layoutRoot)
    {
        var name = Path.GetFileName(file);
        return name.Equals("SmartPerformanceDoctor.Setup.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("INSTALLER_README.txt", StringComparison.OrdinalIgnoreCase);
    }

    private static InstalledFeaturesManifest? LoadManifest()
    {
        var path = Path.Combine(InstallerPaths.ProgramDataRoot, "installed_features.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InstalledFeaturesManifest>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteRepairArtifacts(InstallReport report, InstalledFeaturesManifest manifest)
    {
        var programData = InstallerPaths.ProgramDataRoot;
        var logDir = Path.Combine(programData, "InstallerLogs");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(Path.Combine(programData, "install_report.json"), JsonSerializer.Serialize(report, JsonOptions));
        File.WriteAllText(Path.Combine(programData, "install_report.html"), report.ToHtml(manifest));
        File.WriteAllText(Path.Combine(logDir, "repair_log.txt"), report.ToString());
    }
}