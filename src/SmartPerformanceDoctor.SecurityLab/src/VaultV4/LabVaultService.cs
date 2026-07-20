using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// SecurityLab vault service — Phase2: spd-vault-v5-lab (Astra design subset).
/// Product App references via SecureVaultLabBackend.
/// </summary>
public sealed partial class LabVaultService : IDisposable
{
    public const string FormatId = "spd-vault-v4-lab";
    public const string FormatIdV5 = "spd-vault-v5-lab";

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _root;
    private readonly LabSessionPolicy _sessionPolicy;
    private readonly LabCryptoBroker _broker = new();
    private byte[]? _vmk; // session mirror; sealed with broker
    private byte[]? _metaKey;
    private byte[]? _wrapKey;
    private LabVaultHeader? _header;
    private LabVaultMetadata? _metadata;
    private DateTimeOffset _unlockedAt;
    private DateTimeOffset _lastActivity;
    private bool _writeAllowed = true;
    private LabSecurityState _securityState = LabSecurityState.Locked;

    public LabVaultService(string vaultRoot, LabSessionPolicy? sessionPolicy = null)
    {
        _root = Path.GetFullPath(vaultRoot);
        _sessionPolicy = sessionPolicy ?? LabSessionPolicy.Default;
    }

    public bool IsUnlocked => !_broker.IsSealed && _vmk is not null;
    public bool IsWriteAllowed => IsUnlocked && _writeAllowed && _broker.WriteAllowed;
    public LabSecurityState SecurityState => _securityState;
    public string Root => _root;
    public LabSessionPolicy SessionPolicy => _sessionPolicy;
    /// <summary>S-class: broker sealed ⇒ no key material available to UI.</summary>
    public bool CryptoBrokerSealed => _broker.IsSealed;

    public static bool Exists(string vaultRoot)
    {
        var root = Path.GetFullPath(vaultRoot);
        return File.Exists(Path.Combine(root, "vault.header.json"))
               && File.Exists(Path.Combine(root, "metadata.db.enc"));
    }

    public LabVaultCreateResult Create(string password, LabKdfProfile profile = LabKdfProfile.Strong)
    {
        var policy = LabPasswordPolicy.ValidateForCreate(password);
        if (!policy.IsValid)
        {
            return FailCreate(policy.Message);
        }

        if (Exists(_root))
        {
            return FailCreate("이미 금고가 있습니다.");
        }

        EnsureDirs();
        var kdf = LabKdfParams.FromProfile(profile);
        var salt = LabVaultCrypto.GenerateSalt();
        var vaultId = Guid.NewGuid().ToString("N");
        var kek = LabVaultCrypto.DeriveArgon2id(password, salt, kdf.Iterations, kdf.MemoryKb, kdf.Parallelism);
        var vmk = LabVaultCrypto.GenerateKey();
        try
        {
            var wrapped = WrapKey(kek, vmk, Encoding.UTF8.GetBytes("vmk:" + vaultId));
            var header = new LabVaultHeader
            {
                Magic = "AVLT5",
                Format = FormatIdV5,
                Version = 5,
                Generation = 1,
                VaultId = vaultId,
                CreatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                KdfIterations = kdf.Iterations,
                KdfMemoryKb = kdf.MemoryKb,
                KdfParallelism = kdf.Parallelism,
                ContentSuite = "XChaCha20-Poly1305",
                SaltHex = Convert.ToHexString(salt),
                WrappedVmkNonceHex = Convert.ToHexString(wrapped.Nonce),
                WrappedVmkTagHex = Convert.ToHexString(wrapped.Tag),
                WrappedVmkCipherHex = Convert.ToHexString(wrapped.Cipher)
            };
            WriteHeader(header);
            LabVaultLocator.Write(_root, new LabVaultLocator.Document
            {
                VaultId = vaultId,
                Format = FormatIdV5,
                Suite = "Argon2id+XChaCha20-Poly1305+AES-GCM-wrap+HKDF",
                CreatedUnix = header.CreatedUnix,
                HeaderCopyCount = 2,
                PackFixedSlots = true,
                Av3ProductionWriter = false // design: AV3 writer remains gated OFF
            });

            var metaKey = Hkdf(vmk, "lab/v4/meta");
            var wrapKey = Hkdf(vmk, "lab/v4/wrap");
            var metadata = new LabVaultMetadata { VaultId = vaultId, Version = 5, Generation = 1, Entries = new() };
            WriteMetadata(metadata, metaKey);
            LabDurableCommit.WriteCommitted(_root, vaultId, metadata.Generation);

            var (codes, _) = LabRecoverySlots.GenerateAndWrap(_root, vaultId, vmk);
            File.WriteAllText(
                Path.Combine(_root, "audit", "events.log"),
                $"{DateTimeOffset.UtcNow:o}|create|{vaultId}|v5|ok{Environment.NewLine}",
                Encoding.UTF8);
            LabAuditChain.Append(_root, "create", vaultId);
            LabRateLimiter.Reset(_root);
            RefreshIntegritySnapshot();

            CryptographicOperations.ZeroMemory(metaKey);
            CryptographicOperations.ZeroMemory(wrapKey);
            CryptographicOperations.ZeroMemory(vmk);

            return new LabVaultCreateResult
            {
                Success = true,
                Message = "보안 금고 v5 생성 완료 (locator·이중헤더·복구슬롯·저널). 복구 코드는 이 화면에만 표시됩니다.",
                VaultId = vaultId,
                Path = _root,
                RecoveryCodes = codes,
                Format = FormatIdV5,
                KdfProfile = profile.ToString()
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public LabVaultOperationResult Unlock(string password)
    {
        if (!Exists(_root))
        {
            return FailOp("금고가 없습니다.");
        }

        try
        {
            LabRateLimiter.EnsureNotLocked(_root);
        }
        catch (InvalidOperationException ex)
        {
            return FailOp(ex.Message);
        }

        LabVaultHeader header;
        try
        {
            header = ReadHeader();
        }
        catch (Exception ex)
        {
            return FailOp("헤더 손상: " + ex.Message);
        }

        var salt = Convert.FromHexString(header.SaltHex);
        var kek = LabVaultCrypto.DeriveArgon2id(
            password,
            salt,
            header.KdfIterations,
            header.KdfMemoryKb,
            header.KdfParallelism);
        try
        {
            var wrapped = new Wrapped(
                Convert.FromHexString(header.WrappedVmkNonceHex),
                Convert.FromHexString(header.WrappedVmkTagHex),
                Convert.FromHexString(header.WrappedVmkCipherHex));
            byte[] vmk;
            try
            {
                vmk = UnwrapKey(kek, wrapped, Encoding.UTF8.GetBytes("vmk:" + header.VaultId));
            }
            catch (CryptographicException)
            {
                LabRateLimiter.RecordFailure(_root);
                AppendAudit("unlock_fail", "bad_password");
                return FailOp("비밀번호가 올바르지 않거나 헤더가 손상되었습니다.");
            }

            return OpenSession(vmk, header, writeAllowed: true, auditKind: "unlock");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    /// <summary>Read-only unlock (design §8) — export allowed, import/shred blocked.</summary>
    public LabVaultOperationResult UnlockReadOnly(string password)
    {
        var r = Unlock(password);
        if (!r.Success)
        {
            return r;
        }

        _writeAllowed = false;
        _securityState = LabSecurityState.ReadOnlyUnlocked;
        AppendAudit("unlock_readonly", "ok");
        return new LabVaultOperationResult
        {
            Success = true,
            Message = r.Message + " · 읽기 전용",
            ProcessedCount = r.ProcessedCount,
            ReadOnly = true,
            SecurityState = _securityState.ToString()
        };
    }

    /// <summary>
    /// Recovery unlock with one-time code (Phase2) — unwraps VMK. Does NOT auto-wipe.
    /// </summary>
    public LabVaultOperationResult UnlockWithRecoveryCode(string recoveryCode)
    {
        if (!Exists(_root))
        {
            return FailOp("금고가 없습니다.");
        }

        try
        {
            LabRateLimiter.EnsureNotLocked(_root);
        }
        catch (InvalidOperationException ex)
        {
            return FailOp(ex.Message);
        }

        LabVaultHeader header;
        try
        {
            header = ReadHeader();
        }
        catch (Exception ex)
        {
            return FailOp("헤더 손상: " + ex.Message);
        }

        if (!LabRecoverySlots.TryUnwrapVmk(_root, header.VaultId, recoveryCode, out var vmk, out var msg)
            || vmk is null)
        {
            // fallback: legacy hash-only consume (v4 vaults)
            if (LabRecoveryCodes.TryConsume(_root, recoveryCode, out var legacyMsg))
            {
                AppendAudit("recovery_legacy", "proof_only");
                return FailOp(legacyMsg + " · v4 레거시 코드는 잠금 해제 불가. 비밀번호를 사용하세요.");
            }

            LabRateLimiter.RecordFailure(_root);
            AppendAudit("recovery_fail", "bad_or_used");
            return FailOp(msg);
        }

        try
        {
            var opened = OpenSession(vmk, header, writeAllowed: true, auditKind: "recovery_unlock");
            if (!opened.Success)
            {
                return opened;
            }

            // Design §8: recovery path opens a privileged session — force RecoveryAvailable + password change nudge
            _securityState = LabSecurityState.RecoveryAvailable;
            var snap = LabRecoverySlots.Snapshot(_root);
            AppendAudit("recovery_unlock", "remaining=" + snap.Remaining);
            return new LabVaultOperationResult
            {
                Success = true,
                Message = opened.Message
                          + " · 복구 세션"
                          + " · " + snap.ToUiLine()
                          + " · 비밀번호 변경을 권고합니다",
                ProcessedCount = opened.ProcessedCount,
                ReadOnly = false,
                SecurityState = _securityState.ToString()
            };
        }
        finally
        {
            // OpenSession takes ownership of vmk reference in field; do not zero here if success
        }
    }

    /// <summary>Legacy alias — now attempts VMK recovery unlock (Phase2).</summary>
    public LabVaultOperationResult ProveRecoveryCode(string recoveryCode) =>
        UnlockWithRecoveryCode(recoveryCode);

    public void Lock()
    {
        _broker.Seal();
        if (_vmk is not null)
        {
            CryptographicOperations.ZeroMemory(_vmk);
        }

        if (_metaKey is not null)
        {
            CryptographicOperations.ZeroMemory(_metaKey);
        }

        if (_wrapKey is not null)
        {
            CryptographicOperations.ZeroMemory(_wrapKey);
        }

        _vmk = null;
        _metaKey = null;
        _wrapKey = null;
        _header = null;
        _metadata = null;
        _unlockedAt = default;
        _lastActivity = default;
        _writeAllowed = true;
        _securityState = LabSecurityState.Locked;
    }

    public IReadOnlyList<LabVaultEntry> List()
    {
        EnsureUnlocked();
        return _metadata!.Entries
            .Select(e => new LabVaultEntry
            {
                EntryId = e.EntryId,
                ObjectId = e.ObjectId,
                DisplayName = e.DisplayName,
                RelativePath = e.RelativePath,
                Size = e.Size,
                ContentSha256 = e.ContentSha256,
                AddedAt = e.AddedAt
            })
            .ToArray();
    }

    public LabVaultOperationResult ImportFile(string sourcePath)
    {
        EnsureWriteUnlocked();
        if (!File.Exists(sourcePath))
        {
            return FailOp("파일을 찾을 수 없습니다.");
        }

        var fi = new FileInfo(sourcePath);
        if (fi.Length > _sessionPolicy.MaxImportBytes)
        {
            return FailOp($"파일 크기 한도 초과 (최대 {_sessionPolicy.MaxImportBytes} bytes).");
        }

        var sent = Policy.LabSentinelGate.EvaluateImport(fi.Length, _writeAllowed);
        if (Policy.LabSentinelGate.IsBlocking(sent))
        {
            return FailOp("정책 거부: " + sent);
        }

        // Phase2: stream encrypt for larger files (>= 1 MiB)
        if (fi.Length >= 1024 * 1024)
        {
            return ImportFileStreaming(sourcePath, fi.Length);
        }

        var data = File.ReadAllBytes(sourcePath);
        var name = Path.GetFileName(sourcePath);
        return ImportBytes(name, name, data);
    }

    public LabVaultOperationResult ImportBytes(string displayName, string relativePath, byte[] data)
    {
        EnsureWriteUnlocked();
        if (data.LongLength > _sessionPolicy.MaxImportBytes)
        {
            return FailOp($"페이로드 크기 한도 초과 (최대 {_sessionPolicy.MaxImportBytes} bytes).");
        }

        var entryId = Guid.NewGuid().ToString("N");
        var objectId = LabObjectStore.NewObjectId();
        var dataKey = LabVaultCrypto.GenerateKey();
        var gen = _header?.Generation ?? _metadata?.Generation ?? 1;
        var tx = LabVaultJournal.Begin(_root, "import:" + displayName, gen);
        _securityState = LabSecurityState.Importing;
        try
        {
            // S-class: bind generation into object AAD (design §6 context binding)
            var contentGen = Math.Max(1, (_metadata?.Generation ?? 0) + 1);
            var aad = LabCryptoBroker.BuildObjectAad(_header!.VaultId, entryId, contentGen);
            var suite = ResolveContentSuite();
            var conceal = suite == LabContentSuite.XChaCha20Poly1305 && data.Length > 0 && data.Length < LabVaultCrypto.ConcealedPadBlock;
            var blob = LabVaultCrypto.EncryptChunked(dataKey, data, aad, suite, conceal);
            LabObjectStore.Write(_root, objectId, blob);
            LabVaultJournal.Mark(_root, tx, LabVaultJournal.State.ObjectsReady, "obj:" + objectId, gen);

            // DEK wrap via broker-equivalent domain (dek:objectId) — keeps wrap AAD stable
            Wrapped wrappedDek;
            if (_broker.IsSealed)
            {
                wrappedDek = WrapKey(_wrapKey!, dataKey, Encoding.UTF8.GetBytes("dek:" + objectId));
            }
            else
            {
                var w = _broker.WrapDek(dataKey, objectId);
                wrappedDek = new Wrapped(w.Nonce, w.Tag, w.Cipher);
            }

            _metadata!.Entries.Add(new LabMetaEntry
            {
                EntryId = entryId,
                ObjectId = objectId,
                DisplayName = displayName,
                RelativePath = relativePath.Replace('\\', '/'),
                Size = data.LongLength,
                ContentSha256 = Convert.ToHexString(SHA256.HashData(data)),
                AddedAt = DateTimeOffset.UtcNow.ToString("o"),
                ContentGeneration = contentGen,
                DekNonceHex = Convert.ToHexString(wrappedDek.Nonce),
                DekTagHex = Convert.ToHexString(wrappedDek.Tag),
                DekCipherHex = Convert.ToHexString(wrappedDek.Cipher)
            });
            _metadata.Generation = gen;
            WriteMetadata(_metadata, _metaKey!);
            LabVaultJournal.Mark(_root, tx, LabVaultJournal.State.MetadataReady, entryId, gen);
            // durable flush order: objects → metadata → activation commit → journal Committed
            LabDurableCommit.WriteCommitted(_root, _header.VaultId, _metadata.Generation);
            LabVaultJournal.Mark(_root, tx, LabVaultJournal.State.Committed, entryId, gen);
            RefreshIntegritySnapshot();
            AppendAudit("import", entryId);
            _securityState = LabSecurityState.Unlocked;
            return new LabVaultOperationResult
            {
                Success = true,
                Message = $"가져오기 완료: {displayName}",
                EntryId = entryId,
                ProcessedCount = 1,
                SecurityState = _securityState.ToString()
            };
        }
        catch (Exception ex)
        {
            LabVaultJournal.Mark(_root, tx, LabVaultJournal.State.Aborted, displayName, gen);
            _securityState = LabSecurityState.Unlocked;
            return FailOp("가져오기 실패: " + ex.Message);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            CryptographicOperations.ZeroMemory(data);
        }
    }

    public LabVaultOperationResult ExportEntry(string entryId, string destDirectory, bool stepUpConfirmed = false)
    {
        EnsureUnlocked();
        var riskCount = _metadata?.Entries.Count ?? 0;
        var bulk = Policy.LabSentinelGate.EvaluateExport(riskCount, 0, _writeAllowed);
        if (bulk == Policy.LabSentinelDecision.Deny)
        {
            return FailOp("정책: 내보내기 거부 (대량 금고)");
        }

        if (bulk == Policy.LabSentinelDecision.RequireStepUp && !stepUpConfirmed)
        {
            return FailOp("정책: step-up 확인 필요 (금고 항목이 많습니다). stepUpConfirmed=true 후 재시도.");
        }

        if (bulk == Policy.LabSentinelDecision.DelayAndRateLimit)
        {
            return FailOp("정책: 잠시 후 다시 시도 (rate-limit)");
        }

        var destCheck = LabSecurePath.EvaluateExportDirectory(destDirectory, _root);
        if (!destCheck.Allowed)
        {
            return FailOp(destCheck.Reason);
        }

        var entry = _metadata!.Entries.FirstOrDefault(e => e.EntryId == entryId);
        if (entry is null)
        {
            return FailOp("항목이 없습니다.");
        }

        var plain = DecryptEntry(entry);
        try
        {
            Directory.CreateDirectory(destDirectory);
            var safe = Path.GetFileName(entry.DisplayName);
            if (string.IsNullOrWhiteSpace(safe))
            {
                safe = entryId + ".bin";
            }

            // neutralize path traversal in display name
            safe = safe.Replace("..", "_", StringComparison.Ordinal);
            var outPath = Path.Combine(destDirectory, safe);
            // S-class: atomic write + post-write content hash verify
            var tmp = outPath + ".spdlab.tmp";
            try
            {
                File.WriteAllBytes(tmp, plain);
                var writtenHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(tmp)));
                if (!LabCryptoCompare.FixedTimeEqualsHex(writtenHash, entry.ContentSha256))
                {
                    try { File.Delete(tmp); } catch { /* ignore */ }
                    return FailOp("내보내기 검증 실패: 콘텐츠 해시 불일치");
                }

                File.Move(tmp, outPath, overwrite: true);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignore */ }
                return FailOp("내보내기 실패: " + ex.Message);
            }

            AppendAudit("export", entryId);
            return new LabVaultOperationResult
            {
                Success = true,
                Message = $"내보내기: {outPath}",
                EntryId = entryId,
                ProcessedCount = 1
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>Crypto-shred: drop DEK wrap + destroy object file (lab).</summary>
    public LabVaultOperationResult CryptoShredEntry(string entryId)
    {
        EnsureWriteUnlocked();
        var decision = Policy.LabPolicyEngine.Evaluate(new Policy.LabPolicyRequest
        {
            Kind = Policy.LabActionKind.VaultCryptoShred,
            UserConfirmed = true,
            DryRunCompleted = true,
            TargetCount = 1
        });
        if (!decision.Allowed)
        {
            return FailOp(decision.Reason);
        }

        var idx = _metadata!.Entries.FindIndex(e => e.EntryId == entryId);
        if (idx < 0)
        {
            return FailOp("항목이 없습니다.");
        }

        var entry = _metadata.Entries[idx];
        _metadata.Entries.RemoveAt(idx);
        WriteMetadata(_metadata, _metaKey!);
        LabDurableCommit.WriteCommitted(_root, _header!.VaultId, _metadata.Generation);

        LabObjectStore.DeleteEverywhere(_root, entry.ObjectId);
        RefreshIntegritySnapshot();
        AppendAudit("crypto_shred", entryId);
        return new LabVaultOperationResult
        {
            Success = true,
            Message = "키 파기·객체 손상 삭제 완료",
            EntryId = entryId,
            ProcessedCount = 1
        };
    }

    public void Dispose() => Lock();

    private byte[] DecryptEntry(LabMetaEntry entry)
    {
        var nonce = Convert.FromHexString(entry.DekNonceHex);
        var tag = Convert.FromHexString(entry.DekTagHex);
        var cipher = Convert.FromHexString(entry.DekCipherHex);
        var dataKey = !_broker.IsSealed
            ? _broker.UnwrapDek(nonce, tag, cipher, entry.ObjectId)
            : UnwrapKey(_wrapKey!, new Wrapped(nonce, tag, cipher), Encoding.UTF8.GetBytes("dek:" + entry.ObjectId));
        try
        {
            var blob = LabObjectStore.Read(_root, entry.ObjectId);
            var gen = entry.ContentGeneration > 0 ? entry.ContentGeneration : 0;
            byte[] plain;
            try
            {
                if (gen > 0)
                {
                    var aad = LabCryptoBroker.BuildObjectAad(_header!.VaultId, entry.EntryId, gen);
                    plain = LabVaultCrypto.DecryptChunked(dataKey, blob, aad);
                }
                else
                {
                    var legacy = Encoding.UTF8.GetBytes($"lab-obj:{_header!.VaultId}:{entry.EntryId}");
                    plain = LabVaultCrypto.DecryptChunked(dataKey, blob, legacy);
                }
            }
            catch (CryptographicException) when (gen > 0)
            {
                // fallback legacy AAD
                var legacy = Encoding.UTF8.GetBytes($"lab-obj:{_header!.VaultId}:{entry.EntryId}");
                plain = LabVaultCrypto.DecryptChunked(dataKey, blob, legacy);
            }

            var hash = Convert.ToHexString(SHA256.HashData(plain));
            if (!LabCryptoCompare.FixedTimeEqualsHex(hash, entry.ContentSha256))
            {
                CryptographicOperations.ZeroMemory(plain);
                throw new CryptographicException("content hash mismatch");
            }

            return plain;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private void EnsureUnlocked()
    {
        if (_vmk is null || _metadata is null || _header is null || _metaKey is null || _wrapKey is null)
        {
            throw new InvalidOperationException("금고가 잠겨 있습니다.");
        }

        var now = DateTimeOffset.UtcNow;
        if (_sessionPolicy.IsMaxSessionExpired(_unlockedAt, now))
        {
            AppendAudit("session_expire", "max");
            _securityState = LabSecurityState.SessionExpired;
            Lock();
            throw new InvalidOperationException("최대 세션 시간 초과로 금고가 잠겼습니다. 다시 잠금 해제하세요.");
        }

        if (_sessionPolicy.IsIdleExpired(_unlockedAt, _lastActivity, now, _writeAllowed))
        {
            AppendAudit("session_expire", "idle");
            _securityState = LabSecurityState.SessionExpired;
            Lock();
            throw new InvalidOperationException("유휴 시간 초과로 금고가 잠겼습니다. 다시 잠금 해제하세요.");
        }

        if (_sessionPolicy.IsIdleWarning(_lastActivity, now, _writeAllowed))
        {
            _securityState = _writeAllowed
                ? LabSecurityState.AutoLockScheduled
                : LabSecurityState.ReadOnlyUnlocked;
        }
        else if (_securityState == LabSecurityState.AutoLockScheduled)
        {
            _securityState = _writeAllowed ? LabSecurityState.Unlocked : LabSecurityState.ReadOnlyUnlocked;
        }

        _lastActivity = now;
    }

    private void EnsureWriteUnlocked()
    {
        EnsureUnlocked();
        LabWriteGate.EnsureAllowed(vaultUnlocked: true, writeAllowed: _writeAllowed);
    }

    private LabVaultOperationResult OpenSession(byte[] vmk, LabVaultHeader header, bool writeAllowed, string auditKind)
    {
        Lock();
        // S-class: keys enter via CryptoBroker; session mirrors for existing helpers
        var vmkCopy = (byte[])vmk.Clone();
        try
        {
            _broker.Unseal(vmkCopy, header.VaultId, writeAllowed);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(vmk);
            throw;
        }

        _vmk = (byte[])vmk.Clone(); // keep session VMK for password-change rewrap
        CryptographicOperations.ZeroMemory(vmk);
        _metaKey = _broker.BorrowMetaKeyCopy();
        _wrapKey = _broker.BorrowWrapKeyCopy();
        _header = header;
        _writeAllowed = writeAllowed;
        try
        {
            _metadata = ReadMetadata(_metaKey, header.VaultId);
        }
        catch (Exception ex)
        {
            Lock();
            LabRateLimiter.RecordFailure(_root);
            return FailOp("메타데이터 복호화 실패: " + ex.Message);
        }

        // S-class: activation commit / generation / metadata digest
        var commit = LabDurableCommit.VerifyOnUnlock(
            _root,
            header.VaultId,
            header.Generation,
            _metadata.Generation);
        if (!commit.Ok)
        {
            Lock();
            AppendAudit("unlock_fail", "rollback_or_digest");
            return FailOp("무결성/rollback 의심: " + commit.Message);
        }

        LabRateLimiter.Reset(_root);
        _unlockedAt = DateTimeOffset.UtcNow;
        _lastActivity = _unlockedAt;
        _securityState = writeAllowed ? LabSecurityState.Unlocked : LabSecurityState.ReadOnlyUnlocked;
        if (commit.RollbackSuspected)
        {
            _securityState = LabSecurityState.RecoveryAvailable;
            AppendAudit("unlock_warn", "rollback_suspected");
        }

        AppendAudit(auditKind, "ok");
        var chainIssues = LabAuditChain.Verify(_root);
        if (chainIssues.Count > 0)
        {
            AppendAudit("audit_chain_warn", chainIssues.Count + " issues");
            _securityState = LabSecurityState.CorruptionDetected;
        }

        var recovered = RecoverIncompleteJournal();
        // after orphan recover, re-commit activation if needed
        if (recovered > 0 && writeAllowed)
        {
            LabDurableCommit.WriteCommitted(_root, header.VaultId, _metadata.Generation);
        }

        // S-class: purge loose object orphans not in metadata (crash leftovers)
        var orphanPurged = 0;
        if (writeAllowed && _metadata is not null)
        {
            var purge = LabOrphanScanner.Purge(_root, _metadata.Entries.Select(e => e.ObjectId));
            orphanPurged = purge.Purged;
            if (orphanPurged > 0)
            {
                AppendAudit("orphan_purge", orphanPurged + " loose");
            }
        }

        // S-class: v5 vault with missing activation after successful decrypt → self-heal (write path)
        var selfHealed = false;
        if (writeAllowed
            && LabDurableCommit.TryRead(_root) is null
            && string.Equals(header.Format, FormatIdV5, StringComparison.Ordinal))
        {
            LabDurableCommit.WriteCommitted(_root, header.VaultId, _metadata!.Generation);
            selfHealed = true;
            _securityState = LabSecurityState.RecoveryAvailable;
            AppendAudit("activation_self_heal", "rewrote missing marker after unlock");
        }

        // S-class: rewrite dual headers when primary is torn but a valid copy was used
        var headerHealed = false;
        if (writeAllowed && _header is not null && HeaderPrimaryNeedsRewrite())
        {
            WriteHeader(_header);
            LabDurableCommit.WriteCommitted(_root, header.VaultId, _metadata!.Generation);
            headerHealed = true;
            if (_securityState is not LabSecurityState.CorruptionDetected)
            {
                _securityState = LabSecurityState.RecoveryAvailable;
            }

            AppendAudit("header_self_heal", "rewrote dual headers from valid copy");
        }

        var incomplete = LabVaultJournal.ListIncomplete(_root);
        var suite = _header?.ContentSuite ?? "AES-256-GCM";
        var entryCount = _metadata?.Entries.Count ?? 0;
        var msg = $"잠금 해제 · 항목 {entryCount}개 · suite {suite}"
                  + (writeAllowed ? "" : " · 읽기 전용")
                  + (commit.RollbackSuspected ? " · rollback 감시" : " · commit OK")
                  + (selfHealed ? " · activation self-heal" : "")
                  + (headerHealed ? " · header self-heal" : "")
                  + (orphanPurged > 0 ? $" · orphan 정리 {orphanPurged}" : "")
                  + (chainIssues.Count > 0 ? $" · 감사체인 경고 {chainIssues.Count}" : "")
                  + (recovered > 0 ? $" · 저널 정리 {recovered}" : "")
                  + (incomplete.Count > 0 ? $" · 미완료 TX {incomplete.Count}" : "");
        return new LabVaultOperationResult
        {
            Success = true,
            Message = msg,
            ProcessedCount = entryCount,
            ReadOnly = !writeAllowed,
            SecurityState = _securityState.ToString()
        };
    }

    private LabContentSuite ResolveContentSuite()
    {
        var s = _header?.ContentSuite ?? "";
        if (s.Contains("XChaCha", StringComparison.OrdinalIgnoreCase))
        {
            return LabContentSuite.XChaCha20Poly1305;
        }

        // v5 default XChaCha even if field empty on older mid-builds
        if (string.Equals(_header?.Format, FormatIdV5, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_header?.Magic, "AVLT5", StringComparison.OrdinalIgnoreCase))
        {
            return LabContentSuite.XChaCha20Poly1305;
        }

        return LabContentSuite.Aes256Gcm;
    }

    /// <summary>Phase4: abort incomplete TX + shred orphan objects (obj:id in journal).</summary>
    private int RecoverIncompleteJournal()
    {
        var n = LabVaultJournal.RecoverOrphans(_root, _header?.Generation ?? 1);
        if (n > 0)
        {
            AppendAudit("journal_recover", n + " orphans");
        }

        return n;
    }

    private LabVaultOperationResult ImportFileStreaming(string sourcePath, long size)
    {
        EnsureWriteUnlocked();
        var name = Path.GetFileName(sourcePath);
        var entryId = Guid.NewGuid().ToString("N");
        var objectId = LabObjectStore.NewObjectId();
        var dataKey = LabVaultCrypto.GenerateKey();
        var gen = _header?.Generation ?? 1;
        var tx = LabVaultJournal.Begin(_root, "stream-import:" + name, gen);
        _securityState = LabSecurityState.Importing;
        try
        {
            // S-class: stream path uses same generation-bound AAD as ImportBytes
            var contentGen = Math.Max(1, (_metadata?.Generation ?? 0) + 1);
            var aad = LabCryptoBroker.BuildObjectAad(_header!.VaultId, entryId, contentGen);
            var objPath = LabObjectStore.AbsolutePath(_root, objectId);
            Directory.CreateDirectory(Path.GetDirectoryName(objPath)!);
            using (var input = File.OpenRead(sourcePath))
            using (var output = new FileStream(objPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                LabVaultCrypto.EncryptChunkedToFile(dataKey, input, output, aad, ResolveContentSuite());
            }

            // large stream stays loose; if cipher fits pack threshold, repack for concealment
            var cipherLen = new FileInfo(objPath).Length;
            if (cipherLen > 0 && cipherLen <= LabPackStore.PackThresholdBytes)
            {
                var cipher = File.ReadAllBytes(objPath);
                LabObjectStore.Write(_root, objectId, cipher);
                CryptographicOperations.ZeroMemory(cipher);
                try { File.Delete(objPath); } catch { /* ignore */ }
            }

            LabVaultJournal.Mark(_root, tx, LabVaultJournal.State.ObjectsReady, "obj:" + objectId, gen);

            string contentHash;
            using (var fs = File.OpenRead(sourcePath))
            {
                contentHash = Convert.ToHexString(SHA256.HashData(fs));
            }

            var wrappedDek = WrapKey(_wrapKey!, dataKey, Encoding.UTF8.GetBytes("dek:" + objectId));
            _metadata!.Entries.Add(new LabMetaEntry
            {
                EntryId = entryId,
                ObjectId = objectId,
                DisplayName = name,
                RelativePath = name,
                Size = size,
                ContentSha256 = contentHash,
                AddedAt = DateTimeOffset.UtcNow.ToString("o"),
                ContentGeneration = contentGen,
                DekNonceHex = Convert.ToHexString(wrappedDek.Nonce),
                DekTagHex = Convert.ToHexString(wrappedDek.Tag),
                DekCipherHex = Convert.ToHexString(wrappedDek.Cipher)
            });
            WriteMetadata(_metadata, _metaKey!);
            LabDurableCommit.WriteCommitted(_root, _header!.VaultId, _metadata.Generation);
            LabVaultJournal.Mark(_root, tx, LabVaultJournal.State.Committed, entryId, gen);
            RefreshIntegritySnapshot();
            AppendAudit("import_stream", entryId);
            _securityState = LabSecurityState.Unlocked;
            return new LabVaultOperationResult
            {
                Success = true,
                Message = $"스트리밍 가져오기 완료: {name}",
                EntryId = entryId,
                ProcessedCount = 1,
                SecurityState = _securityState.ToString()
            };
        }
        catch (Exception ex)
        {
            LabVaultJournal.Mark(_root, tx, LabVaultJournal.State.Aborted, name, gen);
            _securityState = LabSecurityState.Unlocked;
            return FailOp("스트리밍 import 실패: " + ex.Message);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private void RefreshIntegritySnapshot()
    {
        try
        {
            LabIntegrityManifest.Write(
                _root,
                LabIntegrityManifest.BuildForDirectory(Path.Combine(_root, "objects")));
            // also hash header
            var headerPath = Path.Combine(_root, "vault.header.json");
            if (File.Exists(headerPath))
            {
                var report = LabIntegrityManifest.BuildForDirectory(_root, "vault.header.*");
                var existing = Path.Combine(_root, "integrity", "critical.sha256.json");
                Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
                File.WriteAllText(
                    existing,
                    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch
        {
            // optional
        }
    }

    private void EnsureDirs()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "objects"));
        Directory.CreateDirectory(Path.Combine(_root, "recovery"));
        Directory.CreateDirectory(Path.Combine(_root, "audit"));
        Directory.CreateDirectory(Path.Combine(_root, "tombstones"));
        Directory.CreateDirectory(Path.Combine(_root, "integrity"));
        Directory.CreateDirectory(Path.Combine(_root, "journal"));
        Directory.CreateDirectory(Path.Combine(_root, "packs"));
    }

    private void WriteHeader(LabVaultHeader header)
    {
        // Dual-copy authenticated headers (design §5 subset) + generation
        var json = JsonSerializer.SerializeToUtf8Bytes(header, JsonOpts);
        LabParserGuard.EnsureHeaderSize(json.Length);
        var hash = Convert.ToHexString(SHA256.HashData(json));
        File.WriteAllBytes(Path.Combine(_root, "vault.header.json"), json);
        File.WriteAllText(Path.Combine(_root, "vault.header.blake3"), hash);
        File.WriteAllBytes(Path.Combine(_root, "vault.header.copy1.json"), json);
        File.WriteAllText(Path.Combine(_root, "vault.header.copy1.sha256"), hash);
        File.WriteAllBytes(Path.Combine(_root, "vault.header.backup.json"), json);
    }

    /// <summary>True when primary header file is missing, truncated, or hash-mismatched.</summary>
    private bool HeaderPrimaryNeedsRewrite()
    {
        var path = Path.Combine(_root, "vault.header.json");
        if (!File.Exists(path))
        {
            return true;
        }

        try
        {
            var json = File.ReadAllBytes(path);
            LabParserGuard.EnsureHeaderSize(json.Length);
            var hashSide = Path.Combine(_root, "vault.header.blake3");
            if (File.Exists(hashSide))
            {
                var expected = File.ReadAllText(hashSide).Trim();
                var actual = Convert.ToHexString(SHA256.HashData(json));
                if (!LabCryptoCompare.FixedTimeEqualsHex(expected, actual))
                {
                    return true;
                }
            }

            // must deserialize as header
            return JsonSerializer.Deserialize<LabVaultHeader>(json) is null;
        }
        catch
        {
            return true;
        }
    }

    private LabVaultHeader ReadHeader()
    {
        // Prefer highest generation among integrity-valid copies
        LabVaultHeader? best = null;
        foreach (var name in new[] { "vault.header.json", "vault.header.copy1.json", "vault.header.backup.json" })
        {
            var path = Path.Combine(_root, name);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllBytes(path);
                LabParserGuard.EnsureHeaderSize(json.Length);
                var hashSide = name switch
                {
                    "vault.header.copy1.json" => Path.Combine(_root, "vault.header.copy1.sha256"),
                    _ => Path.Combine(_root, "vault.header.blake3")
                };
                if (File.Exists(hashSide))
                {
                    var expected = File.ReadAllText(hashSide).Trim();
                    var actual = Convert.ToHexString(SHA256.HashData(json));
                    if (!LabCryptoCompare.FixedTimeEqualsHex(expected, actual)
                        && name != "vault.header.backup.json")
                    {
                        continue;
                    }
                }

                var h = JsonSerializer.Deserialize<LabVaultHeader>(json);
                if (h is null)
                {
                    continue;
                }

                if (best is null || h.Generation >= best.Generation)
                {
                    best = h;
                }
            }
            catch
            {
                // try next copy
            }
        }

        return best ?? throw new CryptographicException("header integrity mismatch / no valid copy");
    }

    private void WriteMetadata(LabVaultMetadata metadata, byte[] metaKey)
    {
        LabParserGuard.EnsureEntryCount(metadata.Entries.Count);
        metadata.Generation = Math.Max(1, metadata.Generation) + 1;
        if (_header is not null)
        {
            _header.Generation = Math.Max(_header.Generation, metadata.Generation);
            // persist generation into dual headers so unlock generation checks stay consistent
            try
            {
                WriteHeader(_header);
            }
            catch
            {
                // best effort — commit marker still carries metadata digest
            }
        }

        var plain = JsonSerializer.SerializeToUtf8Bytes(metadata);
        LabParserGuard.EnsureMetadataSize(plain.Length);
        try
        {
            var aad = Encoding.UTF8.GetBytes("lab-meta-v4:" + metadata.VaultId);
            var nonce = RandomNumberGenerator.GetBytes(LabVaultCrypto.NonceSize);
            var cipher = new byte[plain.Length];
            var tag = new byte[LabVaultCrypto.TagSize];
            using var gcm = new AesGcm(metaKey, LabVaultCrypto.TagSize);
            gcm.Encrypt(nonce, plain, cipher, tag, aad);
            using var ms = new MemoryStream();
            ms.Write(nonce);
            ms.Write(tag);
            ms.Write(cipher);
            var enc = ms.ToArray();
            LabParserGuard.EnsureMetadataSize(enc.Length);
            File.WriteAllBytes(Path.Combine(_root, "metadata.db.enc"), enc);
            // note: caller should LabDurableCommit.WriteCommitted after successful write path
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private LabVaultMetadata ReadMetadata(byte[] metaKey, string vaultId)
    {
        var blob = File.ReadAllBytes(Path.Combine(_root, "metadata.db.enc"));
        LabParserGuard.EnsureMetadataSize(blob.Length);
        if (blob.Length < LabVaultCrypto.NonceSize + LabVaultCrypto.TagSize)
        {
            throw new CryptographicException("metadata too short");
        }

        var nonce = blob.AsSpan(0, LabVaultCrypto.NonceSize).ToArray();
        var tag = blob.AsSpan(LabVaultCrypto.NonceSize, LabVaultCrypto.TagSize).ToArray();
        var cipher = blob.AsSpan(LabVaultCrypto.NonceSize + LabVaultCrypto.TagSize).ToArray();
        var plain = new byte[cipher.Length];
        var aad = Encoding.UTF8.GetBytes("lab-meta-v4:" + vaultId);
        using var gcm = new AesGcm(metaKey, LabVaultCrypto.TagSize);
        gcm.Decrypt(nonce, cipher, tag, plain, aad);
        var meta = JsonSerializer.Deserialize<LabVaultMetadata>(plain)
                   ?? throw new InvalidOperationException("metadata parse failed");
        CryptographicOperations.ZeroMemory(plain);
        if (!string.Equals(meta.VaultId, vaultId, StringComparison.Ordinal))
        {
            throw new CryptographicException("vault id mismatch");
        }

        LabParserGuard.EnsureEntryCount(meta.Entries.Count);
        return meta;
    }

    private void AppendAudit(string kind, string subject)
    {
        try
        {
            var line = $"{DateTimeOffset.UtcNow:o}|{kind}|{subject}|ok{Environment.NewLine}";
            File.AppendAllText(Path.Combine(_root, "audit", "events.log"), line, Encoding.UTF8);
            LabAuditChain.Append(_root, kind, subject);
        }
        catch
        {
            // best effort
        }
    }

    private static void DamageAndDelete(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var len = new FileInfo(path).Length;
            if (len > 0)
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
                var buf = new byte[Math.Min(4096, (int)Math.Min(int.MaxValue, len))];
                RandomNumberGenerator.Fill(buf);
                fs.Write(buf, 0, buf.Length);
                fs.SetLength(0);
                fs.Flush(true);
                CryptographicOperations.ZeroMemory(buf);
            }

            File.Delete(path);
        }
        catch
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    private static Wrapped WrapKey(byte[] wrappingKey, byte[] plainKey, byte[] aad)
    {
        var nonce = RandomNumberGenerator.GetBytes(LabVaultCrypto.NonceSize);
        var cipher = new byte[plainKey.Length];
        var tag = new byte[LabVaultCrypto.TagSize];
        using var gcm = new AesGcm(wrappingKey, LabVaultCrypto.TagSize);
        gcm.Encrypt(nonce, plainKey, cipher, tag, aad);
        return new Wrapped(nonce, tag, cipher);
    }

    private static byte[] UnwrapKey(byte[] wrappingKey, Wrapped wrapped, byte[] aad)
    {
        var plain = new byte[wrapped.Cipher.Length];
        using var gcm = new AesGcm(wrappingKey, LabVaultCrypto.TagSize);
        gcm.Decrypt(wrapped.Nonce, wrapped.Cipher, wrapped.Tag, plain, aad);
        return plain;
    }

    private static byte[] Hkdf(byte[] ikm, string info)
    {
        // Simple HKDF-Extract/Expand via HMAC-SHA256 (single-block expand)
        var salt = new byte[32];
        using var extract = new HMACSHA256(salt);
        var prk = extract.ComputeHash(ikm);
        using var expand = new HMACSHA256(prk);
        var infoBytes = Encoding.UTF8.GetBytes(info);
        var input = new byte[infoBytes.Length + 1];
        Buffer.BlockCopy(infoBytes, 0, input, 0, infoBytes.Length);
        input[^1] = 1;
        var okm = expand.ComputeHash(input);
        CryptographicOperations.ZeroMemory(prk);
        return okm;
    }

    private static LabVaultCreateResult FailCreate(string msg) =>
        new() { Success = false, Message = msg };

    private static LabVaultOperationResult FailOp(string msg) =>
        new() { Success = false, Message = msg };

    private sealed record Wrapped(byte[] Nonce, byte[] Tag, byte[] Cipher);

    private sealed class LabVaultHeader
    {
        public string Magic { get; set; } = "";
        public string Format { get; set; } = "";
        public int Version { get; set; }
        public long Generation { get; set; } = 1;
        public string VaultId { get; set; } = "";
        public long CreatedUnix { get; set; }
        public int KdfIterations { get; set; }
        public int KdfMemoryKb { get; set; }
        public int KdfParallelism { get; set; }
        /// <summary>Content AEAD suite: XChaCha20-Poly1305 (v5 default) or AES-256-GCM (v4).</summary>
        public string ContentSuite { get; set; } = "XChaCha20-Poly1305";
        public string SaltHex { get; set; } = "";
        public string WrappedVmkNonceHex { get; set; } = "";
        public string WrappedVmkTagHex { get; set; } = "";
        public string WrappedVmkCipherHex { get; set; } = "";
    }

    private sealed class LabVaultMetadata
    {
        public string VaultId { get; set; } = "";
        public int Version { get; set; }
        public long Generation { get; set; } = 1;
        public List<LabMetaEntry> Entries { get; set; } = new();
    }

    private sealed class LabMetaEntry
    {
        public string EntryId { get; set; } = "";
        public string ObjectId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public long Size { get; set; }
        public string ContentSha256 { get; set; } = "";
        public string AddedAt { get; set; } = "";
        /// <summary>Object AAD generation binding; 0 = legacy lab-obj:vault:entry only.</summary>
        public long ContentGeneration { get; set; }
        public string DekNonceHex { get; set; } = "";
        public string DekTagHex { get; set; } = "";
        public string DekCipherHex { get; set; } = "";
    }
}
