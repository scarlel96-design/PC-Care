using System.Diagnostics;
using System.Security.Principal;
using SmartPerformanceDoctor.App.Services.SystemCare;
using SmartPerformanceDoctor.UpdateEngine;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>Pin a bounded directory tree for review. This class never authorizes deletion.</summary>
public sealed class SecureDeleteDirectoryReview : IDisposable
{
    private sealed record Entry(string Path, string Identity, long Length, string Hash);
    private readonly List<WindowsFileBoundary.DirectoryPin> _directories = [];
    private readonly List<Entry> _files = [];
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
    public bool ProductionExecutionEnabled => false;
    public string RootIdentity => _directories[0].Identity;
    public int FileCount => _files.Count;

    public static SecureDeleteDirectoryReview Capture(string root, CancellationToken token = default)
    {
        var result = new SecureDeleteDirectoryReview(); var timer = Stopwatch.StartNew();
        var pending = new Stack<(string Path,int Depth)>(); pending.Push((System.IO.Path.GetFullPath(root),0));
        long bytes = 0;
        try
        {
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var (path,depth) = pending.Pop();
                if (depth > 4 || timer.ElapsedMilliseconds > 5000 || result._paths.Count >= 512)
                    throw new InvalidDataException("DirectoryScanIncomplete");
                if (StorageTargetPolicy.IsProtected(path)) throw new InvalidDataException("ProtectedDirectory");
                var pin = WindowsFileBoundary.PinDirectory(path); result._directories.Add(pin);
                foreach (var child in Directory.EnumerateFileSystemEntries(path))
                {
                    token.ThrowIfCancellationRequested();
                    if (!result._paths.Add(child) || result._paths.Count > 512 || timer.ElapsedMilliseconds > 5000)
                        throw new InvalidDataException("DirectoryScanIncomplete");
                    var attrs=File.GetAttributes(child);
                    if (StorageTargetPolicy.IsProtected(child) || (attrs & (FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden)) != 0)
                        throw new InvalidDataException("ProtectedDirectoryEntry");
                    if ((attrs & FileAttributes.Directory) != 0) {pending.Push((child,depth+1));continue;}
                    using var file=WindowsFileBoundary.OpenRead(path,System.IO.Path.GetFileName(child));
                    if (file.ReadSecurity().GetOwner(typeof(SecurityIdentifier)) != WindowsIdentity.GetCurrent().User)
                        throw new UnauthorizedAccessException("DirectoryEntryOwnerUnknown");
                    bytes+=file.Stream.Length;
                    if (file.Stream.Length>64*1024*1024 || bytes>256*1024*1024) throw new InvalidDataException("DirectoryHashBudget");
                    result._files.Add(new(child,file.Identity,file.Stream.Length,file.Hash()));
                }
            }
            result.ValidateUnchanged(); return result;
        }
        catch {result.Dispose();throw;}
    }

    public void ValidateUnchanged()
    {
        var current=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var pin in _directories)
        {
            pin.Validate();
            foreach(var child in Directory.EnumerateFileSystemEntries(pin.Root))
                if (!current.Add(child) || current.Count>512) throw new IOException("DirectoryEntriesChanged");
        }
        if (!current.SetEquals(_paths)) throw new IOException("DirectoryEntriesChanged");
        foreach(var entry in _files)
        {
            using var file=WindowsFileBoundary.OpenRead(System.IO.Path.GetDirectoryName(entry.Path)!,System.IO.Path.GetFileName(entry.Path));
            file.Verify(entry.Length,entry.Hash,entry.Identity);
            if (file.ReadSecurity().GetOwner(typeof(SecurityIdentifier)) != WindowsIdentity.GetCurrent().User)
                throw new UnauthorizedAccessException("DirectoryEntryOwnerChanged");
        }
    }

    public void Dispose() {for(var i=_directories.Count-1;i>=0;i--) _directories[i].Dispose();_directories.Clear();}
}
