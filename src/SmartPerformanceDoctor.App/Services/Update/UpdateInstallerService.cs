using System.Diagnostics;
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
            var needsElevatedApply = UpdateInstallElevation.RequiresElevation(UpdatePaths.AppInstallDirectory);

            reporter.Report(
                28,
                3,
                StepCount,
                skippedVerify ? "적용" : "검증",
                skippedVerify ? "무결성 확인됨" : "체크섬 생략",
                needsElevatedApply
                    ? "Program Files 설치 · 종료 후 관리자 권한으로 일괄 적용"
                    : skippedVerify
                        ? "검사 단계에서 지문 확인 완료 · 파일별 SHA256 생략"
                        : "매니페스트 신뢰 · 파일별 SHA256 생략");

            if (needsElevatedApply)
            {
                // In-process copy always fails under Program Files without admin.
                // Defer the full payload and finalize via elevated restart script (UAC).
                foreach (var entry in manifest.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    deferred.Add(NormalizePayloadPath(entry.Path));
                }

                reporter.Report(
                    80,
                    4,
                    StepCount,
                    "적용",
                    "관리자 권한 필요",
                    $"보류 예약 {deferred.Count}개 · UAC 확인 후 Program Files에 적용");
            }
            else
            {
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
            }

            reporter.Report(88, 5, StepCount, "마무리", "결과 집계", $"즉시 적용 {applied}개 · 보류 {deferred.Count}개");

            var restartScheduled = false;
            if (deferred.Count > 0 || manifest.RequiresRestart || needsElevatedApply)
            {
                WritePendingState(stagingDir, deferred, manifest);
                restartScheduled = ScheduleRestart(stagingDir, manifest.ToVersion, deferred, needsElevatedApply);
                reporter.Report(
                    95,
                    6,
                    StepCount,
                    "재시작",
                    needsElevatedApply ? "관리자 권한으로 종료 후 적용" : "종료 후 적용 예약",
                    needsElevatedApply
                        ? $"Program Files · UAC 허용 후 {deferred.Count}개 적용"
                        : deferred.Count > 0
                            ? $"잠긴/보류 파일 {deferred.Count}개 · 앱 종료 후 자동 적용"
                            : "앱 재시작 후 마무리");
            }

            var verification = AppVersionService.VerifyInstalledVersion(manifest.ToVersion);
            if (!restartScheduled && verification.Success)
            {
                AppVersionService.WriteInstalledVersion(manifest.ToVersion, "in-app-apply");
            }

            var historyNote = restartScheduled
                ? needsElevatedApply
                    ? $"업데이트 적용 · 관리자 권한으로 재시작 후 {deferred.Count}개 마무리 예정"
                    : $"업데이트 적용 · 재시작 후 {deferred.Count}개 마무리 예정"
                : verification.Success
                    ? "업데이트 적용 완료 · 버전 확인됨"
                    : $"업데이트 적용 · 버전 확인 필요 ({verification.Details})";

            _history.Append(new UpdateHistoryEntry(
                DateTimeOffset.Now.ToString("o"),
                currentVersion,
                manifest.ToVersion,
                Path.GetFileName(inspection.PackagePath),
                historyNote));

            reporter.Report(100, StepCount, StepCount, "완료", "업데이트 완료", verification.Details);

            var userMessage = restartScheduled
                ? needsElevatedApply
                    ? $"업데이트가 준비되었습니다. 앱 종료 후 관리자 권한(UAC) 확인이 뜨면 허용해 주세요. ({deferred.Count}개 파일)"
                    : $"업데이트가 적용되었습니다. 앱을 다시 시작하면 보류 {deferred.Count}개 파일이 마무리됩니다."
                : verification.Success
                    ? $"업데이트가 완료되었습니다. ({verification.Details})"
                    : $"파일은 복사됐지만 버전 확인이 필요합니다. ({verification.Details})";

            return new UpdateApplyResult(
                true,
                currentVersion,
                manifest.ToVersion,
                userMessage,
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
            exePath = Path.Combine(UpdatePaths.AppInstallDirectory, "PCCare.exe")
        };
        File.WriteAllText(UpdatePaths.PendingState, JsonSerializer.Serialize(state, JsonOptions));
    }

    /// <summary>
    /// Regenerates apply_pending.ps1 for an existing staging folder (startup recovery).
    /// </summary>
    public static bool EnsurePendingApplyScript(string stagingDir, string toVersion)
    {
        if (string.IsNullOrWhiteSpace(stagingDir) || !Directory.Exists(stagingDir))
        {
            return false;
        }

        return ScheduleRestart(
            stagingDir,
            toVersion,
            Array.Empty<string>(),
            UpdateInstallElevation.RequiresElevation(UpdatePaths.AppInstallDirectory));
    }

    private static bool ScheduleRestart(
        string stagingDir,
        string toVersion,
        IReadOnlyList<string> deferred,
        bool requireElevation = false)
    {
        var exe = Path.Combine(UpdatePaths.AppInstallDirectory, "PCCare.exe");
        if (!File.Exists(exe))
        {
            foreach (var name in new[] { "SmartPerformanceDoctor.exe", "AstraCare.exe" })
            {
                var candidate = Path.Combine(UpdatePaths.AppInstallDirectory, name);
                if (File.Exists(candidate))
                {
                    exe = candidate;
                    break;
                }
            }
        }

        var payloadRoot = Path.Combine(stagingDir, "payload");
        if (!Directory.Exists(payloadRoot))
        {
            payloadRoot = stagingDir;
        }

        // requireElevation is advisory; script also self-elevates if write probe fails.
        _ = deferred;
        _ = requireElevation;

        // 50.4.1: single-instance, silent window, restart app only on success (stops PS flash / reopen loops).
        var lockPath = EscapePs1(Path.Combine(UpdatePaths.Root, "apply_inflight.lock"));
        var ps1 = $$"""
            $ErrorActionPreference = 'Continue'
            $log = '{{EscapePs1(UpdatePaths.LastApplyLog)}}'
            $target = '{{EscapePs1(UpdatePaths.AppInstallDirectory)}}'
            $payload = '{{EscapePs1(payloadRoot)}}'
            $exe = '{{EscapePs1(exe)}}'
            $expected = '{{EscapePs1(toVersion)}}'
            $pending = '{{EscapePs1(UpdatePaths.PendingState)}}'
            $installed = '{{EscapePs1(UpdatePaths.InstalledVersionFile)}}'
            $lockFile = '{{lockPath}}'

            function Write-Log([string]$msg) {
                $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [restart] $msg"
                try { Add-Content -LiteralPath $log -Value $line -Encoding UTF8 } catch { }
            }

            function Test-IsAdmin {
                try {
                    $p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
                    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
                } catch { return $false }
            }

            function Test-CanWriteTarget {
                try {
                    if (-not (Test-Path -LiteralPath $target)) { return $false }
                    $probe = Join-Path $target ('.pccare_write_probe_' + [guid]::NewGuid().ToString('N') + '.tmp')
                    [IO.File]::WriteAllText($probe, 'ok')
                    Remove-Item -LiteralPath $probe -Force -ErrorAction SilentlyContinue
                    return $true
                } catch { return $false }
            }

            # Single-instance: prevent overlapping apply scripts (PS flash storm).
            $mutex = $null
            try {
                $mutex = New-Object System.Threading.Mutex($false, 'Local\PCCareUpdateApplyPending')
                if (-not $mutex.WaitOne(0)) {
                    Write-Log "another apply instance is running; exit quietly"
                    exit 0
                }
            } catch {
                Write-Log "mutex unavailable; continuing best-effort"
            }

            try {
                try { Set-Content -LiteralPath $lockFile -Value (Get-Date).ToString('o') -Encoding UTF8 } catch { }

                if (-not (Test-IsAdmin) -and -not (Test-CanWriteTarget)) {
                    Write-Log "elevation required; relaunching elevated (hidden)"
                    try {
                        $arg = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$PSCommandPath`""
                        Start-Process -FilePath 'powershell.exe' -ArgumentList $arg -Verb RunAs -WindowStyle Hidden | Out-Null
                        exit 0
                    } catch {
                        Write-Log "UAC elevation denied or failed: $($_.Exception.Message)"
                    }
                }

                Write-Log "finalize start -> $expected admin=$(Test-IsAdmin)"

                $waitNames = @('PCCare','SmartPerformanceDoctor','AstraCare')
                $deadline = (Get-Date).AddSeconds(90)
                while ((Get-Date) -lt $deadline) {
                    $alive = @()
                    foreach ($n in $waitNames) {
                        if (Get-Process -Name $n -ErrorAction SilentlyContinue) { $alive += $n }
                    }
                    if ($alive.Count -eq 0) { break }
                    Start-Sleep -Seconds 1
                }
                # Force-release file locks so copy can proceed; do not restart app yet.
                foreach ($n in $waitNames) {
                    Get-Process -Name $n -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
                }
                Start-Sleep -Milliseconds 800

                $copied = 0
                $failed = 0
                if (-not (Test-Path -LiteralPath $payload)) {
                    Write-Log "payload missing: $payload"
                    $failed = 1
                } else {
                    Get-ChildItem -LiteralPath $payload -Recurse -File | ForEach-Object {
                        $relative = $_.FullName.Substring($payload.Length).TrimStart('\','/')
                        $dest = Join-Path $target $relative
                        $destDir = Split-Path $dest -Parent
                        if (-not (Test-Path -LiteralPath $destDir)) {
                            try { New-Item -ItemType Directory -Path $destDir -Force | Out-Null } catch {
                                $failed++
                                Write-Log "mkdir failed: $relative :: $($_.Exception.Message)"
                                return
                            }
                        }
                        try {
                            Copy-Item -LiteralPath $_.FullName -Destination $dest -Force -ErrorAction Stop
                            $copied++
                        } catch {
                            $failed++
                            Write-Log "copy failed: $relative :: $($_.Exception.Message)"
                        }
                    }
                }

                $dll = Join-Path $target 'SmartPerformanceDoctor.dll'
                $actual = ''
                if (Test-Path -LiteralPath $dll) {
                    $actual = (Get-Item -LiteralPath $dll).VersionInfo.ProductVersion
                }
                Write-Log "copied=$copied failed=$failed dll=$actual expected=$expected"

                $ok = ($failed -eq 0) -and $actual -and ($actual.StartsWith($expected))
                if ($ok) {
                    $state = @{
                        version = $expected
                        source = 'restart-script'
                        verifiedAt = (Get-Date).ToString('o')
                        installDir = $target
                    } | ConvertTo-Json -Compress
                    Set-Content -LiteralPath $installed -Value $state -Encoding UTF8
                    if (Test-Path -LiteralPath $pending) { Remove-Item -LiteralPath $pending -Force }
                    Write-Log "verification ok; pending cleared"
                    # Restart app only after successful apply (50.4.1: no reopen-on-failure loop).
                    if (Test-Path -LiteralPath $exe) {
                        Start-Process -FilePath $exe
                        Write-Log "started $exe"
                    }
                } else {
                    Write-Log "verification failed; pending kept; app NOT auto-started"
                }
            } finally {
                try { if (Test-Path -LiteralPath $lockFile) { Remove-Item -LiteralPath $lockFile -Force } } catch { }
                if ($mutex) { try { $mutex.ReleaseMutex() | Out-Null } catch { }; $mutex.Dispose() }
            }
            """;

        File.WriteAllText(UpdatePaths.PendingScriptPs1, ps1, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var cmd = $"""
            @echo off
            start "" /min powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "{UpdatePaths.PendingScriptPs1}"
            del "%~f0"
            """;
        File.WriteAllText(UpdatePaths.PendingScript, cmd, Encoding.UTF8);
        return true;
    }

    private static string EscapePs1(string value) => value.Replace("'", "''");

    /// <summary>
    /// Launches the pending finalize script. Uses UAC (runas) when the install
    /// directory is not writable so Program Files updates actually apply.
    /// </summary>
    public static bool LaunchPendingRestart()
    {
        if (!File.Exists(UpdatePaths.PendingScriptPs1))
        {
            return false;
        }

        // Avoid launching dozens of apply scripts when startup + scheduled task race.
        var lockFile = Path.Combine(UpdatePaths.Root, "apply_inflight.lock");
        try
        {
            if (File.Exists(lockFile))
            {
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(lockFile);
                if (age < TimeSpan.FromMinutes(3))
                {
                    UpdatePendingApplier.AppendApplyLog("[restart] apply already in flight; skip launch");
                    return true;
                }
            }
        }
        catch
        {
            // Best effort.
        }

        var needsElevation = UpdateInstallElevation.RequiresElevation(UpdatePaths.AppInstallDirectory);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{UpdatePaths.PendingScriptPs1}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            if (needsElevation)
            {
                psi.Verb = "runas";
            }

            Process.Start(psi);
            try
            {
                File.WriteAllText(lockFile, DateTimeOffset.Now.ToString("o"));
            }
            catch
            {
                // Best effort.
            }

            UpdatePendingApplier.AppendApplyLog(
                needsElevation
                    ? "[restart] launched apply_pending.ps1 elevated (UAC, hidden)"
                    : "[restart] launched apply_pending.ps1 (hidden)");
            return true;
        }
        catch (Exception ex)
        {
            // User cancelled UAC or shell failed — keep pending for next attempt.
            UpdatePendingApplier.AppendApplyLog($"[restart] launch failed: {ex.Message}");
            return false;
        }
    }

    private static UpdateApplyResult Fail(string from, string to, string message) =>
        new(false, from, to, message, false, 0, 0);
}