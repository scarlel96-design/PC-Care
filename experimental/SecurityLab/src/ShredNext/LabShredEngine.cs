using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.Hardening;
using SmartPerformanceDoctor.SecurityLab.Policy;

namespace SmartPerformanceDoctor.SecurityLab.ShredNext;

public sealed class LabShredTarget
{
    public required string Path { get; init; }
    public long Size { get; init; }
    public bool Allowed { get; init; }
    public string? BlockReason { get; init; }
    public string Protocol { get; init; } = "";
}

public sealed class LabShredDryRunReport
{
    public required IReadOnlyList<LabShredTarget> Targets { get; init; }
    public int AllowedCount { get; init; }
    public int BlockedCount { get; init; }
    public string Disclaimer { get; init; } = "";
    public string ConfirmationPhrase { get; init; } = LabShredPolicy.IrreversiblePhrase;
    public bool DryRunCompleted => true;
}

public sealed class LabShredExecuteResult
{
    public int Deleted { get; init; }
    public int Failed { get; init; }
    public string Report { get; init; } = "";
}

/// <summary>
/// Lab secure-delete engine: dry-run → phrase → best-effort overwrite + truncate.
/// Does not claim absolute unrecoverability (SSD limits documented).
/// </summary>
public static class LabShredEngine
{
    public static LabShredDryRunReport DryRun(IEnumerable<string> paths)
    {
        var targets = new List<LabShredTarget>();
        foreach (var p in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string full;
            try
            {
                full = System.IO.Path.GetFullPath(p);
            }
            catch
            {
                targets.Add(new LabShredTarget
                {
                    Path = p,
                    Allowed = false,
                    BlockReason = "경로를 해석할 수 없습니다."
                });
                continue;
            }

            var (ok, reason) = LabSecurePath.Evaluate(full);
            if (!ok)
            {
                targets.Add(new LabShredTarget
                {
                    Path = full,
                    Allowed = false,
                    BlockReason = reason,
                    Protocol = "blocked"
                });
                continue;
            }

            if (!File.Exists(full))
            {
                targets.Add(new LabShredTarget
                {
                    Path = full,
                    Allowed = false,
                    BlockReason = "파일 없음",
                    Protocol = "n/a"
                });
                continue;
            }

            var size = new FileInfo(full).Length;
            var protocol = LabSecurePath.IsCloudSyncPath(full)
                ? "lab.random_overwrite_2x · cloud-sync-warning"
                : "lab.random_overwrite_2x.truncate.rename_delete.v1";
            targets.Add(new LabShredTarget
            {
                Path = full,
                Size = size,
                Allowed = true,
                Protocol = protocol
            });
        }

        var allowed = targets.Count(t => t.Allowed);
        return new LabShredDryRunReport
        {
            Targets = targets,
            AllowedCount = allowed,
            BlockedCount = targets.Count - allowed,
            Disclaimer =
                "Lab 보안 삭제: 덮어쓰기·truncate·이름 난수화. SSD/NVMe 물리 완전 삭제를 보장하지 않습니다. " +
                "금고 데이터는 crypto-shred(키 파기)를 우선하세요. 클라우드 동기화 경로의 원격 사본은 제거되지 않습니다."
        };
    }

    public static LabShredExecuteResult Execute(LabShredDryRunReport report, string confirmationPhrase, bool userConfirmed)
    {
        var decision = LabPolicyEngine.Evaluate(new LabPolicyRequest
        {
            Kind = LabActionKind.SecureDeleteExecute,
            UserConfirmed = userConfirmed,
            DryRunCompleted = report.DryRunCompleted,
            ConfirmPhrase = confirmationPhrase,
            TargetCount = report.AllowedCount
        });
        if (!decision.Allowed)
        {
            throw new InvalidOperationException(decision.Reason);
        }

        var deleted = 0;
        var failed = 0;
        foreach (var t in report.Targets.Where(x => x.Allowed))
        {
            try
            {
                SecureDeleteFile(t.Path);
                deleted++;
            }
            catch
            {
                failed++;
            }
        }

        return new LabShredExecuteResult
        {
            Deleted = deleted,
            Failed = failed,
            Report = JsonSerializer.Serialize(new
            {
                deleted,
                failed,
                disclaimer = report.Disclaimer
            })
        };
    }

    /// <summary>Vault-internal helper: damage object then delete.</summary>
    public static void CryptoShredFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        SecureDeleteFile(path);
    }

    private static void SecureDeleteFile(string path)
    {
        if (LabShredPolicy.IsSystemPathBlocked(path))
        {
            throw new InvalidOperationException("blocked path");
        }

        var info = new FileInfo(path);
        var len = info.Length;
        if (len > 0)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
            var buf = new byte[Math.Min(65536, (int)Math.Min(int.MaxValue, len))];
            for (var pass = 0; pass < 2; pass++)
            {
                fs.Position = 0;
                long rem = len;
                while (rem > 0)
                {
                    var n = (int)Math.Min(buf.Length, rem);
                    RandomNumberGenerator.Fill(buf.AsSpan(0, n));
                    fs.Write(buf, 0, n);
                    rem -= n;
                }

                fs.Flush(true);
            }

            fs.SetLength(0);
            fs.Flush(true);
            CryptographicOperations.ZeroMemory(buf);
        }

        var dir = Path.GetDirectoryName(path) ?? ".";
        var tmp = Path.Combine(dir, ".lab-shred-" + Guid.NewGuid().ToString("N"));
        File.Move(path, tmp, overwrite: true);
        File.Delete(tmp);
    }
}
