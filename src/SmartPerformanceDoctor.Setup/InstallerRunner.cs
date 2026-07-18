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
        var reporter = new InstallProgressReporter(progress);
        reporter.Report(0, "설치 준비 중...", force: true);
        await Task.Yield();

        var filesToCopy = Directory.EnumerateFiles(sourceLayout, "*", SearchOption.AllDirectories)
            .Where(file => !LayoutFileFilter.ShouldSkip(file, sourceLayout))
            .Select(file => (Source: file, Relative: Path.GetRelativePath(sourceLayout, file)))
            .Where(item => FeatureInstallMapper.ShouldInstallRelativePath(item.Relative, manifest))
            .ToList();

        var skipped = Directory.EnumerateFiles(sourceLayout, "*", SearchOption.AllDirectories)
            .Where(file => !LayoutFileFilter.ShouldSkip(file, sourceLayout))
            .Select(file => Path.GetRelativePath(sourceLayout, file))
            .Where(relative => !FeatureInstallMapper.ShouldInstallRelativePath(relative, manifest))
            .ToList();

        var copyTotal = Math.Max(filesToCopy.Count, 1);
        var copied = 0;

        await Task.Run(() =>
        {
            InstallFileOperations.PrepareTargetDirectory(targetDir);
            InstallShellAssets.EnsureFromLayout(sourceLayout, targetDir);
            foreach (var (source, relative) in filesToCopy)
            {
                var dest = Path.Combine(targetDir, relative);
                InstallFileOperations.CopyFile(source, dest);
                copied++;
                var percent = (int)(copied * 85.0 / copyTotal);
                reporter.Report(percent, $"복사 중 ({copied}/{copyTotal}): {relative}");
            }

            InstallShellAssets.EnsureFromLayout(sourceLayout, targetDir);
            InstallShellAssets.EnsurePresentOrThrow(targetDir);
            InstallRuntimeGuard.EnsureSelfContainedFromLayout(sourceLayout, targetDir);
        }).ConfigureAwait(true);

        reporter.Report(86, "설치 설정 저장 중...", force: true);
        var programData = InstallerPaths.ProgramDataRoot;
        Directory.CreateDirectory(programData);
        var manifestPath = Path.Combine(programData, "installed_features.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));

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
        await File.WriteAllTextAsync(
            Path.Combine(programData, "install_features.json"),
            JsonSerializer.Serialize(manifest.Features, JsonOptions));

        reporter.Report(88, "제거 프로그램 등록 중...", force: true);
        await Task.Run(() => InstallerStaging.StageUninstallerArtifacts(targetDir)).ConfigureAwait(true);

        reporter.Report(90, "바로가기 생성 중...", force: true);
        await Task.Run(() => InstallShortcutService.CreateShortcuts(targetDir, InstallerPaths.ProductName))
            .ConfigureAwait(true);

        reporter.Report(93, "복구 미러 준비 중...", force: true);
        await Task.Run(() => InstallRuntimeGuard.EnsurePresentOrThrow(targetDir)).ConfigureAwait(true);
        var aegis = await Task.Run(() => TryFinalizeAegis(targetDir, manifest.Version)).ConfigureAwait(true);
        report = report with
        {
            AegisProtectionLevel = aegis.ProtectionLevel,
            AegisRecoveryServiceInstalled = aegis.RecoveryServiceInstalled,
            AegisBaselineReady = aegis.BaselineReady
        };

        var reportPath = Path.Combine(programData, "install_report.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var logText = report.ToString();
        await File.WriteAllTextAsync(Path.Combine(logDir, $"install_{stamp}.txt"), logText);
        await File.WriteAllTextAsync(Path.Combine(logDir, "install_log.txt"), logText);
        await File.WriteAllTextAsync(Path.Combine(programData, "install_report.html"), report.ToHtml(manifest, aegis));

        reporter.Report(100, "설치 완료", force: true);
        await Task.Delay(350).ConfigureAwait(true);
        return report;
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