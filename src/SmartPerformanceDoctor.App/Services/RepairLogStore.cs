using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class RepairLogStore
{
    public string LogsRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "RepairLogs");

    public IReadOnlyList<RepairLogSummary> LoadRecentLogs()
    {
        if (!Directory.Exists(LogsRoot))
        {
            return Array.Empty<RepairLogSummary>();
        }

        return Directory.GetFiles(LogsRoot, "repair_*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(50)
            .Select(ToSummary)
            .ToArray();
    }

    private static RepairLogSummary ToSummary(string path)
    {
        var title = Path.GetFileName(path);
        var created = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss");
        var preview = "";

        try
        {
            preview = string.Join(" ", File.ReadLines(path).Take(8));
            if (preview.Length > 360)
            {
                preview = preview[..360] + "…";
            }
        }
        catch
        {
            preview = "로그 미리보기를 읽지 못했습니다.";
        }

        return new RepairLogSummary(title, created, path, preview);
    }
}
