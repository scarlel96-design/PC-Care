using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.App.Models.Update;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class UpdateInstallerService
{
    private const int StepCount = 6;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly UpdatePackageInspector _inspector = new();
    private readonly UpdateHistoryStore _history = new();

    public Task<UpdatePackageInspection> InspectAsync(
        string packagePath,
        IProgress<UpdateProgressReport>? progress = null,
        CancellationToken cancellationToken = default) =>
        _inspector.InspectAsync(packagePath, AppInfo.Version, progress, cancellationToken);

    public UpdatePackageInspection Inspect(string packagePath) =>
        _inspector.Inspect(packagePath, AppInfo.Version);

    public void InvalidateInspectionCache()
    {
    }

    public async Task<UpdateApplyResult> ApplyAsync(
        UpdatePackageInspection inspection,
        IProgress<UpdateProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!inspection.IsValid || !inspection.CanApply || inspection.Manifest is null)
        {
            return new UpdateApplyResult(false, AppInfo.Version, "", inspection.Message, false, 0, 0);
        }

        UpdatePaths.EnsureLayout();
        var manifest = inspection.Manifest;
        var currentVersion = AppInfo.Version;
        var reporter = new UpdateProgressReporter(progress);
        var stagingDir = Path.Combine(UpdatePaths.Staging, $"apply_{DateTimeOffset.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(stagingDir);

        try
        {
            reporter.Report(2, 1, StepCount, "준비", "작업 폴더 생성", stagingDir);
            cancellationToken.ThrowIfCancellationRequested();

            reporter.Report(5, 2, StepCount, "압축 해제", "패키지 열기", $"원본: {Path.GetFileName(inspection.PackagePath)}");
            var payloadRoot = await Task.Run(
                () => ExtractPackageWithProgress(inspection.PackagePath, stagingDir, reporter, cancellationToken),
                cancellationToken);

            var deferred = new List<string>();
            var applied = 0;
            var skippedVerify = inspection.PackageIntegrityVerified || inspection.IsValid;
            var total = manifest.Files.Count;

            reporter.Report(
                28,
                3,
                StepCount,
                skippedVerify ? "적용" : "검증",
                skippedVerify ? "무결성 확인됨" : "체크섬 생략",
                skippedVerify
                    ? "검사 단계에서 지문 확인 완료 · 파일별 SHA256 생략"
                    : "매니페스트 신뢰 · 파일별 SHA256 생략");

            await Task.Run(() =>
            {
                for (var index = 0; index < manifest.Files.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = manifest.Files[index];
                    var relative = NormalizePayloadPath(entry.Path);
                    var source = Path.Combine(payloadRoot, relative);
                    var target = Path.Combine(UpdatePaths.AppInstallDirectory, relative);
                    var fileName = Path.GetFileName(relative);

                    if (!File.Exists(source))
                    {
                        throw new InvalidOperationException($"패키지에 파일이 없습니다: {relative}");
                    }

                    var percent = 28 + (int)((index + 1) / (double)Math.Max(1, total) * 58);
                    var hot = UpdateFileHelper.IsHotFile(relative);
                    var locked = hot && !UpdateFileHelper.IsTargetWritable(target);

                    if (locked)
                    {
                        deferred.Add(relative);
                        reporter.Report(
                            percent,
                            4,
                            StepCount,
                            "적용",
                            "실행 중 잠김",
                            $"보류 예약 · {fileName}",
                            index + 1,
                            total,
                            fileName);
                        continue;
                    }

                    reporter.Report(
                        percent,
                        4,
                        StepCount,
                        "적용",
                        "파일 복사",
                        $"{fileName} → 설치 폴더",
                        index + 1,
                        total,
                        fileName);

                    if (UpdateFileHelper.TryCopy(source, target))
                    {
                        applied++;
                    }
                    else
                    {
                        deferred.Add(relative);
                        reporter.Report(
                            percent,
                            4,
                            StepCount,
                            "적용",
                            "복사 실패",
                            $"보류 예약 · {fileName}",
                            index + 1,
                            total,
                            fileName);
                    }
                }
            }, cancellationToken);

            reporter.Report(88, 5, StepCount, "마무리", "결과 집계", $"즉시 적용 {applied}개 · 보류 {deferred.Count}개");

            var restartScheduled = false;
            if (deferred.Count > 0 || manifest.RequiresRestart)
            {
                WritePendingState(stagingDir, deferred, manifest);
                restartScheduled = ScheduleRestart(stagingDir, manifest.ToVersion, deferred);
                reporter.Report(
                    95,
                    6,
                    StepCount,
                    "재시작",
                    "종료 후 적용 예약",
                    deferred.Count > 0
                        ? $"잠긴/보류 파일 {deferred.Count}개 · 앱 종료 후 자동 적용"
                        : "앱 재시작 후 마무리");
            }

            var verification = AppVersionService.VerifyInstalledVersion(manifest.ToVersion);
            if (!restartScheduled && verification.Success)
            {
                AppVersionService.WriteInstalledVersion(manifest.ToVersion, "in-app-apply");
            }

            _history.Append(new UpdateHistoryEntry(
                DateTimeOffset.Now.ToString("o"),
                currentVersion,
                manifest.ToVersion,
                Path.GetFileName(inspection.PackagePath),
                restartScheduled
                    ? $"업데이트 적용 · 재시작 후 {deferred.Count}개 마무리 예정"
                    : verification.Success
                        ? "업데이트 적용 완료 · 버전 확인됨"
                        : $"업데이트 적용 · 버전 확인 필요 ({verification.Details})"));

            reporter.Report(100, StepCount, StepCount, "완료", "업데이트 완료", verification.Details);
            return new UpdateApplyResult(
                true,
                currentVersion,
                manifest.ToVersion,
                restartScheduled
                    ? $"업데이트가 적용되었습니다. 앱을 다시 시작하면 보류 {deferred.Count}개 파일이 마무리됩니다."
                    : verification.Success
                        ? $"업데이트가 완료되었습니다. ({verification.Details})"
                        : $"파일은 복사됐지만 버전 확인이 필요합니다. ({verification.Details})",
                restartScheduled,
                applied,
                deferred.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail(currentVersion, manifest.ToVersion, $"업데이트 적용 실패: {ex.Message}");
        }
    }

    private static string ExtractPackageWithProgress(
        string packagePath,
        string stagingDir,
        UpdateProgressReporter reporter,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToArray();
        var total = Math.Max(1, entries.Length);

        for (var i = 0; i < entries.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[i];
            var dest = Path.Combine(stagingDir, entry.FullName);
            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!string.IsNullOrEmpty(entry.Name))
            {
                entry.ExtractToFile(dest, true);
            }

            var percent = 5 + (int)((i + 1) / (double)total * 22);
            reporter.Report(
                percent,
                2,
                StepCount,
                "압축 해제",
                "항목 추출",
                $"{entry.FullName.Replace('\\', '/')}",
                i + 1,
                total,
                entry.Name);
        }

        var payloadRoot = Path.Combine(stagingDir, "payload");
        return Directory.Exists(payloadRoot) ? payloadRoot : stagingDir;
    }

    private static string NormalizePayloadPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("payload/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["payload/".Length..];
        }

        return normalized.Replace('/', Path.DirectorySeparatorChar);
    }

    private static void WritePendingState(string stagingDir, IReadOnlyList<string> deferred, UpdateManifestDocument manifest)
    {
        var state = new
        {
            manifest.ToVersion,
            stagingDir,
            deferred,
            targetDir = UpdatePaths.AppInstallDirectory,
            exePath = Path.Combine(UpdatePaths.AppInstallDirectory, "SmartPerformanceDoctor.exe")
        };
        File.WriteAllText(UpdatePaths.PendingState, JsonSerializer.Serialize(state, JsonOptions));
    }

    private static bool ScheduleRestart(string stagingDir, string toVersion, IReadOnlyList<string> deferred)
    {
        var exe = Path.Combine(UpdatePaths.AppInstallDirectory, "SmartPerformanceDoctor.exe");
        var payloadRoot = Path.Combine(stagingDir, "payload");
        if (!Directory.Exists(payloadRoot))
        {
            payloadRoot = stagingDir;
        }

        var ps1 = $$"""
            $ErrorActionPreference = 'Continue'
            $log = '{{EscapePs1(UpdatePaths.LastApplyLog)}}'
            $target = '{{EscapePs1(UpdatePaths.AppInstallDirectory)}}'
            $payload = '{{EscapePs1(payloadRoot)}}'
            $exe = '{{EscapePs1(exe)}}'
            $expected = '{{EscapePs1(toVersion)}}'
            $pending = '{{EscapePs1(UpdatePaths.PendingState)}}'
            $installed = '{{EscapePs1(UpdatePaths.InstalledVersionFile)}}'

            function Write-Log([string]$msg) {
                $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [restart] $msg"
                Add-Content -LiteralPath $log -Value $line -Encoding UTF8
            }

            Write-Log "finalize start -> $expected"
            while (Get-Process -Name 'SmartPerformanceDoctor' -ErrorAction SilentlyContinue) {
                Start-Sleep -Seconds 1
            }

            $copied = 0
            $failed = 0
            Get-ChildItem -LiteralPath $payload -Recurse -File | ForEach-Object {
                $relative = $_.FullName.Substring($payload.Length).TrimStart('\')
                $dest = Join-Path $target $relative
                $destDir = Split-Path $dest -Parent
                if (-not (Test-Path -LiteralPath $destDir)) {
                    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                }
                try {
                    Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
                    $copied++
                } catch {
                    $failed++
                    Write-Log "copy failed: $relative :: $($_.Exception.Message)"
                }
            }

            $dll = Join-Path $target 'SmartPerformanceDoctor.dll'
            $actual = ''
            if (Test-Path -LiteralPath $dll) {
                $actual = (Get-Item -LiteralPath $dll).VersionInfo.ProductVersion
            }
            Write-Log "copied=$copied failed=$failed dll=$actual expected=$expected"

            if ($failed -eq 0 -and $actual -and ($actual.StartsWith($expected))) {
                $state = @{
                    version = $expected
                    source = 'restart-script'
                    verifiedAt = (Get-Date).ToString('o')
                    installDir = $target
                } | ConvertTo-Json -Compress
                Set-Content -LiteralPath $installed -Value $state -Encoding UTF8
                if (Test-Path -LiteralPath $pending) { Remove-Item -LiteralPath $pending -Force }
            } else {
                Write-Log "verification failed; pending state kept"
            }

            Start-Process -FilePath $exe
            """;

        File.WriteAllText(UpdatePaths.PendingScriptPs1, ps1, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var cmd = $"""
            @echo off
            powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{UpdatePaths.PendingScriptPs1}"
            del "%~f0"
            """;
        File.WriteAllText(UpdatePaths.PendingScript, cmd, Encoding.UTF8);
        return true;
    }

    private static string EscapePs1(string value) => value.Replace("'", "''");

    public static void LaunchPendingRestart()
    {
        if (!File.Exists(UpdatePaths.PendingScriptPs1))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{UpdatePaths.PendingScriptPs1}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static UpdateApplyResult Fail(string from, string to, string message) =>
        new(false, from, to, message, false, 0, 0);
}