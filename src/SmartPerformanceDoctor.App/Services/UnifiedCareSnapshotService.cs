using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services;

public static class UnifiedCareSnapshotService
{
    public static string CapturePreRepair(UnifiedCareSessionContext ctx, IReadOnlyList<string> moduleIds)
    {
        var snapshot = new
        {
            phase = "pre-repair",
            capturedAt = DateTimeOffset.Now.ToString("o"),
            moduleIds,
            drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => new
                {
                    d.Name,
                    freeGb = d.AvailableFreeSpace / 1024.0 / 1024 / 1024,
                    totalGb = d.TotalSize / 1024.0 / 1024 / 1024
                })
                .ToArray(),
            services = new[]
            {
                new { name = "wuauserv", state = QueryService("wuauserv") },
                new { name = "Audiosrv", state = QueryService("Audiosrv") }
            }
        };

        var path = Path.Combine(ctx.AuditFolder, "snapshot_pre.json");
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    public static string CapturePostRepair(UnifiedCareSessionContext ctx, int? scoreBefore, int? scoreAfter)
    {
        var snapshot = new
        {
            phase = "post-repair",
            capturedAt = DateTimeOffset.Now.ToString("o"),
            scoreBefore,
            scoreAfter,
            healthDelta = scoreBefore.HasValue && scoreAfter.HasValue ? scoreAfter - scoreBefore : null,
            drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => new
                {
                    d.Name,
                    freeGb = d.AvailableFreeSpace / 1024.0 / 1024 / 1024
                })
                .ToArray()
        };

        var path = Path.Combine(ctx.AuditFolder, "snapshot_post.json");
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static string QueryService(string serviceName)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {serviceName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return "unknown";
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) ? "running" : "stopped";
        }
        catch
        {
            return "unknown";
        }
    }
}