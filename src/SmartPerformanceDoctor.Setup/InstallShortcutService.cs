using System.IO;
using System.Linq;
using SmartPerformanceDoctor.Aegis;

namespace SmartPerformanceDoctor.Setup;

internal static class InstallShortcutService
{
    public static void CreateShortcuts(string installRoot, string productName)
    {
        var exe = AppExecutableResolver.ResolveMainExecutable(installRoot);
        if (exe is null)
        {
            return;
        }

        var iconPath = ProductIconService.ResolveIconPath(installRoot) ?? exe;
        var iconLocation = $"{iconPath},0";

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        CreateShortcut(Path.Combine(desktop, $"{productName}.lnk"), exe, installRoot, iconLocation, productName);

        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var folder = Path.Combine(startMenu, productName);
        Directory.CreateDirectory(folder);
        CreateShortcut(Path.Combine(folder, $"{productName}.lnk"), exe, installRoot, iconLocation, productName);
        var uninstallTarget = InstallerStaging.ResolveStagedUninstallerPath();
        if (uninstallTarget is not null)
        {
            CreateShortcut(
                Path.Combine(folder, $"{productName} 제거.lnk"),
                uninstallTarget,
                installRoot,
                iconLocation,
                $"{productName} 제거",
                "--uninstall");
            var desktopUninstall = Path.Combine(desktop, $"{productName} 제거.lnk");
            CreateShortcut(desktopUninstall, uninstallTarget, installRoot, iconLocation, $"{productName} 제거", "--uninstall");
        }
    }

    private static void CreateShortcut(
        string shortcutPath,
        string targetPath,
        string workingDirectory,
        string iconLocation,
        string description,
        string arguments = "")
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = iconLocation;
            shortcut.Description = description;
            shortcut.Save();
        }
        catch
        {
            // Shortcuts are optional; install still succeeds.
        }
    }

    public static void RemoveAllShortcuts(string productName)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        TryDeleteShortcut(Path.Combine(desktop, $"{productName}.lnk"));
        TryDeleteShortcut(Path.Combine(desktop, $"{productName} 제거.lnk"));

        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var folder = Path.Combine(startMenu, productName);
        if (Directory.Exists(folder))
        {
            foreach (var lnk in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.TopDirectoryOnly))
            {
                TryDeleteShortcut(lnk);
            }

            TryDeleteEmptyDirectory(folder);
        }
    }

    private static void TryDeleteShortcut(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path, false);
            }
        }
        catch
        {
            // best effort
        }
    }
}