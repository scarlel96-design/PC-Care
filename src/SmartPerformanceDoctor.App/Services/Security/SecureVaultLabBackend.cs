using SmartPerformanceDoctor.App.Models.Security;
using SmartPerformanceDoctor.SecurityLab.Hardening;
using SmartPerformanceDoctor.SecurityLab.Migration;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

namespace SmartPerformanceDoctor.App.Services.Security;

/// <summary>
/// Product bridge to SecurityLab VaultV4 (spd-vault-v4-lab).
/// Used for new vaults when product flags enable V4; existing v3 stays on SecureVaultService legacy path.
/// </summary>
internal sealed class SecureVaultLabBackend : IDisposable
{
    private LabVaultService? _vault;

    public string Root => SecureVaultPaths.Root;

    public static bool ExistsOnDisk() =>
        LabVaultService.Exists(SecureVaultPaths.Root);

    public bool IsUnlocked => _vault?.IsUnlocked == true;

    public SecureVaultState State =>
        !ExistsOnDisk()
            ? SecureVaultState.NotCreated
            : IsUnlocked
                ? SecureVaultState.Unlocked
                : SecureVaultState.Locked;

    public SecureVaultOperationResult Create(string password)
    {
        LabProductGate.EnsureEnabled("vault");
        if (SecureVaultPaths.Exists())
        {
            return Fail("레거시 v3 금고가 이미 있습니다. v4와 동시에 같은 경로를 쓸 수 없습니다.");
        }

        if (ExistsOnDisk())
        {
            return Fail("이미 v4 금고가 있습니다.");
        }

        Directory.CreateDirectory(Root);
        _vault?.Dispose();
        _vault = new LabVaultService(Root, new LabSessionPolicy
        {
            IdleLock = TimeSpan.FromMinutes(15),
            MaxSession = TimeSpan.FromHours(4)
        });
        var created = _vault.Create(password, LabKdfProfile.Strong);
        if (!created.Success)
        {
            return Fail(created.Message);
        }

        // Leave locked after create? Product v3 stays unlocked — keep unlocked for UX.
        var unlock = _vault.Unlock(password);
        if (!unlock.Success)
        {
            return new SecureVaultOperationResult
            {
                Success = true,
                Message = created.Message + " (생성됨 · 잠금 해제 필요)",
                RecoveryCodes = created.RecoveryCodes,
                VaultFormat = LabVaultService.FormatId,
                KdfProfile = created.KdfProfile,
                ProcessedCount = 1
            };
        }

        return new SecureVaultOperationResult
        {
            Success = true,
            Message = "보안 금고 v4가 생성되었습니다. 복구 코드는 이 화면에만 표시됩니다(해시만 저장).",
            RecoveryCodes = created.RecoveryCodes,
            RecoveryKey = created.RecoveryCodes.Count > 0
                ? string.Join(' ', created.RecoveryCodes.Take(2)) + " …"
                : null,
            VaultFormat = LabVaultService.FormatIdV5,
            KdfProfile = created.KdfProfile,
            ProcessedCount = 1
        };
    }

    public SecureVaultOperationResult Unlock(string password)
    {
        LabProductGate.EnsureEnabled("vault");
        EnsureVaultHandle();
        var result = _vault!.Unlock(password);
        return new SecureVaultOperationResult
        {
            Success = result.Success,
            Message = result.Message,
            ProcessedCount = result.ProcessedCount,
            VaultFormat = LabVaultService.FormatIdV5
        };
    }

    public SecureVaultOperationResult ProveRecovery(string code)
    {
        LabProductGate.EnsureEnabled("vault");
        EnsureVaultHandle();
        var result = _vault!.UnlockWithRecoveryCode(code);
        return new SecureVaultOperationResult
        {
            Success = result.Success,
            Message = result.Message,
            ProcessedCount = result.ProcessedCount,
            VaultFormat = LabVaultService.FormatIdV5
        };
    }

    public SecureVaultOperationResult ChangePassword(string currentPassword, string newPassword)
    {
        LabProductGate.EnsureEnabled("vault");
        EnsureVaultHandle();
        if (!_vault!.IsUnlocked)
        {
            return Fail("금고를 연 뒤 비밀번호를 변경하세요.");
        }

        var result = _vault.ChangePassword(currentPassword, newPassword);
        return new SecureVaultOperationResult
        {
            Success = result.Success,
            Message = result.Message,
            RecoveryCodes = result.RecoveryCodes,
            VaultFormat = LabVaultService.FormatIdV5,
            ProcessedCount = result.ProcessedCount
        };
    }

    public SecureVaultOperationResult ReissueRecoveryCodes(string password)
    {
        LabProductGate.EnsureEnabled("vault");
        EnsureVaultHandle();
        if (!_vault!.IsUnlocked)
        {
            return Fail("금고를 연 뒤 복구 코드를 재발급하세요.");
        }

        var result = _vault.ReissueRecoveryCodes(password);
        return new SecureVaultOperationResult
        {
            Success = result.Success,
            Message = result.Message,
            RecoveryCodes = result.RecoveryCodes,
            VaultFormat = LabVaultService.FormatIdV5,
            ProcessedCount = result.ProcessedCount
        };
    }

    public SecureVaultOperationResult UnlockReadOnly(string password)
    {
        LabProductGate.EnsureEnabled("vault");
        EnsureVaultHandle();
        var result = _vault!.UnlockReadOnly(password);
        return new SecureVaultOperationResult
        {
            Success = result.Success,
            Message = result.Message,
            ProcessedCount = result.ProcessedCount,
            VaultFormat = LabVaultService.FormatIdV5
        };
    }

    public void Lock()
    {
        _vault?.Lock();
    }

    public IReadOnlyList<SecureVaultEntry> Entries
    {
        get
        {
            if (_vault is null || !_vault.IsUnlocked)
            {
                return Array.Empty<SecureVaultEntry>();
            }

            return _vault.List().Select(e => new SecureVaultEntry
            {
                EntryId = e.EntryId,
                DisplayLabel = e.DisplayName,
                ShardName = e.ObjectId,
                OriginalSize = e.Size,
                AddedAt = DateTimeOffset.TryParse(e.AddedAt, out var dt) ? dt : DateTimeOffset.UtcNow,
                IsFolderBundle = false,
                Kind = SecureVaultEntryKind.StandaloneFile,
                BundleId = null,
                RelativePath = e.RelativePath,
                OriginalPath = null,
                IsSealedAtOrigin = false,
                BlobFormat = 4
            }).ToArray();
        }
    }

    public IReadOnlyList<SecureVaultBrowsableItem> GetBrowsableItems(string? bundleId, string relativePrefix)
    {
        // Flat v4 list via product tree builder
        return SecureVaultTreeBuilder.Build(Entries, bundleId, relativePrefix);
    }

    public async Task<SecureVaultOperationResult> AddFileAsync(
        string path,
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        EnsureUnlocked();
        progress?.Report(new SecureVaultProgressReport
        {
            Phase = SecureVaultProgressPhase.Adding,
            Percent = 10,
            Detail = Path.GetFileName(path)
        });
        var result = await Task.Run(() => _vault!.ImportFile(path)).ConfigureAwait(false);
        progress?.Report(new SecureVaultProgressReport
        {
            Phase = SecureVaultProgressPhase.Completed,
            Percent = 100,
            Detail = result.Message
        });
        return Map(result, LabVaultService.FormatId);
    }

    public async Task<SecureVaultOperationResult> AddFolderAsync(
        string path,
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        EnsureUnlocked();
        if (!Directory.Exists(path))
        {
            return Fail("폴더를 찾을 수 없습니다.");
        }

        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToArray();
        var ok = 0;
        var fail = 0;
        for (var i = 0; i < files.Length; i++)
        {
            var file = files[i];
            progress?.Report(new SecureVaultProgressReport
            {
                Phase = SecureVaultProgressPhase.Adding,
                Percent = files.Length == 0 ? 100 : (int)((i + 1) * 100.0 / files.Length),
                Detail = Path.GetFileName(file)
            });
            var rel = Path.GetRelativePath(path, file).Replace('\\', '/');
            var bytes = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
            var r = _vault!.ImportBytes(Path.GetFileName(file), rel, bytes);
            if (r.Success)
            {
                ok++;
            }
            else
            {
                fail++;
            }
        }

        return new SecureVaultOperationResult
        {
            Success = fail == 0,
            Message = $"폴더 가져오기 완료 · 성공 {ok} · 실패 {fail}",
            ProcessedCount = ok,
            VaultFormat = LabVaultService.FormatId
        };
    }

    public Task<SecureVaultOperationResult> ExportEntryAsync(string entryId, string destination, bool stepUpConfirmed = false)
    {
        EnsureUnlocked();
        var r = _vault!.ExportEntry(entryId, destination, stepUpConfirmed);
        if (!r.Success && r.Message.Contains("step-up", StringComparison.OrdinalIgnoreCase))
        {
            // caller UI should re-confirm; product maps to explicit message
            return Task.FromResult(new SecureVaultOperationResult
            {
                Success = false,
                Message = "보안 게이트: 금고 항목이 많아 추가 확인이 필요합니다. 내보내기를 다시 확인한 뒤 진행하세요.",
                VaultFormat = LabVaultService.FormatIdV5
            });
        }

        return Task.FromResult(Map(r, LabVaultService.FormatIdV5));
    }

    public string? LastSecurityState =>
        _vault is null ? null : LabSecurityStateLabels.Format(_vault.GetSecurityState());

    public string GetSecurityStateLabel()
    {
        if (!ExistsOnDisk())
        {
            return LabSecurityStateLabels.NotCreated;
        }

        if (_vault is null || !_vault.IsUnlocked)
        {
            return LabSecurityStateLabels.Format(LabSecurityState.Locked);
        }

        return _vault.GetSecurityStateLabel();
    }

    /// <summary>Session countdown for UI (Lab path only).</summary>
    public (TimeSpan? Idle, TimeSpan? Session, bool IdleWarning, bool WriteAllowed) GetSessionRemaining()
    {
        if (_vault is null || !_vault.IsUnlocked)
        {
            return (null, null, false, false);
        }

        var (idle, session, warn) = _vault.GetSessionRemaining();
        return (idle, session, warn, _vault.IsWriteAllowed);
    }

    public string GetSessionCountdownLine()
    {
        var (idle, session, warn, write) = GetSessionRemaining();
        return LabSessionPolicy.FormatCountdown(idle, session, warn, write);
    }

    public void ApplyProductAutoLockMinutes(int minutes)
    {
        EnsureVaultHandle();
        _vault!.SessionPolicy.ApplyProductAutoLockMinutes(minutes);
    }

    public void TouchActivity()
    {
        _vault?.TouchActivity();
    }

    public SecureVaultOperationResult RemoveFromVault(string entryId)
    {
        EnsureUnlocked();
        return Map(_vault!.CryptoShredEntry(entryId), LabVaultService.FormatId);
    }

    public SecureVaultIntegrityResult VerifyIntegrity()
    {
        var probe = LabContainerProbe.Probe(Root);
        var probeIssues = probe.Findings
            .Where(f => !f.Ok)
            .Select(f => new SecureVaultIntegrityIssue
            {
                Kind = SecureVaultIntegrityIssueKind.ManifestIntegrity,
                Label = "container-" + f.Id,
                Detail = f.Detail,
                Repairable = f.Id is "C6" // activation may be repaired when unlocked
            })
            .ToList();

        if (!IsUnlocked)
        {
            // S-class: locked vault still gets non-secret container probe
            return new SecureVaultIntegrityResult
            {
                Success = probe.Healthy,
                Message = probe.Healthy
                    ? $"잠금 상태 컨테이너 점검 OK · {probe.Passed}/{probe.Total} · {probe.FormatId} · AV3 writer {(probe.Av3WriterFlag ? "ON(!)" : "OFF")}"
                    : $"잠금 상태 컨테이너 경고 · {probe.Passed}/{probe.Total} · 열어서 콘텐츠 해시 검사 가능",
                CheckedEntries = 0,
                FailedEntries = probeIssues.Count,
                ManifestIntegrityValid = probe.Healthy,
                AuditChainValid = LabAuditChain.Verify(Root).Count == 0,
                Issues = probeIssues
            };
        }

        var chain = LabAuditChain.Verify(Root);
        var content = _vault!.VerifyAllContentHashes();
        var list = _vault.List();
        var issues = chain.Select(c => new SecureVaultIntegrityIssue
        {
            Kind = SecureVaultIntegrityIssueKind.AuditChain,
            Detail = c,
            Label = "audit-chain"
        }).Concat(probeIssues).ToList();

        var ok = chain.Count == 0 && content.Success && probe.Healthy;
        return new SecureVaultIntegrityResult
        {
            Success = ok,
            Message = ok
                ? $"v5 무결성 OK · 항목 {list.Count} · 감사체인·콘텐츠·컨테이너({probe.Passed}/{probe.Total}) · state {_vault.GetSecurityStateLabel()}"
                : $"무결성 경고 · audit {chain.Count} · {content.Message} · container {probe.Passed}/{probe.Total}",
            CheckedEntries = list.Count,
            FailedEntries = chain.Count
                            + (content.Success ? 0 : Math.Max(1, list.Count - content.ProcessedCount))
                            + probeIssues.Count,
            ManifestIntegrityValid = content.Success && probe.Healthy,
            AuditChainValid = chain.Count == 0,
            Issues = issues
        };
    }

    /// <summary>Locked-safe container probe summary for UI/status.</summary>
    public string GetContainerProbeSummary()
    {
        var p = LabContainerProbe.Probe(Root);
        return p.LooksLikeLabVault
            ? $"컨테이너 {p.Passed}/{p.Total} · {(p.Healthy ? "OK" : "경고")} · {p.FormatId}"
            : "컨테이너 미생성";
    }

    public SecureVaultOperationResult CompactPacks(bool userConfirmed = true)
    {
        LabProductGate.EnsureEnabled("vault");
        EnsureVaultHandle();
        if (!_vault!.IsUnlocked)
        {
            return Fail("금고를 연 뒤 pack compact를 실행하세요.");
        }

        var r = _vault.CompactPacks(userConfirmed);
        return Map(r, LabVaultService.FormatIdV5);
    }

    public SecureVaultOperationResult RepairActivation()
    {
        LabProductGate.EnsureEnabled("vault");
        EnsureVaultHandle();
        if (!_vault!.IsUnlocked)
        {
            return Fail("금고를 연 뒤 activation 복구를 실행하세요.");
        }

        var r = _vault.RepairActivationCommit();
        return Map(r, LabVaultService.FormatIdV5);
    }

    public SecureVaultSecurityStatus GetSecurityStatus()
    {
        var rec = LabRecoverySlots.Snapshot(Root);
        var rate = LabRateLimiter.GetSnapshot(Root);
        var health = LabVaultHealth.Probe(Root);
        return new SecureVaultSecurityStatus
        {
            KdfAlgorithm = "Argon2id",
            KdfIterations = 3,
            AclHardened = false,
            RecoveryKeyConfigured = rec.Remaining > 0 || rec.StorePresent,
            RecoveryCodesRemaining = rec.Remaining,
            RecoveryStatusLine = rec.ToUiLine(),
            AuditChainValid = LabAuditChain.Verify(Root).Count == 0,
            AuditEntryCount = 0,
            RateLimitFailures = rate.Failures,
            CryptoStack = "강력한 암호화로 보호 중",
            PolicyLine = health.ToUiLine()
        };
    }

    public string GetRecoveryStatusLine() => LabRecoverySlots.Snapshot(Root).ToUiLine();

    public string GetVaultHealthLine() => LabVaultHealth.Probe(Root).ToUiLine();

    public SecureVaultOperationResult MigrateFromV3(string password)
    {
        LabProductGate.EnsureEnabled("migrate");
        if (!SecureVaultPaths.Exists())
        {
            return Fail("마이그레이션할 v3 금고가 없습니다.");
        }

        if (ExistsOnDisk())
        {
            return Fail("이미 v4 금고가 있습니다. 다른 경로로 마이그레이션하세요.");
        }

        // Lab migrator writes to a target folder; use a sibling then swap is risky.
        // Write into temp lab path under secure_vault/v4-import then if success, user uses that path.
        var target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPerformanceDoctor",
            "secure_vault",
            "v4-migrated");
        if (Directory.Exists(target) && LabVaultService.Exists(target))
        {
            return Fail("이미 마이그레이션 대상(v4-migrated)이 있습니다.");
        }

        var result = V3ToLabMigrator.Execute(
            SecureVaultPaths.Root,
            password,
            target,
            LabKdfProfile.Strong);
        return new SecureVaultOperationResult
        {
            Success = result.Success,
            Message = result.Success
                ? $"{result.Message} · 새 경로: {result.LabVaultPath} (원본 v3 유지)"
                : result.Message + " " + string.Join("; ", result.Errors),
            ProcessedCount = result.Imported,
            VaultFormat = LabVaultService.FormatId
        };
    }

    public void Dispose()
    {
        _vault?.Dispose();
        _vault = null;
    }

    private void EnsureVaultHandle()
    {
        _vault ??= new LabVaultService(Root);
    }

    private void EnsureUnlocked()
    {
        EnsureVaultHandle();
        if (!_vault!.IsUnlocked)
        {
            throw new InvalidOperationException("금고가 잠겨 있습니다.");
        }
    }

    private static SecureVaultOperationResult Map(LabVaultOperationResult r, string format) =>
        new()
        {
            Success = r.Success,
            Message = r.Message,
            ProcessedCount = r.ProcessedCount,
            VaultFormat = format
        };

    private static SecureVaultOperationResult Fail(string msg) =>
        new() { Success = false, Message = msg };
}
