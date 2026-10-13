using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

public sealed record LabTransitionResult(string Status, bool Completed, long SourceBytes,
    IReadOnlyDictionary<string, string> SourceHashes, IReadOnlyDictionary<string, string> ContentHashes,
    IReadOnlyList<string> FreshRecoveryCodes, string Message);

/// <summary>Build an explicit synthetic v6 copy. Never open the source vault for mutation.</summary>
public static class LabV5Transition
{
    public static LabTransitionResult ExecuteSynthetic(string source, string workspace, string password,
        bool consent, CancellationToken token = default, Action<string>? fault = null, bool salvageReadOnly = false,
        string? authenticatedHeaderBackup = null)
    {
        if (!consent) throw new InvalidOperationException("EXPLICIT-TEST-CONSENT-REQUIRED");
        source = Path.GetFullPath(source); workspace = Path.GetFullPath(workspace);
        var fixture = Path.GetDirectoryName(source)!;
        var backupRecovery = salvageReadOnly && authenticatedHeaderBackup is not null;
        if (!(Path.GetFileName(fixture).StartsWith("wo14-vault-fixture-", StringComparison.Ordinal)
              || backupRecovery && Path.GetFileName(fixture).StartsWith("wo15-vault-fixture-", StringComparison.Ordinal))
            || Path.GetDirectoryName(workspace) != fixture || !Path.GetFileName(workspace).StartsWith("work-", StringComparison.Ordinal))
            throw new InvalidOperationException("SYNTHETIC-FIXTURE-REQUIRED");
        using var marker = VaultFileBoundary.OpenRead(fixture, "fixture.marker");
        using (var reader = new StreamReader(marker.Stream, leaveOpen: true))
            if (reader.ReadToEnd() != (Path.GetFileName(fixture).StartsWith("wo14-", StringComparison.Ordinal)
                    ? "WO14_SYNTHETIC_ONLY" : "WO15_SYNTHETIC_ONLY")) throw new InvalidOperationException("SYNTHETIC-MARKER-REQUIRED");
        marker.Validate();
        if (authenticatedHeaderBackup is not null && !backupRecovery)
            throw new InvalidOperationException("BACKUP-RECOVERY-READONLY-REQUIRED");
        using var backup = backupRecovery ? VaultFileBoundary.OpenRead(fixture,
            Path.GetRelativePath(fixture, Path.GetFullPath(authenticatedHeaderBackup!))) : null;
        if (backup is not null && backup.Stream.Length > 1024 * 1024) throw new InvalidDataException("HEADER-BACKUP-BUDGET");
        if (Directory.Exists(workspace) || File.Exists(workspace)) throw new IOException("NEW-WORKSPACE-REQUIRED");
        using var sourcePin = VaultFileBoundary.PinDirectory(source);
        var pins = new List<VaultFileBoundary.DirectoryPin>();
        var files = new Dictionary<string, VaultFileBoundary.FileLease>(StringComparer.OrdinalIgnoreCase);
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        long bytes = 0;
        try
        {
            var queue = new Queue<string>(); queue.Enqueue(source);
            while (queue.TryDequeue(out var directory))
            {
                token.ThrowIfCancellationRequested();
                if (pins.Count + files.Count >= 10000) throw new IOException("SNAPSHOT-ENTRY-BUDGET");
                pins.Add(VaultFileBoundary.PinDirectory(directory));
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new IOException("SNAPSHOT-LINK-REJECTED");
                    if (Directory.Exists(entry)) { queue.Enqueue(entry); continue; }
                    if (pins.Count + files.Count >= 10000) throw new IOException("SNAPSHOT-ENTRY-BUDGET");
                    var relative = Path.GetRelativePath(source, entry);
                    var lease = VaultFileBoundary.OpenRead(source, relative); files.Add(relative, lease);
                    bytes = checked(bytes + lease.Stream.Length);
                    if (bytes > 2L * 1024 * 1024 * 1024) throw new IOException("SNAPSHOT-BYTE-BUDGET");
                    hashes.Add(relative, lease.Hash());
                }
            }
            using (var header = JsonDocument.Parse(backup?.Stream ?? files["vault.header.json"].Stream))
            {
                var h = header.RootElement;
                // Bound unauthenticated KDF parameters before deriving any key.
                if (h.GetProperty("Version").GetInt32() != 5 || h.GetProperty("KdfIterations").GetInt32() != 1
                    || h.GetProperty("KdfMemoryKb").GetInt32() != 65536 || h.GetProperty("KdfParallelism").GetInt32() != 1)
                    throw new InvalidOperationException("LABFAST-V5-FIXTURE-REQUIRED");
                if (backup is not null)
                {
                    files["vault.header.json"].Stream.Position = 0;
                    using var original = JsonDocument.Parse(files["vault.header.json"].Stream);
                    if (h.GetProperty("VaultId").GetString() != original.RootElement.GetProperty("VaultId").GetString())
                        throw new InvalidDataException("HEADER-BACKUP-VAULT-MISMATCH");
                }
            }
            var required = checked(bytes * 2 + 64L * 1024 * 1024);
            if (new DriveInfo(Path.GetPathRoot(workspace)!).AvailableFreeSpace < required) throw new IOException("TRANSITION-SPACE-REQUIRED");
            using var workspacePin = VaultFileBoundary.PinDirectory(workspace, true);
            foreach (var copyName in new[] { "preserved-v5", "candidate-v6" })
            {
                var target = Path.Combine(workspace, copyName);
                using var targetPin = VaultFileBoundary.PinDirectory(target, true);
                foreach (var pin in pins)
                    using (var created = VaultFileBoundary.PinDirectory(Path.Combine(target, Path.GetRelativePath(source, pin.Root)), true)) created.Validate();
                foreach (var (relative, lease) in files)
                {
                    token.ThrowIfCancellationRequested();
                    using var copy = VaultFileBoundary.OpenWrite(target, relative, true);
                    copy.CopyFrom(lease); copy.Verify(lease.Stream.Length, hashes[relative]);
                }
            }
            fault?.Invoke("TransitionSnapshotReady");
            var candidate = Path.Combine(workspace, "candidate-v6");
            if (backup is not null)
            {
                // Replace only the independent candidate. Normal unlock authenticates every restored binding.
                var backupHash = backup.Hash();
                foreach (var headerName in new[] { "vault.header.json", "vault.header.copy1.json", "vault.header.backup.json" })
                {
                    using var restored = VaultFileBoundary.OpenWrite(candidate, headerName, false);
                    restored.CopyFrom(backup); restored.Verify(backup.Stream.Length, backupHash);
                }
                foreach (var digestName in new[] { "vault.header.blake3", "vault.header.copy1.sha256" })
                {
                    using var digest = VaultFileBoundary.OpenWrite(candidate, digestName, false);
                    digest.Stream.SetLength(0);
                    digest.Stream.Write(System.Text.Encoding.UTF8.GetBytes(backupHash)); digest.Stream.Flush(true);
                }
            }
            using var vault = new LabVaultService(candidate);
            var opened = salvageReadOnly && backup is null ? vault.OpenSyntheticSalvage(password) : vault.UnlockReadOnly(password);
            if (!opened.Success) return Result("PreservedButLocked", false, new Dictionary<string, string>(), [], opened.Message);
            fault?.Invoke("TransitionSourceAuthenticated");
            var content = vault.VerifyAllContent(token);
            fault?.Invoke("TransitionContentVerified");
            if (salvageReadOnly)
            {
                var output = Path.Combine(workspace, "recovered");
                using var outputPin = VaultFileBoundary.PinDirectory(output, true);
                foreach (var entry in vault.List())
                {
                    token.ThrowIfCancellationRequested();
                    var relative = entry.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                    if (Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar).Any(p => p is "" or "." or ".." || p.Contains(':'))
                        || Path.GetFileName(relative) != entry.DisplayName)
                        throw new InvalidDataException("RECOVERY-RELATIVE-PATH-REJECTED");
                    var parent = Path.Combine(output, Path.GetDirectoryName(relative) ?? "");
                    using var parentPin = VaultFileBoundary.PinDirectory(parent, true);
                    var exported = vault.ExportEntry(entry.EntryId, parent, true, token);
                    if (!exported.Success) throw new IOException("RECOVERY-EXPORT-FAILED:" + exported.Message);
                    using var verified = VaultFileBoundary.OpenRead(output, relative);
                    verified.Verify(entry.Size, content[entry.EntryId]);
                }
                fault?.Invoke("TransitionRecoveryExported");
                return Result("ReadOnlyRecovery", false, content, [], "Authenticated content verified. Commit history remains unqualified.");
            }
            var codes = vault.PrepareSyntheticGeneration(token, fault);
            vault.Lock();
            using var verifier = new LabVaultService(candidate);
            if (!verifier.UnlockReadOnly(password).Success) throw new CryptographicException("TRANSITION-REOPEN-FAILED");
            var after = verifier.VerifyAllContent(token);
            if (content.Count != after.Count || content.Any(p => !after.TryGetValue(p.Key, out var hash) || hash != p.Value))
                throw new CryptographicException("TRANSITION-CONTENT-MISMATCH");
            fault?.Invoke("TransitionVerified");
            return Result("VerifiedTestTransition", true, content, codes, "Separate test copy verified. Production transition remains disabled.");

            LabTransitionResult Result(string status, bool completed, IReadOnlyDictionary<string, string> content,
                IReadOnlyList<string> codes, string message)
            {
                sourcePin.Validate(); foreach (var pin in pins) pin.Validate();
                foreach (var (relative, lease) in files) lease.Verify(lease.Stream.Length, hashes[relative]);
                var current = Directory.GetFiles(source, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(source, p)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!current.SetEquals(files.Keys)) throw new IOException("SOURCE-INVENTORY-CHANGED");
                return new(status, completed, bytes, hashes, content, codes, message);
            }
        }
        finally { foreach (var lease in files.Values) lease.Dispose(); foreach (var pin in pins) pin.Dispose(); }
    }
}

public sealed partial class LabVaultService
{
    private bool _syntheticSalvage;
    internal LabVaultOperationResult OpenSyntheticSalvage(string password)
    {
        using var operation = EnterOperation();
        _syntheticSalvage = true;
        try { return UnlockReadOnly(password); }
        finally { _syntheticSalvage = false; }
    }

    internal Dictionary<string, string> VerifyAllContent(CancellationToken token)
    {
        using var operation = EnterOperation(); EnsureUnlocked();
        var hashes = new Dictionary<string, string>();
        foreach (var entry in _metadata!.Entries)
        {
            token.ThrowIfCancellationRequested();
            using var sink = new AuthenticatedHashSink();
            DecryptEntryToStream(entry, sink, token);
            var hash = sink.Finish();
            if (sink.Length != entry.Size || !LabCryptoCompare.FixedTimeEqualsHex(hash, entry.ContentSha256))
                throw new CryptographicException("TRANSITION-ENTRY-INTEGRITY");
            hashes.Add(entry.EntryId, hash);
        }
        return hashes;
    }

    internal IReadOnlyList<string> PrepareSyntheticGeneration(CancellationToken token, Action<string>? fault)
    {
        using var operation = EnterOperation(); EnsureUnlocked();
        if (_header!.Version != 5 || string.IsNullOrEmpty(_header.HeaderMacHex)) throw new CryptographicException("AUTHENTICATED-V5-REQUIRED");
        FaultObserver = fault;
        var codes = LabRecoverySlots.GenerateAndWrap(_root, _header.VaultId, _vmk!, true).Codes;
        fault?.Invoke("TransitionRecoveryReady");
        _header.Version = 6; _header.Format = FormatIdV6; _header.Magic = "AVLT6";
        WriteMetadata(_metadata!, _metaKey!);
        CommitGeneration();
        // Reject missing activation. This header is only a format sentinel.
        WriteHeader(_header);
        token.ThrowIfCancellationRequested();
        return codes;
    }

    private sealed class AuthenticatedHashSink : Stream
    {
        private IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long length;
        public string Finish() => Convert.ToHexString(hash.GetHashAndReset());
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => length;
        public override long Position { get => length; set { if (value != 0 || length != 0) throw new NotSupportedException(); } }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer) { hash.AppendData(buffer); length += buffer.Length; }
        public override void SetLength(long value) { if (value != 0) throw new NotSupportedException(); hash.Dispose(); hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); length = 0; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
    }
}
