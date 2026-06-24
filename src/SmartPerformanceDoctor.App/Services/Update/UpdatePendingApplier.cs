using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class UpdatePendingApplier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            var targetDir = root.TryGetProperty("targetDir", out var t) ? t.GetString() ?? UpdatePaths.AppInstallDirectory : UpdatePaths.AppInstallDirectory;
            var toVersion = root.TryGetProperty("toVersion", out var v) ? v.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(stagingDir) || !Directory.Exists(stagingDir))
            {
                return new PendingApplyResult(false, 0, "대기 중인 스테이징 폴더를 찾지 못했습니다. 재시작 스크립트를 다시 실행하세요.", toVersion, false);
            }

            var payloadRoot = Path.Combine(stagingDir, "payload");
            if (!Directory.Exists(payloadRoot))
            {
                payloadRoot = stagingDir;
            }

            var failed = new List<string>();
            var applied = 0;

            if (root.TryGetProperty("deferred", out var deferredNode) && deferredNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in deferredNode.EnumerateArray())
                {
                    var relative = item.GetString();
                    if (string.IsNullOrWhiteSpace(relative))
                    {
                        continue;
                    }

                    if (TryCopyRelative(payloadRoot, targetDir, relative))
                    {
                        applied++;
                    }
                    else
                    {
                        failed.Add(relative);
                    }
                }
            }
            else
            {
                applied = UpdateFileHelper.CopyTree(payloadRoot, targetDir);
            }

            var verification = AppVersionService.VerifyInstalledVersion(toVersion);
            if (failed.Count > 0 || !verification.Success)
            {
                AppendApplyLog(
                    $"[startup] partial apply · copied={applied} failed={failed.Count} · {verification.Details}");
                var rollback = Services.Aegis.AegisMirrorService.Shared.RunPostUpdateCheck(
                    AppInfo.BuildVersion,
                    updateSucceeded: false);
                var rollbackNote = rollback.RepairedFiles > 0
                    ? $" 복구 미러 롤백 {rollback.RepairedFiles}건 적용."
                    : "";
                var message = failed.Count > 0
                    ? $"보류 파일 {failed.Count}개 적용 실패 · 앱을 완전히 종료한 뒤 다시 시도하세요.{rollbackNote}"
                    : $"{verification.Details}{rollbackNote}";
                return new PendingApplyResult(false, applied, message, toVersion, false);
            }

            AppVersionService.WriteInstalledVersion(toVersion, "startup-pending-applier");
            CleanupPending();
            AppendApplyLog($"[startup] verified {toVersion} · copied={applied}");
            _ = Services.Aegis.AegisMirrorService.Shared.RunPostUpdateCheck(toVersion, updateSucceeded: true);
            return new PendingApplyResult(true, applied, $"{toVersion} 적용 및 버전 확인 완료", toVersion, true);
        }
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

    private static bool TryCopyRelative(string payloadRoot, string targetDir, string relative)
    {
        var source = Path.Combine(payloadRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        var target = Path.Combine(targetDir, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(source))
        {
            return false;
        }

        var targetParent = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(targetParent))
        {
            Directory.CreateDirectory(targetParent);
        }

        return UpdateFileHelper.TryCopy(source, target);
    }

    public static void CleanupPending()
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
    }
}

public sealed record PendingApplyResult(
    bool Applied,
    int FilesApplied,
    string Message,
    string ToVersion,
    bool Verified)
{
    public static PendingApplyResult None => new(false, 0, "", "", false);
}