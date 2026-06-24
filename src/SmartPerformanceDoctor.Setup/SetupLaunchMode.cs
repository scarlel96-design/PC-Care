namespace SmartPerformanceDoctor.Setup;

internal enum SetupLaunchMode
{
    Install,
    Modify,
    Repair,
    Uninstall
}

internal static class SetupLaunchModeParser
{
    public static SetupLaunchMode Parse(string[] args)
    {
        if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            return SetupLaunchMode.Uninstall;
        }

        if (args.Any(a => a.Equals("--repair", StringComparison.OrdinalIgnoreCase)))
        {
            return SetupLaunchMode.Repair;
        }

        if (args.Any(a => a.Equals("--modify", StringComparison.OrdinalIgnoreCase)))
        {
            return SetupLaunchMode.Modify;
        }

        return SetupLaunchMode.Install;
    }
}