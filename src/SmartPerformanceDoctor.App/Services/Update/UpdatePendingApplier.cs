using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class UpdatePendingApplier
{
    public PendingApplyResult TryApplyOnStartup()
    {
        if (!File.Exists(UpdatePaths.PendingState))
        {
            return PendingApplyResult.None;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(UpdatePaths.PendingState));
            var root = doc.RootElement;
            var stagingDir = root.TryGetProperty("stagingDir", out var s) ? s.GetString() ?? "" : "";
            var toVersion = root.TryGetProperty("toVersion", out var v) ? v.GetString() ?? "" : "";
            var packagePath = root.TryGetProperty("packagePath", out var p) ? p.GetString() ?? "" : "";

            // A setup repair/direct install may already have placed this exact
            // version on disk. Do not leave a stale pending marker that keeps
            // asking the user to finish an update that is already present.
            var alreadyInstalled = AppVersionService.VerifyInstalledVersion(toVersion);
            if (alreadyInstalled.Success)
            {
                AppVersionService.WriteInstalledVersion(toVersion, "startup-pending-already-installed");
                CleanupPending(packagePath, stagingDir);
                AppendApplyLog($"[startup] stale pending {toVersion} cleared; {alreadyInstalled.Details}");
                return new PendingApplyResult(
                    true,
                    0,
                    $"{toVersion}이 이미 설치되어 남은 업데이트 대기 상태를 정리했습니다.",
                    toVersion,
                    true);
            }

            AppendApplyLog($"[startup] legacy pending {toVersion} blocked: no authenticated publisher/transaction binding");
            return new PendingApplyResult(
                false, 0,
                $"업데이트 {toVersion}의 발행자 인증이 없어 앱 내 적용을 중단했습니다. 공식 설치 파일을 사용해 주세요.",
                toVersion, false);        }
        catch (Exception ex)
        {
            return new PendingApplyResult(false, 0, $"보류 업데이트 적용 실패: {ex.Message}", "", false);
        }
    }

    public static void AppendApplyLog(string line)
    {
        try
        {
            UpdatePaths.EnsureLayout();
            File.AppendAllText(
                UpdatePaths.LastApplyLog,
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // Best effort logging.
        }
    }

    public static void CleanupPending(string? packagePath = null, string? stagingDirectory = null)
    {
        try
        {
            if (File.Exists(UpdatePaths.PendingState))
            {
                File.Delete(UpdatePaths.PendingState);
            }

            if (File.Exists(UpdatePaths.PendingScript))
            {
                File.Delete(UpdatePaths.PendingScript);
            }

            if (File.Exists(UpdatePaths.PendingScriptPs1))
            {
                File.Delete(UpdatePaths.PendingScriptPs1);
            }
        }
        catch
        {
            // Best effort cleanup.
        }

        var cleanup = UpdateArtifactCleanup.TryCleanupCompletedUpdate(packagePath, stagingDirectory);
        if (cleanup.Warnings.Count > 0)
        {
            AppendApplyLog($"[cleanup] {string.Join(" | ", cleanup.Warnings)}");
        }
        else
        {
            AppendApplyLog($"[cleanup] completed package={cleanup.PackageDeleted} staging={cleanup.StagingDeleted}");
        }
    }
}

public sealed record PendingApplyResult(
    bool Applied,
    int FilesApplied,
    string Message,
    string ToVersion,
    bool Verified,
    bool RequestExit = false)
{
    public static PendingApplyResult None => new(false, 0, "", "", false);
}
