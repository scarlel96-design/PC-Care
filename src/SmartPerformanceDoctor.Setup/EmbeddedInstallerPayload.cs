using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.Setup;

internal static class EmbeddedInstallerPayload
{
    private static readonly byte[] PackageMagic = "SPDPKG1\0"u8.ToArray();
    private const int TrailerHeaderSize = 8 + 8 + 8; // magic + layoutLen + msiLen
    private const long MinSelfContainedExeBytes = 256 * 1024;

    public static string CacheRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCCare",
            "installer-cache",
            InstallerPaths.ProductVersion);

    public static string LayoutDirectory => Path.Combine(CacheRoot, "layout");

    public static void EnsureExtracted()
    {
        var layoutDir = LayoutDirectory;
        var marker = Path.Combine(CacheRoot, ".extracted.ok");
        var installerFingerprint = ComputeInstallerFingerprint();
        if (File.Exists(marker)
            && MarkerMatchesInstaller(marker, installerFingerprint)
            && IsLayoutValid(layoutDir))
        {
            return;
        }

        if (Directory.Exists(layoutDir))
        {
            try
            {
                Directory.Delete(layoutDir, true);
            }
            catch
            {
                // Best effort — ExtractZipToLayout also recreates the directory.
            }
        }

        if (File.Exists(marker))
        {
            try
            {
                File.Delete(marker);
            }
            catch
            {
                // Best effort — re-extract below.
            }
        }

        Directory.CreateDirectory(CacheRoot);

        if (!TryExtractAppendedPayload(layoutDir, ResolveMsiCachePath()))
        {
            throw new InvalidOperationException(
                "설치 페이로드를 추출하지 못했습니다. 설치 파일이 손상되었거나 서명 데이터가 손상되었을 수 있습니다.");
        }

        if (!IsLayoutValid(layoutDir))
        {
            throw new InvalidOperationException(
                "설치 레이아웃이 불완전합니다. self-contained .NET 런타임(coreclr.dll 등) 또는 PCCare.exe가 누락되었습니다. " +
                "이전 설치 캐시가 남아 있을 수 있으니 %LocalAppData%\\PCCare\\installer-cache 를 삭제한 뒤 다시 실행하세요.");
        }

        WriteExtractionMarker(marker, installerFingerprint);
    }

    private static string ResolveMsiCachePath()
    {
        var msiName = $"SmartPerformanceDoctor_v{InstallerPaths.ProductVersion.Replace('.', '_')}.msi";
        return Path.Combine(CacheRoot, msiName);
    }

    private static bool IsLayoutValid(string layoutDir)
    {
        if (!Directory.Exists(layoutDir))
        {
            return false;
        }

        string[] required =
        [
            Path.Combine(layoutDir, "PCCare.exe"),
            Path.Combine(layoutDir, "PCCare.runtimeconfig.json"),
            Path.Combine(layoutDir, "SmartPerformanceDoctor.dll"),
            Path.Combine(layoutDir, "coreclr.dll"),
            Path.Combine(layoutDir, "hostfxr.dll"),
            Path.Combine(layoutDir, "hostpolicy.dll"),
            Path.Combine(layoutDir, "engine", "smart_performance_doctor_core.exe"),
            Path.Combine(layoutDir, "engine", "smart_performance_doctor_repair_helper.exe"),
            Path.Combine(layoutDir, "App.xbf"),
            Path.Combine(layoutDir, "MainWindow.xbf"),
            Path.Combine(layoutDir, "Microsoft.UI.Xaml", "Themes", "themeresources.xbf"),
            Path.Combine(layoutDir, "Microsoft.UI.Xaml", "Themes", "generic.xbf")
        ];

        if (!required.All(File.Exists))
        {
            return false;
        }

        var exeInfo = new FileInfo(required[0]);
        if (exeInfo.Length < MinSelfContainedExeBytes)
        {
            return false;
        }

        return IsSelfContainedRuntimeConfig(required[1])
            && Directory.EnumerateFiles(layoutDir, "*", SearchOption.AllDirectories).Any();
    }

    private static bool IsSelfContainedRuntimeConfig(string runtimeConfigPath)
    {
        try
        {
            using var stream = File.OpenRead(runtimeConfigPath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("runtimeOptions", out var runtimeOptions))
            {
                return false;
            }

            if (runtimeOptions.TryGetProperty("framework", out _))
            {
                return false;
            }

            if (!runtimeOptions.TryGetProperty("includedFrameworks", out var includedFrameworks)
                || includedFrameworks.ValueKind != JsonValueKind.Array
                || includedFrameworks.GetArrayLength() == 0)
            {
                return false;
            }

            foreach (var framework in includedFrameworks.EnumerateArray())
            {
                if (framework.TryGetProperty("name", out var name)
                    && name.GetString() == "Microsoft.NETCore.App")
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string? ComputeInstallerFingerprint()
    {
        foreach (var exePath in GetInstallerExeCandidates())
        {
            try
            {
                var info = new FileInfo(exePath);
                if (!info.Exists || info.Length < MinSelfContainedExeBytes)
                {
                    continue;
                }

                using var stream = File.OpenRead(exePath);
                var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                return $"{hash}:{info.Length}";
            }
            catch
            {
                // Try the next candidate path.
            }
        }

        return null;
    }

    private static bool MarkerMatchesInstaller(string markerPath, string? installerFingerprint)
    {
        if (string.IsNullOrWhiteSpace(installerFingerprint))
        {
            return false;
        }

        try
        {
            var marker = File.ReadAllText(markerPath).Trim();
            return marker.Contains(installerFingerprint, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void WriteExtractionMarker(string markerPath, string? installerFingerprint)
    {
        var payload = string.IsNullOrWhiteSpace(installerFingerprint)
            ? DateTimeOffset.Now.ToString("o")
            : $"{DateTimeOffset.Now:O}|{installerFingerprint}";
        File.WriteAllText(markerPath, payload);
    }

    private static bool TryExtractAppendedPayload(string layoutDir, string msiPath)
    {
        foreach (var exePath in GetInstallerExeCandidates())
        {
            if (TryExtractAppendedPayloadFromExe(exePath, layoutDir, msiPath))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetInstallerExeCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        void Collect(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var full = Path.GetFullPath(path);
                if (seen.Add(full) && File.Exists(full))
                {
                    results.Add(full);
                }
            }
            catch
            {
                // Ignore invalid paths.
            }
        }

        Collect(Environment.ProcessPath);
        Collect(Environment.GetCommandLineArgs().FirstOrDefault());
        Collect(Path.Combine(AppContext.BaseDirectory, "SmartPerformanceDoctor.Setup.exe"));
        Collect(Path.Combine(AppContext.BaseDirectory, "PCCare_Setup.exe"));

        return results;
    }

    private static bool TryExtractAppendedPayloadFromExe(string exePath, string layoutDir, string msiPath)
    {
        using var stream = File.OpenRead(exePath);
        if (stream.Length < TrailerHeaderSize + PackageMagic.Length)
        {
            return false;
        }

        if (!TryFindTrailerHeader(stream, out var headerOffset))
        {
            return false;
        }

        stream.Seek(headerOffset + PackageMagic.Length, SeekOrigin.Begin);
        using var header = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var layoutLength = header.ReadInt64();
        var msiLength = header.ReadInt64();
        if (layoutLength < 0 || msiLength < 0)
        {
            return false;
        }

        var payloadLength = layoutLength + msiLength;
        if (payloadLength <= 0 || headerOffset < payloadLength)
        {
            return false;
        }

        var payloadStart = headerOffset - payloadLength;
        if (layoutLength > 0)
        {
            stream.Seek(payloadStart, SeekOrigin.Begin);
            var zipOffset = FindZipPayloadOffset(stream, payloadStart, layoutLength);
            if (zipOffset < 0)
            {
                return false;
            }

            var zipLength = layoutLength - (zipOffset - payloadStart);
            using var zipStream = new SubStream(stream, zipOffset, zipLength);
            if (!ExtractZipToLayout(zipStream, layoutDir))
            {
                return false;
            }
        }

        PurgeStaleMsiFiles(msiPath);

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

    private static void PurgeStaleMsiFiles(string expectedMsiPath)
    {
        var directory = Path.GetDirectoryName(expectedMsiPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "SmartPerformanceDoctor_v*.msi"))
        {
            if (file.Equals(expectedMsiPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch
            {
                // Best effort.
            }
        }
    }

    private static bool ExtractZipToLayout(Stream zipStream, string layoutDir)
    {
        try
        {
            if (Directory.Exists(layoutDir))
            {
                Directory.Delete(layoutDir, true);
            }

            Directory.CreateDirectory(layoutDir);
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
            zip.ExtractToDirectory(layoutDir, overwriteFiles: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryFindTrailerHeader(Stream stream, out long headerOffset)
    {
        const int maxTrailingScan = 64 * 1024 * 1024;
        var searchLength = (int)Math.Min(stream.Length, maxTrailingScan);
        var searchStart = stream.Length - searchLength;
        var buffer = new byte[searchLength];
        stream.Seek(searchStart, SeekOrigin.Begin);
        var read = stream.Read(buffer, 0, searchLength);
        if (read < TrailerHeaderSize)
        {
            headerOffset = -1;
            return false;
        }

        for (var i = read - TrailerHeaderSize; i >= 0; i--)
        {
            if (!buffer.AsSpan(i, PackageMagic.Length).SequenceEqual(PackageMagic))
            {
                continue;
            }

            var layoutLength = BitConverter.ToInt64(buffer, i + PackageMagic.Length);
            var msiLength = BitConverter.ToInt64(buffer, i + PackageMagic.Length + 8);
            var payloadLength = layoutLength + msiLength;
            if (layoutLength < 0 || msiLength < 0 || payloadLength <= 0)
            {
                continue;
            }

            var candidateHeader = searchStart + i;
            var payloadStart = candidateHeader - payloadLength;
            if (payloadStart < 0 || candidateHeader + TrailerHeaderSize > stream.Length)
            {
                continue;
            }

            if (layoutLength > 0 && FindZipPayloadOffset(stream, payloadStart, layoutLength) < 0)
            {
                continue;
            }

            headerOffset = candidateHeader;
            return true;
        }

        headerOffset = -1;
        return false;
    }

    private static long FindZipPayloadOffset(Stream stream, long payloadStart, long payloadLength)
    {
        const int maxLeadingSkew = 16;
        var scanLength = (int)Math.Min(payloadLength, maxLeadingSkew + 4);
        if (scanLength < 4)
        {
            return -1;
        }

        var buffer = new byte[scanLength];
        stream.Seek(payloadStart, SeekOrigin.Begin);
        var read = stream.Read(buffer, 0, scanLength);
        if (read < 4)
        {
            return -1;
        }

        for (var offset = 0; offset <= Math.Min(maxLeadingSkew, read - 4); offset++)
        {
            if (buffer[offset] == 0x50
                && buffer[offset + 1] == 0x4B
                && buffer[offset + 2] == 0x03
                && buffer[offset + 3] == 0x04)
            {
                return payloadStart + offset;
            }
        }

        return -1;
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