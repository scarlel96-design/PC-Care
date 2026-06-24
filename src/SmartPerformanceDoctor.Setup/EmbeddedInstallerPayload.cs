using System.IO;
using System.IO.Compression;
using System.Text;

namespace SmartPerformanceDoctor.Setup;

internal static class EmbeddedInstallerPayload
{
    private static readonly byte[] PackageMagic = "SPDPKG1\0"u8.ToArray();
    private const int TrailerHeaderSize = 8 + 8 + 8; // magic + layoutLen + msiLen

    public static void EnsureExtracted()
    {
        var baseDir = AppContext.BaseDirectory;
        var layoutDir = Path.Combine(baseDir, "layout");
        var msiName = $"SmartPerformanceDoctor_v{InstallerPaths.ProductVersion.Replace('.', '_')}.msi";
        var msiPath = Path.Combine(baseDir, msiName);
        var marker = Path.Combine(baseDir, ".installer-payload.ok");

        if (File.Exists(marker) && Directory.Exists(layoutDir) && Directory.EnumerateFiles(layoutDir).Any())
        {
            return;
        }

        if (!TryExtractAppendedPayload(layoutDir, msiPath))
        {
            return;
        }

        File.WriteAllText(marker, DateTimeOffset.Now.ToString("o"));
    }

    private static bool TryExtractAppendedPayload(string layoutDir, string msiPath)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return false;
        }

        using var stream = File.OpenRead(exePath);
        if (stream.Length < TrailerHeaderSize + PackageMagic.Length)
        {
            return false;
        }

        stream.Seek(-TrailerHeaderSize, SeekOrigin.End);
        using var header = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var magic = header.ReadBytes(PackageMagic.Length);
        if (!magic.AsSpan().SequenceEqual(PackageMagic))
        {
            return false;
        }

        var layoutLength = header.ReadInt64();
        var msiLength = header.ReadInt64();
        if (layoutLength < 0 || msiLength < 0)
        {
            return false;
        }

        var payloadLength = layoutLength + msiLength;
        if (payloadLength <= 0 || payloadLength + TrailerHeaderSize > stream.Length)
        {
            return false;
        }

        var payloadStart = stream.Length - TrailerHeaderSize - payloadLength;
        stream.Seek(payloadStart, SeekOrigin.Begin);

        if (layoutLength > 0)
        {
            if (Directory.Exists(layoutDir))
            {
                Directory.Delete(layoutDir, true);
            }

            Directory.CreateDirectory(layoutDir);
            using var zipStream = new SubStream(stream, payloadStart, layoutLength);
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
            zip.ExtractToDirectory(layoutDir, overwriteFiles: true);
        }

        if (msiLength > 0)
        {
            var msiStart = payloadStart + layoutLength;
            stream.Seek(msiStart, SeekOrigin.Begin);
            var directory = Path.GetDirectoryName(msiPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var output = File.Create(msiPath);
            CopyBytes(stream, output, msiLength);
        }

        return true;
    }

    private static void CopyBytes(Stream input, Stream output, long length)
    {
        var buffer = new byte[81920];
        var remaining = length;
        while (remaining > 0)
        {
            var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read <= 0)
            {
                break;
            }

            output.Write(buffer, 0, read);
            remaining -= read;
        }
    }

    private sealed class SubStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _start;
        private readonly long _length;
        private long _position;

        public SubStream(Stream inner, long start, long length)
        {
            _inner = inner;
            _start = start;
            _length = length;
            _position = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var allowed = (int)Math.Min(count, _length - _position);
            if (allowed <= 0)
            {
                return 0;
            }

            _inner.Position = _start + _position;
            var read = _inner.Read(buffer, offset, allowed);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => _position
            };

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}