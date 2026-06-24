using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class CrashLogStore
{
    public IReadOnlyList<SelfHealingItem> LoadRecentCrashLogs()
    {
        RuntimePaths.EnsureUserFolders();

        return Directory.GetFiles(RuntimePaths.CrashLogsRoot, "crash_*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(50)
            .Select(path => new SelfHealingItem(
                Path.GetFileName(path),
                "LOG",
                "high",
                Preview(path),
                path))
            .ToArray();
    }

    private static string Preview(string path)
    {
        try
        {
            var text = string.Join(" ", File.ReadLines(path).Take(8));
            return text.Length > 360 ? text[..360] + "…" : text;
        }
        catch
        {
            return "크래시 로그 미리보기를 읽지 못했습니다.";
        }
    }
}
