using System.Security.Cryptography;
using System.Text;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

public sealed partial class LabVaultService
{
    /// <summary>Change password keeping VMK (design §11). Rewrites header + recovery slots.</summary>
    public LabVaultOperationResult ChangePassword(string currentPassword, string newPassword)
    {
        EnsureWriteUnlocked();
        var policy = LabPasswordPolicy.ValidateForCreate(newPassword);
        if (!policy.IsValid)
        {
            return FailOp(policy.Message);
        }

        if (_header is null || _vmk is null)
        {
            return FailOp("금고가 잠겨 있습니다.");
        }

        // verify current password
        var salt = Convert.FromHexString(_header.SaltHex);
        var kek = LabVaultCrypto.DeriveArgon2id(
            currentPassword,
            salt,
            _header.KdfIterations,
            _header.KdfMemoryKb,
            _header.KdfParallelism);
        try
        {
            var wrapped = new Wrapped(
                Convert.FromHexString(_header.WrappedVmkNonceHex),
                Convert.FromHexString(_header.WrappedVmkTagHex),
                Convert.FromHexString(_header.WrappedVmkCipherHex));
            try
            {
                var check = UnwrapKey(kek, wrapped, Encoding.UTF8.GetBytes("vmk:" + _header.VaultId));
                if (!LabCryptoCompare.FixedTimeEquals(check, _vmk))
                {
                    CryptographicOperations.ZeroMemory(check);
                    return FailOp("현재 비밀번호가 올바르지 않습니다.");
                }

                CryptographicOperations.ZeroMemory(check);
            }
            catch (CryptographicException)
            {
                return FailOp("현재 비밀번호가 올바르지 않습니다.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(salt);
        }

        var newSalt = LabVaultCrypto.GenerateSalt();
        var kdf = LabKdfParams.FromProfile(LabKdfProfile.Strong);
        // keep vault's profile intensity if stronger than LabFast
        if (_header.KdfMemoryKb >= kdf.MemoryKb)
        {
            kdf = new LabKdfParams(_header.KdfIterations, _header.KdfMemoryKb, _header.KdfParallelism);
        }

        var newKek = LabVaultCrypto.DeriveArgon2id(
            newPassword,
            newSalt,
            kdf.Iterations,
            kdf.MemoryKb,
            kdf.Parallelism);
        try
        {
            var newWrap = WrapKey(newKek, _vmk, Encoding.UTF8.GetBytes("vmk:" + _header.VaultId));
            _header.Generation = Math.Max(1, _header.Generation) + 1;
            _header.SaltHex = Convert.ToHexString(newSalt);
            _header.KdfIterations = kdf.Iterations;
            _header.KdfMemoryKb = kdf.MemoryKb;
            _header.KdfParallelism = kdf.Parallelism;
            _header.WrappedVmkNonceHex = Convert.ToHexString(newWrap.Nonce);
            _header.WrappedVmkTagHex = Convert.ToHexString(newWrap.Tag);
            _header.WrappedVmkCipherHex = Convert.ToHexString(newWrap.Cipher);
            if (string.IsNullOrEmpty(_header.Magic) || _header.Magic == "AVLT4")
            {
                _header.Magic = "AVLT5";
                _header.Format = FormatIdV5;
                _header.Version = 5;
            }

            WriteHeader(_header);
            var before = LabRecoverySlots.Snapshot(_root);
            var (codes, _) = LabRecoverySlots.GenerateAndWrap(_root, _header.VaultId, _vmk);
            if (_metadata is not null)
            {
                LabDurableCommit.WriteCommitted(_root, _header.VaultId, _metadata.Generation);
            }

            // recovery-session / low-slot users leave RecoveryAvailable after change
            _securityState = LabSecurityState.Unlocked;
            _writeAllowed = true;
            AppendAudit("password_change", "gen=" + _header.Generation + ";prev_remaining=" + before.Remaining);
            RefreshIntegritySnapshot();
            var after = LabRecoverySlots.Snapshot(_root);
            return new LabVaultOperationResult
            {
                Success = true,
                Message = "비밀번호가 변경되었습니다. 이전 복구 코드는 모두 무효입니다. "
                          + after.ToUiLine()
                          + " · 새 복구 코드 10개를 안전한 곳에 보관하세요.",
                ProcessedCount = 1,
                RecoveryCodes = codes,
                SecurityState = _securityState.ToString()
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newKek);
            CryptographicOperations.ZeroMemory(newSalt);
        }
    }

    /// <summary>
    /// Re-issue recovery slots without changing password (design §11 re-key recovery).
    /// Requires write session + password proof. Invalidates all previous one-time codes.
    /// </summary>
    public LabVaultOperationResult ReissueRecoveryCodes(string password)
    {
        EnsureWriteUnlocked();
        if (_header is null || _vmk is null)
        {
            return FailOp("금고가 잠겨 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return FailOp("복구 코드 재발급에는 현재 비밀번호 확인이 필요합니다.");
        }

        var salt = Convert.FromHexString(_header.SaltHex);
        var kek = LabVaultCrypto.DeriveArgon2id(
            password,
            salt,
            _header.KdfIterations,
            _header.KdfMemoryKb,
            _header.KdfParallelism);
        try
        {
            var wrapped = new Wrapped(
                Convert.FromHexString(_header.WrappedVmkNonceHex),
                Convert.FromHexString(_header.WrappedVmkTagHex),
                Convert.FromHexString(_header.WrappedVmkCipherHex));
            try
            {
                var check = UnwrapKey(kek, wrapped, Encoding.UTF8.GetBytes("vmk:" + _header.VaultId));
                if (!LabCryptoCompare.FixedTimeEquals(check, _vmk))
                {
                    CryptographicOperations.ZeroMemory(check);
                    return FailOp("비밀번호가 올바르지 않습니다.");
                }

                CryptographicOperations.ZeroMemory(check);
            }
            catch (CryptographicException)
            {
                return FailOp("비밀번호가 올바르지 않습니다.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(salt);
        }

        var (codes, _) = LabRecoverySlots.GenerateAndWrap(_root, _header.VaultId, _vmk);
        _securityState = LabSecurityState.Unlocked;
        AppendAudit("recovery_reissue", "count=" + codes.Count);
        RefreshIntegritySnapshot();
        var snap = LabRecoverySlots.Snapshot(_root);
        return new LabVaultOperationResult
        {
            Success = true,
            Message = "복구 코드가 재발급되었습니다. 이전 코드는 모두 무효입니다. " + snap.ToUiLine(),
            ProcessedCount = codes.Count,
            RecoveryCodes = codes,
            SecurityState = _securityState.ToString()
        };
    }

    public LabSecurityState GetSecurityState() => _securityState;

    /// <summary>UI-ready KR label for current security state machine.</summary>
    public string GetSecurityStateLabel() => LabSecurityStateLabels.Format(_securityState);

    public int RecoveryCodesRemaining() => LabRecoverySlots.Remaining(_root);

    /// <summary>Phase7: remaining idle/session time for UI countdown (null = disabled).</summary>
    public (TimeSpan? Idle, TimeSpan? Session, bool IdleWarning) GetSessionRemaining()
    {
        if (!IsUnlocked)
        {
            return (null, null, false);
        }

        var now = DateTimeOffset.UtcNow;
        return (
            _sessionPolicy.RemainingIdle(_lastActivity, now, _writeAllowed),
            _sessionPolicy.RemainingSession(_unlockedAt, now),
            _sessionPolicy.IsIdleWarning(_lastActivity, now, _writeAllowed));
    }

    /// <summary>Touch activity without mutating data (export/list UX).</summary>
    public void TouchActivity()
    {
        if (IsUnlocked)
        {
            _lastActivity = DateTimeOffset.UtcNow;
            if (_securityState == LabSecurityState.AutoLockScheduled)
            {
                _securityState = _writeAllowed ? LabSecurityState.Unlocked : LabSecurityState.ReadOnlyUnlocked;
            }
        }
    }

    /// <summary>
    /// S-class: if activation marker missing but vault opens (legacy), rewrite commit from current digests.
    /// Does not fix AEAD-corrupt metadata.
    /// </summary>
    public LabVaultOperationResult RepairActivationCommit()
    {
        EnsureWriteUnlocked();
        if (_header is null || _metadata is null)
        {
            return FailOp("금고가 잠겨 있습니다.");
        }

        var ok = LabTornCommit.TryRepairActivation(_root, _header.VaultId, _metadata.Generation);
        if (!ok)
        {
            return FailOp("activation commit 재작성 실패");
        }

        AppendAudit("repair_activation", "gen=" + _metadata.Generation);
        return new LabVaultOperationResult
        {
            Success = true,
            Message = "activation commit 재작성 완료 gen=" + _metadata.Generation,
            ProcessedCount = 1,
            SecurityState = _securityState.ToString()
        };
    }

    /// <summary>Phase6: pack GC / repack live objects, drop tombstones (design §7 maintenance).</summary>
    public LabVaultOperationResult CompactPacks(bool userConfirmed = true)
    {
        EnsureWriteUnlocked();
        var gate = Policy.LabSentinelGate.EvaluateMaintenance(_writeAllowed, userConfirmed);
        if (gate == Policy.LabSentinelDecision.RequireStepUp)
        {
            return FailOp("정책: pack compact는 사용자 확인이 필요합니다.");
        }

        if (Policy.LabSentinelGate.IsBlocking(gate))
        {
            return FailOp("정책: pack compact 거부 · " + gate);
        }

        _securityState = LabSecurityState.Committing;
        try
        {
            var result = LabPackStore.Compact(_root);
            RefreshIntegritySnapshot();
            AppendAudit("pack_compact", $"live={result.LiveObjects};tomb={result.TombstonesRemoved}");
            _securityState = LabSecurityState.Unlocked;
            return new LabVaultOperationResult
            {
                Success = !result.Message.Contains("aborted", StringComparison.OrdinalIgnoreCase),
                Message = result.Message,
                ProcessedCount = result.LiveObjects,
                SecurityState = _securityState.ToString()
            };
        }
        catch (Exception ex)
        {
            _securityState = LabSecurityState.Unlocked;
            return FailOp("pack compact 실패: " + ex.Message);
        }
    }

    /// <summary>List/purge loose objects not referenced by metadata (write session).</summary>
    public LabVaultOperationResult PurgeOrphanLooseObjects()
    {
        EnsureWriteUnlocked();
        if (_metadata is null)
        {
            return FailOp("금고가 잠겨 있습니다.");
        }

        var r = LabOrphanScanner.Purge(_root, _metadata.Entries.Select(e => e.ObjectId));
        if (r.Purged > 0)
        {
            AppendAudit("orphan_purge_manual", r.Purged + " loose");
        }

        return new LabVaultOperationResult
        {
            Success = true,
            Message = r.Purged == 0
                ? "orphan loose 없음"
                : $"orphan loose {r.Purged}개 정리 (발견 {r.Count})",
            ProcessedCount = r.Purged,
            SecurityState = _securityState.ToString()
        };
    }

    /// <summary>Phase4: re-decrypt + content hash check for all entries (design integrity).</summary>
    public LabVaultOperationResult VerifyAllContentHashes()
    {
        EnsureUnlocked();
        _securityState = LabSecurityState.Verifying;
        var ok = 0;
        var fail = 0;
        foreach (var e in List())
        {
            try
            {
                var raw = _metadata!.Entries.First(x => x.EntryId == e.EntryId);
                var plain = DecryptEntry(new LabMetaEntry
                {
                    EntryId = e.EntryId,
                    ObjectId = e.ObjectId,
                    DisplayName = e.DisplayName,
                    RelativePath = e.RelativePath,
                    Size = e.Size,
                    ContentSha256 = e.ContentSha256,
                    AddedAt = e.AddedAt,
                    ContentGeneration = raw.ContentGeneration,
                    DekNonceHex = raw.DekNonceHex,
                    DekTagHex = raw.DekTagHex,
                    DekCipherHex = raw.DekCipherHex
                });
                CryptographicOperations.ZeroMemory(plain);
                ok++;
            }
            catch
            {
                fail++;
            }
        }

        _securityState = fail > 0 ? LabSecurityState.CorruptionDetected : LabSecurityState.Unlocked;
        return new LabVaultOperationResult
        {
            Success = fail == 0,
            Message = fail == 0
                ? $"콘텐츠 무결성 OK · {ok}개"
                : $"콘텐츠 무결성 실패 · ok {ok} · fail {fail}",
            ProcessedCount = ok,
            SecurityState = _securityState.ToString()
        };
    }
}
