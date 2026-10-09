using System.Security.Cryptography;
using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

public sealed class CareRollbackResult
{
    public bool Success { get; init; }
    public int RestoredCount { get; init; }
    public int FailedCount { get; init; }
    public string Message { get; init; } = "";
}

public static class CareRollbackService
{
    private sealed record QuarantineRecord(
        string Id, string OriginalPath, string QuarantinePath,
        long Length, string Sha256, string State);

    public static void RecordQuarantine(string auditFolder, string sourcePath, string quarantinePath)
    {
        var source = Path.GetFullPath(sourcePath);
        var dest = Path.GetFullPath(quarantinePath);
        var root = Path.GetFullPath(Path.Combine(auditFolder, "quarantine"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("격리 대상 경로가 범위를 벗어났습니다.");

        // Existing callers may register an already moved legacy item.
        var current = File.Exists(source) ? source : dest;
        if (!File.Exists(current))
            throw new FileNotFoundException("격리 파일을 찾을 수 없습니다.");
        var state = current == source ? "Prepared" : "Quarantined";
        var record = new QuarantineRecord(Guid.NewGuid().ToString("N"), source, dest,
            new FileInfo(current).Length, Hash(current), state);
        var path = JournalPath(auditFolder, Path.GetFileName(dest));
        if (File.Exists(path)) throw new IOException("격리 기록이 이미 존재합니다.");
        WriteAtomic(path, record, replace: false);
    }

    public static void MarkQuarantined(string auditFolder, string quarantinePath)
    {
        var path = JournalPath(auditFolder, Path.GetFileName(quarantinePath));
        var record = ReadRecord(path) ?? throw new InvalidDataException("격리 기록이 없습니다.");
        if (!string.Equals(Path.GetFullPath(quarantinePath), record.QuarantinePath,
                StringComparison.OrdinalIgnoreCase) || !Matches(record.QuarantinePath, record))
            throw new InvalidDataException("격리 파일이 원본 기록과 일치하지 않습니다.");
        WriteAtomic(path, record with { State = "Quarantined" }, replace: true);
    }

    public static CareRollbackResult Rollback(string auditFolder)
    {
        var quarantine = Path.Combine(auditFolder, "quarantine");
        if (!Directory.Exists(quarantine))
            return new CareRollbackResult { Message = "격리 폴더가 없습니다. 롤백할 항목이 없습니다." };

        var restored = 0;
        var failed = 0;
        var map = LoadLegacyMap(Path.Combine(auditFolder, "quarantine_map.json"));
        foreach (var file in Directory.EnumerateFiles(quarantine, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var journal = JournalPath(auditFolder, Path.GetFileName(file));
                var record = File.Exists(journal) ? ReadRecord(journal) : null;
                if (File.Exists(journal) && record is null)
                    throw new InvalidDataException("격리 기록이 손상되었습니다.");
                var original = record?.OriginalPath ?? map.GetValueOrDefault(Path.GetFileName(file));
                if (string.IsNullOrWhiteSpace(original)
                    || (record is not null &&
                        (!string.Equals(Path.GetFullPath(file), record.QuarantinePath,
                            StringComparison.OrdinalIgnoreCase) || !Matches(file, record))))
                    throw new InvalidDataException("격리 파일과 복구 기록이 일치하지 않습니다.");
                if (File.Exists(original) || Directory.Exists(original))
                    throw new IOException("원본 경로에 새 파일이 있습니다.");
                var dir = Path.GetDirectoryName(original);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                File.Move(file, original, overwrite: false);
                if (record is not null)
                    WriteAtomic(journal, record with { State = "Restored" }, replace: true);
                restored++;
            }
            catch
            {
                failed++;
            }
        }
        CareAuditChain.Append(auditFolder, "rollback", $"복원 {restored} · 실패 {failed}");
        return new CareRollbackResult
        {
            Success = restored > 0 && failed == 0,
            RestoredCount = restored,
            FailedCount = failed,
            Message = restored > 0 ? $"격리 항목 {restored}개 복원 · 미복원 {failed}개." : "복원할 수 있는 항목이 없습니다."
        };
    }

    private static bool Matches(string path, QuarantineRecord record) =>
        File.Exists(path) && new FileInfo(path).Length == record.Length
        && string.Equals(Hash(path), record.Sha256, StringComparison.OrdinalIgnoreCase);

    private static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string JournalPath(string auditFolder, string name) =>
        Path.Combine(auditFolder, "quarantine_journal", name + ".json");

    private static QuarantineRecord? ReadRecord(string path)
    {
        try { return JsonSerializer.Deserialize<QuarantineRecord>(File.ReadAllText(path)); }
        catch { return null; }
    }

    private static void WriteAtomic(string path, QuarantineRecord record, bool replace)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, record);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: replace);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static Dictionary<string, string> LoadLegacyMap(string path)
    {
        if (!File.Exists(path)) return new(StringComparer.OrdinalIgnoreCase);
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new(StringComparer.OrdinalIgnoreCase); }
        catch { return new(StringComparer.OrdinalIgnoreCase); }
    }
}
