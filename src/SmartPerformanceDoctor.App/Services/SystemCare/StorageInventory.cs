using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json.Serialization;
using SmartPerformanceDoctor.UpdateEngine;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

public enum CleanupDisposition { SafeCandidate, RequiresReview, InUse, Protected, UnknownOwnership, Unsupported, ScanIncomplete }
public sealed record ExactTargetIdentity(string RootId, string FileId, long Length, DateTime LastWriteUtc);
public sealed record CleanupCandidate(string CandidateId, CleanupDisposition Disposition, long? LogicalBytes,
    DateTime? LastWriteUtc, string Reason, ExactTargetIdentity? Identity,
    [property: JsonIgnore] string Root, [property: JsonIgnore] string RelativePath)
{
    [JsonIgnore] public string Display => $"{RelativePath} · {DispositionLabel} · {(LogicalBytes is long n ? $"{n:N0} B" : "크기 미확인")} · {Reason}";
    [JsonIgnore] public string DispositionLabel => Disposition switch
    {
        CleanupDisposition.SafeCandidate => "임시 파일 후보",
        CleanupDisposition.RequiresReview => "사용 목적 확인 필요",
        CleanupDisposition.InUse => "사용 중",
        CleanupDisposition.Protected => "보호 대상",
        CleanupDisposition.UnknownOwnership => "소유권 미확인",
        CleanupDisposition.ScanIncomplete => "검사 미완료",
        _ => "지원하지 않는 항목"
    };
    public long? AllocatedBytes { get; init; }
    public bool CanExecute => false;
}
public sealed record DriveSpace(string Drive, long? CapacityBytes, long? AvailableBytes, long? TotalFreeBytes, string Status);
public sealed record StorageInventoryResult(IReadOnlyList<DriveSpace> Drives, IReadOnlyList<CleanupCandidate> Candidates,
    long MeasuredLogicalBytes, int ExcludedCount, bool Complete, string Limitation, double ElapsedMs,
    double CollectorCpuMs, long WorkingSetBytes)
{
    public long? MeasuredAllocatedBytes => Candidates.Where(c => c.LogicalBytes.HasValue).All(c => c.AllocatedBytes.HasValue)
        ? Candidates.Sum(c => c.AllocatedBytes ?? 0) : null;
    public IReadOnlyList<string> MissingReasons { get; init; } = [];
}
public sealed record CleanupPreview(int SelectedCount, long CandidateBytes, long QuarantinedBytes,
    long? ActualFreedBytes, long RestorableBytes, long FinalDeletedBytes, bool ExecutionEnabled,
    int SystemMutations, IReadOnlyList<string> Checks, string Status);

/// <summary>Read bounded metadata through pinned handles. Never infer unmeasured file sizes.</summary>
public static class StorageInventory
{
    public const int FileBudget = 512;
    public static IReadOnlyList<DriveSpace> CaptureDrives() => DriveInfo.GetDrives().Select(d =>
    {
        try { return d.DriveType == DriveType.Fixed && d.IsReady
            ? new DriveSpace(d.Name, d.TotalSize, d.AvailableFreeSpace, d.TotalFreeSpace, "Measured")
            : new DriveSpace(d.Name, null, null, null, "Unsupported"); }
        catch (IOException) { return new DriveSpace(d.Name, null, null, null, "Unavailable"); }
        catch (UnauthorizedAccessException) { return new DriveSpace(d.Name, null, null, null, "Unavailable"); }
    }).ToArray();

    public static StorageInventoryResult CaptureUserTemp(CancellationToken token = default) =>
        Capture(Path.GetTempPath(), new StorageScanPolicy(), token);

    // Test callers use generated roots. This method grants no execution capability.
    public static StorageInventoryResult CaptureIsolated(string root, int budget = FileBudget, CancellationToken token = default, StorageScanPolicy? policy = null)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        if (!name.StartsWith("pccare-wo10-", StringComparison.Ordinal)
            || !Guid.TryParseExact(name[12..], "N", out _))
            throw new InvalidDataException("SyntheticInventoryRootRequired");
        return Capture(root, policy ?? new StorageScanPolicy(0, Math.Clamp(budget, 1, FileBudget)), token);
    }

    private static StorageInventoryResult Capture(string root, StorageScanPolicy policy, CancellationToken token)
    {
        policy.Validate();
        var missing = new List<string>();
        var timer = Stopwatch.StartNew();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var rows = new List<CleanupCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var complete = true;
        var limitation = "파일 논리 크기입니다. 할당 크기와 실제 확보 가능 공간은 미확인입니다. 검사 깊이와 항목 수를 제한합니다.";
        var sid = WindowsIdentity.GetCurrent().User?.Value;
        try
        {
            using var directory = WindowsFileBoundary.PinDirectory(root);
            var pending = new Stack<(string Path, int Depth)>();
            pending.Push((directory.Root, 0));
            while (pending.Count > 0)
            {
            var current = pending.Pop();
            using var currentPin = WindowsFileBoundary.PinDirectory(current.Path);
            foreach (var path in Directory.EnumerateFileSystemEntries(currentPin.Root))
            {
                token.ThrowIfCancellationRequested();
                if (rows.Count >= policy.MaximumEntries || timer.ElapsedMilliseconds >= policy.MaximumMilliseconds) { complete = false; missing.Add("EntryOrTimeBudget"); pending.Clear(); break; }
                var relative = Path.GetRelativePath(directory.Root, path);
                var id = Guid.NewGuid().ToString("N");
                if (Directory.Exists(path))
                {
                    var attrs = File.GetAttributes(path);
                    var protectedDirectory = StorageTargetPolicy.IsProtected(path)
                        || (attrs & (FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden)) != 0;
                    if (!protectedDirectory && current.Depth < policy.MaximumDepth)
                    {
                        pending.Push((path, current.Depth + 1));
                        rows.Add(new(id, CleanupDisposition.RequiresReview, null, null, "제한된 하위 검사", null, root, relative));
                    }
                    else
                    {
                        rows.Add(new(id, CleanupDisposition.Protected, null, null, "하위 폴더 제외 · 보호 또는 깊이 제한", null, root, relative));
                        if (!protectedDirectory) { complete = false; missing.Add("DepthBudget"); }
                    }
                    continue;
                }
                try
                {
                    using var file = WindowsFileBoundary.OpenRead(directory.Root, relative);
                    var length = file.Stream.Length;
                    var modified = File.GetLastWriteTimeUtc(file.Stream.SafeFileHandle);
                    var owner = file.ReadSecurity().GetOwner(typeof(SecurityIdentifier))?.Value;
                    var attributes = File.GetAttributes(file.Stream.SafeFileHandle);
                    file.Validate(); directory.Validate();
                    if (length != file.Stream.Length || modified != File.GetLastWriteTimeUtc(file.Stream.SafeFileHandle))
                        throw new IOException("ScanChanged");
                    if (!seen.Add(file.Identity)) continue;
                    var disposition = Classify(path, owner == sid && sid is not null, attributes, modified, DateTime.UtcNow);
                    rows.Add(new(id, disposition, length, modified, Reason(disposition),
                        new(directory.Identity, file.Identity, length, modified), directory.Root, relative)
                    { AllocatedBytes = StorageTargetPolicy.AllocatedBytes(file.Stream.SafeFileHandle) });
                }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
                {
                    var native = e as Win32Exception ?? e.InnerException as Win32Exception;
                    var disposition = native?.NativeErrorCode is 32 or 33 ? CleanupDisposition.InUse
                        : e is UnauthorizedAccessException || native?.NativeErrorCode == 5 ? CleanupDisposition.UnknownOwnership
                        : CleanupDisposition.Unsupported;
                    rows.Add(new(id, disposition, null, null, Reason(disposition), null, root, relative));
                    complete = false; missing.Add(disposition.ToString());
                }
            }
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
        { complete = false; limitation += " 검사 루트 접근 제한."; }
        if (!complete) limitation += " 검사 누락이 있습니다. 누락 용량은 추정하지 않습니다.";
        process.Refresh();
        return new(CaptureDrives(), rows, rows.Sum(r => r.LogicalBytes ?? 0),
            rows.Count(r => r.Disposition != CleanupDisposition.SafeCandidate), complete, limitation,
            timer.Elapsed.TotalMilliseconds, (process.TotalProcessorTime - cpu).TotalMilliseconds, process.WorkingSet64) { MissingReasons = missing.Distinct().ToArray() };
    }

    public static CleanupDisposition Classify(string name, bool owned, FileAttributes attributes, DateTime modified, DateTime now)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (StorageTargetPolicy.IsProtected(name)) return CleanupDisposition.Protected;
        if (name.StartsWith("vault.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("recovery_slots.", StringComparison.OrdinalIgnoreCase)
            || name.Equals("state.dpapi", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("state.dpapi.", StringComparison.OrdinalIgnoreCase)
            || extension is ".avpack" or ".dpapi") return CleanupDisposition.Protected;
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.System | FileAttributes.Hidden)) != 0
            || extension is ".av3" or ".av4" or ".av5" or ".vault" or ".exe" or ".dll" or ".bak") return CleanupDisposition.Protected;
        if (!owned) return CleanupDisposition.UnknownOwnership;
        if ((attributes & (FileAttributes.ReadOnly | FileAttributes.SparseFile | FileAttributes.Compressed | FileAttributes.Encrypted | FileAttributes.Offline)) != 0
            || modified > now || now - modified < TimeSpan.FromDays(7)) return CleanupDisposition.RequiresReview;
        return extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase) || extension.Equals(".cache", StringComparison.OrdinalIgnoreCase)
            ? CleanupDisposition.SafeCandidate : CleanupDisposition.RequiresReview;
    }

    public static CleanupPreview DryRun(IReadOnlyList<CleanupCandidate> selected, CancellationToken token = default)
    {
        var checks = new List<string>(); long bytes = 0;
        if (selected.Count > FileBudget || selected.Select(c => c.CandidateId).Distinct().Count() != selected.Count
            || selected.Where(c => c.Identity != null).Select(c => c.Identity!.FileId).Distinct().Count() != selected.Count(c => c.Identity != null))
            return new(selected.Count, 0, 0, null, 0, 0, false, 0, ["중복 선택 또는 예산 초과"], "Blocked");
        foreach (var candidate in selected)
        {
            token.ThrowIfCancellationRequested();
            if (candidate.Disposition != CleanupDisposition.SafeCandidate || candidate.Identity is not { } expected)
            { checks.Add("선택 항목: 검토 또는 보호 대상"); continue; }
            try
            {
                using var directory = WindowsFileBoundary.PinDirectory(candidate.Root);
                using var file = WindowsFileBoundary.OpenRead(candidate.Root, candidate.RelativePath);
                file.Validate(expected.FileId);
                if (directory.Identity != expected.RootId || file.Stream.Length != expected.Length
                    || File.GetLastWriteTimeUtc(file.Stream.SafeFileHandle) != expected.LastWriteUtc)
                    throw new InvalidDataException("TargetChanged");
                if (file.ReadSecurity().GetOwner(typeof(SecurityIdentifier))?.Value != WindowsIdentity.GetCurrent().User?.Value)
                    throw new InvalidDataException("OwnerChanged");
                if (Classify(Path.Combine(candidate.Root, candidate.RelativePath), true, File.GetAttributes(file.Stream.SafeFileHandle),
                    expected.LastWriteUtc, DateTime.UtcNow) != CleanupDisposition.SafeCandidate)
                    throw new InvalidDataException("CandidatePolicyChanged");
                bytes += expected.Length; checks.Add("파일 ID·소유자·크기·수정 시각 재검증 완료");
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
            { checks.Add("대상 변경 또는 접근 제한: 재검사 필요"); }
        }
        checks.Add("위험: 파일 격리 / 권한: 일반 사용자 / 동의: 실행 직전 필요");
        checks.Add("생산 정리 실행은 검증 대기 중입니다. 동일 볼륨 격리만으로 확보 공간이 늘지 않습니다.");
        return new(selected.Count, bytes, 0, null, 0, 0, false, 0, checks, "PreviewOnly");
    }

    private static string Reason(CleanupDisposition value) => value switch
    {
        CleanupDisposition.SafeCandidate => "현재 사용자 소유의 7일 이상 된 임시 후보 · 실행 승인 아님",
        CleanupDisposition.Protected => "보호 대상",
        CleanupDisposition.InUse => "사용 중 · 검사 제외",
        CleanupDisposition.UnknownOwnership => "소유권 미확인 · 처리 차단",
        CleanupDisposition.Unsupported => "링크 또는 지원하지 않는 파일 경계 · 처리 차단",
        _ => "사용 목적 확인 필요"
    };
}
