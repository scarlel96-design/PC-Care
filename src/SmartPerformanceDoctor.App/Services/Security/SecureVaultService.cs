using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartPerformanceDoctor.App.Models.Security;
using SmartPerformanceDoctor.App.Services.Commercial;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;

namespace SmartPerformanceDoctor.App.Services.Security;

public sealed class SecureVaultService : IDisposable
{
    private static readonly byte[] EnvelopeMagic = "SPDVLT1\0"u8.ToArray();
    private static readonly byte[] RecoveryMagic = "SPDREC1\0"u8.ToArray();
    private static readonly JsonSerializerOptions RecoveryPayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private byte[]? _vaultKey;
    private byte[]? _metadataKey;
    private byte[]? _macKey;
    private byte[]? _envelopeSalt;
    private SecureVaultManifestDocument? _manifest;
    private readonly List<string> _auditChain = new();
    private VaultKdfParameters _kdfParameters = VaultKdfParameters.DefaultNewVault;
    private SecureVaultAuditVerificationResult? _lastAuditVerification;
    private SecureVaultAclStatus? _aclStatus;
    private readonly SecureVaultLabBackend _lab = new();

    /// <summary>True when on-disk vault is SecurityLab v4 (not legacy spd-vault-v3).</summary>
    public bool IsLabVaultFormat => SecureVaultLabBackend.ExistsOnDisk() && !SecureVaultPaths.Exists();

    public bool UsesLabEngine => IsLabVaultFormat || (_lab.IsUnlocked && !SecureVaultPaths.Exists());

    /// <summary>Lab security state label for UI (design §10 subset).</summary>
    public string GetLabSecurityStateLabel()
    {
        if (!IsLabVaultFormat)
        {
            return State.ToString();
        }

        return _lab.GetSecurityStateLabel();
    }

    /// <summary>Lab session idle/max countdown line (empty when locked or legacy).</summary>
    public string GetLabSessionCountdownLine()
    {
        if (!IsLabVaultFormat || State != SecureVaultState.Unlocked)
        {
            return "";
        }

        return _lab.GetSessionCountdownLine();
    }

    public void ApplyLabAutoLockMinutes(int minutes)
    {
        if (IsLabVaultFormat || SecureVaultLabBackend.ExistsOnDisk())
        {
            _lab.ApplyProductAutoLockMinutes(minutes);
        }
    }

    public void TouchLabActivity()
    {
        if (IsLabVaultFormat)
        {
            _lab.TouchActivity();
        }
    }

    public string GetLabRecoveryStatusLine()
    {
        if (!IsLabVaultFormat && !SecureVaultLabBackend.ExistsOnDisk())
        {
            return "";
        }

        return _lab.GetRecoveryStatusLine();
    }

    public string GetLabContainerProbeSummary()
    {
        if (!IsLabVaultFormat && !SecureVaultLabBackend.ExistsOnDisk())
        {
            return "";
        }

        return _lab.GetContainerProbeSummary();
    }

    public string GetLabVaultHealthLine()
    {
        if (!IsLabVaultFormat && !SecureVaultLabBackend.ExistsOnDisk())
        {
            return "";
        }

        return _lab.GetVaultHealthLine();
    }

    public SecureVaultState State
    {
        get
        {
            if (SecureVaultLabBackend.ExistsOnDisk() && !SecureVaultPaths.Exists())
            {
                return _lab.State;
            }

            if (!SecureVaultPaths.Exists())
            {
                // Prefer not-created; if only lab partially exists handled above
                return SecureVaultState.NotCreated;
            }

            return _vaultKey is null ? SecureVaultState.Locked : SecureVaultState.Unlocked;
        }
    }

    public IReadOnlyList<SecureVaultEntry> Entries
    {
        get
        {
            if (IsLabVaultFormat)
            {
                return _lab.Entries;
            }

            if (_manifest is null || _metadataKey is null)
            {
                return Array.Empty<SecureVaultEntry>();
            }

            return _manifest.Entries.Select(DecodeEntry).ToArray();
        }
    }

    public IReadOnlyList<SecureVaultBrowsableItem> GetBrowsableItems(string? bundleId, string relativePrefix) =>
        IsLabVaultFormat
            ? _lab.GetBrowsableItems(bundleId, relativePrefix)
            : SecureVaultTreeBuilder.Build(Entries, bundleId, relativePrefix);

    public SecureVaultOperationResult CreateVault(string masterPassword, string? recoveryHint = null)
    {
        // 50.3.0+: new vaults use SecurityLab v4 when product flags enable it and no v3 exists.
        if (ProductFeatureFlags.VaultV4UiEnabled
            && !SecureVaultPaths.Exists()
            && !SecureVaultLabBackend.ExistsOnDisk())
        {
            return _lab.Create(masterPassword);
        }

        // PRODUCT STABLE PATH (spd-vault-v3) for existing installs / flag-off.
        var policy = SecureVaultPasswordPolicy.ValidateForNewVault(masterPassword);
        if (!policy.IsValid)
        {
            return Fail(policy.Message);
        }

        if (SecureVaultPaths.Exists())
        {
            return Fail("이미 금고가 있습니다.");
        }

        SecureVaultPaths.EnsureLayout();
        var salt = SecureVaultCrypto.GenerateSalt();
        var kdf = VaultKdfParameters.DefaultNewVault;
        var kek = SecureVaultCrypto.DeriveKek(masterPassword, salt, kdf);
        var vaultKey = SecureVaultCrypto.GenerateKey();
        var metadataKey = SecureVaultCrypto.DeriveSubKey(kek, salt, "spd-vault-metadata");
        var macKey = SecureVaultCrypto.DeriveSubKey(kek, salt, "spd-vault-mac");
        var recoveryKey = SecureVaultCrypto.GenerateKey();

        try
        {
            WriteKeyEnvelope(salt, kek, vaultKey, kdf);
            var manifest = new SecureVaultManifestDocument
            {
                CreatedAt = DateTimeOffset.Now.ToString("o"),
                Format = "spd-vault-v3"
            };
            SaveManifest(manifest, metadataKey, macKey);
            WriteMarker(kdf);
            WriteRecoveryHint(recoveryHint, metadataKey);
            WriteRecoveryEnvelope(recoveryKey, salt, vaultKey, metadataKey, macKey, kdf);
            AppendAudit("vault-created", "금고 생성 · v3", metadataKey, macKey);
            _aclStatus = SecureVaultAclHelper.HardenVaultDirectory();
            SecureVaultRateLimiter.ResetOnSuccess();

            _envelopeSalt = salt;
            _vaultKey = vaultKey;
            _metadataKey = metadataKey;
            _macKey = macKey;
            _kdfParameters = kdf;
            _manifest = manifest;
            _lastAuditVerification = new SecureVaultAuditVerificationResult
            {
                IsValid = true,
                VerifiedEntries = 1,
                Message = "생성 직후 감사 체인 시작"
            };

            var formattedRecoveryKey = SecureVaultPasswordPolicy.FormatRecoveryKey(recoveryKey);
            return new SecureVaultOperationResult
            {
                Success = true,
                Message = "보안 금고가 생성되었습니다. 복구 키를 안전한 곳에 보관하세요.",
                ProcessedCount = 1,
                RecoveryKey = formattedRecoveryKey,
                VaultFormat = "spd-vault-v3"
            };
        }
        finally
        {
            SecureVaultCrypto.Zero(kek);
            SecureVaultCrypto.Zero(recoveryKey);
        }
    }

    public SecureVaultOperationResult Unlock(string masterPassword)
    {
        if (IsLabVaultFormat || (ProductFeatureFlags.VaultV4UiEnabled && SecureVaultLabBackend.ExistsOnDisk() && !SecureVaultPaths.Exists()))
        {
            return _lab.Unlock(masterPassword);
        }

        if (!SecureVaultPaths.Exists())
        {
            return Fail("금고가 없습니다. 먼저 금고를 만드세요.");
        }

        if (!SecureVaultPasswordPolicy.IsValidForUnlock(masterPassword))
        {
            return Fail("비밀번호 형식이 올바르지 않습니다.");
        }

        var rateStatus = SecureVaultRateLimiter.CheckLockout();
        if (rateStatus.IsLockedOut)
        {
            return Fail(rateStatus.Message);
        }

        Lock();
        try
        {
            var (salt, vaultKey, metadataKey, macKey, kdf) = ReadKeyEnvelope(masterPassword);
            var manifest = LoadManifest(metadataKey, macKey);
            _envelopeSalt = salt;
            _vaultKey = vaultKey;
            _metadataKey = metadataKey;
            _macKey = macKey;
            _kdfParameters = kdf;
            _manifest = manifest;
            if (NormalizeLegacyManifest(manifest) | RepairOrphanBundleRoots(manifest, metadataKey))
            {
                SaveManifest(manifest, metadataKey, macKey);
            }

            _lastAuditVerification = SecureVaultAuditVerifier.Verify(metadataKey, macKey);
            if (!_lastAuditVerification.IsValid && IsStrictAuditVault())
            {
                Lock();
                return Fail(_lastAuditVerification.Message);
            }

            ReapplyMissingOriginSeals();
            AppendAudit("vault-unlocked", "금고 잠금 해제", metadataKey, macKey);
            SecureVaultRateLimiter.ResetOnSuccess();
            var rootCount = Entries.Count(e => e.Kind is SecureVaultEntryKind.FolderRoot or SecureVaultEntryKind.StandaloneFile or SecureVaultEntryKind.LegacyFolderFile);
            return new SecureVaultOperationResult
            {
                Success = true,
                Message = $"금고가 열렸습니다. 보관 항목 {rootCount}개 · 감사 {_lastAuditVerification.VerifiedEntries}건",
                ProcessedCount = rootCount
            };
        }
        catch (CryptographicException ex) when (ex.Message.Contains("매니페스트", StringComparison.Ordinal))
        {
            SecureVaultRateLimiter.RecordFailedAttempt();
            return Fail("금고 목록이 손상되었습니다. 이전 버전과의 호환 문제일 수 있으니 앱을 다시 실행해 보세요.");
        }
        catch (CryptographicException)
        {
            SecureVaultRateLimiter.RecordFailedAttempt();
            var status = SecureVaultRateLimiter.CheckLockout();
            return Fail(status.IsLockedOut
                ? status.Message
                : "비밀번호가 올바르지 않거나 금고 데이터가 손상되었습니다.");
        }
        catch (Exception ex)
        {
            return Fail($"금고를 열 수 없습니다: {ex.Message}");
        }
    }

    /// <summary>Lab v5 read-only unlock (design §8).</summary>
    public SecureVaultOperationResult UnlockReadOnlyLab(string masterPassword)
    {
        if (!IsLabVaultFormat && !(SecureVaultLabBackend.ExistsOnDisk() && !SecureVaultPaths.Exists()))
        {
            return Fail("읽기 전용 열기는 v5 Lab 금고에서만 지원됩니다.");
        }

        return _lab.UnlockReadOnly(masterPassword);
    }

    public SecureVaultOperationResult UnlockWithRecoveryKey(string recoveryKeyInput)
    {
        if (IsLabVaultFormat || (SecureVaultLabBackend.ExistsOnDisk() && !SecureVaultPaths.Exists()))
        {
            return _lab.ProveRecovery(recoveryKeyInput);
        }

        if (!SecureVaultPaths.Exists())
        {
            return Fail("금고가 없습니다.");
        }

        // Recovery unlock must share the same anti-bruteforce gate as password unlock.
        var rateStatus = SecureVaultRateLimiter.CheckLockout();
        if (rateStatus.IsLockedOut)
        {
            return Fail(rateStatus.Message);
        }

        if (!SecureVaultPasswordPolicy.TryParseRecoveryKey(recoveryKeyInput, out var recoveryKey))
        {
            return Fail("복구 키 형식이 올바르지 않습니다.");
        }

        Lock();
        try
        {
            var payload = ReadRecoveryEnvelope(recoveryKey);
            _envelopeSalt = payload.Salt;
            _vaultKey = payload.VaultKey;
            _metadataKey = payload.MetadataKey;
            _macKey = payload.MacKey;
            _kdfParameters = payload.KdfParameters;
            _manifest = LoadManifest(_metadataKey, _macKey);
            if (NormalizeLegacyManifest(_manifest) | RepairOrphanBundleRoots(_manifest, _metadataKey))
            {
                SaveManifest(_manifest, _metadataKey, _macKey);
            }

            _lastAuditVerification = SecureVaultAuditVerifier.Verify(_metadataKey, _macKey);
            if (!_lastAuditVerification.IsValid && IsStrictAuditVault())
            {
                Lock();
                return Fail(_lastAuditVerification.Message);
            }

            ReapplyMissingOriginSeals();
            AppendAudit("vault-recovery-unlock", "복구 키 잠금 해제", _metadataKey, _macKey);
            SecureVaultRateLimiter.ResetOnSuccess();
            return new SecureVaultOperationResult
            {
                Success = true,
                Message = "복구 키로 금고를 열었습니다. 비밀번호를 변경하는 것을 권장합니다.",
                ProcessedCount = Entries.Count
            };
        }
        catch (CryptographicException)
        {
            SecureVaultRateLimiter.RecordFailedAttempt();
            return Fail("복구 키가 올바르지 않습니다.");
        }
        finally
        {
            SecureVaultCrypto.Zero(recoveryKey);
        }
    }

    public SecureVaultOperationResult ChangeMasterPassword(string currentPassword, string newPassword)
    {
        if (IsLabVaultFormat)
        {
            return _lab.ChangePassword(currentPassword, newPassword);
        }


        EnsureUnlocked();
        var policy = SecureVaultPasswordPolicy.ValidateForNewVault(newPassword);
        if (!policy.IsValid)
        {
            return Fail(policy.Message);
        }

        try
        {
            var (_, vaultKey, oldMetadataKey, oldMacKey, _) = ReadKeyEnvelope(currentPassword);
            if (!CryptographicOperations.FixedTimeEquals(vaultKey, _vaultKey!))
            {
                return Fail("현재 비밀번호가 올바르지 않습니다.");
            }

            var newSalt = SecureVaultCrypto.GenerateSalt();
            var newKdf = VaultKdfParameters.DefaultNewVault;
            var newKek = SecureVaultCrypto.DeriveKek(newPassword, newSalt, newKdf);
            var newMetadataKey = SecureVaultCrypto.DeriveSubKey(newKek, newSalt, "spd-vault-metadata");
            var newMacKey = SecureVaultCrypto.DeriveSubKey(newKek, newSalt, "spd-vault-mac");
            var newRecoveryKey = SecureVaultCrypto.GenerateKey();

            try
            {
                ReencryptManifestMetadata(newMetadataKey, oldMetadataKey);
                ReencryptAuditLog(oldMetadataKey, oldMacKey, newMetadataKey, newMacKey);
                _manifest!.ManifestMac = "";
                SaveManifest(_manifest, newMetadataKey, newMacKey);
                WriteKeyEnvelope(newSalt, newKek, _vaultKey!, newKdf);
                WriteMarker(newKdf);
                WriteRecoveryEnvelope(newRecoveryKey, newSalt, _vaultKey!, newMetadataKey, newMacKey, newKdf);

                _envelopeSalt = newSalt;
                _metadataKey = newMetadataKey;
                _macKey = newMacKey;
                _kdfParameters = newKdf;
                AppendAudit("password-changed", "마스터 비밀번호 변경", newMetadataKey, newMacKey);
                SecureVaultRateLimiter.ResetOnSuccess();

                var formattedRecoveryKey = SecureVaultPasswordPolicy.FormatRecoveryKey(newRecoveryKey);
                return new SecureVaultOperationResult
                {
                    Success = true,
                    Message = "마스터 비밀번호가 변경되었습니다. 새 복구 키를 보관하세요.",
                    ProcessedCount = 1,
                    RecoveryKey = formattedRecoveryKey
                };
            }
            finally
            {
                SecureVaultCrypto.Zero(newKek);
                SecureVaultCrypto.Zero(oldMetadataKey);
                SecureVaultCrypto.Zero(oldMacKey);
                SecureVaultCrypto.Zero(newRecoveryKey);
            }
        }
        catch (CryptographicException)
        {
            return Fail("현재 비밀번호가 올바르지 않습니다.");
        }
    }

    /// <summary>Lab v5: re-issue one-time recovery codes (invalidates previous). Requires password proof.</summary>
    public SecureVaultOperationResult ReissueLabRecoveryCodes(string password)
    {
        if (!IsLabVaultFormat)
        {
            return Fail("복구 코드 재발급은 보안 금고 v5 Lab 경로에서만 지원됩니다.");
        }

        return _lab.ReissueRecoveryCodes(password);
    }

    public SecureVaultOperationResult MigrateKdfToArgon2(string masterPassword)
    {
        EnsureUnlocked();
        try
        {
            var (_, vaultKey, _, _, currentKdf) = ReadKeyEnvelope(masterPassword);
            if (!CryptographicOperations.FixedTimeEquals(vaultKey, _vaultKey!))
            {
                return Fail("비밀번호가 올바르지 않습니다.");
            }

            if (currentKdf.Algorithm == VaultKdfAlgorithm.Argon2id)
            {
                return new SecureVaultOperationResult
                {
                    Success = true,
                    Message = "이미 Argon2id KDF를 사용 중입니다.",
                    ProcessedCount = 0
                };
            }

            var newSalt = SecureVaultCrypto.GenerateSalt();
            var newKdf = VaultKdfParameters.DefaultNewVault;
            var newKek = SecureVaultCrypto.DeriveKek(masterPassword, newSalt, newKdf);
            try
            {
                WriteKeyEnvelope(newSalt, newKek, _vaultKey!, newKdf);
                WriteMarker(newKdf);
                var recoveryKey = SecureVaultCrypto.GenerateKey();
                try
                {
                    WriteRecoveryEnvelope(recoveryKey, newSalt, _vaultKey!, _metadataKey!, _macKey!, newKdf);
                    var formattedRecoveryKey = SecureVaultPasswordPolicy.FormatRecoveryKey(recoveryKey);
                    _envelopeSalt = newSalt;
                    _kdfParameters = newKdf;
                    AppendAudit("kdf-migrated", "PBKDF2 → Argon2id", _metadataKey!, _macKey!);
                    return new SecureVaultOperationResult
                    {
                        Success = true,
                        Message = "KDF가 Argon2id로 마이그레이션되었습니다. 새 복구 키를 보관하세요.",
                        ProcessedCount = 1,
                        RecoveryKey = formattedRecoveryKey
                    };
                }
                finally
                {
                    SecureVaultCrypto.Zero(recoveryKey);
                }
            }
            finally
            {
                SecureVaultCrypto.Zero(newKek);
            }
        }
        catch (CryptographicException)
        {
            return Fail("비밀번호가 올바르지 않습니다.");
        }
    }

    public bool NeedsKdfMigration
    {
        get
        {
            if (IsLabVaultFormat)
            {
                return false;
            }

            if (!SecureVaultPaths.Exists())
            {
                return false;
            }

            if (_vaultKey is not null)
            {
                return _kdfParameters.Algorithm != VaultKdfAlgorithm.Argon2id;
            }

            var markerKdf = ReadMarkerKdfAlgorithm();
            return markerKdf is null or VaultKdfAlgorithm.Pbkdf2Sha512;
        }
    }

    public SecureVaultSecurityStatus GetSecurityStatus()
    {
        if (IsLabVaultFormat)
        {
            return _lab.GetSecurityStatus();
        }

        var rateStatus = SecureVaultRateLimiter.CheckLockout();
        var kdfLine = _kdfParameters.DisplayName;
        return new SecureVaultSecurityStatus
        {
            KdfAlgorithm = kdfLine,
            KdfIterations = _kdfParameters.Iterations,
            AclHardened = _aclStatus?.Applied ?? File.Exists(SecureVaultPaths.MarkerFile),
            RecoveryKeyConfigured = File.Exists(SecureVaultPaths.RecoveryEnvelopeFile),
            AuditChainValid = _lastAuditVerification?.IsValid ?? false,
            AuditEntryCount = _lastAuditVerification?.VerifiedEntries ?? 0,
            RateLimitFailures = rateStatus.FailedAttempts,
            CryptoStack = $"{kdfLine} · HKDF · AES-256-GCM · DEK 이중 봉인 · 샤드 MAC · DPAPI · NTFS ACL",
            PolicyLine = $"신규 금고 {SecureVaultPasswordPolicy.MinLengthNewVault}자+ · {kdfLine}"
        };
    }

    /// <summary>v3 → Lab v4 re-encrypt migration (source v3 preserved).</summary>
    public SecureVaultOperationResult MigrateLegacyVaultToLabV4(string masterPassword)
    {
        if (!ProductFeatureFlags.MigrationUiEnabled)
        {
            return Fail("마이그레이션 기능이 비활성화되어 있습니다.");
        }

        return _lab.MigrateFromV3(masterPassword);
    }

    public void Lock()
    {
        _lab.Lock();
        SecureVaultCrypto.Zero(_vaultKey);
        SecureVaultCrypto.Zero(_metadataKey);
        SecureVaultCrypto.Zero(_macKey);
        SecureVaultCrypto.Zero(_envelopeSalt);
        _vaultKey = null;
        _metadataKey = null;
        _macKey = null;
        _envelopeSalt = null;
        _manifest = null;
    }

    public async Task<SecureVaultOperationResult> AddFileAsync(
        string sourcePath,
        bool sealOrigin = true,
        CancellationToken cancellationToken = default,
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        if (IsLabVaultFormat)
        {
            return await _lab.AddFileAsync(sourcePath, progress).ConfigureAwait(false);
        }

        EnsureUnlocked();
        if (!File.Exists(sourcePath))
        {
            return Fail("파일을 찾을 수 없습니다.");
        }

        var label = Path.GetFileName(sourcePath);
        ReportProgress(progress, SecureVaultProgressPhase.Preparing, 0, "파일 준비", $"「{label}」을(를) 읽고 있습니다.", label, totalCount: 1);
        byte[] bytes;
        try
        {
            bytes = await ReadVaultSourceFileAsync(sourcePath, cancellationToken);
        }
        catch (Exception ex)
        {
            return Fail($"파일을 읽지 못했습니다 · {sourcePath}: {ex.Message}");
        }
        ReportProgress(progress, SecureVaultProgressPhase.Adding, 35, "금고에 보관", "파일을 암호화해 금고에 저장하고 있습니다.", label, totalCount: 1);
        var result = AddPayload(
            label,
            bytes,
            SecureVaultEntryKind.StandaloneFile,
            bundleId: null,
            relativePath: null,
            originalPath: sourcePath,
            sealOrigin: sealOrigin);

        if (!result.Success)
        {
            return result;
        }

        if (sealOrigin)
        {
            ReportProgress(progress, SecureVaultProgressPhase.Sealing, 80, "원본 잠금", "원본 파일 위치에 잠금 표시를 적용합니다.", label, totalCount: 1);
            var entryId = FindLastAddedEntryId();
            SecureVaultOriginSealService.SecureDeleteOriginalFile(sourcePath);
            SecureVaultOriginSealService.SealFile(sourcePath, entryId);
        }

        ReportProgress(
            progress,
            SecureVaultProgressPhase.Completed,
            100,
            sealOrigin ? "보관 · 잠금 완료" : "보관 완료",
            $"「{label}」을(를) 금고에 넣었습니다.",
            label,
            processedCount: 1,
            totalCount: 1);
        return result;
    }

    public async Task<SecureVaultOperationResult> AddFolderAsync(
        string folderPath,
        bool sealOrigin = true,
        CancellationToken cancellationToken = default,
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        if (IsLabVaultFormat)
        {
            return await _lab.AddFolderAsync(folderPath, progress).ConfigureAwait(false);
        }

        EnsureUnlocked();
        if (!Directory.Exists(folderPath))
        {
            return Fail("폴더를 찾을 수 없습니다.");
        }

        var rootName = new DirectoryInfo(folderPath).Name;
        var bundleId = Guid.NewGuid().ToString("N");
        var rootEntryId = Guid.NewGuid().ToString("N");
        var files = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(f => !IsVaultArtifact(f))
            .ToArray();

        if (files.Length == 0)
        {
            return Fail(
                "폴더에 보관할 파일이 없습니다.\n" +
                "이전 보관 시도에서 원본이 이미 제거됐을 수 있습니다. 금고 목록과 무결성 검사를 확인하세요.");
        }

        var fullFolderPath = Path.GetFullPath(folderPath);
        ReportProgress(
            progress,
            SecureVaultProgressPhase.Preparing,
            0,
            "폴더 준비",
            $"「{rootName}」 · {files.Length}개 파일 · {fullFolderPath}",
            totalCount: files.Length);

        var batchStartIndex = _manifest!.Entries.Count;
        var createdShards = new List<string>();
        var manifestSaved = false;
        var rootEntry = new SecureVaultManifestEntry
        {
            EntryId = rootEntryId,
            EntryKind = "folderRoot",
            BundleId = bundleId,
            ShardName = "",
            AddedAt = DateTimeOffset.Now.ToString("o"),
            IsFolderBundle = true,
            IsSealedAtOrigin = sealOrigin,
            BlobFormat = SecureVaultCrypto.BlobFormatLayered
        };
        PopulateEncryptedFields(rootEntry, nameof(rootEntry.EncryptedLabel), rootName);
        PopulateEncryptedPath(rootEntry, fullFolderPath);
        _manifest.Entries.Add(rootEntry);

        var relativePaths = new List<string>(files.Length);
        var added = 0;
        try
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(folderPath, file).Replace('\\', '/');
                relativePaths.Add(relative);
                try
                {
                    var bytes = await ReadVaultSourceFileAsync(file, cancellationToken);
                    var result = AddPayload(
                        relative,
                        bytes,
                        SecureVaultEntryKind.FolderMember,
                        bundleId,
                        relative,
                        originalPath: file,
                        sealOrigin: false,
                        persistManifest: false,
                        writeAudit: false);
                    if (!result.Success)
                    {
                        AbandonFolderAddBatch(batchStartIndex, createdShards);
                        return Fail($"파일 보관 실패 · {relative}: {result.Message}");
                    }

                    createdShards.Add(_manifest.Entries[^1].ShardName);
                    added++;
                    var percent = 5 + (int)Math.Round(added * 70.0 / files.Length);
                    ReportProgress(
                        progress,
                        SecureVaultProgressPhase.Adding,
                        percent,
                        "금고에 보관하는 중",
                        "파일을 암호화해 금고에 저장하고 있습니다.",
                        Path.GetFileName(file),
                        added,
                        files.Length);
                }
                catch (Exception ex)
                {
                    AbandonFolderAddBatch(batchStartIndex, createdShards);
                    return Fail($"파일 읽기/보관 실패 ({added}/{files.Length}개 처리됨) · {file}: {ex.Message}");
                }
            }

            SaveManifest(_manifest, _metadataKey!, _macKey!);
            manifestSaved = true;

            var deleteWarnings = new List<string>();
            string? sealWarning = null;
            if (sealOrigin)
            {
                ReportProgress(
                    progress,
                    SecureVaultProgressPhase.Sealing,
                    82,
                    "원본 정리",
                    "원본 파일을 제거하고 잠금 표시를 적용합니다.",
                    rootName,
                    processedCount: added,
                    totalCount: files.Length);
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        SecureVaultOriginSealService.SecureDeleteOriginalFile(file);
                    }
                    catch (Exception ex)
                    {
                        deleteWarnings.Add($"{Path.GetFileName(file)}: {ex.Message}");
                    }
                }

                sealWarning = SecureVaultOriginSealService.TrySealFolderTree(
                    folderPath,
                    relativePaths,
                    bundleId,
                    rootEntryId);
            }

            AppendAudit("folder-added", $"폴더 추가 · {HashPath(rootName)}", _metadataKey!, _macKey!);
            ReportProgress(
                progress,
                SecureVaultProgressPhase.Completed,
                100,
                sealOrigin ? "보관 · 잠금 완료" : "보관 완료",
                $"「{rootName}」 {added}개 파일을 금고에 넣었습니다.",
                processedCount: added,
                totalCount: files.Length);

            var message = sealOrigin
                ? $"폴더 「{rootName}」 {added}개 파일을 금고에 넣고 원본을 잠갔습니다.\n원본: {fullFolderPath}"
                : $"폴더 「{rootName}」 {added}개 파일을 금고에 넣었습니다.\n원본: {fullFolderPath}";
            if (deleteWarnings.Count > 0)
            {
                message += $"\n\n원본 삭제 경고 {deleteWarnings.Count}건 (금고에는 보관됨):\n{string.Join(Environment.NewLine, deleteWarnings.Take(3))}";
            }

            if (!string.IsNullOrWhiteSpace(sealWarning))
            {
                message += $"\n\n잠금 표시 경고 (보관은 완료됨):\n{sealWarning}";
            }

            return new SecureVaultOperationResult
            {
                Success = true,
                Message = message,
                ProcessedCount = added
            };
        }
        catch (Exception ex)
        {
            if (!manifestSaved)
            {
                AbandonFolderAddBatch(batchStartIndex, createdShards);
                return Fail($"폴더 보관 실패 ({added}/{files.Length}개 처리됨): {ex.Message}");
            }

            return new SecureVaultOperationResult
            {
                Success = true,
                Message =
                    $"폴더 「{rootName}」 {added}개 파일은 금고에 저장됐지만 마무리 중 오류가 발생했습니다.\n" +
                    $"원본: {fullFolderPath}\n" +
                    $"오류: {ex.Message}\n" +
                    "금고 목록에서 항목을 확인한 뒤 필요하면 원본 복원을 실행하세요.",
                ProcessedCount = added
            };
        }
    }

    public Task<SecureVaultOperationResult> ExportBundleAsync(
        string bundleId,
        string destinationDirectory,
        CancellationToken cancellationToken = default,
        IProgress<SecureVaultProgressReport>? progress = null) =>
        ExportBundleInternalAsync(bundleId, destinationDirectory, removeFromVault: false, cancellationToken, progress);

    public async Task<SecureVaultOperationResult> ExportEntryAsync(
        string entryId,
        string destinationDirectory,
        CancellationToken cancellationToken = default,
        bool stepUpConfirmed = false)
    {
        if (IsLabVaultFormat)
        {
            var first = await _lab.ExportEntryAsync(entryId, destinationDirectory, stepUpConfirmed: false)
                .ConfigureAwait(false);
            if (!first.Success
                && first.Message.Contains("추가 확인", StringComparison.Ordinal)
                && !stepUpConfirmed)
            {
                // product UI may call again with stepUpConfirmed; signal explicitly
                return first;
            }

            if (!first.Success && stepUpConfirmed)
            {
                return await _lab.ExportEntryAsync(entryId, destinationDirectory, stepUpConfirmed: true)
                    .ConfigureAwait(false);
            }

            return first;
        }

        EnsureUnlocked();
        var entry = _manifest!.Entries.FirstOrDefault(e => e.EntryId == entryId);
        if (entry is null)
        {
            return Fail("항목을 찾을 수 없습니다.");
        }

        if (entry.EntryKind == "folderRoot")
        {
            return await ExportBundleInternalAsync(entry.BundleId!, destinationDirectory, removeFromVault: false, cancellationToken);
        }

        return await ExportSingleEntryAsync(entry, destinationDirectory, cancellationToken);
    }

    public async Task<SecureVaultOperationResult> RestoreToOriginAsync(
        string entryId,
        CancellationToken cancellationToken = default,
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        if (IsLabVaultFormat)
        {
            return Fail("v4 금고는 원본 경로 봉인을 사용하지 않습니다. 「내보내기」로 복구하세요.");
        }

        EnsureUnlocked();
        var entry = _manifest!.Entries.FirstOrDefault(e => e.EntryId == entryId);
        if (entry is null)
        {
            return Fail("항목을 찾을 수 없습니다.");
        }

        if (entry.EntryKind == "folderRoot")
        {
            var origin = DecryptOriginalPath(entry);
            if (string.IsNullOrWhiteSpace(origin))
            {
                return Fail("원본 폴더 경로가 없습니다.");
            }

            return await RestoreBundleToOriginInternal(entry.BundleId!, origin, cancellationToken, progress);
        }

        ReportProgress(progress, SecureVaultProgressPhase.Preparing, 0, "복원 준비", "파일 정보를 확인합니다.", totalCount: 1);
        var fileOrigin = DecryptOriginalPath(entry);
        if (string.IsNullOrWhiteSpace(fileOrigin))
        {
            return Fail("원본 파일 경로가 없습니다.");
        }

        fileOrigin = Path.GetFullPath(fileOrigin);
        var targetDir = Path.GetDirectoryName(fileOrigin);
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            return Fail("원본 경로가 올바르지 않습니다.");
        }

        try
        {
            ReportProgress(progress, SecureVaultProgressPhase.Unsealing, 15, "잠금 해제", "원본 위치의 잠금 표시를 해제합니다.", Path.GetFileName(fileOrigin), totalCount: 1);
            SecureVaultOriginSealService.UnsealFileStub(fileOrigin);
            SecureVaultOriginSealService.UnsealFolder(targetDir);
            ReportProgress(progress, SecureVaultProgressPhase.Restoring, 40, "파일 복원", "암호화된 파일을 원본 위치에 되돌립니다.", Path.GetFileName(fileOrigin), totalCount: 1);
            var export = await ExportSingleEntryAsync(entry, targetDir, cancellationToken, forceFileName: Path.GetFileName(fileOrigin));
            if (!export.Success)
            {
                return export;
            }

            ReportProgress(progress, SecureVaultProgressPhase.RemovingFromVault, 90, "금고 정리", "복원된 항목을 금고에서 제거합니다.", totalCount: 1);
            CryptoEraseEntryInternal(entry, unsealOrigin: true);
            ReportProgress(progress, SecureVaultProgressPhase.Completed, 100, "복원 완료", $"원본 위치에 복원했습니다: {fileOrigin}", processedCount: 1, totalCount: 1);
            return new SecureVaultOperationResult
            {
                Success = true,
                Message = $"원본 위치에 복원했습니다: {fileOrigin}",
                ProcessedCount = 1
            };
        }
        catch (Exception ex)
        {
            return Fail($"복원 실패: {ex.Message}");
        }
    }

    public async Task<SecureVaultOperationResult> RestoreBundleToOriginAsync(
        string bundleId,
        CancellationToken cancellationToken = default,
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        EnsureUnlocked();
        var origin = ResolveBundleOriginDirectory(bundleId);
        if (string.IsNullOrWhiteSpace(origin))
        {
            return Fail("원본 폴더 경로를 찾을 수 없습니다.");
        }

        return await RestoreBundleToOriginInternal(bundleId, origin, cancellationToken, progress);
    }

    public SecureVaultOperationResult RemoveFromVault(string entryId, bool unsealOrigin = true)
    {
        if (IsLabVaultFormat)
        {
            return _lab.RemoveFromVault(entryId);
        }

        EnsureUnlocked();
        var entry = _manifest!.Entries.FirstOrDefault(e => e.EntryId == entryId);
        if (entry is null)
        {
            return Fail("항목을 찾을 수 없습니다.");
        }

        if (entry.EntryKind == "folderRoot")
        {
            return RemoveBundleFromVault(entry.BundleId!, unsealOrigin);
        }

        CryptoEraseEntryInternal(entry, unsealOrigin);
        return new SecureVaultOperationResult
        {
            Success = true,
            Message = "항목을 영구 삭제했습니다.",
            ProcessedCount = 1
        };
    }

    public SecureVaultOperationResult RemoveBundleFromVault(string bundleId, bool unsealOrigin = true)
    {
        EnsureUnlocked();
        var entries = ResolveBundleRemovalEntries(bundleId);
        if (entries.Length == 0)
        {
            return Fail("폴더 항목을 찾을 수 없습니다.");
        }

        var root = entries.FirstOrDefault(e => e.EntryKind == "folderRoot");
        try
        {
            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.ShardName))
                {
                    SecureEraseShardFiles(entry.ShardName);
                }
            }

            foreach (var entry in entries)
            {
                _manifest!.Entries.Remove(entry);
            }

            if (_manifest is null || _metadataKey is null || _macKey is null)
            {
                return Fail("금고 매니페스트 상태가 유효하지 않습니다.");
            }

            SaveManifest(_manifest, _metadataKey, _macKey);
            AppendAudit("bundle-removed", $"폴더 제거 · {HashPath(bundleId)}", _metadataKey!, _macKey!);
        }
        catch (Exception ex)
        {
            return Fail($"금고에서 폴더를 제거하지 못했습니다: {ex.Message}");
        }

        if (unsealOrigin && root is not null)
        {
            var origin = DecryptOriginalPath(root);
            if (!string.IsNullOrWhiteSpace(origin))
            {
                SecureVaultOriginSealService.UnsealFolder(origin);
            }
        }

        return new SecureVaultOperationResult
        {
            Success = true,
            Message = "폴더를 영구 삭제하고 원본 잠금 표시를 해제했습니다.",
            ProcessedCount = entries.Length
        };
    }

    public SecureVaultOperationResult CryptoEraseEntry(string entryId) =>
        RemoveFromVault(entryId, unsealOrigin: true);

    public SecureVaultStorageDiagnostic DiagnoseStorage()
    {
        EnsureUnlocked();

        var shardsOnDisk = Directory.Exists(SecureVaultPaths.DataDirectory)
            ? Directory.EnumerateFiles(SecureVaultPaths.DataDirectory, "*.blob", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var referencedShards = _manifest!.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.ShardName))
            .Select(e => e.ShardName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rootBundleIds = _manifest.Entries
            .Where(e => e.EntryKind == "folderRoot" && !string.IsNullOrWhiteSpace(e.BundleId))
            .Select(e => e.BundleId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invisibleMemberCount = _manifest.Entries.Count(e =>
            e.EntryKind == "folderMember"
            && !string.IsNullOrWhiteSpace(e.BundleId)
            && !rootBundleIds.Contains(e.BundleId));

        var invalidPaths = FindInvalidRelativePathSamples();

        return new SecureVaultStorageDiagnostic
        {
            ShardsOnDisk = shardsOnDisk.Count,
            ManifestShardReferences = referencedShards.Count,
            VisibleRootItems = GetBrowsableItems(null, "").Count,
            OrphanShardCount = shardsOnDisk.Except(referencedShards).Count(),
            MissingShardCount = referencedShards.Except(shardsOnDisk).Count(),
            InvisibleMemberCount = invisibleMemberCount,
            InvalidRelativePathCount = invalidPaths.Count,
            OrphanShardNames = shardsOnDisk.Except(referencedShards).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).Take(5).ToArray(),
            MissingShardNames = referencedShards.Except(shardsOnDisk).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).Take(5).ToArray(),
            InvalidRelativePathSamples = invalidPaths.Take(5).ToArray()
        };
    }

    private List<string> FindInvalidRelativePathSamples()
    {
        var invalid = new List<string>();
        foreach (var entry in _manifest!.Entries.Where(e => !string.IsNullOrWhiteSpace(e.ShardName)))
        {
            try
            {
                if (string.Equals(entry.EntryKind, "folderRoot", StringComparison.Ordinal))
                {
                    var origin = DecryptOriginalPath(entry);
                    if (!string.IsNullOrWhiteSpace(origin))
                    {
                        SecureVaultPathHelper.NormalizeDirectory(origin);
                    }

                    continue;
                }

                var bundleId = entry.BundleId ?? "";
                if (bundleId.StartsWith("legacy:", StringComparison.Ordinal))
                {
                    var origin = ResolveLegacyBundleOriginDirectory(bundleId);
                    _ = ResolveMemberRelativePath(entry, bundleId, origin);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(bundleId))
                {
                    var origin = ResolveBundleOriginDirectory(bundleId);
                    _ = ResolveMemberRelativePath(entry, bundleId, origin);
                    continue;
                }

                _ = DecryptRelativePath(entry) ?? SecureVaultPathHelper.SanitizeFileName(DecryptLabel(entry));
            }
            catch (Exception ex)
            {
                invalid.Add($"{entry.EntryId[..8]}: {ex.Message}");
            }
        }

        return invalid;
    }

    public SecureVaultIntegrityResult VerifyIntegrity() =>
        IsLabVaultFormat ? _lab.VerifyIntegrity() : VerifyIntegrityInternal(repair: false);

    public SecureVaultIntegrityResult RepairIntegrity() =>
        IsLabVaultFormat ? _lab.VerifyIntegrity() : VerifyIntegrityInternal(repair: true);

    /// <summary>Lab v5 pack GC (design §7). No-op message on legacy v3.</summary>
    public SecureVaultOperationResult CompactLabPacks(bool userConfirmed = true)
    {
        if (!IsLabVaultFormat)
        {
            return Fail("Pack compact는 보안 금고 v5 Lab 경로에서만 지원됩니다.");
        }

        return _lab.CompactPacks(userConfirmed);
    }

    /// <summary>Lab v5: rewrite activation.commit from current digests (S-class repair).</summary>
    public SecureVaultOperationResult RepairLabActivation()
    {
        if (!IsLabVaultFormat)
        {
            return Fail("Activation 복구는 보안 금고 v5 Lab 경로에서만 지원됩니다.");
        }

        return _lab.RepairActivation();
    }

    private SecureVaultIntegrityResult VerifyIntegrityInternal(bool repair)
    {
        if (!SecureVaultPaths.Exists())
        {
            return new SecureVaultIntegrityResult { Success = false, Message = "금고가 없습니다." };
        }

        try
        {
            if (State != SecureVaultState.Unlocked)
            {
                return new SecureVaultIntegrityResult
                {
                    Success = false,
                    Message = "무결성 검사는 금고를 연 상태에서 실행하세요."
                };
            }

            Directory.CreateDirectory(SecureVaultPaths.RedundantDataDirectory);
            var repaired = 0;
            if (repair)
            {
                repaired += RepairShardCopies();
                if (_manifest is not null && _metadataKey is not null && _macKey is not null
                    && RepairOrphanBundleRoots(_manifest, _metadataKey))
                {
                    SaveManifest(_manifest, _metadataKey, _macKey);
                    repaired++;
                }
            }

            var issues = new List<SecureVaultIntegrityIssue>();
            var failed = 0;
            var checkedCount = 0;
            foreach (var entry in _manifest!.Entries.Where(e => !string.IsNullOrWhiteSpace(e.ShardName)))
            {
                checkedCount++;
                var label = DecryptLabel(entry);
                var primaryPath = GetPrimaryShardPath(entry.ShardName);
                var redundantPath = GetRedundantShardPath(entry.ShardName);
                var primaryOk = VerifyEntryIntegrityFromPath(entry, primaryPath);
                var redundantOk = File.Exists(redundantPath) && VerifyEntryIntegrityFromPath(entry, redundantPath);

                if (!primaryOk)
                {
                    failed++;
                    issues.Add(new SecureVaultIntegrityIssue
                    {
                        Kind = File.Exists(primaryPath)
                            ? SecureVaultIntegrityIssueKind.CorruptShard
                            : SecureVaultIntegrityIssueKind.MissingShard,
                        EntryId = entry.EntryId,
                        Label = label,
                        Detail = entry.ShardName,
                        Repairable = redundantOk
                    });
                }
                else if (!File.Exists(redundantPath))
                {
                    issues.Add(new SecureVaultIntegrityIssue
                    {
                        Kind = SecureVaultIntegrityIssueKind.MissingRedundantCopy,
                        EntryId = entry.EntryId,
                        Label = label,
                        Detail = "중복 보호 샤드 없음",
                        Repairable = repair
                    });
                }
            }

            var manifestValid = VerifyManifestIntegrityOnDisk();
            if (!manifestValid)
            {
                issues.Add(new SecureVaultIntegrityIssue
                {
                    Kind = SecureVaultIntegrityIssueKind.ManifestIntegrity,
                    Detail = "매니페스트 MAC 검증 실패",
                    Repairable = false
                });
            }

            var audit = SecureVaultAuditVerifier.Verify(_metadataKey!, _macKey!);
            _lastAuditVerification = audit;
            if (!audit.IsValid)
            {
                issues.Add(new SecureVaultIntegrityIssue
                {
                    Kind = SecureVaultIntegrityIssueKind.AuditChain,
                    Detail = audit.Message,
                    Repairable = false
                });
            }

            var storage = DiagnoseStorage();
            foreach (var orphan in storage.OrphanShardNames)
            {
                issues.Add(new SecureVaultIntegrityIssue
                {
                    Kind = SecureVaultIntegrityIssueKind.OrphanShard,
                    Detail = orphan,
                    Repairable = false
                });
            }

            foreach (var sample in storage.InvalidRelativePathSamples)
            {
                issues.Add(new SecureVaultIntegrityIssue
                {
                    Kind = SecureVaultIntegrityIssueKind.InvalidPath,
                    Detail = sample,
                    Repairable = false
                });
            }

            if (storage.InvisibleMemberCount > 0)
            {
                issues.Add(new SecureVaultIntegrityIssue
                {
                    Kind = SecureVaultIntegrityIssueKind.InvisibleMember,
                    Detail = $"목록에 표시되지 않는 멤버 {storage.InvisibleMemberCount}개",
                    Repairable = repair
                });
            }

            var ok = failed == 0
                && manifestValid
                && audit.IsValid
                && storage.OrphanShardCount == 0
                && storage.MissingShardCount == 0
                && storage.InvisibleMemberCount == 0
                && storage.InvalidRelativePathCount == 0;
            AppendAudit(
                ok ? "integrity-ok" : repair ? "integrity-repair" : "integrity-fail",
                repair
                    ? $"무결성 검사·복구 · 복구 {repaired} · 실패 {failed}"
                    : $"무결성 검사 · 실패 {failed}",
                _metadataKey!,
                _macKey!);

            var message = ok
                ? (repair && repaired > 0
                    ? $"무결성 정상 · {repaired}건 복구/보강 완료 · 샤드 {storage.ShardsOnDisk}개 · 목록 {storage.VisibleRootItems}개"
                    : $"무결성 정상 · AES-GCM·샤드MAC·SHA256 검증 통과 · 샤드 {storage.ShardsOnDisk}개 · 목록 {storage.VisibleRootItems}개")
                : BuildIntegrityFailureMessage(failed, storage, issues, repaired);

            return new SecureVaultIntegrityResult
            {
                Success = ok,
                Message = message,
                CheckedEntries = checkedCount,
                FailedEntries = failed,
                RepairedEntries = repaired,
                ManifestIntegrityValid = manifestValid,
                AuditChainValid = audit.IsValid,
                Issues = issues,
                StorageDiagnostic = storage
            };
        }
        catch (Exception ex)
        {
            return new SecureVaultIntegrityResult { Success = false, Message = ex.Message };
        }
    }

    private int RepairShardCopies()
    {
        var repaired = 0;
        foreach (var entry in _manifest!.Entries.Where(e => !string.IsNullOrWhiteSpace(e.ShardName)))
        {
            var primaryPath = GetPrimaryShardPath(entry.ShardName);
            var redundantPath = GetRedundantShardPath(entry.ShardName);
            var primaryOk = File.Exists(primaryPath) && VerifyEntryIntegrityFromPath(entry, primaryPath);
            var redundantOk = File.Exists(redundantPath) && VerifyEntryIntegrityFromPath(entry, redundantPath);

            if (!primaryOk && redundantOk)
            {
                File.Copy(redundantPath, primaryPath, overwrite: true);
                repaired++;
                continue;
            }

            if (primaryOk && !File.Exists(redundantPath))
            {
                WriteRedundantShardCopy(primaryPath, entry.ShardName);
                repaired++;
            }
        }

        return repaired;
    }

    private bool VerifyManifestIntegrityOnDisk()
    {
        try
        {
            _ = LoadManifest(_metadataKey!, _macKey!);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildIntegrityFailureMessage(
        int failed,
        SecureVaultStorageDiagnostic storage,
        IReadOnlyList<SecureVaultIntegrityIssue> issues,
        int repaired)
    {
        var parts = new List<string>();
        if (repaired > 0)
        {
            parts.Add($"복구·보강 {repaired}건");
        }

        if (failed > 0)
        {
            parts.Add($"손상·검증 실패 {failed}개");
        }

        if (storage.OrphanShardCount > 0)
        {
            parts.Add($"목록에 없는 디스크 샤드 {storage.OrphanShardCount}개");
        }

        if (storage.MissingShardCount > 0)
        {
            parts.Add($"목록은 있으나 파일 없음 {storage.MissingShardCount}개");
        }

        if (storage.InvisibleMemberCount > 0)
        {
            parts.Add($"목록에 안 보이는 폴더 멤버 {storage.InvisibleMemberCount}개");
        }

        if (storage.InvalidRelativePathCount > 0)
        {
            parts.Add($"잘못된 경로 정보 {storage.InvalidRelativePathCount}개");
        }

        var repairable = issues.Count(i => i.Repairable);
        if (repairable > 0)
        {
            parts.Add($"중복 샤드로 복구 가능 {repairable}건");
        }

        return string.Join(" · ", parts)
            + $" (디스크 {storage.ShardsOnDisk} · 목록 {storage.VisibleRootItems})";
    }

    public void Dispose()
    {
        Lock();
        _lab.Dispose();
    }

    private async Task<SecureVaultOperationResult> ExportBundleInternalAsync(
        string bundleId,
        string destinationDirectory,
        bool removeFromVault,
        CancellationToken cancellationToken,
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        var members = ResolveBundleMembers(bundleId);
        if (members.Length == 0)
        {
            return Fail("폴더에 보낼 파일이 없습니다.");
        }

        var (allowed, reason) = PathSafetyGuard.Evaluate(destinationDirectory);
        if (!allowed)
        {
            return Fail(reason);
        }

        var normalizedDestination = SecureVaultPathHelper.NormalizeDirectory(destinationDirectory);
        var bundleOrigin = ResolveBundleOriginDirectory(bundleId) ?? normalizedDestination;
        ReportProgress(
            progress,
            SecureVaultProgressPhase.Preparing,
            2,
            "복원 준비",
            $"{members.Length}개 파일 경로를 확인합니다.",
            totalCount: members.Length);

        var plans = new List<BundleExportTarget>(members.Length);
        try
        {
            foreach (var member in members)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = ResolveMemberRelativePath(member, bundleId, bundleOrigin);
                var target = SecureVaultPathHelper.CombineUnderRoot(normalizedDestination, relative);
                plans.Add(new BundleExportTarget(member, relative, target));
            }
        }
        catch (Exception ex)
        {
            return Fail($"복원 경로 확인 실패: {ex.Message}");
        }

        var relativePaths = plans.Select(plan => plan.RelativePath).ToArray();
        ReportProgress(
            progress,
            SecureVaultProgressPhase.Unsealing,
            10,
            "잠금 해제",
            "원본 폴더와 하위 폴더의 금고 잠금 표시를 해제합니다.",
            Path.GetFileName(normalizedDestination),
            totalCount: members.Length);
        Directory.CreateDirectory(normalizedDestination);
        SecureVaultOriginSealService.UnsealFolderTree(normalizedDestination, relativePaths, notifyShell: false);

        foreach (var directory in plans.Select(plan => plan.TargetDirectory).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(directory!);
        }

        var exported = 0;
        BundleExportTarget? failedPlan = null;
        try
        {
            foreach (var plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                failedPlan = plan;
                var percent = 15 + (int)Math.Round((exported + 1) * 70.0 / plans.Count);
                ReportProgress(
                    progress,
                    SecureVaultProgressPhase.Restoring,
                    percent,
                    "파일 복원",
                    "암호화된 파일을 원본 위치에 되돌리고 있습니다.",
                    plan.RelativePath,
                    exported,
                    plans.Count);

                var plaintext = await DecryptEntryPayloadAsync(plan.Member, cancellationToken);
                PrepareRestoreTarget(plan.TargetPath);
                await WriteFileAtomicallyAsync(plan.TargetPath, plaintext, cancellationToken);
                SecureVaultCrypto.Zero(plaintext);
                exported++;
            }
        }
        catch (Exception ex)
        {
            var itemLabel = failedPlan?.RelativePath ?? "알 수 없는 항목";
            return Fail(removeFromVault
                ? $"복원 중 오류 ({exported}/{members.Length}개 처리됨) · {itemLabel}: {ex.Message}"
                : $"보내기 중 오류 ({exported}/{members.Length}개 처리됨) · {itemLabel}: {ex.Message}");
        }

        SecureVaultOriginSealService.NotifyShellRefresh(normalizedDestination);

        if (removeFromVault)
        {
            ReportProgress(
                progress,
                SecureVaultProgressPhase.RemovingFromVault,
                92,
                "금고 정리",
                "복원된 항목을 금고에서 제거합니다.",
                processedCount: exported,
                totalCount: members.Length);
            var removed = RemoveBundleFromVault(bundleId, unsealOrigin: false);
            if (!removed.Success)
            {
                return Fail($"파일은 복원됐지만 금고에서 제거하지 못했습니다: {removed.Message}");
            }

            if (CountBundleEntries(bundleId) > 0)
            {
                return Fail("파일은 복원됐지만 금고 목록에 항목이 남아 있습니다. 무결성 검사를 실행해 주세요.");
            }
        }
        else
        {
            AppendAudit("bundle-exported", $"폴더 보내기 · {HashPath(bundleId)}", _metadataKey!, _macKey!);
        }

        ReportProgress(
            progress,
            SecureVaultProgressPhase.Completed,
            100,
            removeFromVault ? "복원 완료" : "보내기 완료",
            removeFromVault
                ? $"폴더 항목 {exported}개를 원본 위치에 복원하고 금고에서 제거했습니다."
                : $"폴더 항목 {exported}개를 보냈습니다.",
            processedCount: exported,
            totalCount: members.Length);

        return new SecureVaultOperationResult
        {
            Success = true,
            Message = removeFromVault
                ? $"폴더 항목 {exported}개를 원본 위치에 복원하고 금고에서 제거했습니다."
                : $"폴더 항목 {exported}개를 보냈습니다.",
            ProcessedCount = exported
        };
    }

    private async Task<SecureVaultOperationResult> ExportSingleEntryAsync(
        SecureVaultManifestEntry entry,
        string destinationDirectory,
        CancellationToken cancellationToken,
        string? forceFileName = null)
    {
        var (allowed, reason) = PathSafetyGuard.Evaluate(destinationDirectory);
        if (!allowed)
        {
            return Fail(reason);
        }

        var normalizedDestination = SecureVaultPathHelper.NormalizeDirectory(destinationDirectory);
        Directory.CreateDirectory(normalizedDestination);
        SecureVaultOriginSealService.UnsealFolder(normalizedDestination);

        string target;
        string displayName;
        if (!string.IsNullOrWhiteSpace(forceFileName))
        {
            displayName = forceFileName;
            target = SecureVaultPathHelper.CombineUnderRoot(
                normalizedDestination,
                SecureVaultPathHelper.SanitizeFileName(forceFileName));
        }
        else
        {
            var label = DecryptLabel(entry);
            var relative = DecryptRelativePath(entry)
                ?? SecureVaultPathHelper.TryDeriveRelativeFromOriginal(normalizedDestination, DecryptOriginalPath(entry) ?? "")
                ?? label;
            displayName = relative;
            target = relative.Contains('/')
                ? SecureVaultPathHelper.CombineUnderRoot(normalizedDestination, relative)
                : SecureVaultPathHelper.CombineUnderRoot(
                    normalizedDestination,
                    SecureVaultPathHelper.SanitizeFileName(relative));
        }

        var targetDir = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            Directory.CreateDirectory(targetDir);
            SecureVaultOriginSealService.UnsealFolder(targetDir);
        }

        var plaintext = await DecryptEntryPayloadAsync(entry, cancellationToken);
        await File.WriteAllBytesAsync(target, plaintext, cancellationToken);
        SecureVaultCrypto.Zero(plaintext);
        AppendAudit("entry-exported", $"보내기 · {HashPath(displayName)}", _metadataKey!, _macKey!);
        return new SecureVaultOperationResult
        {
            Success = true,
            Message = $"보냈습니다: {displayName}",
            ProcessedCount = 1
        };
    }

    private async Task<byte[]> DecryptEntryPayloadAsync(SecureVaultManifestEntry entry, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var shardPath = GetPrimaryShardPath(entry.ShardName);
        if (!File.Exists(shardPath))
        {
            throw new FileNotFoundException("암호화 데이터가 없습니다.");
        }

        var dek = UnwrapDek(entry);
        var layered = UsesLayeredSecurity(entry, shardPath);
        var shardMacKey = layered
            ? SecureVaultCrypto.DeriveShardKey(_vaultKey!, entry.EntryId, "spd-shard-mac")
            : null;
        var blob = ReadEncryptedBlob(shardPath, shardMacKey);
        var aad = layered ? BuildAssociatedData(entry) : null;
        var padded = SecureVaultCrypto.Decrypt(dek, blob, aad);
        SecureVaultCrypto.Zero(dek);
        var plaintext = layered
            ? SecureVaultCrypto.Unpad(padded, (int)entry.OriginalSize)
            : padded;
        if (!string.Equals(SecureVaultCrypto.HashSha256Hex(plaintext), entry.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new CryptographicException("무결성 검증에 실패했습니다.");
        }

        return plaintext;
    }

    private SecureVaultOperationResult AddPayload(
        string label,
        byte[] plaintext,
        SecureVaultEntryKind kind,
        string? bundleId,
        string? relativePath,
        string? originalPath,
        bool sealOrigin,
        bool persistManifest = true,
        bool writeAudit = true)
    {
        // PRODUCT STABLE: layered AEAD shards under data/ (v3). Chunked objects/ is SecurityLab only.
        var entryId = Guid.NewGuid().ToString("N");
        var shardName = $"shard_{DateTimeOffset.Now:yyyyMMddHHmmssfff}_{entryId[..8]}.blob";
        var shardPath = Path.Combine(SecureVaultPaths.DataDirectory, shardName);
        var contentHash = SecureVaultCrypto.HashSha256Hex(plaintext);
        var originalSize = plaintext.LongLength;

        var dek = SecureVaultCrypto.GenerateKey();
        var shardDekKey = SecureVaultCrypto.DeriveShardKey(_vaultKey!, entryId, "spd-shard-dek");
        var shardMacKey = SecureVaultCrypto.DeriveShardKey(_vaultKey!, entryId, "spd-shard-mac");
        var padded = SecureVaultCrypto.PadWithRandom(plaintext);
        SecureVaultCrypto.Zero(plaintext);

        var manifestEntry = new SecureVaultManifestEntry
        {
            EntryId = entryId,
            EntryKind = kind switch
            {
                SecureVaultEntryKind.FolderMember => "folderMember",
                SecureVaultEntryKind.FolderRoot => "folderRoot",
                _ => "file"
            },
            BundleId = bundleId,
            ShardName = shardName,
            ContentSha256 = contentHash,
            OriginalSize = originalSize,
            AddedAt = DateTimeOffset.Now.ToString("o"),
            IsFolderBundle = bundleId is not null,
            IsSealedAtOrigin = sealOrigin,
            BlobFormat = SecureVaultCrypto.BlobFormatLayered
        };

        PopulateEncryptedFields(manifestEntry, nameof(manifestEntry.EncryptedLabel), label);
        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            manifestEntry.RelativePath = EncryptString(relativePath);
        }

        if (!string.IsNullOrWhiteSpace(originalPath))
        {
            PopulateEncryptedPath(manifestEntry, originalPath);
        }

        var aad = BuildAssociatedData(manifestEntry);
        var inner = SecureVaultCrypto.Encrypt(dek, padded, aad);
        SecureVaultCrypto.Zero(padded);
        var wrappedDek = SecureVaultCrypto.Encrypt(_vaultKey!, dek);
        var wrappedShardDek = SecureVaultCrypto.Encrypt(shardDekKey, dek);
        SecureVaultCrypto.Zero(dek);
        SecureVaultCrypto.Zero(shardDekKey);

        manifestEntry.DekWrapped = Convert.ToBase64String(wrappedDek.Ciphertext);
        manifestEntry.DekNonce = Convert.ToBase64String(wrappedDek.Nonce);
        manifestEntry.DekTag = Convert.ToBase64String(wrappedDek.Tag);
        manifestEntry.DekShardWrapped = Convert.ToBase64String(wrappedShardDek.Ciphertext);
        manifestEntry.DekShardNonce = Convert.ToBase64String(wrappedShardDek.Nonce);
        manifestEntry.DekShardTag = Convert.ToBase64String(wrappedShardDek.Tag);
        manifestEntry.ShardMac = Convert.ToHexString(
            SecureVaultCrypto.ComputeShardMac(shardMacKey, inner.Nonce, inner.Tag, inner.Ciphertext)).ToLowerInvariant();
        SecureVaultCrypto.Zero(shardMacKey);

        var shardBytes = SecureVaultCrypto.WriteLayeredShardBlob(
            SecureVaultCrypto.DeriveShardKey(_vaultKey!, entryId, "spd-shard-mac"),
            inner);
        File.WriteAllBytes(shardPath, shardBytes);
        WriteRedundantShardCopy(shardBytes, shardName);

        _manifest!.Entries.Add(manifestEntry);
        if (persistManifest)
        {
            SaveManifest(_manifest, _metadataKey!, _macKey!);
        }

        if (writeAudit)
        {
            AppendAudit("entry-added", $"추가 · {HashPath(label)}", _metadataKey!, _macKey!);
        }

        return new SecureVaultOperationResult
        {
            Success = true,
            Message = $"금고에 넣었습니다: {label}",
            ProcessedCount = 1,
            VaultFormat = _manifest.Format
        };
    }

    private void CryptoEraseEntryInternal(SecureVaultManifestEntry entry, bool unsealOrigin)
    {
        if (!string.IsNullOrWhiteSpace(entry.ShardName))
        {
            SecureEraseShardFiles(entry.ShardName);
        }

        _manifest!.Entries.Remove(entry);
        SaveManifest(_manifest, _metadataKey!, _macKey!);
        AppendAudit("entry-crypto-erased", $"키 파기 · {HashPath(entry.EntryId)}", _metadataKey!, _macKey!);

        if (!unsealOrigin)
        {
            return;
        }

        var origin = DecryptOriginalPath(entry);
        if (string.IsNullOrWhiteSpace(origin))
        {
            return;
        }

        if (entry.EntryKind == "folderRoot")
        {
            SecureVaultOriginSealService.UnsealFolder(origin);
        }
        else if (entry.EntryKind is "file" or "folderMember")
        {
            SecureVaultOriginSealService.UnsealFileStub(origin);
        }
    }

    private bool VerifyEntryIntegrity(SecureVaultManifestEntry entry) =>
        VerifyEntryIntegrityFromPath(entry, GetPrimaryShardPath(entry.ShardName));

    private bool VerifyEntryIntegrityFromPath(SecureVaultManifestEntry entry, string shardPath)
    {
        if (!File.Exists(shardPath))
        {
            return false;
        }

        try
        {
            var dek = UnwrapDek(entry);
            var layered = UsesLayeredSecurity(entry, shardPath);
            var shardMacKey = layered
                ? SecureVaultCrypto.DeriveShardKey(_vaultKey!, entry.EntryId, "spd-shard-mac")
                : null;
            var blob = ReadEncryptedBlob(shardPath, shardMacKey);
            var aad = layered ? BuildAssociatedData(entry) : null;
            var padded = SecureVaultCrypto.Decrypt(dek, blob, aad);
            SecureVaultCrypto.Zero(dek);
            var plaintext = layered
                ? SecureVaultCrypto.Unpad(padded, (int)entry.OriginalSize)
                : padded;
            var valid = string.Equals(SecureVaultCrypto.HashSha256Hex(plaintext), entry.ContentSha256, StringComparison.OrdinalIgnoreCase);
            SecureVaultCrypto.Zero(plaintext);
            return valid;
        }
        catch
        {
            return false;
        }
    }

    private static string GetPrimaryShardPath(string shardName) =>
        Path.Combine(SecureVaultPaths.DataDirectory, shardName);

    private static string GetRedundantShardPath(string shardName) =>
        Path.Combine(SecureVaultPaths.RedundantDataDirectory, shardName);

    private static void WriteRedundantShardCopy(string primaryShardPath, string shardName)
    {
        Directory.CreateDirectory(SecureVaultPaths.RedundantDataDirectory);
        File.Copy(primaryShardPath, GetRedundantShardPath(shardName), overwrite: true);
    }

    private static void WriteRedundantShardCopy(byte[] shardBytes, string shardName)
    {
        Directory.CreateDirectory(SecureVaultPaths.RedundantDataDirectory);
        File.WriteAllBytes(GetRedundantShardPath(shardName), shardBytes);
    }

    private static void SecureEraseShardFiles(string shardName)
    {
        SecureEraseShardAtPath(GetPrimaryShardPath(shardName));
        SecureEraseShardAtPath(GetRedundantShardPath(shardName));
    }

    private static void SecureEraseShardAtPath(string shardPath)
    {
        if (!File.Exists(shardPath))
        {
            return;
        }

        var storage = SecureDeleteStorageProfiler.Profile(shardPath);
        ForensicSecureDeleteEngine.SecureDeleteFileAsync(
            shardPath,
            storage,
            SecureDeleteSecurityLevel.Professional,
            CancellationToken.None).GetAwaiter().GetResult();
    }

    private byte[] UnwrapDek(SecureVaultManifestEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.DekShardWrapped))
        {
            var shardDekKey = SecureVaultCrypto.DeriveShardKey(_vaultKey!, entry.EntryId, "spd-shard-dek");
            try
            {
                var shardWrapped = new EncryptedBlob(
                    Convert.FromBase64String(entry.DekShardWrapped),
                    Convert.FromBase64String(entry.DekShardNonce ?? ""),
                    Convert.FromBase64String(entry.DekShardTag ?? ""));
                return SecureVaultCrypto.Decrypt(shardDekKey, shardWrapped);
            }
            finally
            {
                SecureVaultCrypto.Zero(shardDekKey);
            }
        }

        var wrapped = new EncryptedBlob(
            Convert.FromBase64String(entry.DekWrapped),
            Convert.FromBase64String(entry.DekNonce),
            Convert.FromBase64String(entry.DekTag));
        return SecureVaultCrypto.Decrypt(_vaultKey!, wrapped);
    }

    private SecureVaultEntry DecodeEntry(SecureVaultManifestEntry entry)
    {
        var kind = entry.EntryKind switch
        {
            "folderRoot" => SecureVaultEntryKind.FolderRoot,
            "folderMember" => SecureVaultEntryKind.FolderMember,
            "file" when !string.IsNullOrWhiteSpace(entry.BundleId) => SecureVaultEntryKind.FolderMember,
            _ when entry.IsFolderBundle && (entry.RelativePath is null && DecryptLabel(entry).Contains('/')) => SecureVaultEntryKind.LegacyFolderFile,
            _ when entry.IsFolderBundle && !string.IsNullOrWhiteSpace(entry.BundleId) => SecureVaultEntryKind.FolderMember,
            _ => SecureVaultEntryKind.StandaloneFile
        };

        return new SecureVaultEntry
        {
            EntryId = entry.EntryId,
            DisplayLabel = DecryptLabel(entry),
            ShardName = entry.ShardName,
            OriginalSize = entry.OriginalSize,
            AddedAt = DateTimeOffset.Parse(entry.AddedAt),
            IsFolderBundle = entry.IsFolderBundle,
            Kind = kind,
            BundleId = entry.BundleId,
            RelativePath = DecryptRelativePath(entry),
            OriginalPath = DecryptOriginalPath(entry),
            IsSealedAtOrigin = entry.IsSealedAtOrigin,
            BlobFormat = entry.BlobFormat
        };
    }

    private string DecryptLabel(SecureVaultManifestEntry entry)
    {
        var blob = new EncryptedBlob(
            Convert.FromBase64String(entry.EncryptedLabel),
            Convert.FromBase64String(entry.LabelNonce),
            Convert.FromBase64String(entry.LabelTag));
        return Encoding.UTF8.GetString(SecureVaultCrypto.Decrypt(_metadataKey!, blob));
    }

    private string? DecryptOriginalPath(SecureVaultManifestEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.EncryptedOriginalPath))
        {
            return null;
        }

        var blob = new EncryptedBlob(
            Convert.FromBase64String(entry.EncryptedOriginalPath),
            Convert.FromBase64String(entry.OriginalPathNonce ?? ""),
            Convert.FromBase64String(entry.OriginalPathTag ?? ""));
        return Encoding.UTF8.GetString(SecureVaultCrypto.Decrypt(_metadataKey!, blob));
    }

    private string? DecryptRelativePath(SecureVaultManifestEntry entry) =>
        DecryptRelativePathWithKey(entry, _metadataKey!);

    private static string? TryNormalizeStoredRelativePath(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        if (SecureVaultPathHelper.LooksLikeEncryptedRelativePath(stored)
            || SecureVaultPathHelper.ContainsInvalidPathCharacters(stored))
        {
            return null;
        }

        try
        {
            return SecureVaultPathHelper.NormalizeRelative(stored);
        }
        catch
        {
            return null;
        }
    }

    private string EncryptString(string value)
    {
        var blob = SecureVaultCrypto.Encrypt(_metadataKey!, Encoding.UTF8.GetBytes(value));
        return $"{Convert.ToBase64String(blob.Ciphertext)}|{Convert.ToBase64String(blob.Nonce)}|{Convert.ToBase64String(blob.Tag)}";
    }

    private void PopulateEncryptedFields(SecureVaultManifestEntry entry, string _, string value)
    {
        var blob = SecureVaultCrypto.Encrypt(_metadataKey!, Encoding.UTF8.GetBytes(value));
        entry.EncryptedLabel = Convert.ToBase64String(blob.Ciphertext);
        entry.LabelNonce = Convert.ToBase64String(blob.Nonce);
        entry.LabelTag = Convert.ToBase64String(blob.Tag);
    }

    private void PopulateEncryptedPath(SecureVaultManifestEntry entry, string path)
    {
        var blob = SecureVaultCrypto.Encrypt(_metadataKey!, Encoding.UTF8.GetBytes(path));
        entry.EncryptedOriginalPath = Convert.ToBase64String(blob.Ciphertext);
        entry.OriginalPathNonce = Convert.ToBase64String(blob.Nonce);
        entry.OriginalPathTag = Convert.ToBase64String(blob.Tag);
    }

    private byte[] BuildAssociatedData(SecureVaultManifestEntry entry) =>
        Encoding.UTF8.GetBytes($"{entry.EntryId}|{entry.ContentSha256}|v{entry.BlobFormat}");

    private string FindLastAddedEntryId() => _manifest!.Entries[^1].EntryId;

    private SecureVaultManifestEntry[] ResolveBundleMembers(string bundleId)
    {
        if (bundleId.StartsWith("legacy:", StringComparison.Ordinal))
        {
            var folderName = bundleId["legacy:".Length..];
            return _manifest!.Entries
                .Where(e => e.IsFolderBundle && e.EntryKind != "folderRoot" && DecryptLabel(e).StartsWith(folderName + "/", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        return _manifest!.Entries
            .Where(e => string.Equals(e.BundleId, bundleId, StringComparison.Ordinal)
                && !string.Equals(e.EntryKind, "folderRoot", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(e.ShardName))
            .ToArray();
    }

    private int CountBundleEntries(string bundleId)
    {
        if (bundleId.StartsWith("legacy:", StringComparison.Ordinal))
        {
            return ResolveBundleMembers(bundleId).Length;
        }

        return _manifest!.Entries.Count(e => string.Equals(e.BundleId, bundleId, StringComparison.Ordinal));
    }

    private string ResolveMemberRelativePath(
        SecureVaultManifestEntry member,
        string bundleId,
        string? bundleOriginDirectory = null)
    {
        var relative = DecryptRelativePath(member);
        if (string.IsNullOrWhiteSpace(relative) && !string.IsNullOrWhiteSpace(bundleOriginDirectory))
        {
            relative = SecureVaultPathHelper.TryDeriveRelativeFromOriginal(
                bundleOriginDirectory,
                DecryptOriginalPath(member) ?? "");
        }

        relative ??= TryNormalizeStoredRelativePath(DecryptLabel(member))
            ?? throw new InvalidOperationException(
                $"항목 {member.EntryId[..8]}의 경로 정보를 복구하지 못했습니다.");

        if (!bundleId.StartsWith("legacy:", StringComparison.Ordinal))
        {
            return SecureVaultPathHelper.SanitizeRelativePath(relative);
        }

        var folderName = bundleId["legacy:".Length..];
        if (relative.StartsWith(folderName + "/", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative[(folderName.Length + 1)..];
        }

        return SecureVaultPathHelper.SanitizeRelativePath(relative);
    }

    private SecureVaultManifestEntry[] ResolveBundleRemovalEntries(string bundleId)
    {
        if (bundleId.StartsWith("legacy:", StringComparison.Ordinal))
        {
            return ResolveBundleMembers(bundleId);
        }

        return _manifest!.Entries
            .Where(e => string.Equals(e.BundleId, bundleId, StringComparison.Ordinal)
                || string.Equals(e.EntryId, bundleId, StringComparison.Ordinal))
            .ToArray();
    }

    private async Task<SecureVaultOperationResult> RestoreBundleToOriginInternal(
        string bundleId,
        string origin,
        CancellationToken cancellationToken,
        IProgress<SecureVaultProgressReport>? progress = null)
    {
        var members = ResolveBundleMembers(bundleId);
        if (members.Length == 0)
        {
            return Fail("폴더에 복원할 파일이 없습니다.");
        }

        var normalizedOrigin = SecureVaultPathHelper.NormalizeDirectory(origin);
        try
        {
            var result = await ExportBundleInternalAsync(
                bundleId,
                normalizedOrigin,
                removeFromVault: true,
                cancellationToken,
                progress);
            if (!result.Success)
            {
                return result;
            }

            return new SecureVaultOperationResult
            {
                Success = true,
                Message = $"원본 위치에 복원했습니다: {normalizedOrigin} ({result.ProcessedCount}개 파일) · 금고에서 제거됨",
                ProcessedCount = result.ProcessedCount
            };
        }
        catch (Exception ex)
        {
            return Fail($"복원 실패: {ex.Message}");
        }
    }

    private sealed record BundleExportTarget(
        SecureVaultManifestEntry Member,
        string RelativePath,
        string TargetPath)
    {
        public string? TargetDirectory => Path.GetDirectoryName(TargetPath);
    }

    private static void ReportProgress(
        IProgress<SecureVaultProgressReport>? progress,
        SecureVaultProgressPhase phase,
        int percent,
        string title,
        string detail,
        string? currentItem = null,
        int processedCount = 0,
        int totalCount = 0)
    {
        progress?.Report(new SecureVaultProgressReport
        {
            Phase = phase,
            Percent = Math.Clamp(percent, 0, 100),
            Title = title,
            Detail = detail,
            CurrentItem = currentItem,
            ProcessedCount = processedCount,
            TotalCount = totalCount
        });
    }

    private static void PrepareRestoreTarget(string targetPath)
    {
        SecureVaultOriginSealService.UnsealFileStub(targetPath);
        if (!File.Exists(targetPath))
        {
            return;
        }

        try
        {
            File.SetAttributes(targetPath, FileAttributes.Normal);
            File.Delete(targetPath);
        }
        catch (IOException)
        {
            // WriteFileAtomicallyAsync will surface a clearer error if deletion still fails.
        }
    }

    private static async Task WriteFileAtomicallyAsync(string targetPath, byte[] content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = targetPath + ".spdrestore.tmp";
        if (File.Exists(tempPath))
        {
            File.SetAttributes(tempPath, FileAttributes.Normal);
            File.Delete(tempPath);
        }

        await File.WriteAllBytesAsync(tempPath, content, cancellationToken);
        if (File.Exists(targetPath))
        {
            File.SetAttributes(targetPath, FileAttributes.Normal);
            File.Delete(targetPath);
        }

        File.Move(tempPath, targetPath);
    }

    private static async Task<byte[]> ReadVaultSourceFileAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);
        if (stream.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var bytes = new byte[stream.Length];
        var read = 0;
        while (read < bytes.Length)
        {
            var chunk = await stream.ReadAsync(bytes.AsMemory(read), cancellationToken);
            if (chunk == 0)
            {
                break;
            }

            read += chunk;
        }

        if (read == bytes.Length)
        {
            return bytes;
        }

        return bytes.AsSpan(0, read).ToArray();
    }

    private void AbandonFolderAddBatch(int batchStartIndex, IReadOnlyList<string> createdShards)
    {
        while (_manifest!.Entries.Count > batchStartIndex)
        {
            _manifest.Entries.RemoveAt(_manifest.Entries.Count - 1);
        }

        foreach (var shardName in createdShards.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            SecureEraseShardFiles(shardName);
        }
    }

    private string? ResolveBundleOriginDirectory(string bundleId)
    {
        if (bundleId.StartsWith("legacy:", StringComparison.Ordinal))
        {
            return ResolveLegacyBundleOriginDirectory(bundleId);
        }

        var root = _manifest!.Entries.FirstOrDefault(e => e.BundleId == bundleId && e.EntryKind == "folderRoot");
        return root is null ? null : DecryptOriginalPath(root);
    }

    private string? ResolveLegacyBundleOriginDirectory(string bundleId)
    {
        var folderName = bundleId["legacy:".Length..];
        var members = ResolveBundleMembers(bundleId);
        string? origin = null;

        foreach (var member in members)
        {
            var original = DecryptOriginalPath(member);
            if (string.IsNullOrWhiteSpace(original))
            {
                continue;
            }

            var label = DecryptLabel(member);
            var relativeFromFolder = GetLegacyRelativePath(label, folderName);
            if (relativeFromFolder is null)
            {
                continue;
            }

            var fileDir = Path.GetDirectoryName(original);
            if (string.IsNullOrWhiteSpace(fileDir))
            {
                continue;
            }

            var relativeDir = Path.GetDirectoryName(relativeFromFolder.Replace('/', Path.DirectorySeparatorChar));
            var depth = string.IsNullOrEmpty(relativeDir)
                ? 0
                : relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length;

            var candidate = fileDir;
            for (var i = 0; i < depth; i++)
            {
                var parent = Path.GetDirectoryName(candidate);
                if (string.IsNullOrWhiteSpace(parent))
                {
                    break;
                }

                candidate = parent;
            }

            origin = origin is null
                ? candidate
                : GetLongestCommonDirectory(origin, candidate);
        }

        return origin;
    }

    private static string? GetLegacyRelativePath(string label, string folderName)
    {
        if (label.StartsWith(folderName + "/", StringComparison.OrdinalIgnoreCase))
        {
            return label[(folderName.Length + 1)..];
        }

        return string.Equals(label, folderName, StringComparison.OrdinalIgnoreCase) ? "" : null;
    }

    private static string GetLongestCommonDirectory(string left, string right)
    {
        var pathA = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var pathB = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var segmentsA = pathA.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var segmentsB = pathB.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var common = new List<string>();
        var count = Math.Min(segmentsA.Length, segmentsB.Length);
        for (var i = 0; i < count; i++)
        {
            if (!string.Equals(segmentsA[i], segmentsB[i], StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            common.Add(segmentsA[i]);
        }

        return common.Count == 0 ? pathA : string.Join(Path.DirectorySeparatorChar, common);
    }

    private static bool IsVaultArtifact(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".spd_vault_sealed", StringComparison.OrdinalIgnoreCase)
            || name.Equals("._spd_vault_folder.ico", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".spdvault.lock", StringComparison.OrdinalIgnoreCase)
            || name.Contains("보안 금고에 보관됨", StringComparison.Ordinal);
    }

    private void EnsureUnlocked()
    {
        if (State != SecureVaultState.Unlocked)
        {
            throw new InvalidOperationException("금고가 잠겨 있습니다.");
        }
    }

    private static void WriteMarker(VaultKdfParameters kdf)
    {
        var marker = new
        {
            format = "spd-vault-v3",
            product = "PC 케어 프로",
            createdAt = DateTimeOffset.Now.ToString("o"),
            kdfAlgorithm = kdf.Algorithm.ToString(),
            kdfIterations = kdf.Iterations,
            kdfMemoryKb = kdf.MemoryKb,
            kdfParallelism = kdf.Parallelism,
            features = new[] { "acl", "rate-limit", "audit-chain", "recovery-key", "shard-mac", "argon2id" }
        };
        File.WriteAllText(SecureVaultPaths.MarkerFile, JsonSerializer.Serialize(marker), Encoding.UTF8);
    }

    private static void WriteKeyEnvelope(byte[] salt, byte[] kek, byte[] vaultKey, VaultKdfParameters kdf)
    {
        var encrypted = SecureVaultCrypto.Encrypt(kek, vaultKey);
        using var ms = new MemoryStream();
        ms.Write(EnvelopeMagic);
        ms.Write(BitConverter.GetBytes(3));
        ms.WriteByte((byte)kdf.Algorithm);
        ms.Write(salt);
        ms.Write(BitConverter.GetBytes(kdf.Iterations));
        ms.Write(BitConverter.GetBytes(kdf.MemoryKb));
        ms.Write(BitConverter.GetBytes(kdf.Parallelism));
        ms.Write(encrypted.Nonce);
        ms.Write(encrypted.Tag);
        ms.Write(encrypted.Ciphertext);
        var protectedBytes = SecureVaultCrypto.ProtectWithDpapi(ms.ToArray());
        File.WriteAllBytes(SecureVaultPaths.KeyEnvelopeFile, protectedBytes);
    }

    private static (byte[] salt, byte[] vaultKey, byte[] metadataKey, byte[] macKey, VaultKdfParameters kdf) ReadKeyEnvelope(string password)
    {
        var protectedBytes = File.ReadAllBytes(SecureVaultPaths.KeyEnvelopeFile);
        var raw = SecureVaultCrypto.UnprotectWithDpapi(protectedBytes);
        using var ms = new MemoryStream(raw);
        var magic = new byte[EnvelopeMagic.Length];
        ms.ReadExactly(magic);
        if (!magic.SequenceEqual(EnvelopeMagic))
        {
            throw new CryptographicException("금고 형식이 올바르지 않습니다.");
        }

        var versionBytes = new byte[4];
        ms.ReadExactly(versionBytes);
        var version = BitConverter.ToInt32(versionBytes);
        VaultKdfParameters kdf;
        byte[] salt;
        if (version >= 3)
        {
            var algorithm = (VaultKdfAlgorithm)ms.ReadByte();
            salt = new byte[32];
            ms.ReadExactly(salt);
            var iterBytes = new byte[4];
            ms.ReadExactly(iterBytes);
            var iterations = BitConverter.ToInt32(iterBytes);
            var memoryBytes = new byte[4];
            ms.ReadExactly(memoryBytes);
            var memoryKb = BitConverter.ToInt32(memoryBytes);
            var parallelBytes = new byte[4];
            ms.ReadExactly(parallelBytes);
            var parallelism = BitConverter.ToInt32(parallelBytes);
            kdf = new VaultKdfParameters(algorithm, iterations, memoryKb, parallelism);
        }
        else
        {
            salt = new byte[32];
            ms.ReadExactly(salt);
            var iterBytes = new byte[4];
            ms.ReadExactly(iterBytes);
            var iterations = BitConverter.ToInt32(iterBytes);
            if (iterations <= 0)
            {
                iterations = SecureVaultCrypto.KdfIterationsLegacy;
            }

            kdf = VaultKdfParameters.LegacyPbkdf2(iterations);
        }

        var nonce = new byte[SecureVaultCrypto.NonceSize];
        ms.ReadExactly(nonce);
        var tag = new byte[SecureVaultCrypto.TagSize];
        ms.ReadExactly(tag);
        var cipherLength = (int)(ms.Length - ms.Position);
        var cipher = new byte[cipherLength];
        ms.ReadExactly(cipher);

        var kek = SecureVaultCrypto.DeriveKekWithParameters(password, salt, kdf);
        try
        {
            var vaultKey = SecureVaultCrypto.Decrypt(kek, new EncryptedBlob(cipher, nonce, tag));
            var metadataKey = SecureVaultCrypto.DeriveSubKey(kek, salt, "spd-vault-metadata");
            var macKey = SecureVaultCrypto.DeriveSubKey(kek, salt, "spd-vault-mac");
            return (salt, vaultKey, metadataKey, macKey, kdf);
        }
        finally
        {
            SecureVaultCrypto.Zero(kek);
        }
    }

    private static void WriteRecoveryEnvelope(
        byte[] recoveryKey,
        byte[] envelopeSalt,
        byte[] vaultKey,
        byte[] metadataKey,
        byte[] macKey,
        VaultKdfParameters kdf)
    {
        var recoverySalt = SecureVaultCrypto.GenerateSalt();
        var recoveryKek = SecureVaultCrypto.DeriveRecoveryKek(recoveryKey, recoverySalt);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            salt = Convert.ToBase64String(envelopeSalt),
            vaultKey = Convert.ToBase64String(vaultKey),
            metadataKey = Convert.ToBase64String(metadataKey),
            macKey = Convert.ToBase64String(macKey),
            kdfAlgorithm = (int)kdf.Algorithm,
            kdfIterations = kdf.Iterations,
            kdfMemoryKb = kdf.MemoryKb,
            kdfParallelism = kdf.Parallelism
        });
        var encrypted = SecureVaultCrypto.Encrypt(recoveryKek, payload);
        SecureVaultCrypto.Zero(recoveryKek);

        using var ms = new MemoryStream();
        ms.Write(RecoveryMagic);
        ms.Write(recoverySalt);
        ms.Write(encrypted.Nonce);
        ms.Write(encrypted.Tag);
        ms.Write(encrypted.Ciphertext);
        var protectedBytes = SecureVaultCrypto.ProtectWithDpapi(ms.ToArray());
        File.WriteAllBytes(SecureVaultPaths.RecoveryEnvelopeFile, protectedBytes);
    }

    private static RecoveryPayload ReadRecoveryEnvelope(byte[] recoveryKey)
    {
        var protectedBytes = File.ReadAllBytes(SecureVaultPaths.RecoveryEnvelopeFile);
        var raw = SecureVaultCrypto.UnprotectWithDpapi(protectedBytes);
        using var ms = new MemoryStream(raw);
        var magic = new byte[RecoveryMagic.Length];
        ms.ReadExactly(magic);
        if (!magic.SequenceEqual(RecoveryMagic))
        {
            throw new CryptographicException("복구 봉투 형식 오류");
        }

        var recoverySalt = new byte[32];
        ms.ReadExactly(recoverySalt);
        var nonce = new byte[SecureVaultCrypto.NonceSize];
        ms.ReadExactly(nonce);
        var tag = new byte[SecureVaultCrypto.TagSize];
        ms.ReadExactly(tag);
        var cipher = new byte[ms.Length - ms.Position];
        ms.ReadExactly(cipher);

        var recoveryKek = SecureVaultCrypto.DeriveRecoveryKek(recoveryKey, recoverySalt);
        try
        {
            var json = SecureVaultCrypto.Decrypt(recoveryKek, new EncryptedBlob(cipher, nonce, tag));
            var doc = JsonSerializer.Deserialize<RecoveryPayloadDto>(json, RecoveryPayloadJsonOptions)
                ?? throw new CryptographicException("복구 데이터 파싱 실패");
            var kdf = doc.KdfAlgorithm >= 0
                ? new VaultKdfParameters(
                    (VaultKdfAlgorithm)doc.KdfAlgorithm,
                    doc.KdfIterations,
                    doc.KdfMemoryKb,
                    doc.KdfParallelism)
                : VaultKdfParameters.LegacyPbkdf2(doc.KdfIterations);
            return new RecoveryPayload(
                Convert.FromBase64String(doc.Salt),
                Convert.FromBase64String(doc.VaultKey),
                Convert.FromBase64String(doc.MetadataKey),
                Convert.FromBase64String(doc.MacKey),
                kdf);
        }
        finally
        {
            SecureVaultCrypto.Zero(recoveryKek);
        }
    }

    private void ReencryptManifestMetadata(byte[] newMetadataKey, byte[] oldMetadataKey)
    {
        foreach (var entry in _manifest!.Entries)
        {
            var label = DecryptLabelWithKey(entry, oldMetadataKey);
            PopulateEncryptedFields(entry, nameof(entry.EncryptedLabel), label, newMetadataKey);

            var origin = DecryptOriginalPathWithKey(entry, oldMetadataKey);
            if (!string.IsNullOrWhiteSpace(origin))
            {
                PopulateEncryptedPath(entry, origin, newMetadataKey);
            }

            if (!string.IsNullOrWhiteSpace(entry.RelativePath) && entry.RelativePath.Contains('+'))
            {
                var relative = DecryptRelativePathWithKey(entry, oldMetadataKey);
                if (!string.IsNullOrWhiteSpace(relative))
                {
                    entry.RelativePath = EncryptStringWithKey(relative, newMetadataKey);
                }
            }
        }
    }

    private string DecryptLabelWithKey(SecureVaultManifestEntry entry, byte[] key)
    {
        var blob = new EncryptedBlob(
            Convert.FromBase64String(entry.EncryptedLabel),
            Convert.FromBase64String(entry.LabelNonce),
            Convert.FromBase64String(entry.LabelTag));
        return Encoding.UTF8.GetString(SecureVaultCrypto.Decrypt(key, blob));
    }

    private string? DecryptOriginalPathWithKey(SecureVaultManifestEntry entry, byte[] key)
    {
        if (string.IsNullOrWhiteSpace(entry.EncryptedOriginalPath))
        {
            return null;
        }

        var blob = new EncryptedBlob(
            Convert.FromBase64String(entry.EncryptedOriginalPath),
            Convert.FromBase64String(entry.OriginalPathNonce ?? ""),
            Convert.FromBase64String(entry.OriginalPathTag ?? ""));
        return Encoding.UTF8.GetString(SecureVaultCrypto.Decrypt(key, blob));
    }

    private string? DecryptRelativePathWithKey(SecureVaultManifestEntry entry, byte[] key)
    {
        if (string.IsNullOrWhiteSpace(entry.RelativePath))
        {
            return null;
        }

        var stored = entry.RelativePath;
        if (stored.Contains('|'))
        {
            var parts = stored.Split('|');
            if (parts.Length == 3)
            {
                try
                {
                    var blob = new EncryptedBlob(
                        Convert.FromBase64String(parts[0]),
                        Convert.FromBase64String(parts[1]),
                        Convert.FromBase64String(parts[2]));
                    var decrypted = Encoding.UTF8.GetString(SecureVaultCrypto.Decrypt(key, blob));
                    return TryNormalizeStoredRelativePath(decrypted) ?? decrypted.Replace('\\', '/').TrimStart('/');
                }
                catch
                {
                    return null;
                }
            }
        }

        return TryNormalizeStoredRelativePath(stored);
    }

    private void PopulateEncryptedFields(SecureVaultManifestEntry entry, string _, string value, byte[] metadataKey)
    {
        var blob = SecureVaultCrypto.Encrypt(metadataKey, Encoding.UTF8.GetBytes(value));
        entry.EncryptedLabel = Convert.ToBase64String(blob.Ciphertext);
        entry.LabelNonce = Convert.ToBase64String(blob.Nonce);
        entry.LabelTag = Convert.ToBase64String(blob.Tag);
    }

    private void PopulateEncryptedPath(SecureVaultManifestEntry entry, string path, byte[] metadataKey)
    {
        var blob = SecureVaultCrypto.Encrypt(metadataKey, Encoding.UTF8.GetBytes(path));
        entry.EncryptedOriginalPath = Convert.ToBase64String(blob.Ciphertext);
        entry.OriginalPathNonce = Convert.ToBase64String(blob.Nonce);
        entry.OriginalPathTag = Convert.ToBase64String(blob.Tag);
    }

    private string EncryptStringWithKey(string value, byte[] metadataKey)
    {
        var blob = SecureVaultCrypto.Encrypt(metadataKey, Encoding.UTF8.GetBytes(value));
        return $"{Convert.ToBase64String(blob.Ciphertext)}|{Convert.ToBase64String(blob.Nonce)}|{Convert.ToBase64String(blob.Tag)}";
    }

    private sealed record RecoveryPayload(byte[] Salt, byte[] VaultKey, byte[] MetadataKey, byte[] MacKey, VaultKdfParameters KdfParameters);

    private sealed class RecoveryPayloadDto
    {
        public string Salt { get; set; } = "";
        public string VaultKey { get; set; } = "";
        public string MetadataKey { get; set; } = "";
        public string MacKey { get; set; } = "";
        public int KdfAlgorithm { get; set; } = -1;
        public int KdfIterations { get; set; }
        public int KdfMemoryKb { get; set; }
        public int KdfParallelism { get; set; }
    }

    private static void SaveManifest(SecureVaultManifestDocument manifest, byte[] metadataKey, byte[] macKey)
    {
        manifest.ManifestMac = "";
        var json = SecureVaultManifestCodec.Save(manifest);
        manifest.ManifestMac = ComputeManifestMac(macKey, json);
        json = SecureVaultManifestCodec.Save(manifest);
        var encrypted = SecureVaultCrypto.Encrypt(metadataKey, json);
        WriteEncryptedBlob(SecureVaultPaths.ManifestFile, encrypted);
    }

    private static SecureVaultManifestDocument LoadManifest(byte[] metadataKey, byte[] macKey)
    {
        var blob = ReadEncryptedBlob(SecureVaultPaths.ManifestFile, shardMacKey: null);
        var json = SecureVaultCrypto.Decrypt(metadataKey, blob);
        var manifest = SecureVaultManifestCodec.Load(json);
        var expected = manifest.ManifestMac;
        if (!VerifyManifestMac(macKey, json, expected))
        {
            throw new CryptographicException("매니페스트 무결성 검증 실패");
        }

        manifest.ManifestMac = expected;
        return manifest;
    }

    private static bool VerifyManifestMac(byte[] macKey, byte[] signedJson, string expectedMac)
    {
        if (string.IsNullOrWhiteSpace(expectedMac))
        {
            return false;
        }

        var unsignedFromRaw = BuildUnsignedManifestJson(signedJson);
        var actualFromRaw = ComputeManifestMac(macKey, unsignedFromRaw);
        if (string.Equals(expectedMac, actualFromRaw, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var manifest = SecureVaultManifestCodec.Load(signedJson);
        manifest.ManifestMac = "";
        var unsignedFromModel = SecureVaultManifestCodec.Save(manifest);
        var actualFromModel = ComputeManifestMac(macKey, unsignedFromModel);
        return string.Equals(expectedMac, actualFromModel, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] BuildUnsignedManifestJson(byte[] signedJson)
    {
        var text = Encoding.UTF8.GetString(signedJson);
        var unsigned = Regex.Replace(text, "\"manifestMac\"\\s*:\\s*\"[^\"]*\"", "\"manifestMac\": \"\"");
        return Encoding.UTF8.GetBytes(unsigned);
    }

    private void ReapplyMissingOriginSeals()
    {
        foreach (var entry in _manifest!.Entries.Where(e => e.EntryKind == "folderRoot" && e.IsSealedAtOrigin))
        {
            var origin = DecryptOriginalPath(entry);
            if (string.IsNullOrWhiteSpace(origin) || !Directory.Exists(origin))
            {
                continue;
            }

            var markerPath = Path.Combine(origin, ".spd_vault_sealed");
            if (!File.Exists(markerPath))
            {
                SecureVaultOriginSealService.EnsureSealedFolderShell(origin, entry.BundleId!, entry.EntryId);
            }
        }
    }

    private bool RepairOrphanBundleRoots(SecureVaultManifestDocument manifest, byte[] metadataKey)
    {
        var rootBundleIds = manifest.Entries
            .Where(e => string.Equals(e.EntryKind, "folderRoot", StringComparison.Ordinal))
            .Select(e => e.BundleId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphanBundleIds = manifest.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.BundleId)
                && !string.IsNullOrWhiteSpace(e.ShardName)
                && !string.Equals(e.EntryKind, "folderRoot", StringComparison.Ordinal))
            .Select(e => e.BundleId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(bundleId => !rootBundleIds.Contains(bundleId))
            .ToArray();

        if (orphanBundleIds.Length == 0)
        {
            return false;
        }

        var changed = false;
        foreach (var bundleId in orphanBundleIds)
        {
            var members = manifest.Entries
                .Where(e => string.Equals(e.BundleId, bundleId, StringComparison.Ordinal)
                    && !string.Equals(e.EntryKind, "folderRoot", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(e.ShardName))
                .ToArray();
            if (members.Length == 0)
            {
                continue;
            }

            var memberOrigins = new List<string>();
            foreach (var member in members)
            {
                var path = DecryptOriginalPathWithKey(member, metadataKey);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    memberOrigins.Add(path);
                }
            }

            var origin = FindCommonRootDirectory(memberOrigins);
            if (string.IsNullOrWhiteSpace(origin))
            {
                continue;
            }

            var folderName = new DirectoryInfo(origin).Name;
            var rootEntry = new SecureVaultManifestEntry
            {
                EntryId = Guid.NewGuid().ToString("N"),
                EntryKind = "folderRoot",
                BundleId = bundleId,
                ShardName = "",
                AddedAt = DateTimeOffset.Now.ToString("o"),
                IsFolderBundle = true,
                IsSealedAtOrigin = members.Any(m => m.IsSealedAtOrigin),
                BlobFormat = SecureVaultCrypto.BlobFormatLayered
            };
            PopulateEncryptedFields(rootEntry, nameof(rootEntry.EncryptedLabel), folderName, metadataKey);
            PopulateEncryptedPath(rootEntry, origin, metadataKey);
            manifest.Entries.Add(rootEntry);
            changed = true;
        }

        return changed;
    }

    private static string? FindCommonRootDirectory(IEnumerable<string> filePaths)
    {
        string? commonDirectory = null;
        foreach (var filePath in filePaths)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            if (commonDirectory is null)
            {
                commonDirectory = directory;
                continue;
            }

            commonDirectory = GetCommonDirectoryPrefix(commonDirectory, directory);
            if (string.IsNullOrWhiteSpace(commonDirectory))
            {
                return null;
            }
        }

        return commonDirectory;
    }

    private static string? GetCommonDirectoryPrefix(string left, string right)
    {
        var leftParts = Path.GetFullPath(left).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rightParts = Path.GetFullPath(right).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var shared = new List<string>(Math.Min(leftParts.Length, rightParts.Length));
        for (var i = 0; i < Math.Min(leftParts.Length, rightParts.Length); i++)
        {
            if (!string.Equals(leftParts[i], rightParts[i], StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            shared.Add(leftParts[i]);
        }

        return shared.Count == 0 ? null : string.Join(Path.DirectorySeparatorChar, shared);
    }

    private static bool NormalizeLegacyManifest(SecureVaultManifestDocument manifest)
    {
        var changed = false;
        foreach (var entry in manifest.Entries)
        {
            if (entry.BlobFormat == 0)
            {
                entry.BlobFormat = SecureVaultCrypto.BlobFormatLegacy;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(entry.EntryKind))
            {
                entry.EntryKind = "file";
                changed = true;
            }

            if (!string.Equals(entry.EntryKind, "folderRoot", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(entry.BundleId)
                && !string.IsNullOrWhiteSpace(entry.ShardName)
                && !string.Equals(entry.EntryKind, "folderMember", StringComparison.Ordinal))
            {
                entry.EntryKind = "folderMember";
                changed = true;
            }
        }

        if (manifest.Format == "spd-vault-v1")
        {
            manifest.Format = "spd-vault-v2";
            changed = true;
        }

        return changed;
    }

    private static bool UsesLayeredSecurity(SecureVaultManifestEntry entry, string shardPath)
    {
        if (entry.BlobFormat >= SecureVaultCrypto.BlobFormatLayered)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(entry.DekShardWrapped))
        {
            return true;
        }

        if (!File.Exists(shardPath))
        {
            return false;
        }

        var bytes = File.ReadAllBytes(shardPath);
        return bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual("SPDSH2\0"u8);
    }

    private static string ComputeManifestMac(byte[] macKey, byte[] json)
    {
        using var hmac = new HMACSHA256(macKey);
        return Convert.ToHexString(hmac.ComputeHash(json)).ToLowerInvariant();
    }

    private static void WriteEncryptedBlob(string path, EncryptedBlob blob)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(blob.Nonce);
        fs.Write(blob.Tag);
        fs.Write(blob.Ciphertext);
    }

    private static EncryptedBlob ReadEncryptedBlob(string path, byte[]? shardMacKey)
    {
        var bytes = File.ReadAllBytes(path);
        return SecureVaultCrypto.ReadShardBlob(bytes, shardMacKey);
    }

    private void WriteRecoveryHint(string? hint, byte[] metadataKey)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            hint = hint ?? "비밀번호 분실 시 복구 불가",
            createdAt = DateTimeOffset.Now.ToString("o"),
            warning = "마스터 비밀번호는 저장되지 않습니다."
        });
        var encrypted = SecureVaultCrypto.Encrypt(metadataKey, payload);
        WriteEncryptedBlob(SecureVaultPaths.RecoveryHintFile, encrypted);
    }

    private void ReencryptAuditLog(byte[] oldMetadataKey, byte[] oldMacKey, byte[] newMetadataKey, byte[] newMacKey)
    {
        const int recordOverhead = SecureVaultCrypto.NonceSize + SecureVaultCrypto.TagSize;
        _auditChain.Clear();

        if (!File.Exists(SecureVaultPaths.AuditLogFile))
        {
            return;
        }

        var bytes = File.ReadAllBytes(SecureVaultPaths.AuditLogFile);
        if (bytes.Length == 0)
        {
            File.WriteAllBytes(SecureVaultPaths.AuditLogFile, Array.Empty<byte>());
            return;
        }

        var rebuilt = new List<byte[]>();
        var offset = 0;
        var previousHash = "GENESIS";

        while (offset + recordOverhead < bytes.Length)
        {
            var nonce = bytes.AsSpan(offset, SecureVaultCrypto.NonceSize).ToArray();
            offset += SecureVaultCrypto.NonceSize;
            var tag = bytes.AsSpan(offset, SecureVaultCrypto.TagSize).ToArray();
            offset += SecureVaultCrypto.TagSize;

            int cipherLength;
            if (offset + 4 <= bytes.Length)
            {
                var prefixedLength = BitConverter.ToInt32(bytes, offset);
                if (prefixedLength > 0 && offset + 4 + prefixedLength <= bytes.Length)
                {
                    offset += 4;
                    cipherLength = prefixedLength;
                }
                else
                {
                    cipherLength = bytes.Length - offset;
                }
            }
            else
            {
                cipherLength = bytes.Length - offset;
            }

            if (cipherLength <= 0)
            {
                throw new CryptographicException("감사 로그 재암호화 중 형식 오류");
            }

            var cipher = bytes.AsSpan(offset, cipherLength).ToArray();
            offset += cipherLength;

            var plaintext = SecureVaultCrypto.Decrypt(oldMetadataKey, new EncryptedBlob(cipher, nonce, tag));
            var text = Encoding.UTF8.GetString(plaintext);
            var parts = text.Split('|');
            if (parts.Length < 5)
            {
                throw new CryptographicException("감사 로그 재암호화 중 레코드 파싱 실패");
            }

            var line = string.Join('|', parts[..^1].Take(parts.Length - 2).Append(previousHash));
            using var hmac = new HMACSHA256(newMacKey);
            var newHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(line))).ToLowerInvariant();
            _auditChain.Add(newHash);
            previousHash = newHash;

            var encrypted = SecureVaultCrypto.Encrypt(newMetadataKey, Encoding.UTF8.GetBytes($"{line}|{newHash}"));
            var record = encrypted.Nonce
                .Concat(encrypted.Tag)
                .Concat(BitConverter.GetBytes(encrypted.Ciphertext.Length))
                .Concat(encrypted.Ciphertext)
                .ToArray();
            rebuilt.Add(record);
        }

        using (var stream = File.Create(SecureVaultPaths.AuditLogFile))
        {
            foreach (var record in rebuilt)
            {
                stream.Write(record);
            }
        }
    }

    private void AppendAudit(string action, string detail, byte[] metadataKey, byte[] macKey)
    {
        var previous = _auditChain.Count == 0 ? "GENESIS" : _auditChain[^1];
        var line = $"{DateTimeOffset.Now:O}|{action}|{detail}|{previous}";
        using var hmac = new HMACSHA256(macKey);
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(line))).ToLowerInvariant();
        _auditChain.Add(hash);
        var encrypted = SecureVaultCrypto.Encrypt(metadataKey, Encoding.UTF8.GetBytes($"{line}|{hash}"));
        var record = encrypted.Nonce
            .Concat(encrypted.Tag)
            .Concat(BitConverter.GetBytes(encrypted.Ciphertext.Length))
            .Concat(encrypted.Ciphertext)
            .ToArray();
        File.AppendAllBytes(SecureVaultPaths.AuditLogFile, record);
    }

    private static string HashPath(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..16];

    private static void SecureDeleteFile(string path) => SecureEraseShardAtPath(path);

    private static VaultKdfAlgorithm? ReadMarkerKdfAlgorithm()
    {
        try
        {
            if (!File.Exists(SecureVaultPaths.MarkerFile))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(SecureVaultPaths.MarkerFile));
            if (doc.RootElement.TryGetProperty("kdfAlgorithm", out var node)
                && Enum.TryParse<VaultKdfAlgorithm>(node.GetString(), out var algorithm))
            {
                return algorithm;
            }

            return VaultKdfAlgorithm.Pbkdf2Sha512;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsStrictAuditVault()
    {
        try
        {
            if (!File.Exists(SecureVaultPaths.MarkerFile))
            {
                return false;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(SecureVaultPaths.MarkerFile));
            if (doc.RootElement.TryGetProperty("format", out var format))
            {
                return string.Equals(format.GetString(), "spd-vault-v3", StringComparison.Ordinal);
            }
        }
        catch
        {
            // Legacy marker — lenient audit verification.
        }

        return false;
    }

    private static SecureVaultOperationResult Fail(string message) =>
        new() { Success = false, Message = message };
}