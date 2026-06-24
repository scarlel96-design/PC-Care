namespace SmartPerformanceDoctor.App.Services.Update;

public static class UpdatePaths
{
    public static string Root =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPerformanceDoctor",
            "updates");

    public static string Inbox => Path.Combine(Root, "inbox");
    public static string Staging => Path.Combine(Root, "staging");
    public static string History => Path.Combine(Root, "history");
    public static string PendingScript => Path.Combine(Root, "apply_pending.cmd");
    public static string PendingScriptPs1 => Path.Combine(Root, "apply_pending.ps1");
    public static string PendingState => Path.Combine(Root, "pending_update.json");
    public static string InstalledVersionFile => Path.Combine(Root, "installed_version.json");
    public static string LastApplyLog => Path.Combine(Root, "last_apply.log");

    public static string AppInstallDirectory => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static void EnsureLayout()
    {
        Directory.CreateDirectory(Inbox);
        Directory.CreateDirectory(Staging);
        Directory.CreateDirectory(History);
    }
}