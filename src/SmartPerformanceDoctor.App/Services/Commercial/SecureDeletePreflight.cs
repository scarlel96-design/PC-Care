using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SmartPerformanceDoctor.App.Models.Commercial;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>Non-destructive gate. A folder is blocked when any contained object cannot be safely handled.</summary>
internal static class SecureDeletePreflight
{
    public static SecureDeletePreflightReport Inspect(string path, string storageType, string fileSystem, SecureDeleteSecurityLevel level, bool pathAllowed, string pathReason)
    {
        var checks = new List<SecureDeletePreflightCheck>();
        Add(checks, "P01", pathAllowed ? "pass" : "fail", pathAllowed ? "보호 경로 정책 통과" : pathReason);
        var items = EnumerateTarget(path, out var traversalError);
        var rootAttributes = TryGetAttributes(path);
        var reparse = rootAttributes?.HasFlag(FileAttributes.ReparsePoint) == true || items.Any(x => x.Attributes.HasFlag(FileAttributes.ReparsePoint));
        Add(checks, "P06", reparse ? "fail" : "pass", reparse ? "대상 또는 하위 항목에 재분석 지점·심볼릭 링크가 있어 실행을 차단했습니다." : "재분석 지점 없음");
        Add(checks, "P12", traversalError ? "fail" : "pass", traversalError ? "폴더의 모든 하위 항목을 읽을 수 없어 부분 삭제를 방지하기 위해 차단했습니다." : $"대상 스냅샷: 파일 {items.Count}개");

        var locked = items.Where(x => !CanOpenExclusively(x.Path)).Take(3).ToArray();
        Add(checks, "P02", locked.Length > 0 ? "fail" : "pass", locked.Length > 0 ? $"배타적으로 열 수 없는 파일: {string.Join(", ", locked.Select(x => Path.GetFileName(x.Path)))}" : "모든 파일 배타적 열기 가능");
        var readOnly = items.Count(x => x.Attributes.HasFlag(FileAttributes.ReadOnly));
        Add(checks, "P03", readOnly > 0 ? "fail" : "pass", readOnly > 0 ? $"읽기 전용 파일 {readOnly}개는 별도 검토가 필요합니다." : "읽기 전용 파일 없음");

        var hardLinks = items.Where(x => GetHardLinkCount(x.Path) > 1).Take(3).ToArray();
        Add(checks, "P05", hardLinks.Length > 0 ? "fail" : "pass", hardLinks.Length > 0 ? $"하드링크가 있는 파일은 다른 경로의 데이터에 영향을 줄 수 있어 차단했습니다: {string.Join(", ", hardLinks.Select(x => Path.GetFileName(x.Path)))}" : "하드링크 미감지");

        var ads = fileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase) ? items.Sum(x => CountAlternateDataStreams(x.Path)) : 0;
        Add(checks, "P04", fileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase) ? "pass" : "warn", fileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase) ? $"ADS 실제 열거 완료: {ads}개 스트림" : "NTFS가 아니므로 ADS 열거를 지원하지 않을 수 있습니다.");
        Add(checks, "P07", storageType == "Unknown" ? "warn" : "pass", storageType == "Unknown" ? "저장장치 유형 미확인: 보수적 파일 Clear만 적용" : $"저장장치 유형: {storageType}");
        Add(checks, "P08", fileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase) ? "pass" : "warn", $"파일 시스템: {fileSystem}");

        var shadow = ForensicSecureDeleteEngine.ScanShadowCopyRisk(path);
        Add(checks, "P09", "warn", shadow.HasRisk ? "섀도 복사본이 감지되었습니다. 이 작업은 VSS·백업·클라우드 사본을 삭제하지 않습니다." : "VSS 부재는 검증되지 않았습니다. 백업·클라우드 사본은 처리하지 않습니다.");
        var special = items.Count(x => x.Attributes.HasFlag(FileAttributes.Compressed) || x.Attributes.HasFlag(FileAttributes.Encrypted) || x.Attributes.HasFlag(FileAttributes.SparseFile) || x.Attributes.HasFlag(FileAttributes.Offline));
        Add(checks, "P10", special > 0 ? "fail" : "pass", special > 0 ? $"압축·EFS·희소·오프라인 파일 {special}개는 파일 단위 Clear의 범위를 벗어나므로 실행을 차단했습니다." : "특수 할당 속성 미감지");
        var freeBytes = TryGetAvailableFreeSpace(path);
        Add(checks, "P11", freeBytes >= 64L * 1024 * 1024 ? "pass" : "warn", freeBytes >= 64L * 1024 * 1024 ? "동일 볼륨 작업 공간 확인" : "여유 공간이 낮아 정리·검증 범위가 제한될 수 있습니다.");
        var elevated = ProcessElevationService.IsAdministrator();
        Add(checks, "E_ELEVATION_REQUIRED", level == SecureDeleteSecurityLevel.Maximum && !elevated ? "fail" : "pass", level == SecureDeleteSecurityLevel.Maximum && !elevated ? "강화 파일 Clear는 관리자 권한으로 다시 실행해야 합니다." : elevated ? "관리자 권한 확인" : "전문 파일 Clear는 표준 권한으로 실행 가능");

        return new SecureDeletePreflightReport { Checks = checks, FileSystem = fileSystem, StorageType = storageType, IsAdministrator = elevated, BitLockerProtected = TryDetectBitLocker(path), HasShadowCopies = shadow.HasRisk };
    }

    private sealed record TargetFile(string Path, FileAttributes Attributes);
    private static List<TargetFile> EnumerateTarget(string path, out bool error)
    {
        error = false; var result = new List<TargetFile>();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var visited = 0;
        try
        {
            if (File.Exists(path)) { result.Add(new(path, File.GetAttributes(path))); return result; }
            var pending = new Stack<string>(); pending.Push(path);
            while (pending.Count > 0)
            {
                if (++visited > 512 || timer.ElapsedMilliseconds > 5000) { error = true; break; }
                var current = pending.Pop(); var attrs = File.GetAttributes(current);
                if (attrs.HasFlag(FileAttributes.ReparsePoint)) { error = true; continue; }
                foreach (var file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly))
                {
                    if (result.Count >= 512 || timer.ElapsedMilliseconds > 5000) { error = true; return result; }
                    result.Add(new(file, File.GetAttributes(file)));
                }
                foreach (var dir in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                {
                    if (pending.Count + visited >= 512) { error = true; return result; }
                    pending.Push(dir);
                }
            }
        }
        catch { error = true; }
        return result;
    }

    private static void Add(List<SecureDeletePreflightCheck> checks, string code, string status, string detail) => checks.Add(new SecureDeletePreflightCheck { Code = code, Status = status, Detail = detail });
    private static FileAttributes? TryGetAttributes(string path) { try { return File.GetAttributes(path); } catch { return null; } }
    private static bool CanOpenExclusively(string path) { try { using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return true; } catch { return false; } }
    private static long TryGetAvailableFreeSpace(string path) { try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace; } catch { return 0; } }

    private static int GetHardLinkCount(string path)
    {
        try { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); return GetFileInformationByHandle(stream.SafeFileHandle, out var info) ? (int)info.NumberOfLinks : int.MaxValue; } catch { return int.MaxValue; }
    }
    private static int CountAlternateDataStreams(string path)
    {
        var count=0; var data=new WIN32_FIND_STREAM_DATA(); var handle=FindFirstStreamW(path,0,ref data,0); if(handle==IntPtr.Zero || handle==new IntPtr(-1)) return 0;
        try { do { if (!string.Equals(data.Name, "::$DATA", StringComparison.OrdinalIgnoreCase)) count++; } while(FindNextStreamW(handle,ref data)); } finally { FindClose(handle); } return count;
    }
    private static bool TryDetectBitLocker(string path)
    {
        try { var root=Path.GetPathRoot(Path.GetFullPath(path)); if(string.IsNullOrWhiteSpace(root)) return false; using var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName="manage-bde.exe", Arguments=$"-status {root.TrimEnd('\\')}", UseShellExecute=false, RedirectStandardOutput=true, CreateNoWindow=true }); if(p is null) return false; var output=p.StandardOutput.ReadToEnd(); p.WaitForExit(4000); return output.Contains("Protection On",StringComparison.OrdinalIgnoreCase)||output.Contains("보호 상태:    켜짐",StringComparison.OrdinalIgnoreCase); } catch { return false; }
    }

    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION info);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern IntPtr FindFirstStreamW(string fileName, int level, ref WIN32_FIND_STREAM_DATA data, int flags);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool FindNextStreamW(IntPtr handle, ref WIN32_FIND_STREAM_DATA data);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool FindClose(IntPtr handle);
    [StructLayout(LayoutKind.Sequential)] private struct BY_HANDLE_FILE_INFORMATION { public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] private struct WIN32_FIND_STREAM_DATA { public long Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=296)] public string Name; }
}
