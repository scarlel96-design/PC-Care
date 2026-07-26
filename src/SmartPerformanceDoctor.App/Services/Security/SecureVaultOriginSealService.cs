using System.Runtime.InteropServices;

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

    // v51.0.4+: Do not create origin markers, readme files, icon overlays or lock stubs.
    // The vault portal is the only state surface. These methods only remove markers made
    // by older versions so imports cannot leave existence metadata at the source path.
    public static void SealFolder(string folderPath, string bundleId, string rootEntryId) =>
        UnsealFolder(folderPath);

    public static void SealFolder(string folderPath, string bundleId, string rootEntryId, IList<string>? warnings) =>
        UnsealFolder(folderPath);

    public static string? TrySealFolderTree(
        string rootFolder,
        IEnumerable<string> relativeFilePaths,
        string bundleId,
        string rootEntryId)
    {
        UnsealFolderTree(rootFolder, relativeFilePaths);
        return null;
    }

    public static void SealFile(string filePath, string entryId) =>
        UnsealFileStub(filePath);
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

    /// <summary>
    /// Removes the original only through the shared secure-delete engine. A failed wipe
    /// is surfaced to the import transaction; it must never fall back to recycle-bin or
    /// a plain File.Delete path and still be reported as a sealed import.
    /// </summary>
    public static void SecureDeleteOriginalFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("원본 파일을 찾을 수 없습니다.", filePath);
        }

        var storage = Commercial.SecureDeleteStorageProfiler.Profile(filePath);
        Commercial.ForensicSecureDeleteEngine
            .SecureDeleteFileAsync(
                filePath,
                storage,
                Commercial.SecureDeleteSecurityLevel.Professional,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (File.Exists(filePath))
        {
            throw new IOException("원본 보안 삭제 후 경로 검증에 실패했습니다.");
        }
    }
    public static void SealIntermediateDirectories(string rootFolder, IEnumerable<string> relativeFilePaths) =>
        UnsealFolderTree(rootFolder, relativeFilePaths);

    public static void SealIntermediateDirectories(
        string rootFolder,
        IEnumerable<string> relativeFilePaths,
        IList<string>? warnings) =>
        UnsealFolderTree(rootFolder, relativeFilePaths);

    public static void EnsureSealedFolderShell(string folderPath, string bundleId, string rootEntryId) =>
        UnsealFolder(folderPath);
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