namespace SmartPerformanceDoctor.App.Services;

public static class AppLaunchOptions
{
    public static bool StartMinimizedToBackground { get; private set; }

    public static void Parse(string[]? args)
    {
        StartMinimizedToBackground = false;
        if (args is null || args.Length == 0)
        {
            return;
        }

        foreach (var arg in args)
        {
            if (arg.Equals("--background", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("/background", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
            {
                StartMinimizedToBackground = true;
            }
        }
    }
}