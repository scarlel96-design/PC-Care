using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services.Security;

internal static class SecureVaultOriginSealService
{
    private const string MarkerFileName = ".spd_vault_sealed";
    private const string ReadmeFileName = "🔒 보안 금고에 보관됨.txt";
    private const string FileStubSuffix = ".spdvault.lock";
    private const uint ShcneAssocChanged = 0x08000000;
    private const uint ShcneUpdateDir = 0x00001000;
    private const uint ShcnfPathW = 0x0005;
    private const uint ShcnfFlush = 0x1000;
    private const uint ShcnfIdlist = 0x0000;

    public static void SealFolder(string folderPath, string bundleId, string rootEntryId) =>
        SealFolder(folderPath, bundleId, rootEntryId, warnings: null);

    public static void SealFolder(string folderPath, string bundleId, string rootEntryId, IList<string>? warnings)
    {
        Directory.CreateDirectory(folderPath);
        RunSealStep(() => SecureVaultSealIconService.CopyIconBesideDesktopIni(folderPath), "아이콘", folderPath, warnings);
        RunSealStep(() => WriteFolderDesktopIni(folderPath), "desktop.ini", folderPath, warnings);
        RunSealStep(() => WriteMarker(folderPath, bundleId, rootEntryId, isFolder: true), "잠금 표시", folderPath, warnings);
        RunSealStep(() => WriteReadme(folderPath), "안내 파일", folderPath, warnings);
        RunSealStep(() => ApplyFolderShellAttributes(folderPath), "폴더 속성", folderPath, warnings);
        NotifyShell(folderPath);
    }

    public static string? TrySealFolderTree(
        string rootFolder,
        IEnumerable<string> relativeFilePaths,
        string bundleId,
        string rootEntryId)
    {
        var warnings = new List<string>();
        try
        {
            SealIntermediateDirectories(rootFolder, relativeFilePaths, warnings);
            SealFolder(rootFolder, bundleId, rootEntryId, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add(ex.Message);
        }

        return warnings.Count == 0 ? null : string.Join(Environment.NewLine, warnings);
    }

    public static void SealFile(string filePath, string entryId)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var fileName = Path.GetFileName(filePath);
        var stubPath = Path.Combine(directory, fileName + FileStubSuffix);
        var readme = BuildFileReadme(fileName, entryId);
        File.WriteAllText(stubPath, readme, Encoding.UTF8);
        WriteMarker(directory, entryId, entryId, isFolder: false, stubPath);
        NotifyShell(directory);
    }

    public static void UnsealFolder(string folderPath, bool notifyShell = true)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        RemoveIfExists(Path.Combine(folderPath, "desktop.ini"));
        RemoveIfExists(Path.Combine(folderPath, "._spd_vault_folder.ico"));
        RemoveIfExists(Path.Combine(folderPath, MarkerFileName));
        RemoveIfExists(Path.Combine(folderPath, ReadmeFileName));
        ClearFolderShellAttributes(folderPath);
        if (notifyShell)
        {
            NotifyShell(folderPath);
        }
    }

    public static void UnsealFolderTree(string rootFolder, IEnumerable<string> relativeFilePaths, bool notifyShell = true)
    {
        if (string.IsNullOrWhiteSpace(rootFolder))
        {
            return;
        }

        var normalizedRoot = Path.GetFullPath(rootFolder);
        var directories = CollectDirectoriesForTree(normalizedRoot, relativeFilePaths);
        foreach (var directory in directories)
        {
            UnsealFolder(directory, notifyShell: false);
        }

        if (notifyShell)
        {
            NotifyShellRefresh(normalizedRoot);
        }
    }

    public static IReadOnlyList<string> CollectDirectoriesForTree(string rootFolder, IEnumerable<string> relativeFilePaths)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(rootFolder))
        {
            directories.Add(Path.GetFullPath(rootFolder));
        }

        foreach (var relative in relativeFilePaths)
        {
            var normalized = relative.Replace('\\', '/');
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
            {
                continue;
            }

            var current = Path.GetFullPath(rootFolder);
            for (var i = 0; i < segments.Length - 1; i++)
            {
                current = Path.Combine(current, segments[i]);
                directories.Add(current);
            }
        }

        return directories.OrderBy(path => path.Length).ToArray();
    }

    public static void NotifyShellRefresh(string path)
    {
        NotifyShell(path);
    }

    public static void UnsealFileStub(string originalFilePath)
    {
        var directory = Path.GetDirectoryName(originalFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var stubPath = Path.Combine(directory, Path.GetFileName(originalFilePath) + FileStubSuffix);
        RemoveIfExists(stubPath);
        RemoveIfExists(Path.Combine(directory, MarkerFileName));
        NotifyShell(directory);
    }

    public static void SecureDeleteOriginalFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var length = new FileInfo(filePath).Length;
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            fs.SetLength(0);
            var buffer = RandomNumberGenerator.GetBytes(Math.Min(64 * 1024, (int)Math.Max(1, length)));
            for (long offset = 0; offset < length; offset += buffer.Length)
            {
                fs.Write(buffer, 0, (int)Math.Min(buffer.Length, length - offset));
            }
        }

        File.Delete(filePath);
    }

    public static void SealIntermediateDirectories(string rootFolder, IEnumerable<string> relativeFilePaths) =>
        SealIntermediateDirectories(rootFolder, relativeFilePaths, warnings: null);

    public static void SealIntermediateDirectories(
        string rootFolder,
        IEnumerable<string> relativeFilePaths,
        IList<string>? warnings)
    {
        var sealedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootFolder };
        foreach (var relative in relativeFilePaths)
        {
            var normalized = relative.Replace('\\', '/');
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
            {
                continue;
            }

            var current = rootFolder;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                current = Path.Combine(current, segments[i]);
                if (sealedDirs.Add(current))
                {
                    Directory.CreateDirectory(current);
                    RunSealStep(() => SecureVaultSealIconService.CopyIconBesideDesktopIni(current), "아이콘", current, warnings);
                    RunSealStep(() => WriteFolderDesktopIni(current), "desktop.ini", current, warnings);
                    RunSealStep(() => ApplyFolderShellAttributes(current), "폴더 속성", current, warnings);
                }
            }
        }

        NotifyShell(rootFolder);
    }

    public static void EnsureSealedFolderShell(string folderPath, string bundleId, string rootEntryId)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        SealFolder(folderPath, bundleId, rootEntryId);
    }

    private static void WriteFolderDesktopIni(string folderPath)
    {
        var desktopIni = Path.Combine(folderPath, "desktop.ini");
        var localIcon = Path.Combine(folderPath, "._spd_vault_folder.ico");
        var iconLine = File.Exists(localIcon)
            ? $"IconFile={localIcon}\r\nIconIndex=0\r\n"
            : $"IconResource={SecureVaultSealIconService.GetLockedFolderIconResource()}\r\n";

        var content =
            "[.ShellClassInfo]\r\n" +
            iconLine +
            "InfoTip=PC 케어 프로 보안 금고에 보관 중입니다.\r\n" +
            "ConfirmFileOp=0\r\n" +
            "NoSharing=1\r\n";
        File.WriteAllText(desktopIni, content, Encoding.Unicode);
        File.SetAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);
    }

    private static void WriteReadme(string folderPath)
    {
        var path = Path.Combine(folderPath, ReadmeFileName);
        File.WriteAllText(
            path,
            "이 폴더의 파일은 PC 케어 프로 보안 금고에 암호화되어 보관 중입니다.\r\n" +
            "원본 파일은 안전하게 제거되었으며, 폴더 아이콘(폴더+자물쇠)은 금고 보관 상태를 나타냅니다.\r\n" +
            "복원하려면 앱의 보안 금고 메뉴에서 「원본 복원」을 실행하세요.\r\n",
            Encoding.UTF8);
    }

    private static string BuildFileReadme(string fileName, string entryId) =>
        $"「{fileName}」은(는) PC 케어 프로 보안 금고에 암호화되어 보관 중입니다.\r\n" +
        $"항목 ID: {entryId[..8]}\r\n" +
        "원본 파일은 안전하게 제거되었습니다. 복원은 보안 금고 메뉴에서 실행하세요.\r\n";

    private static void WriteMarker(string directory, string bundleId, string entryId, bool isFolder, string? stubPath = null)
    {
        var marker = new
        {
            format = "spd-vault-seal-v1",
            bundleId,
            entryId,
            isFolder,
            stubPath,
            sealedAt = DateTimeOffset.Now.ToString("o")
        };
        var markerPath = Path.Combine(directory, MarkerFileName);
        File.WriteAllText(markerPath, JsonSerializer.Serialize(marker), Encoding.UTF8);
        File.SetAttributes(markerPath, FileAttributes.Hidden | FileAttributes.System);
    }

    private static void ApplyFolderShellAttributes(string folderPath)
    {
        var info = new DirectoryInfo(folderPath);
        info.Attributes |= FileAttributes.System | FileAttributes.ReadOnly;
    }

    private static void ClearFolderShellAttributes(string folderPath)
    {
        var info = new DirectoryInfo(folderPath);
        info.Attributes &= ~(FileAttributes.System | FileAttributes.ReadOnly);
    }

    private static void RemoveIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    }

    private static void RunSealStep(Action action, string stepName, string folderPath, IList<string>? warnings)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (warnings is null)
            {
                throw;
            }

            warnings.Add($"{stepName} ({folderPath}): {ex.Message}");
        }
    }

    private static void NotifyShell(string path)
    {
        try
        {
            var ptr = Marshal.StringToCoTaskMemUni(path);
            try
            {
                SHChangeNotify(ShcneUpdateDir, ShcnfPathW | ShcnfFlush, ptr, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }

            SHChangeNotify(ShcneAssocChanged, ShcnfIdlist, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // Explorer refresh is best-effort.
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}