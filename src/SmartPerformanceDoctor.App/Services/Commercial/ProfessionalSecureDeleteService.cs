using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Text.Json;
using SmartPerformanceDoctor.App.Branding;
using SmartPerformanceDoctor.App.Models.Commercial;
using SmartPerformanceDoctor.SecurityLab.Hardening;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;
using SmartPerformanceDoctor.SecurityLab.ShredNext;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class ProfessionalSecureDeleteService
{
    private readonly string? _auditStorageRoot;
    internal Action<string>? TestCheckpoint { get; set; }

    // The default remains per-user LocalAppData. An explicit root is used by
    // isolated tests and recovery tooling so audit availability can be tested
    // without silently falling back to an unaudited delete.
    public ProfessionalSecureDeleteService(string? auditStorageRoot = null)
    {
        _auditStorageRoot = string.IsNullOrWhiteSpace(auditStorageRoot)
            ? null
            : Path.GetFullPath(auditStorageRoot);
    }

    public SecureDeletePlan PlanDryRun(
        IReadOnlyList<string> paths,
        SecureDeleteSecurityLevel level = SecureDeleteSecurityLevel.Professional)
    {
        var operationId = $"secure-delete-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var targets = new List<SecureDeleteTarget>();
        var blocked = new List<SecureDeleteTarget>();
        var useLab = ProductFeatureFlags.ShredNextEnabled;

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                blocked.Add(new SecureDeleteTarget { Path = path, Blocked = true, Risk = "blocked", BlockReason = "대상 없음 · 검사 누락" });
                continue;
            }

            var (allowed, reason) = PathSafetyGuard.Evaluate(path);
            // 50.3.0: SecurityLab path policy (UNC / special roots) as additional gate
            if (allowed && useLab)
            {
                var lab = LabSecurePath.Evaluate(path);
                if (!lab.Allowed)
                {
                    allowed = false;
                    reason = "Lab: " + lab.Reason;
                }
            }

            if (!allowed)
            {
                blocked.Add(new SecureDeleteTarget
                {
                    Path = path,
                    Type = Directory.Exists(path) ? "folder" : "file",
                    Size = 0,
                    StorageType = "Unknown",
                    FileSystem = "Unknown",
                    Risk = "blocked",
                    RecommendedProtocol = "blocked-before-profiling",
                    OverwritePasses = 0,
                    Blocked = true,
                    BlockReason = reason
                });
                continue;
            }

            var storage = SecureDeleteStorageProfiler.Profile(path);
            var fs = DetectFileSystem(path);
            var size = File.Exists(path) ? new FileInfo(path).Length : EstimateDirectorySize(path);
            var preflight = SecureDeletePreflight.Inspect(path, storage, fs, level, allowed, reason);
            if (!preflight.Passed)
            {
                blocked.Add(new SecureDeleteTarget
                {
                    Path = path,
                    Type = Directory.Exists(path) ? "folder" : "file",
                    Size = size,
                    StorageType = storage,
                    FileSystem = fs,
                    Risk = "blocked",
                    RecommendedProtocol = "blocked-by-preflight",
                    OverwritePasses = 0,
                    Blocked = true,
                    BlockReason = string.IsNullOrWhiteSpace(preflight.Summary) ? "사전 검사 실패" : preflight.Summary,
                    Preflight = preflight
                });
                continue;
            }
            var protocol = useLab && File.Exists(path)
                ? "lab.shred-next.v1+" + SecureDeleteStorageProfiler.SelectProtocol(storage, level)
                : SecureDeleteStorageProfiler.SelectProtocol(storage, level);
            var passes = SecureDeleteStorageProfiler.GetOverwritePasses(storage, level);

            string fingerprint;
            try
            {
                // A dry run is only actionable when its identity snapshot is complete.
                // Treat races, inaccessible children, and reparse points as a per-target
                // fail-closed result instead of aborting a multi-target plan.
                fingerprint = CaptureTargetFingerprint(path);
            }
            catch (Exception exception) when (exception is IOException
                                               or InvalidDataException
                                               or UnauthorizedAccessException
                                               or FileNotFoundException
                                               or DirectoryNotFoundException)
            {
                blocked.Add(new SecureDeleteTarget
                {
                    Path = path,
                    Type = Directory.Exists(path) ? "folder" : "file",
                    Size = size,
                    StorageType = storage,
                    FileSystem = fs,
                    Risk = "blocked",
                    RecommendedProtocol = "blocked-by-snapshot",
                    OverwritePasses = 0,
                    Blocked = true,
                    BlockReason = "실행 스냅샷을 안전하게 고정할 수 없어 삭제 대상을 차단했습니다: " + exception.Message,
                    Preflight = preflight
                });
                continue;
            }

            var target = new SecureDeleteTarget
            {
                Path = path,
                Type = Directory.Exists(path) ? "folder" : "file",
                Size = size,
                StorageType = storage,
                FileSystem = fs,
                Risk = allowed ? "high" : "blocked",
                RecommendedProtocol = protocol,
                OverwritePasses = passes,
                Blocked = !allowed,
                BlockReason = reason,
                TargetFingerprint = fingerprint,
                Preflight = preflight
            };

            targets.Add(target);
        }

        var primaryStorage = targets.Count == 0 ? "Unknown" : targets[0].StorageType;
        // A file-level Clear is never a certified sanitization or a device Purge.
        var maxCertified = targets.Count == 0 ? 0 : Math.Min(3,
            targets.Max(t => SecureDeleteStorageProfiler.EstimateCertifiedResistanceLevel(t.StorageType, level)));
        var maxIntensity = targets.Count == 0
            ? 0
            : targets.Max(t => SecureDeleteStorageProfiler.GetTechnicalDeletionIntensity(t.StorageType, level));
        var level5Certified = false;
        var disclaimer = SecureDeleteStorageProfiler.BuildResistanceDisclaimer(primaryStorage, level);
        var chainSummary = targets.Count == 0
            ? ""
            : string.Join(" → ", ForensicSecureDeleteEngine.BuildChainSteps(targets[0].StorageType, level));

        var recoveryRisk = maxCertified >= 4 && level5Certified
            ? "very-low"
            : maxCertified >= 3
                ? "low"
                : "storage-dependent";
        return new SecureDeletePlan
        {
            OperationId = operationId,
            Mode = "dry-run",
            SecurityLevel = level.ToString(),
            Targets = targets,
            BlockedTargets = blocked,
            RecoveryResistanceLevel = maxCertified,
            TechnicalDeletionIntensity = maxIntensity,
            Level5Certified = level5Certified,
            CertifiedResistanceLabel = targets.Count == 0 ? "대상 없음" : "인증 아님 · 파일 단위 Clear",
            ResistanceDisclaimer = disclaimer,
            ProfessionalRecoveryRisk = recoveryRisk,
            Limitations =
                "보안 삭제 원칙: (1) P01~P12 사전 검사 (2) dry-run (3) 확인 문구 " +
                $"「{AstraCareBranding.ShredIrreversibleConfirmation}」 (4) 저장장치별 체인 " +
                "(HDD 다중 패스 / SSD·미확인 매체 CSPRNG 덮어쓰기+표본 검증+TRIM 요청) (5) ADS 실제 열거·제거 (6) VSS 잔존 고지.\n" +

                "이 작업은 인증 또는 장치 Purge가 아닌 파일 단위 Clear입니다. 장치·컨트롤러 수준의 Sanitize/Purge는 별도 단계에서만 제공합니다.\n" +
                "금고 내부 데이터 삭제는 키 파기(crypto-shred)가 핵심입니다.\n" +
                "섀도 복사본/복구 지점/클라우드 사본은 자동 삭제하지 않습니다.\n" +
                (string.IsNullOrWhiteSpace(disclaimer) ? AstraCareBranding.ShredSsdLimitation : disclaimer),
            EstimatedDuration = TimeSpan.FromSeconds(Math.Max(
                12,
                targets.Sum(t =>
                    t.StorageType is "SSD" or "NVMe"
                        ? SecureDeleteStorageProfiler.GetFileClearPasses(t.StorageType, level) * 2 + 8
                        : Math.Max(1, t.OverwritePasses) * 3 + 6))).ToString(@"mm\:ss"),
            ChainSummary = chainSummary,
            RequestedPcLevel = "파일 단위 Clear",
            MaximumAchievablePcLevel = "파일 단위 Clear (장치 Purge 아님)",
            VerifyMatrix = targets.SelectMany(target => target.Preflight?.Checks ?? Array.Empty<SecureDeletePreflightCheck>()).ToArray()
        };
    }

    public async Task<(int Deleted, int Failed, string AuditPath, bool AuditValid)> ApplyAsync(
        SecureDeletePlan plan, string typedConfirmation, SecureDeleteSecurityLevel level = SecureDeleteSecurityLevel.Professional,
        IProgress<(int percent, string detail)>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = await ApplyDetailedAsync(plan, typedConfirmation, level, progress, cancellationToken);
        return (result.Deleted, result.Failed, result.AuditPath, result.AuditValid);
    }

    public async Task<SecureDeleteExecutionResult> ApplyDetailedAsync(
        SecureDeletePlan plan,
        string typedConfirmation,
        SecureDeleteSecurityLevel level = SecureDeleteSecurityLevel.Professional,
        IProgress<(int percent, string detail)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(plan.OperationId, @"^secure-delete-\d{8}-\d{6}-[0-9a-f]{32}$")
            || plan.SecurityLevel != level.ToString() || plan.Targets.Any(t => string.IsNullOrEmpty(t.TargetFingerprint)))
            throw new InvalidDataException("ValidationFailed");
        var confirm = typedConfirmation.Trim();
        var phraseOk =
            string.Equals(confirm, AstraCareBranding.ShredIrreversibleConfirmation, StringComparison.Ordinal)
            || string.Equals(confirm, AstraCareBranding.ShredConfirmation, StringComparison.Ordinal)
            || string.Equals(confirm, AstraCareBranding.LegacyShredConfirmation, StringComparison.Ordinal);
        if (!phraseOk)
        {
            throw new InvalidOperationException(
                $"확인 문구가 일치하지 않습니다. 다음 중 하나를 정확히 입력하세요: " +
                $"「{AstraCareBranding.ShredIrreversibleConfirmation}」 또는 「{AstraCareBranding.ShredConfirmation}」");
        }

        if (plan.Targets.Count == 0)
        {
            throw new InvalidOperationException("삭제 가능한 대상이 없습니다. dry-run 결과를 확인하세요.");
        }

        var auditRoot = _auditStorageRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PC-Care", "secure_delete");
        var auditDir = Path.Combine(auditRoot, "audit");
        SecureDeleteAuditChain.PrepareDirectory(auditDir);
        var auditPath = Path.Combine(auditDir, plan.OperationId + ".log");
        TestCheckpoint?.Invoke("BeforeJournalCreate");
        using var operationLock = new FileStream(auditPath + ".started", FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var key = SecureDeleteAuditChain.EnsureOperationKey(plan.OperationId, auditDir);
        var outcomes = new List<SecureDeleteItemResult>();
        var deleted = 0; var failed = 0; var cancelled = false; var review = false; var anyMutation = false;
        var chain = "GENESIS";
        try
        {
            using var journal = new SecureDeleteJournal(auditDir, plan.OperationId, key, TestCheckpoint);
            TestCheckpoint?.Invoke("JournalCreated");
            chain = SecureDeleteAuditChain.Append(auditPath, plan.OperationId, "n/a", "n/a", "file-clear", "started", chain, key);
            for (var i = 0; i < plan.Targets.Count; i++)
            {
                var target = plan.Targets[i]; DeleteHandleEvidence? bound = null;
                var mutation = false; var completed = false; var state = "Planned";
                var protocol = SecureDeleteStorageProfiler.SelectProtocol(target.StorageType, level);
                journal.Append(i, "Planned", target.Path, protocol: protocol, media: target.StorageType);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(((int)(i / (double)plan.Targets.Count * 99), Path.GetFileName(target.Path)));
                    if (!File.Exists(target.Path) && !Directory.Exists(target.Path)) throw new FileNotFoundException("TargetAbsent");
                    if (Directory.Exists(target.Path)) throw new InvalidDataException("FolderExecutionRequiresHandleBoundDirectoryRemoval");
                    var gate = PathSafetyGuard.Evaluate(target.Path);
                    var media = SecureDeleteStorageProfiler.Profile(target.Path);
                    protocol = SecureDeleteStorageProfiler.SelectProtocol(media, level);
                    var preflight = SecureDeletePreflight.Inspect(target.Path, media, DetectFileSystem(target.Path), level, gate.Allowed, gate.Reason);
                    if (!preflight.Passed) throw new InvalidDataException("PreflightChanged");
                    journal.Append(i, "PreflightVerified", target.Path, protocol: protocol, media: media);
                    state = "PreflightVerified";
                    await ForensicSecureDeleteEngine.SecureDeleteFileAsync(target.Path, media, level, cancellationToken,
                        target.TargetFingerprint, checkpoint => TestCheckpoint?.Invoke(checkpoint), (next, evidence) =>
                        {
                            if (next == "TargetHandleBound")
                            {
                                bound = evidence;
                                ReserveIdentity(auditDir, plan.OperationId, evidence.Identity);
                            }
                            journal.Append(i, next, target.Path, evidence, protocol, media);
                            state = next;
                            if (next == "DeletionObserved") deleted++;
                            if (next is "WriteInProgress" or "DeleteDispositionAccepted") { mutation = true; anyMutation = true; }
                        });
                    TestCheckpoint?.Invoke("BeforeAudit");
                    var content = SecureDeleteAuditChain.ProtectContentFingerprint(bound!.ContentHash, key);
                    chain = SecureDeleteAuditChain.Append(auditPath, plan.OperationId,
                        HashPath(target.Path) + ":" + bound.Identity, content, protocol, "deleted", chain, key);
                    TestCheckpoint?.Invoke("AfterAudit");
                    journal.Append(i, "AuditCommitted", target.Path, bound, protocol, media);
                    state = "AuditCommitted";
                    journal.Append(i, "Completed", target.Path, bound, protocol, media);
                    completed = true;
                    outcomes.Add(new("Completed", false) {TargetIndex=i, MutationStarted=mutation});
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    var terminal = mutation || state == "DestructiveWriteIntent" ? "RecoveryReviewRequired" : "CancelledBeforeMutation";
                    try { journal.Append(i, terminal, target.Path, bound, protocol, target.StorageType, "Cancellation"); }
                    catch { review = true; }
                    review |= terminal == "RecoveryReviewRequired";
                    outcomes.Add(new("Cancelled", mutation) {TargetIndex=i, MutationStarted=mutation});
                    break;
                }
                catch (Exception error)
                {
                    failed++; review |= mutation || error.Message.Contains("RequiresReview", StringComparison.Ordinal);
                    try { journal.Append(i, "RecoveryReviewRequired", target.Path, bound, protocol, target.StorageType, error.GetType().Name); }
                    catch { review = true; }
                    outcomes.Add(new(error is UnauthorizedAccessException ? "Unauthorized"
                        : error.Message.Contains("TargetChanged", StringComparison.Ordinal) ? "TargetChanged"
                        : error is InvalidDataException ? "ValidationFailed" : "Failed", mutation) {TargetIndex=i, MutationStarted=mutation});
                    if (review) break;
                }
                if (!completed && mutation) break;
            }
            var auditValid = SecureDeleteAuditChain.Verify(auditPath, key)
                && outcomes.Where(o => o.MutationStarted).All(o => o.Status == "Completed");
            review |= !auditValid;
            var status = review ? "RecoveryReviewRequired" : cancelled ? "Cancelled"
                : failed > 0 ? (deleted > 0 ? "PartiallyCompleted" : "Failed") : "Completed";
            var result = new SecureDeleteExecutionResult(deleted, failed, auditPath, auditValid, status, outcomes)
            {
                WasCancellationRequested = cancelled || cancellationToken.IsCancellationRequested,
                AnyMutationStarted = anyMutation, IncompleteTargets = plan.Targets.Count - outcomes.Count(o => o.Status == "Completed"),
                RequiresManualReview = review, JournalPath = journal.Path
            };
            try
            {
                journal.Append(-1, "AuditCommitted");
                TestCheckpoint?.Invoke("BeforeResultJson");
                var data = JsonSerializer.SerializeToUtf8Bytes(result);
                using (var output = SmartPerformanceDoctor.UpdateEngine.WindowsFileBoundary.OpenExclusive(auditDir, plan.OperationId + ".log.result.json", true))
                { output.Stream.Write(data); output.Stream.Flush(true); output.Validate(); }
                TestCheckpoint?.Invoke("AfterResultJson");
                journal.Append(-1, "Completed", error: "ResultSha:" + Convert.ToHexString(SHA256.HashData(data)));
            }
            catch { result = result with {Status="RecoveryReviewRequired", RequiresManualReview=true}; }
            if (result.Status == "Completed" && !result.RequiresManualReview) progress?.Report((100, "완료"));
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public IReadOnlyList<DeleteReconciliationResult> InspectInterruptedOperations()
    {
        var root = Path.Combine(_auditStorageRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PC-Care", "secure_delete"), "audit");
        if (!Directory.Exists(root)) return [];
        var results = new List<DeleteReconciliationResult>();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        foreach (var file in Directory.EnumerateFiles(root, "*.log.started").Take(32))
        {
            if (timer.ElapsedMilliseconds > 5000) break;
            var operation = Path.GetFileName(file)[..^12];
            try { var result = SecureDeleteJournal.Reconcile(root, operation); if (result.RequiresManualReview) results.Add(result); }
            catch (InvalidDataException) { results.Add(new(operation,"RecoveryReviewRequired",false,false,true,[])); }
        }
        return results;
    }

    private static void ReserveIdentity(string auditDir, string operation, string identity)
    {
        var root = Path.Combine(auditDir, "reservations"); SecureDeleteAuditChain.PrepareDirectory(root);
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))) + ".claim";
        try
        {
            using var file = SmartPerformanceDoctor.UpdateEngine.WindowsFileBoundary.OpenExclusive(root, name, true);
            file.Stream.Write(Encoding.UTF8.GetBytes(operation)); file.Stream.Flush(true); file.Validate();
        }
        catch (IOException error) { throw new InvalidDataException("PendingIdentityReservationRequiresReview", error); }
    }

    private static DirectoryExecutionSnapshot CaptureDirectorySnapshot(string directoryPath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
        var rootPrefix = root + Path.DirectorySeparatorChar;
        var pending = new Stack<string>();
        var directories = new List<string>();
        var files = new List<DirectoryExecutionFile>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!IsWithinRoot(current, root, rootPrefix))
            {
                throw new IOException("폴더 범위를 벗어난 항목이 감지되어 삭제를 중단했습니다.");
            }

            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("폴더에 재분석 지점이 있어 실행 스냅샷을 만들 수 없습니다.");
            }

            directories.Add(current);
            foreach (var file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!IsWithinRoot(file, root, rootPrefix)) throw new IOException("폴더 범위를 벗어난 파일이 감지되었습니다.");
                var fileAttributes = File.GetAttributes(file);
                if ((fileAttributes & FileAttributes.ReparsePoint) != 0) throw new IOException("재분석 지점 파일은 보안 삭제할 수 없습니다.");
                var (allowed, reason) = PathSafetyGuard.Evaluate(file);
                if (!allowed) throw new IOException("보호 경로가 감지되어 삭제를 중단했습니다: " + reason);
                files.Add(new DirectoryExecutionFile(file, CaptureFileFingerprint(file), SecureDeleteStorageProfiler.Profile(file)));
            }

            foreach (var child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!IsWithinRoot(child, root, rootPrefix)) throw new IOException("폴더 범위를 벗어난 하위 폴더가 감지되었습니다.");
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) throw new IOException("재분석 지점 폴더는 보안 삭제할 수 없습니다.");
                pending.Push(child);
            }
        }

        return new DirectoryExecutionSnapshot(directories, files);
    }

    private static void EnsureFileMatchesSnapshot(DirectoryExecutionFile file)
    {
        if (!File.Exists(file.Path)
            || (File.GetAttributes(file.Path) & FileAttributes.ReparsePoint) != 0
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(file.Fingerprint),
                Convert.FromHexString(CaptureFileFingerprint(file.Path))))
        {
            throw new IOException("실행 중 파일 식별 스냅샷이 바뀌어 폴더 삭제를 중단했습니다.");
        }
    }

    private static bool IsWithinRoot(string path, string root, string rootPrefix)
    {
        var full = Path.GetFullPath(path);
        return string.Equals(full, root, StringComparison.OrdinalIgnoreCase)
               || full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string CaptureTargetFingerprint(string path)
    {
        if (File.Exists(path)) return CaptureFileFingerprint(path);
        if (!Directory.Exists(path)) throw new FileNotFoundException("대상 경로를 찾을 수 없습니다.", path);

        var snapshot = CaptureDirectorySnapshot(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var directory in snapshot.Directories.OrderBy(value => value, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes("D|" + directory + "\n"));
        }
        foreach (var file in snapshot.Files.OrderBy(value => value.Path, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes("F|" + file.Path + "|" + file.Fingerprint + "\n"));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string CaptureFileFingerprint(string path) => ForensicSecureDeleteEngine.CaptureFingerprint(path);

    private sealed record DirectoryExecutionSnapshot(IReadOnlyList<string> Directories, IReadOnlyList<DirectoryExecutionFile> Files);
    private sealed record DirectoryExecutionFile(string Path, string Fingerprint, string StorageType);

    private static string ComputeTargetHash(string path)
    {
        if (!File.Exists(path))
        {
            return "dir:" + HashPath(path);
        }

        try
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        }
        catch
        {
            return "unreadable:" + HashPath(path);
        }
    }

    private static string HashPath(string path) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path))).ToLowerInvariant()[..16];

    private static string DetectFileSystem(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
            return new DriveInfo(root).DriveFormat;
        }
        catch
        {
            return "Unknown";
        }
    }

    private static long EstimateDirectorySize(string dir)
    {
        long total = 0;
        var pending = new Stack<string>();
        pending.Push(dir);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                        {
                            total = checked(total + info.Length);
                        }
                    }
                    catch (OverflowException)
                    {
                        return long.MaxValue;
                    }
                    catch
                    {
                        // Inaccessible files are excluded from the estimate.
                    }
                }

                foreach (var child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                {
                    pending.Push(child);
                }
            }
            catch
            {
                // Inaccessible branches are excluded from the estimate.
            }
        }

        return total;
    }
    private void WriteSecureDeleteReport(
        SecureDeletePlan plan,
        int deleted,
        int failed,
        string auditPath,
        bool auditValid,
        SecureDeleteSecurityLevel level)
    {
        var auditRoot = _auditStorageRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PC-Care",
            "secure_delete");
        var root = Path.Combine(auditRoot, "reports");
        Directory.CreateDirectory(root);
        var basePath = Path.Combine(root, plan.OperationId);
        var payload = new
        {
            plan.OperationId,
            protocol = "pc-care.file-clear.v7",
            securityLevel = level.ToString(),
            plan.RecoveryResistanceLevel,
            plan.TechnicalDeletionIntensity,
            plan.Level5Certified,
            plan.CertifiedResistanceLabel,
            plan.ResistanceDisclaimer,
            plan.ProfessionalRecoveryRisk,
            plan.ChainSummary,
            plan.Limitations,
            deleted,
            failed,
            auditPath,
            auditChainValid = auditValid,
            completedAt = DateTimeOffset.Now.ToString("o")
        };

        File.WriteAllText($"{basePath}.json", JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(
            $"{basePath}.html",
            $"""
            <html><head><meta charset="utf-8"/><title>Secure Delete Report</title></head>
            <body style="font-family:Segoe UI,sans-serif;padding:24px">
            <h1>PC Care 파일 단위 Clear 보고서</h1>
            <p><b>작업 ID:</b> {plan.OperationId}</p>
            <p><b>보안 등급:</b> {level}</p>
            <p><b>체인:</b> {plan.ChainSummary}</p>
            <p><b>삭제:</b> {deleted} · <b>실패:</b> {failed}</p>
            <p><b>작업 범위:</b> {plan.CertifiedResistanceLabel}</p>
            <p><b>기술 삭제 강도 Tier:</b> {plan.TechnicalDeletionIntensity}</p>
            <p><b>인증/장치 Purge:</b> 제공하지 않음 (파일 단위 Clear 보고서)</p>
            {(string.IsNullOrWhiteSpace(plan.ResistanceDisclaimer) ? "" : $"<p><b>고지:</b> {plan.ResistanceDisclaimer}</p>")}
            <p><b>감사 체인:</b> {(auditValid ? "정상" : "주의")}</p>
            <p><b>감사 로그:</b> {auditPath}</p>
            <p style="color:#666">{plan.Limitations}</p>
            </body></html>
            """);
    }
}
