using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Text;
using Microsoft.Win32.SafeHandles;

#if PCCARE_VAULT_BOUNDARY
namespace SmartPerformanceDoctor.SecurityLab.Hardening;
#else
namespace SmartPerformanceDoctor.UpdateEngine;
#endif

/// <summary>Pin local directory ancestors. Reject links before using the opened file.</summary>
#if PCCARE_VAULT_BOUNDARY
public static class VaultFileBoundary
#else
public static class WindowsFileBoundary
#endif
{
    private const uint Read = 0x80000000, Write = 0x40000000, Delete = 0x10000;
    private const uint OpenReparse = 0x00200000, BackupSemantics = 0x02000000;

#if !PCCARE_VAULT_BOUNDARY
    [ThreadStatic] private static DirectoryHandleScope? _directoryScope;

    // Retain each native directory handle until the synchronous inspection ends.
    internal sealed class DirectoryHandleScope : IDisposable
    {
        private readonly DirectoryHandleScope? _previous = _directoryScope;
        private readonly Dictionary<string, (SafeFileHandle Handle, string Path, string Id)> _handles = new(StringComparer.OrdinalIgnoreCase);
        internal int ReusedHandles { get; private set; }
        internal int UniqueHandles => _handles.Count;
        internal DirectoryHandleScope() { _directoryScope = this; }
        internal (SafeFileHandle Handle, string Path, string Id) Acquire(string path)
        {
            if (_handles.TryGetValue(path, out var existing))
            {
                if (Check(existing.Handle, path, true) != existing.Id) throw new IOException("DIRECTORY-IDENTITY-CHANGED");
                ReusedHandles++;
                return existing;
            }
            var handle = CreateFileW(NativePath(path), 0x80, 3, IntPtr.Zero, 3, OpenReparse | BackupSemantics, IntPtr.Zero);
            if (handle.IsInvalid) { handle.Dispose(); throw Error("DIRECTORY-PIN"); }
            try
            {
                var item = (handle, path, Check(handle, path, true));
                _handles.Add(path, item);
                return item;
            }
            catch { handle.Dispose(); throw; }
        }
        internal void Validate()
        {
            foreach (var item in _handles.Values)
                if (Check(item.Handle, item.Path, true) != item.Id) throw new IOException("DIRECTORY-IDENTITY-CHANGED");
        }
        public void Dispose()
        {
            _directoryScope = _previous;
            foreach (var item in _handles.Values) item.Handle.Dispose();
            _handles.Clear();
        }
    }
    internal static DirectoryHandleScope RetainSharedDirectoryHandles() => new();
    internal sealed record CopyProgress(string Phase, long BytesWritten, long TotalBytes, int WriteCalls);
#endif

    public sealed class DirectoryPin : IDisposable
    {
        private readonly List<(SafeFileHandle Handle, string Path, string Id)> _handles = [];
#if !PCCARE_VAULT_BOUNDARY
        private readonly DirectoryHandleScope? _owner = _directoryScope;
#endif
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
#if !PCCARE_VAULT_BOUNDARY
            if (_owner is not null) { _handles.Add(_owner.Acquire(path)); return; }
#endif
            var handle = CreateFileW(NativePath(path), 0x80, 3, IntPtr.Zero, 3, OpenReparse | BackupSemantics, IntPtr.Zero);
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
#if !PCCARE_VAULT_BOUNDARY
            if (_owner is not null) { _handles.Clear(); return; }
#endif
            for (var index = _handles.Count - 1; index >= 0; index--) _handles[index].Handle.Dispose();
            _handles.Clear();
        }
    }

    public sealed class FileLease : IDisposable
    {
        private readonly DirectoryPin _parent;
        private string _path;
        public FileStream Stream { get; }
        private DirectoryPin? _publicationParent;
        public string Identity { get; }

        internal FileLease(string root, string relative, bool write, bool create, bool deletable, bool exclusive = false, int bufferSize = 128 * 1024)
        {
#if !PCCARE_VAULT_BOUNDARY
            using var openingMeasurement=UpdatePerformanceProfile.Active?.Measure(create?"FileCreateAndBind":"FileOpenAndBind");
#endif
#if PCCARE_VAULT_BOUNDARY
            if (Path.IsPathRooted(relative) || relative.Split(['\\', '/']).Any(part =>
                    string.IsNullOrEmpty(part) || part is "." or ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
#else
            if (!SignedPackage.SafeRelativePath("payload/" + relative.Replace('\\', '/')))
#endif
                throw new InvalidDataException("BOUNDARY-PATH");
            var target = Path.GetFullPath(Path.Combine(root, relative));
            var prefix = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("BOUNDARY-ESCAPE");
            _path = target;
            _parent = PinDirectory(Path.GetDirectoryName(target)!, create);
            SafeFileHandle? handle = null;
            try
            {
                handle = CreateFileW(NativePath(target), Read | 0x20000 | (write ? Write : 0) | (deletable ? Delete : 0),
                    exclusive ? 0u : 1u, IntPtr.Zero, create ? 1u : 3u, OpenReparse | (write ? 0x80000000u : 0), IntPtr.Zero);
                if (handle.IsInvalid) throw Error("FILE-OPEN");
                Identity = Check(handle, target, directory: false);
                Stream = new FileStream(handle, write ? FileAccess.ReadWrite : FileAccess.Read, bufferSize);
                handle = null;
                _parent.Validate();
            }
            catch { handle?.Dispose(); _parent.Dispose(); throw; }
        }

        public void Validate(string? expectedIdentity = null)
        {
#if !PCCARE_VAULT_BOUNDARY
            using var measurement = UpdatePerformanceProfile.Active?.Measure("FileIdentityValidation");
#endif
            _parent.Validate();
            _publicationParent?.Validate();
            if (Check(Stream.SafeFileHandle, _path, directory: false) != Identity ||
                (expectedIdentity is not null && Identity != expectedIdentity))
                throw new IOException("FILE-IDENTITY-CHANGED");
        }

        public FileSecurity ReadSecurity()
        {
#if !PCCARE_VAULT_BOUNDARY
            using var measurement = UpdatePerformanceProfile.Active?.Measure("AclRead");
#endif
            Validate();
            var security = Stream.GetAccessControl();
            Validate();
            return security;
        }

        public string Hash()
        {
#if !PCCARE_VAULT_BOUNDARY
            using var measurement = UpdatePerformanceProfile.Active?.Measure("HashAndIdentity");
#endif
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

        public void CopyFrom(FileLease source, Action? afterFirstWrite = null, CancellationToken cancellationToken = default)
        {
#if !PCCARE_VAULT_BOUNDARY
            using var measurement = UpdatePerformanceProfile.Active?.Measure("CopyAndFlush");
#endif
            source.Validate();
            Validate();
            source.Stream.Position = 0;
            Stream.Position = 0;
            cancellationToken.ThrowIfCancellationRequested();
            if (afterFirstWrite is null && !cancellationToken.CanBeCanceled) source.Stream.CopyTo(Stream);
            else
            {
                var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(128 * 1024);
                try
                {
                    var first = true; int count;
                    while ((count = source.Stream.Read(buffer)) != 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Stream.Write(buffer, 0, count);
                        if (first) { first = false; afterFirstWrite?.Invoke(); }
                    }
                    if (first) afterFirstWrite?.Invoke();
                }
                finally { CryptographicOperations.ZeroMemory(buffer); System.Buffers.ArrayPool<byte>.Shared.Return(buffer); }
            }
            Stream.SetLength(Stream.Position);
#if !PCCARE_VAULT_BOUNDARY
            using(UpdatePerformanceProfile.Active?.Measure("PayloadFlush")) Stream.Flush(flushToDisk: true);
#else
            Stream.Flush(flushToDisk: true);
#endif
            source.Validate();
            Validate();
        }

#if !PCCARE_VAULT_BOUNDARY
        // The synthetic profile observes completed native writes, not managed buffer acceptance.
        internal void CopyFromObserved(FileLease source, Action<CopyProgress> observe)
        {
            source.Validate(); Validate();
            source.Stream.Position = 0;
            Stream.Flush();
            var total = source.Stream.Length;
            long written = 0;
            var calls = 0;
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(4096);
            void Report(string phase) => observe(new(phase, written, total, calls));
            try
            {
                Report("BeforeFirstWrite");
                while (written < total)
                {
                    var count = source.Stream.Read(buffer, 0, (int)Math.Min(4096, total - written));
                    if (count == 0) throw new EndOfStreamException("COPY-SOURCE-SHORT");
                    if (written + count == total) Report("BeforeLastWrite");
                    RandomAccess.Write(Stream.SafeFileHandle, buffer.AsSpan(0, count), written);
                    written += count; calls++;
                    Report("AfterWrite");
                    if (written == total) Report("AfterLastWrite");
                }
                Stream.SetLength(written);
                Report("BeforeFlush");
                Stream.Flush(flushToDisk: true);
                Report("AfterFlush");
                source.Validate(); Validate();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(buffer);
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
#endif

        public void DeleteOpenedFile()
        {
            Validate();
            var disposition = 1;
            if (!SetFileInformationByHandle(Stream.SafeFileHandle, 4, ref disposition, 4))
                throw Error("FILE-DELETE");
        }

        public void RenameOpenedFile(string destination)
        {
            Validate();
            var full = Path.GetFullPath(destination);
            if (!string.Equals(Path.GetDirectoryName(full), Path.GetDirectoryName(_path), StringComparison.OrdinalIgnoreCase))
                throw new IOException("RENAME-PARENT-MISMATCH");
            RenameCore(full);
        }

        private void RenameCore(string full)
        {
            var name = Encoding.Unicode.GetBytes(NativePath(full));
            var offset = IntPtr.Size == 8 ? 20 : 12;
            var size = offset + name.Length + 2;
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                for (var i = 0; i < size; i++) Marshal.WriteByte(buffer, i, 0);
                Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 16 : 8, name.Length);
                Marshal.Copy(name, 0, buffer + offset, name.Length);
                if (!SetFileInformationByHandle(Stream.SafeFileHandle, 3, buffer, (uint)size))
                    throw Error("FILE-RENAME");
                _path = full;
                Validate();
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        /// <summary>Publish a verified file without replacing an existing destination.</summary>
        public void PublishOpenedFile(string destinationRoot, string relative)
        {
            Validate();
            if (_publicationParent is not null) throw new IOException("ALREADY-PUBLISHED");
            var full = Path.GetFullPath(Path.Combine(destinationRoot, relative));
            var rootPrefix = Path.GetFullPath(destinationRoot).TrimEnd('\\') + "\\";
            if (Path.IsPathRooted(relative) || !full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                relative.Split('\\', '/').Any(p => p is ".." or "." or "" || p.Contains(':')))
                throw new IOException("PUBLISH-RELATIVE-PATH");
            if (!string.Equals(Path.GetPathRoot(full), Path.GetPathRoot(_path), StringComparison.OrdinalIgnoreCase))
                throw new IOException("PUBLISH-SAME-VOLUME-REQUIRED");
            using var root = PinDirectory(destinationRoot);
            var parent = PinDirectory(Path.GetDirectoryName(full)!, create: true);
            try
            {
                root.Validate(); parent.Validate(); Validate();
                if (File.Exists(full) || Directory.Exists(full)) throw new IOException("PUBLISH-DESTINATION-EXISTS");
                _publicationParent = parent;
                RenameCore(full);
                root.Validate(); parent.Validate(); Validate();
            }
            catch { if (_publicationParent == parent) _publicationParent = null; parent.Dispose(); throw; }
        }

        public void Dispose() { Stream.Dispose(); _publicationParent?.Dispose(); _parent.Dispose(); }
    }

    public static DirectoryPin PinDirectory(string root, bool create = false) => new(root, create);
    public static FileLease OpenRead(string root, string relative) => new(root, relative, false, false, false);
    internal static FileLease OpenMetadata(string root, string relative) => new(root, relative, false, false, false, bufferSize: 4096);
    public static FileLease OpenWrite(string root, string relative, bool create = false, bool deletable = false) =>
        new(root, relative, true, create, deletable);
    public static FileLease OpenExclusive(string root, string relative, bool create) => new(root, relative, true, create, false, true);

    private static string Check(SafeFileHandle handle, string expected, bool directory)
    {
#if !PCCARE_VAULT_BOUNDARY
        using var measurement = UpdatePerformanceProfile.Active?.Measure("NativeIdentityCheck");
#endif
        if (!GetFileInformationByHandle(handle, out var basic)) throw Error("FILE-INFORMATION");
        if ((basic.Attributes & 0x400) != 0 || ((basic.Attributes & 0x10) != 0) != directory ||
            (!directory && basic.Links != 1)) throw new IOException("BOUNDARY-LINK-OR-TYPE");
        if (!GetFileInformationByHandleEx(handle, 18, out var id, (uint)Marshal.SizeOf<FileIdInfo>()))
            throw Error("FILE-ID-UNSUPPORTED");
        var buffer = new StringBuilder(512);
        var size = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        if (size >= buffer.Capacity && size <= 32768)
        {
            buffer = new StringBuilder(checked((int)size + 1));
            size = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        }
        if (size == 0 || size >= buffer.Capacity || size > 32768) throw Error("FINAL-PATH");
        var actual = buffer.ToString();
        if (actual.StartsWith("\\\\?\\", StringComparison.Ordinal)) actual = actual[4..];
        if (!string.Equals(actual.TrimEnd('\\'), Path.GetFullPath(expected).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new IOException("FINAL-PATH-MISMATCH: " + actual + " != " + expected);
        return $"{id.Volume:X16}:{id.Low:X16}{id.High:X16}";
    }

    private static IOException Error(string code)
    {
        var error = new Win32Exception(Marshal.GetLastWin32Error());
        return new IOException(code + ": " + error.Message, error);
    }
    private static string NativePath(string path) => "\\\\?\\" + Path.GetFullPath(path);
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
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int type, IntPtr info, uint size);
}
