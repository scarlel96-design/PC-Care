using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SmartPerformanceDoctor.UpdateEngine;

/// <summary>Pin local directory ancestors. Reject links before using the opened file.</summary>
public static class WindowsFileBoundary
{
    private const uint Read = 0x80000000, Write = 0x40000000, Delete = 0x10000;
    private const uint OpenReparse = 0x00200000, BackupSemantics = 0x02000000;

    public sealed class DirectoryPin : IDisposable
    {
        private readonly List<(SafeFileHandle Handle, string Path, string Id)> _handles = [];
        public string Root { get; }
        public string Identity => _handles[^1].Id;

        internal DirectoryPin(string root, bool create)
        {
            var full = Path.GetFullPath(root);
            Root = full == Path.GetPathRoot(full) ? full : full.TrimEnd(Path.DirectorySeparatorChar);
            var drive = Path.GetPathRoot(Root)!;
            if (!OperatingSystem.IsWindows() || drive.StartsWith("\\\\", StringComparison.Ordinal) ||
                GetDriveTypeW(drive) != 3) throw new IOException("BOUNDARY-LOCAL-FIXED-DISK-REQUIRED");
            try
            {
                Add(drive);
                var current = drive;
                foreach (var part in Root[drive.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
                {
                    current = Path.Combine(current, part);
                    if (create && !Directory.Exists(current)) Directory.CreateDirectory(current);
                    Add(current);
                }
                Validate();
            }
            catch { Dispose(); throw; }
        }

        private void Add(string path)
        {
            var handle = CreateFileW(path, 0x80, 3, IntPtr.Zero, 3, OpenReparse | BackupSemantics, IntPtr.Zero);
            if (handle.IsInvalid) { handle.Dispose(); throw Error("DIRECTORY-PIN"); }
            try
            {
                var id = Check(handle, path, directory: true);
                _handles.Add((handle, path, id));
            }
            catch { handle.Dispose(); throw; }
        }

        public void Validate()
        {
            foreach (var item in _handles)
                if (Check(item.Handle, item.Path, directory: true) != item.Id)
                    throw new IOException("DIRECTORY-IDENTITY-CHANGED");
        }

        public void Dispose()
        {
            for (var index = _handles.Count - 1; index >= 0; index--) _handles[index].Handle.Dispose();
            _handles.Clear();
        }
    }

    public sealed class FileLease : IDisposable
    {
        private readonly DirectoryPin _parent;
        private readonly string _path;
        public FileStream Stream { get; }
        public string Identity { get; }

        internal FileLease(string root, string relative, bool write, bool create, bool deletable, bool exclusive = false)
        {
            if (!SignedPackage.SafeRelativePath("payload/" + relative.Replace('\\', '/')))
                throw new InvalidDataException("BOUNDARY-PATH");
            var target = Path.GetFullPath(Path.Combine(root, relative));
            var prefix = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("BOUNDARY-ESCAPE");
            _path = target;
            _parent = PinDirectory(Path.GetDirectoryName(target)!, create);
            SafeFileHandle? handle = null;
            try
            {
                handle = CreateFileW(target, Read | 0x20000 | (write ? Write : 0) | (deletable ? Delete : 0),
                    exclusive ? 0u : 1u, IntPtr.Zero, create ? 1u : 3u, OpenReparse | (write ? 0x80000000u : 0), IntPtr.Zero);
                if (handle.IsInvalid) throw Error("FILE-OPEN");
                Identity = Check(handle, target, directory: false);
                Stream = new FileStream(handle, write ? FileAccess.ReadWrite : FileAccess.Read, 128 * 1024);
                handle = null;
                _parent.Validate();
            }
            catch { handle?.Dispose(); _parent.Dispose(); throw; }
        }

        public void Validate(string? expectedIdentity = null)
        {
            _parent.Validate();
            if (Check(Stream.SafeFileHandle, _path, directory: false) != Identity ||
                (expectedIdentity is not null && Identity != expectedIdentity))
                throw new IOException("FILE-IDENTITY-CHANGED");
        }

        public FileSecurity ReadSecurity()
        {
            Validate();
            var security = Stream.GetAccessControl();
            Validate();
            return security;
        }

        public string Hash()
        {
            Validate();
            Stream.Position = 0;
            var hash = Convert.ToHexString(SHA256.HashData(Stream));
            Stream.Position = 0;
            Validate();
            return hash;
        }

        public void Verify(long length, string hash, string? identity = null)
        {
            Validate(identity);
            if (Stream.Length != length || !Hash().Equals(hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("FILE-INTEGRITY");
        }

        public void CopyFrom(FileLease source)
        {
            source.Validate();
            Validate();
            source.Stream.Position = 0;
            Stream.Position = 0;
            source.Stream.CopyTo(Stream);
            Stream.SetLength(Stream.Position);
            Stream.Flush(flushToDisk: true);
            source.Validate();
            Validate();
        }

        public void DeleteOpenedFile()
        {
            Validate();
            var disposition = 1;
            if (!SetFileInformationByHandle(Stream.SafeFileHandle, 4, ref disposition, 4))
                throw Error("FILE-DELETE");
        }

        public void Dispose() { Stream.Dispose(); _parent.Dispose(); }
    }

    public static DirectoryPin PinDirectory(string root, bool create = false) => new(root, create);
    public static FileLease OpenRead(string root, string relative) => new(root, relative, false, false, false);
    public static FileLease OpenWrite(string root, string relative, bool create = false, bool deletable = false) =>
        new(root, relative, true, create, deletable);
    public static FileLease OpenExclusive(string root, string relative, bool create) => new(root, relative, true, create, false, true);

    private static string Check(SafeFileHandle handle, string expected, bool directory)
    {
        if (!GetFileInformationByHandle(handle, out var basic)) throw Error("FILE-INFORMATION");
        if ((basic.Attributes & 0x400) != 0 || ((basic.Attributes & 0x10) != 0) != directory ||
            (!directory && basic.Links != 1)) throw new IOException("BOUNDARY-LINK-OR-TYPE");
        if (!GetFileInformationByHandleEx(handle, 18, out var id, (uint)Marshal.SizeOf<FileIdInfo>()))
            throw Error("FILE-ID-UNSUPPORTED");
        var buffer = new StringBuilder(32768);
        var size = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        if (size == 0 || size >= buffer.Capacity) throw Error("FINAL-PATH");
        var actual = buffer.ToString();
        if (actual.StartsWith("\\\\?\\", StringComparison.Ordinal)) actual = actual[4..];
        if (!string.Equals(actual.TrimEnd('\\'), Path.GetFullPath(expected).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new IOException("FINAL-PATH-MISMATCH");
        return $"{id.Volume:X16}:{id.Low:X16}{id.High:X16}";
    }

    private static IOException Error(string code) => new(code + ": " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
    [StructLayout(LayoutKind.Sequential)] private struct FileIdInfo { public ulong Volume, Low, High; }
    [StructLayout(LayoutKind.Sequential)] private struct BasicInfo
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out BasicInfo info);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int type, out FileIdInfo info, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint size, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern uint GetDriveTypeW(string root);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int type, ref int info, uint size);
}
