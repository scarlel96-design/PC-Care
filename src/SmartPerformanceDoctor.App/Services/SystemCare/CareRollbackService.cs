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
    public static CareRollbackResult Rollback(string auditFolder)
    {
        var quarantine = Path.Combine(auditFolder, "quarantine");
        if (!Directory.Exists(quarantine))
        {
            return new CareRollbackResult
            {
                Success = false,
                Message = "격리 폴더가 없습니다. 롤백할 항목이 없습니다."
            };
        }

        var restored = 0;
        var failed = 0;
        var mapPath = Path.Combine(auditFolder, "quarantine_map.json");
        var map = LoadMap(mapPath);

        foreach (var file in Directory.EnumerateFiles(quarantine, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var original = map.GetValueOrDefault(Path.GetFileName(file));
                if (string.IsNullOrWhiteSpace(original))
                {
                    failed++;
                    continue;
                }

                var targetDir = Path.GetDirectoryName(original);
                if (!string.IsNullOrWhiteSpace(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                if (File.Exists(original))
                {
                    File.Delete(original);
                }

                File.Move(file, original, overwrite: true);
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
            Success = restored > 0,
            RestoredCount = restored,
            FailedCount = failed,
            Message = restored > 0
                ? $"격리 항목 {restored}개를 원래 위치로 복원했습니다."
                : "복원할 수 있는 항목이 없습니다."
        };
    }

    public static void RecordQuarantine(string auditFolder, string sourcePath, string quarantinePath)
    {
        var mapPath = Path.Combine(auditFolder, "quarantine_map.json");
        var map = LoadMap(mapPath);
        map[Path.GetFileName(quarantinePath)] = sourcePath;
        File.WriteAllText(mapPath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Dictionary<string, string> LoadMap(string mapPath)
    {
        if (!File.Exists(mapPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(mapPath))
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}