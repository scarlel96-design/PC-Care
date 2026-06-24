using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using SmartPerformanceDoctor.App.Branding;

namespace SmartPerformanceDoctor.App.Services.Commercial;

internal static class ForensicSecureDeleteEngine
{
    private const uint FsctlFileLevelTrim = 0x00098208;
    private const uint FsctlSetZeroData = 0x000980C8;
    private const uint FileFlagWriteThrough = 0x80000000;
    private const uint FileFlagNoBuffering = 0x20000000;

    public static IReadOnlyList<string> BuildChainSteps(string storageType, SecureDeleteSecurityLevel level)
    {
        var steps = new List<string> { "읽기전용 해제", "대체 데이터 스트림(ADS) 제거" };
        var passes = SecureDeleteStorageProfiler.GetOverwritePasses(storageType, level);
        if (passes > 0)
        {
            steps.Add($"데이터 영역 {passes}회 덮어쓰기 ({SecureDeleteStorageProfiler.SelectProtocol(storageType, level)})");
        }
        else
        {
            var obfuscation = SecureDeleteStorageProfiler.GetSsdObfuscationPasses(level);
            var crypto = SecureDeleteStorageProfiler.GetSsdCryptoErasePasses(level);
            steps.Add($"SSD/NVMe: 난독화 {obfuscation}회 · 랜덤 덮어쓰기 {crypto}회 · TRIM · 볼륨 retrim");
        }

        steps.Add("파일명 난수화");
        steps.Add("메타데이터 축소 후 삭제");
        steps.Add("섀도 복사본(VSS) 잔존 위험 스캔 및 고지");
        return steps;
    }

    public static async Task SecureDeleteFileAsync(
        string path,
        string storageType,
        SecureDeleteSecurityLevel level,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return;
        }

        ClearReadOnlyAttributes(path);
        WipeAlternateDataStreams(path);

        if (storageType is "SSD" or "NVMe")
        {
            await SsdForensicObfuscationAsync(path, level, cancellationToken);
            await SsdCryptoEraseAsync(path, level, cancellationToken);
        }
        else
        {
            var passes = SecureDeleteStorageProfiler.GetOverwritePasses(storageType, level);
            if (passes > 0)
            {
                await OverwriteFileAsync(path, passes, storageType, cancellationToken);
            }
        }

        TryTrimOrZeroData(path);
        await RandomizeNameAndDeleteAsync(path, cancellationToken);
        if (storageType is "SSD" or "NVMe")
        {
            TryVolumeRetrim(path);
        }
    }

    public static ShadowCopyRiskReport ScanShadowCopyRisk(string path)
    {
        var report = new ShadowCopyRiskReport();
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
            {
                return report;
            }

            var drive = root.TrimEnd('\\');
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" +
                    $"$d='{drive}'; " +
                    "(Get-CimInstance Win32_ShadowCopy -ErrorAction SilentlyContinue | " +
                    "Where-Object { $_.VolumeName -like ('*' + $d.Replace('\\\\','') + '*') }).Count\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return report;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            if (int.TryParse(output, out var count) && count > 0)
            {
                report.ShadowCopyCount = count;
                report.Volume = drive;
                report.Advisory =
                    $"볼륨 {drive}에 섀도 복사본 {count}개가 감지되었습니다. " +
                    "삭제한 파일의 이전 버전이 시스템 보호/복구 지점에 남아 있을 수 있습니다. " +
                    "Windows 설정 > 시스템 > 저장소 > 고급 저장소 설정 > 시스템 보호에서 관리자 정책을 확인하세요.";
            }
        }
        catch
        {
            // Scan-only; never mutate VSS.
        }

        return report;
    }

    internal sealed class ShadowCopyRiskReport
    {
        public string Volume { get; set; } = "";
        public int ShadowCopyCount { get; set; }
        public string Advisory { get; set; } = "";
        public bool HasRisk => ShadowCopyCount > 0;
    }

    private static void ClearReadOnlyAttributes(string path)
    {
        try
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
        catch
        {
            // Continue with best effort.
        }
    }

    private static void WipeAlternateDataStreams(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var pattern = Path.Combine(directory, fileName + ":*");
            foreach (var streamPath in Directory.EnumerateFiles(directory, fileName + ":*"))
            {
                if (string.Equals(streamPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.SetAttributes(streamPath, FileAttributes.Normal);
                    File.Delete(streamPath);
                }
                catch
                {
                    // Ignore individual ADS failures.
                }
            }
        }
        catch
        {
            // ADS enumeration may fail on non-NTFS volumes.
        }
    }

    private static async Task OverwriteFileAsync(
        string path,
        int passes,
        string storageType,
        CancellationToken cancellationToken)
    {
        var len = new FileInfo(path).Length;
        if (len <= 0)
        {
            return;
        }

        var useWriteThrough = storageType is "HDD" or "USB-NTFS";
        var options = FileOptions.SequentialScan;
        if (useWriteThrough)
        {
            options |= FileOptions.WriteThrough;
        }

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None, 4096, options);
        var buffer = new byte[Math.Min(65536, Math.Max(1, len))];
        for (var pass = 0; pass < passes; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FillOverwriteBuffer(buffer, pass, passes);
            fs.Position = 0;
            var remaining = len;
            while (remaining > 0)
            {
                var chunk = (int)Math.Min(buffer.Length, remaining);
                await fs.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken);
                remaining -= chunk;
            }

            await fs.FlushAsync(cancellationToken);
            if (useWriteThrough)
            {
                FlushFileBuffers(fs.SafeFileHandle);
            }
        }
    }

    private static void FillOverwriteBuffer(byte[] buffer, int pass, int totalPasses)
    {
        if (totalPasses >= 7)
        {
            switch (pass)
            {
                case 0:
                case 6:
                    Array.Fill(buffer, (byte)0x00);
                    return;
                case 1:
                    Array.Fill(buffer, (byte)0xFF);
                    return;
                default:
                    RandomNumberGenerator.Fill(buffer);
                    return;
            }
        }

        switch (pass)
        {
            case 0:
                Array.Fill(buffer, (byte)0x00);
                break;
            case 1:
                Array.Fill(buffer, (byte)0xFF);
                break;
            default:
                RandomNumberGenerator.Fill(buffer);
                break;
        }
    }

    private static async Task SsdForensicObfuscationAsync(
        string path,
        SecureDeleteSecurityLevel level,
        CancellationToken cancellationToken)
    {
        var originalLen = new FileInfo(path).Length;
        if (originalLen <= 0)
        {
            return;
        }

        var inflationCap = level == SecureDeleteSecurityLevel.Maximum
            ? 64L * 1024 * 1024
            : 16L * 1024 * 1024;
        var multiplier = level == SecureDeleteSecurityLevel.Maximum ? 3.0 : level == SecureDeleteSecurityLevel.Professional ? 2.0 : 1.5;
        var targetLen = Math.Min(
            (long)(originalLen * multiplier),
            originalLen + inflationCap);
        targetLen = Math.Max(targetLen, originalLen);

        var passes = SecureDeleteStorageProfiler.GetSsdObfuscationPasses(level);

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        fs.SetLength(targetLen);

        var buffer = new byte[65536];
        for (var pass = 0; pass < passes; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FillForensicObfuscationBuffer(buffer, pass, originalLen);
            fs.Position = 0;
            var remaining = targetLen;
            long offset = 0;
            while (remaining > 0)
            {
                var chunk = (int)Math.Min(buffer.Length, remaining);
                if (pass % 2 == 1)
                {
                    InjectDecoySignatures(buffer, chunk, offset);
                }

                if (pass % 3 == 2)
                {
                    ApplyXorDiffusion(buffer, chunk, pass);
                }

                await fs.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken);
                remaining -= chunk;
                offset += chunk;
            }

            await fs.FlushAsync(cancellationToken);
        }

        fs.SetLength(0);
        await fs.FlushAsync(cancellationToken);
    }

    private static void FillForensicObfuscationBuffer(byte[] buffer, int pass, long originalLen)
    {
        switch (pass % 5)
        {
            case 0:
                RandomNumberGenerator.Fill(buffer);
                break;
            case 1:
                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = (byte)((i * 131 + pass * 17 + (int)(originalLen % 251)) & 0xFF);
                }

                break;
            case 2:
                Array.Fill(buffer, (byte)0xA5);
                break;
            case 3:
                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = (byte)((pass ^ i ^ 0x5A) & 0xFF);
                }

                break;
            default:
                for (var i = 0; i < buffer.Length; i += 2)
                {
                    buffer[i] = 0x00;
                    if (i + 1 < buffer.Length)
                    {
                        buffer[i + 1] = 0xFF;
                    }
                }

                break;
        }
    }

    private static void InjectDecoySignatures(byte[] buffer, int length, long offset)
    {
        byte[][] decoys =
        [
            [0xFF, 0xD8, 0xFF, 0xE0],
            [0x89, 0x50, 0x4E, 0x47],
            [0x50, 0x4B, 0x03, 0x04],
            [0x25, 0x50, 0x44, 0x46],
            [0x7F, 0x45, 0x4C, 0x46],
            [0x52, 0x61, 0x72, 0x21]
        ];

        var slots = Math.Min(6, Math.Max(1, length / 8192));
        for (var slot = 0; slot < slots; slot++)
        {
            var decoy = decoys[(int)((offset / 8192 + slot) % decoys.Length)];
            var insertAt = (int)((offset + slot * 4099 + 37) % Math.Max(1, length - decoy.Length));
            Buffer.BlockCopy(decoy, 0, buffer, insertAt, decoy.Length);
        }
    }

    private static void ApplyXorDiffusion(byte[] buffer, int length, int pass)
    {
        var key = (byte)(0x3C ^ (pass * 29));
        for (var i = 0; i < length; i++)
        {
            buffer[i] ^= (byte)(key ^ (i & 0xFF) ^ ((pass * 7) & 0xFF));
        }
    }

    private static async Task SsdCryptoEraseAsync(
        string path,
        SecureDeleteSecurityLevel level,
        CancellationToken cancellationToken)
    {
        var len = new FileInfo(path).Length;
        if (len <= 0)
        {
            return;
        }

        var passes = SecureDeleteStorageProfiler.GetSsdCryptoErasePasses(level);
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        var buffer = new byte[Math.Min(65536, Math.Max(1, len))];
        for (var pass = 0; pass < passes; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RandomNumberGenerator.Fill(buffer);
            fs.Position = 0;
            var remaining = len;
            while (remaining > 0)
            {
                var chunk = (int)Math.Min(buffer.Length, remaining);
                await fs.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken);
                remaining -= chunk;
            }

            await fs.FlushAsync(cancellationToken);
        }
    }

    private static void TryTrimOrZeroData(string path)
    {
        try
        {
            using var handle = CreateFile(
                path,
                unchecked((int)0x80000000) | 0x40000000,
                FileShare.ReadWrite | FileShare.Delete,
                IntPtr.Zero,
                3,
                0,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                return;
            }

            var fileSize = new FileInfo(path).Length;
            if (fileSize > 0)
            {
                var zero = new FILE_ZERO_DATA_INFORMATION
                {
                    FileOffset = 0,
                    BeyondFinalZero = fileSize
                };
                _ = DeviceIoControl(
                    handle,
                    FsctlSetZeroData,
                    ref zero,
                    Marshal.SizeOf<FILE_ZERO_DATA_INFORMATION>(),
                    IntPtr.Zero,
                    0,
                    out _,
                    IntPtr.Zero);
            }

            var range = new FILE_ALLOCATED_RANGE_BUFFER
            {
                FileOffset = 0,
                Length = Math.Max(1, fileSize)
            };
            _ = DeviceIoControl(
                handle,
                FsctlFileLevelTrim,
                ref range,
                Marshal.SizeOf<FILE_ALLOCATED_RANGE_BUFFER>(),
                IntPtr.Zero,
                0,
                out _,
                IntPtr.Zero);
        }
        catch
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                fs.SetLength(0);
            }
            catch
            {
                // Best-effort deallocation.
            }
        }
    }

    public static void TryVolumeRetrim(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var drive = root.TrimEnd('\\');
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" +
                    $"Optimize-Volume -DriveLetter '{drive.TrimEnd(':')}' -ReTrim -ErrorAction SilentlyContinue\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(12000);
        }
        catch
        {
            // Best-effort volume retrim; deletion chain already applied.
        }
    }

    private static async Task RandomizeNameAndDeleteAsync(string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            File.Delete(path);
            return;
        }

        var randomName = Path.Combine(directory, Guid.NewGuid().ToString("N"));
        if (File.Exists(path))
        {
            File.Move(path, randomName);
            await using (var fs = new FileStream(randomName, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                fs.SetLength(0);
                await fs.FlushAsync(cancellationToken);
            }

            File.Delete(randomName);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        int dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        int dwCreationDisposition,
        int dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref FILE_ZERO_DATA_INFORMATION lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref FILE_ALLOCATED_RANGE_BUFFER lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushFileBuffers(SafeFileHandle hFile);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_ZERO_DATA_INFORMATION
    {
        public long FileOffset;
        public long BeyondFinalZero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_ALLOCATED_RANGE_BUFFER
    {
        public long FileOffset;
        public long Length;
    }
}