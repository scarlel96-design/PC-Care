using System.Security.Cryptography;
using SmartPerformanceDoctor.SecurityLab.Hardening;
using SmartPerformanceDoctor.SecurityLab.Policy;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

namespace SmartPerformanceDoctor.SecurityLab.Migration;

public sealed class V3MigrationExecuteResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public int Imported { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public string LabVaultPath { get; init; } = "";
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ImportedEntryIds { get; init; } = Array.Empty<string>();
    public V3MigrationDryRun.Report? Preflight { get; init; }
}

/// <summary>
/// Executes re-encrypt migration: product v3 → Lab v4.
/// Never deletes the source vault. Does not reference product App assemblies.
/// </summary>
public static class V3ToLabMigrator
{
    public static V3MigrationExecuteResult Execute(
        string productVaultRoot,
        string password,
        string labVaultRoot,
        LabKdfProfile labProfile = LabKdfProfile.LabFast,
        bool createLabIfMissing = true)
    {
        var preflight = V3MigrationDryRun.Analyze(productVaultRoot);
        if (!preflight.LooksLikeProductVault)
        {
            return new V3MigrationExecuteResult
            {
                Success = false,
                Message = "소스 경로가 제품 v3 금고로 보이지 않습니다.",
                Preflight = preflight,
                Errors = preflight.Warnings.ToArray()
            };
        }

        var sourceEqDest = string.Equals(
            Path.GetFullPath(productVaultRoot).TrimEnd('\\'),
            Path.GetFullPath(labVaultRoot).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);
        var policy = LabPolicyEngine.Evaluate(new LabPolicyRequest
        {
            Kind = LabActionKind.MigrateExecute,
            UserConfirmed = true,
            DryRunCompleted = true,
            SourceEqualsDestination = sourceEqDest
        });
        if (!policy.Allowed)
        {
            return new V3MigrationExecuteResult
            {
                Success = false,
                Message = policy.Reason,
                Preflight = preflight
            };
        }

        using var reader = new ProductV3Reader(productVaultRoot);
        try
        {
            reader.Open(password);
        }
        catch (Exception ex)
        {
            return new V3MigrationExecuteResult
            {
                Success = false,
                Message = "v3 잠금 해제 실패: " + ex.Message,
                Preflight = preflight
            };
        }

        IReadOnlyList<ProductV3ExportEntry> exports;
        try
        {
            exports = reader.ExportFileEntries();
        }
        catch (Exception ex)
        {
            return new V3MigrationExecuteResult
            {
                Success = false,
                Message = "v3 export 실패: " + ex.Message,
                Preflight = preflight
            };
        }

        Directory.CreateDirectory(labVaultRoot);
        using var lab = new LabVaultService(labVaultRoot);
        if (!LabVaultService.Exists(labVaultRoot))
        {
            if (!createLabIfMissing)
            {
                return new V3MigrationExecuteResult
                {
                    Success = false,
                    Message = "Lab 금고가 없고 createLabIfMissing=false 입니다.",
                    Preflight = preflight
                };
            }

            var created = lab.Create(password, labProfile);
            if (!created.Success)
            {
                return new V3MigrationExecuteResult
                {
                    Success = false,
                    Message = "Lab 금고 생성 실패: " + created.Message,
                    Preflight = preflight
                };
            }
        }

        var unlock = lab.Unlock(password);
        if (!unlock.Success)
        {
            return new V3MigrationExecuteResult
            {
                Success = false,
                Message = "Lab 금고 잠금 해제 실패: " + unlock.Message,
                Preflight = preflight
            };
        }

        var imported = 0;
        var failed = 0;
        var skipped = 0;
        var errors = new List<string>();
        var ids = new List<string>();

        foreach (var entry in exports)
        {
            try
            {
                if (entry.Content.Length == 0 && entry.EntryKind.Contains("folder", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                var result = lab.ImportBytes(entry.DisplayName, entry.RelativePath, entry.Content);
                if (result.Success && result.EntryId is not null)
                {
                    imported++;
                    ids.Add(result.EntryId);

                    // verify hash via re-export into wiped temp
                    using var tmp = new LabSecureTempDir("lab-mig-verify");
                    var exp = lab.ExportEntry(result.EntryId, tmp.Path);
                    if (!exp.Success)
                    {
                        failed++;
                        errors.Add($"{entry.DisplayName}: lab export verify failed");
                        continue;
                    }

                    var outFile = Directory.GetFiles(tmp.Path).FirstOrDefault();
                    if (outFile is null)
                    {
                        failed++;
                        errors.Add($"{entry.DisplayName}: no export file");
                        continue;
                    }

                    var bytes = File.ReadAllBytes(outFile);
                    try
                    {
                        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                        if (!hash.Equals(entry.ContentSha256, StringComparison.OrdinalIgnoreCase)
                            && !bytes.AsSpan().SequenceEqual(entry.Content))
                        {
                            failed++;
                            errors.Add($"{entry.DisplayName}: hash mismatch after re-import");
                        }
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(bytes);
                    }
                }
                else
                {
                    failed++;
                    errors.Add($"{entry.DisplayName}: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{entry.DisplayName}: {ex.Message}");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(entry.Content);
            }
        }

        lab.Lock();
        reader.Lock();

        var ok = failed == 0 && imported > 0;
        if (imported == 0 && failed == 0)
        {
            ok = true; // empty vault
        }

        return new V3MigrationExecuteResult
        {
            Success = ok,
            Message = ok
                ? $"마이그레이션 완료 · import {imported} · skip {skipped} · fail {failed}. 소스 v3는 삭제하지 않았습니다."
                : $"마이그레이션 부분 실패 · import {imported} · fail {failed}",
            Imported = imported,
            Failed = failed,
            Skipped = skipped,
            LabVaultPath = Path.GetFullPath(labVaultRoot),
            Errors = errors,
            ImportedEntryIds = ids,
            Preflight = preflight
        };
    }
}
