using System.Drawing;
using System.Runtime.InteropServices;

namespace SmartPerformanceDoctor.App.Services.Security;

internal static class SecureVaultSealIconService
{
    private const int LockedFolderIconIndex = 47;
    private static string? _cachedIconPath;

    public static string GetLockedFolderIconResource()
    {
        var iconPath = EnsureLockedFolderIconFile();
        return string.IsNullOrWhiteSpace(iconPath)
            ? $"{Environment.GetFolderPath(Environment.SpecialFolder.System)}\\shell32.dll,{LockedFolderIconIndex}"
            : iconPath;
    }

    public static string? EnsureLockedFolderIconFile()
    {
        if (!string.IsNullOrWhiteSpace(_cachedIconPath) && File.Exists(_cachedIconPath))
        {
            return _cachedIconPath;
        }

        Directory.CreateDirectory(SecureVaultPaths.Root);
        var deployed = Path.Combine(SecureVaultPaths.Root, "vault_folder_locked.ico");
        if (File.Exists(deployed))
        {
            _cachedIconPath = deployed;
            return deployed;
        }

        foreach (var asset in GetAssetIconCandidates())
        {
            if (!File.Exists(asset))
            {
                continue;
            }

            File.Copy(asset, deployed, true);
            _cachedIconPath = deployed;
            return deployed;
        }

        if (TryExtractShellIcon(deployed))
        {
            _cachedIconPath = deployed;
            return deployed;
        }

        return null;
    }

    public static void CopyIconBesideDesktopIni(string folderPath)
    {
        var source = EnsureLockedFolderIconFile();
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        var target = Path.Combine(folderPath, "._spd_vault_folder.ico");
        try
        {
            File.Copy(source, target, true);
            File.SetAttributes(target, FileAttributes.Hidden | FileAttributes.System);
        }
        catch (IOException)
        {
            // Icon copy is optional; desktop.ini can still point at shell32.dll.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort only.
        }
    }

    private static IEnumerable<string> GetAssetIconCandidates()
    {
        yield return Path.Combine(RuntimePaths.AssetsDirectory, "vault_folder_locked.ico");
        yield return Path.Combine(RuntimePaths.AssetsDirectory, "SmartPerformanceDoctor.ico");
    }

    private static bool TryExtractShellIcon(string targetPath)
    {
        var shell32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
        foreach (var index in new[] { LockedFolderIconIndex, 48, 46 })
        {
            if (ExtractIconEx(shell32, index, out var large, out _, 1) == 0 || large == IntPtr.Zero)
            {
                continue;
            }

            try
            {
                if (SaveHiconAsIco(large, targetPath))
                {
                    return true;
                }
            }
            finally
            {
                DestroyIcon(large);
            }
        }

        return false;
    }

    private static bool SaveHiconAsIco(IntPtr hIcon, string targetPath)
    {
        var icon = Icon.FromHandle(hIcon);
        try
        {
            using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            icon.Save(fs);
            return fs.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}