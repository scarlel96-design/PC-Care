using System.Text;

namespace SmartPerformanceDoctor.App.Services;

public static class CrashCaptureService
{
    private static int _installed;

    public static void InstallGlobalHandlers()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1)
        {
            return;
        }

        RuntimePaths.EnsureUserFolders();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            WriteCrash("unhandled", args.ExceptionObject as Exception, args.ExceptionObject?.ToString());
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrash("unobserved-task", args.Exception, args.Exception.ToString());
            args.SetObserved();
        };
    }

    public static string WriteCrash(string kind, Exception? exception, string? raw = null)
    {
        RuntimePaths.EnsureUserFolders();

        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss_fff");
        var path = Path.Combine(RuntimePaths.CrashLogsRoot, $"crash_{stamp}_{Sanitize(kind)}.log");

        var builder = new StringBuilder();
        builder.AppendLine("PC 케어 프로 오류 기록");
        builder.AppendLine($"Kind: {kind}");
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:o}");
        builder.AppendLine($"OS: {Environment.OSVersion}");
        builder.AppendLine($"Machine: {Environment.MachineName}");
        builder.AppendLine($"User: {Environment.UserName}");
        builder.AppendLine($"Process64Bit: {Environment.Is64BitProcess}");
        builder.AppendLine($"BaseDirectory: {AppContext.BaseDirectory}");
        builder.AppendLine();

        if (exception is not null)
        {
            builder.AppendLine("Exception:");
            builder.AppendLine(exception.ToString());
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            builder.AppendLine();
            builder.AppendLine("Raw:");
            builder.AppendLine(raw);
        }

        File.WriteAllText(path, builder.ToString());
        return path;
    }

    private static string Sanitize(string value)
    {
        return string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
    }
}
