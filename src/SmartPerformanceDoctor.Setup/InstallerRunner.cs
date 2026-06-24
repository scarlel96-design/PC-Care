using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.Contracts.Models.Installation;
using SmartPerformanceDoctor.Contracts.Services.Installation;

namespace SmartPerformanceDoctor.Setup;

internal sealed class InstallerRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<InstallReport> InstallAsync(
        string sourceLayout,
        string targetDir,
        InstalledFeaturesManifest manifest,
        IProgress<(int percent, string detail)> progress)
    {
        Directory.CreateDirectory(targetDir);
        var files = Directory.EnumerateFiles(sourceLayout, "*", SearchOption.AllDirectories).ToArray();
        var total = Math.Max(files.Length, 1);
        var copied = 0;

        var skipped = new List<string>();
        foreach (var file in files.Where(f => !ShouldSkipLayoutFile(f, sourceLayout)))
        {
            var relative = Path.GetRelativePath(sourceLayout, file);
            if (!FeatureInstallMapper.ShouldInstallRelativePath(relative, manifest))
            {
                skipped.Add(relative);
                continue;
            }

            var dest = Path.Combine(targetDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
            copied++;
            var percent = (int)(copied * 100.0 / total);
            progress.Report((percent, $"복사 중: {relative}"));
            await Task.Yield();
        }

        var programData = InstallerPaths.ProgramDataRoot;
        Directory.CreateDirectory(programData);
        var manifestPath = Path.Combine(programData, "installed_features.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));

        var logDir = Path.Combine(programData, "InstallerLogs");
        Directory.CreateDirectory(logDir);
        var report = new InstallReport
        {
            Version = manifest.Version,
            TargetDirectory = targetDir,
            InstalledAt = manifest.InstalledAt,
            InstallMode = manifest.InstallMode,
            FeatureCount = manifest.Features.Count(f => f.Value),
            SkippedOptionalFiles = skipped.Count,
            Success = true
        };
        File.WriteAllText(Path.Combine(programData, "install_features.json"), JsonSerializer.Serialize(manifest.Features, JsonOptions));

        MsiHelper.TryInstallMsi(InstallerPaths.ResolveMsiPath());
        var aegis = TryFinalizeAegis(targetDir, manifest.Version);
        report = report with
        {
            AegisProtectionLevel = aegis.ProtectionLevel,
            AegisRecoveryServiceInstalled = aegis.RecoveryServiceInstalled,
            AegisBaselineReady = aegis.BaselineReady
        };

        var reportPath = Path.Combine(programData, "install_report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var logText = report.ToString();
        File.WriteAllText(Path.Combine(logDir, $"install_{stamp}.txt"), logText);
        File.WriteAllText(Path.Combine(logDir, "install_log.txt"), logText);
        File.WriteAllText(Path.Combine(programData, "install_report.html"), report.ToHtml(manifest, aegis));
        progress.Report((100, "설치 완료"));
        return report;
    }

    private static bool ShouldSkipLayoutFile(string file, string layoutRoot)
    {
        var name = Path.GetFileName(file);
        return name.Equals("SmartPerformanceDoctor.Setup.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("INSTALLER_README.txt", StringComparison.OrdinalIgnoreCase);
    }

    private static AegisInstallStatus TryFinalizeAegis(string targetDir, string version)
    {
        try
        {
            return AegisPostInstall.FinalizeInstall(targetDir, version);
        }
        catch
        {
            return new AegisInstallStatus();
        }
    }

    public static string ComputeFolderSha256(string folder)
    {
        using var sha = SHA256.Create();
        var names = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).OrderBy(p => p);
        foreach (var file in names)
        {
            var hash = sha.ComputeHash(File.ReadAllBytes(file));
            sha.TransformBlock(hash, 0, hash.Length, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}

internal sealed record InstallReport
{
    public string Version { get; init; } = "";
    public string TargetDirectory { get; init; } = "";
    public bool IsModify { get; init; }
    public int RepairedFiles { get; init; }
    public string InstalledAt { get; init; } = "";
    public string InstallMode { get; init; } = "";
    public int FeatureCount { get; init; }
    public int SkippedOptionalFiles { get; init; }
    public bool Success { get; init; }
    public int AegisProtectionLevel { get; init; }
    public bool AegisRecoveryServiceInstalled { get; init; }
    public bool AegisBaselineReady { get; init; }

    public override string ToString() =>
        $"version={Version}\r\ntarget={TargetDirectory}\r\nmode={InstallMode}\r\nfeatures={FeatureCount}\r\nsuccess={Success}\r\naegisLevel={AegisProtectionLevel}";

    public string ToHtml(InstalledFeaturesManifest manifest, AegisInstallStatus? aegis = null)
    {
        var rows = string.Join("", manifest.Features
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => $"<tr><td>{kv.Key}</td><td>{(kv.Value ? "설치됨" : "미설치")}</td></tr>"));
        return "<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"utf-8\"/><title>SPD Install Report</title>" +
               "<style>body{font-family:Segoe UI,sans-serif;margin:24px}table{border-collapse:collapse}td,th{border:1px solid #ccc;padding:6px 10px}</style>" +
               "</head><body><h1>PC 케어 프로 설치 보고서</h1>" +
               $"<p>버전: {Version}<br/>대상: {TargetDirectory}<br/>모드: {InstallMode}<br/>시각: {InstalledAt}<br/>활성 기능: {FeatureCount}</p>" +
               (aegis is null ? "" : $"<p><b>복구 미러</b><br/>보호 등급: Level {aegis.ProtectionLevel}<br/>기준선: {(aegis.BaselineReady ? "준비됨" : "미준비")}<br/>복구 서비스: {(aegis.RecoveryServiceInstalled ? "설치됨" : "미설치")}<br/>TPM: {(aegis.TpmAvailable ? "사용 가능" : "DPAPI 폴백")}<br/>오프라인 캡슐: {(aegis.OfflinePackPath is null ? "없음" : "생성됨")}</p>") +
               $"<table><thead><tr><th>기능</th><th>상태</th></tr></thead><tbody>{rows}</tbody></table></body></html>";
    }
}