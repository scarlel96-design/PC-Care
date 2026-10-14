using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

namespace SmartPerformanceDoctor.RecoverySandbox;

public sealed record TargetIdentity(string RootId, string FileId, long Length, string Sha256);
public sealed record ActionDescriptor(string ActionId, string Target, string Quarantine, string RiskLevel);
public sealed record ConsentRecord(string ActionId, string RootId, DateTimeOffset ExpiresUtc, bool Approved);
public sealed record ExecutionJournal(int Schema, ActionDescriptor Action, TargetIdentity Target, string State);
public sealed record RecoveryResult(string State, bool BytesVerified, int Mutations);
public sealed record FinalDeleteConsent(string ActionId, string FileId, DateTimeOffset ExpiresUtc, bool Approved);
public sealed record CleanupMeasurement(long CandidateBytes, long QuarantinedBytes, long RestorableBytes,
    long FinalDeletedBytes, long AvailableBefore, long AvailableAfter, long ObservedFreeSpaceDelta,
    long? AttributableFreedBytes, double ElapsedMs, string Limitation)
{
    public long LogicalBytes => CandidateBytes;
    public long? AllocatedBytes => null;
    public long? VerifiedFreedBytes => AttributableFreedBytes;
    public long UnattributedVolumeDelta => ObservedFreeSpaceDelta - (AttributableFreedBytes ?? 0);
}

/// <summary>Execute only fixed synthetic fixtures. This tool is absent from the product dependency graph.</summary>
public sealed class SandboxRecovery
{
    public string Root { get; }
    private const string Original = "fixture.dat", Quarantine = "fixture.quarantine", Journal = "execution.json";
    private static readonly byte[] Fixture = Enumerable.Range(0, 4096).Select(i => (byte)(i * 17 % 251)).ToArray();
    private static readonly string FixtureHash = Convert.ToHexString(SHA256.HashData(Fixture));
    private SandboxRecovery(string root) => Root = Path.GetFullPath(root);

    public static SandboxRecovery Create(string parent)
        => CreateSynthetic(parent, 4096);

    public static SandboxRecovery CreateSynthetic(string parent, int bytes)
    {
        if (bytes < 4096 || bytes > 32 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(bytes));
        using var parentPin = WindowsFileBoundary.PinDirectory(parent);
        var root = Path.Combine(parentPin.Root, "pccare-wo10-" + Guid.NewGuid().ToString("N"));
        var directory = Directory.CreateDirectory(root);
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
        var sid = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("UserSidMissing");
        acl.SetOwner(sid);
        foreach (var principal in new[] { sid, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(principal, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(acl);
        using var pin = WindowsFileBoundary.PinDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "key.bin"), RandomNumberGenerator.GetBytes(32));
        using (var output = new FileStream(Path.Combine(root, Original), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            for (var written = 0; written < bytes; written += Fixture.Length)
                output.Write(Fixture.AsSpan(0, Math.Min(Fixture.Length, bytes - written)));
            output.Flush(true);
        }
        using var target = WindowsFileBoundary.OpenRead(root, Original);
        var identity = new TargetIdentity(pin.Identity, target.Identity, target.Stream.Length, target.Hash());
        var action = new ActionDescriptor(Guid.NewGuid().ToString("N"), Original, Quarantine, "SyntheticOnly");
        var sandbox = new SandboxRecovery(root);
        sandbox.Save(new(bytes == 4096 ? 1 : 2, action, identity, "Ready"));
        return sandbox;
    }

    public static SandboxRecovery CreateSelectedSynthetic(string parent, string name, int bytes = 4096)
    {
        if (!ValidSelectedName(name)) throw new InvalidDataException("SyntheticTargetNameRequired");
        var sandbox = CreateSynthetic(parent, bytes);
        var journal = sandbox.Read();
        using (var file = WindowsFileBoundary.OpenWrite(sandbox.Root, Original, deletable: true))
        {
            Verify(file, journal.Target);
            file.RenameOpenedFile(Path.Combine(sandbox.Root, name));
            Verify(file, journal.Target);
        }
        sandbox.Save(journal with { Schema = 3, Action = journal.Action with {Target = name} });
        return sandbox;
    }

    private static bool ValidSelectedName(string name) => Path.GetFileName(name) == name
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name.Length <= 128
        && Path.GetExtension(name).ToLowerInvariant() is ".tmp" or ".cache";

    public static SandboxRecovery Open(string root)
    {
        var full = Path.GetFullPath(root);
        var name = Path.GetFileName(full);
        if (!name.StartsWith("pccare-wo10-", StringComparison.Ordinal) || !Guid.TryParseExact(name[12..], "N", out _))
            throw new InvalidDataException("SyntheticWorkspaceRequired");
        var sandbox = new SandboxRecovery(full); sandbox.Read(); return sandbox;
    }

    public ConsentRecord Approve()
    {
        var journal = Read(); return new(journal.Action.ActionId, journal.Target.RootId, DateTimeOffset.UtcNow.AddMinutes(2), true);
    }

    public FinalDeleteConsent ApproveFinalDelete()
    {
        var journal = Read();
        if (journal.State != "Quarantined") throw new InvalidOperationException("VerifiedQuarantineRequired");
        using var file = WindowsFileBoundary.OpenRead(Root, Quarantine);
        Verify(file, journal.Target);
        return new(journal.Action.ActionId, file.Identity, DateTimeOffset.UtcNow.AddMinutes(2), true);
    }

    public CleanupMeasurement FinalDelete(FinalDeleteConsent consent, Action<string>? checkpoint = null)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        using var transactionLock = WindowsFileBoundary.OpenExclusive(Root, "operation.lock", false);
        using var root = WindowsFileBoundary.PinDirectory(Root);
        var journal = Read();
        if (journal.State != "Quarantined") throw new InvalidOperationException("FinalDeleteState");
        var before = new DriveInfo(Path.GetPathRoot(Root)!).AvailableFreeSpace;
        using (var file = WindowsFileBoundary.OpenWrite(Root, Quarantine, deletable: true))
        {
            Verify(file, journal.Target); root.Validate(); checkpoint?.Invoke("FinalDeletePreflight");
            if (!consent.Approved || consent.ExpiresUtc <= DateTimeOffset.UtcNow || consent.ActionId != journal.Action.ActionId
                || consent.FileId != file.Identity) throw new InvalidOperationException("SeparateFinalDeleteConsentRequired");
            Save(journal with { State = "DeletePrepared" }); checkpoint?.Invoke("DeletePrepared");
            file.DeleteOpenedFile();
        }
        checkpoint?.Invoke("Deleted");
        if (File.Exists(Path.Combine(Root, Quarantine)) || File.Exists(Path.Combine(Root, journal.Action.Target)))
            throw new IOException("FinalDeleteVerificationFailed");
        Save(journal with { State = "FinalDeleted" });
        var after = new DriveInfo(Path.GetPathRoot(Root)!).AvailableFreeSpace;
        return new(journal.Target.Length, 0, 0, journal.Target.Length, before, after, after - before, null,
            timer.Elapsed.TotalMilliseconds, "Global free-space delta includes concurrent activity. Attribution is unverified. Final deletion has no rollback.");
    }

    public CleanupMeasurement MeasureQuarantine(long availableBefore, double elapsedMs)
    {
        var journal = Read();
        if (journal.State != "Quarantined") throw new InvalidOperationException("VerifiedQuarantineRequired");
        using var file = WindowsFileBoundary.OpenRead(Root, Quarantine); Verify(file, journal.Target);
        var after = new DriveInfo(Path.GetPathRoot(Root)!).AvailableFreeSpace;
        return new(journal.Target.Length, journal.Target.Length, journal.Target.Length, 0, availableBefore, after,
            after - availableBefore, 0, elapsedMs, "Same-volume rename releases no payload allocation. Global free-space delta is not cleanup gain.");
    }

    public RecoveryResult Execute(ConsentRecord consent, CancellationToken cancellation = default, Action<string>? checkpoint = null)
    {
        using var transactionLock = WindowsFileBoundary.OpenExclusive(Root, "operation.lock", !File.Exists(Path.Combine(Root, "operation.lock")));
        using var root = WindowsFileBoundary.PinDirectory(Root);
        var journal = Read();
        if (journal.State is "DeletePrepared" or "FinalDeleted")
            throw new InvalidOperationException("IrreversibleDeleteRequiresReview:PreserveEvidence");
        if (journal.State != "Ready") throw new InvalidOperationException("DuplicateOrPendingExecution");
        using var file = WindowsFileBoundary.OpenWrite(Root, journal.Action.Target, deletable: true);
        Verify(file, journal.Target); root.Validate();
        checkpoint?.Invoke("PreflightValidated"); cancellation.ThrowIfCancellationRequested();
        if (!consent.Approved || consent.ExpiresUtc <= DateTimeOffset.UtcNow || consent.ActionId != journal.Action.ActionId
            || consent.RootId != root.Identity) throw new InvalidOperationException("ConsentRequired");
        Save(journal with { State = "Prepared" }); checkpoint?.Invoke("Prepared");
        cancellation.ThrowIfCancellationRequested();
        file.Validate(journal.Target.FileId);
        file.RenameOpenedFile(Path.Combine(Root, Quarantine)); checkpoint?.Invoke("Moved");
        Verify(file, journal.Target);
        Save(journal with { State = "Quarantined" }); checkpoint?.Invoke("Quarantined");
        return new("Quarantined", true, 1);
    }

    public RecoveryResult Recover(Action<string>? checkpoint = null)
    {
        using var transactionLock = WindowsFileBoundary.OpenExclusive(Root, "operation.lock", !File.Exists(Path.Combine(Root, "operation.lock")));
        var journal = Read();
        if (journal.State is "DeletePrepared" or "FinalDeleted")
            throw new InvalidDataException("IrreversibleDeleteRequiresReview:PreserveEvidence");
        if (journal.State == "Ready")
        {
            using var ready = WindowsFileBoundary.OpenRead(Root, journal.Action.Target);
            Verify(ready, journal.Target); return new("Ready", true, 0);
        }
        if (journal.State is not ("Prepared" or "Quarantined" or "RollingBack" or "Restored")) throw new InvalidDataException("InvalidState");
        var originalExists = File.Exists(Path.Combine(Root, journal.Action.Target));
        var quarantineExists = File.Exists(Path.Combine(Root, Quarantine));
        if (journal.State == "Restored" && quarantineExists) throw new IOException("RecoveryRequired:RestoredStateMismatch");
        if (originalExists == quarantineExists) throw new IOException("RecoveryRequired:MissingOrConflictingTargets");
        using var file = WindowsFileBoundary.OpenWrite(Root, originalExists ? Original : Quarantine, deletable: true);
        Verify(file, journal.Target);
        Save(journal with { State = "RollingBack" }); checkpoint?.Invoke("RollingBack");
        if (!originalExists) file.RenameOpenedFile(Path.Combine(Root, journal.Action.Target));
        checkpoint?.Invoke("RestoredMove"); Verify(file, journal.Target);
        Save(journal with { State = "Restored" }); checkpoint?.Invoke("Restored");
        return new("Restored", true, originalExists ? 0 : 1);
    }

    private static void Verify(WindowsFileBoundary.FileLease file, TargetIdentity expected) => file.Verify(expected.Length, expected.Sha256, expected.FileId);

    private ExecutionJournal Read()
    {
        using var root = WindowsFileBoundary.PinDirectory(Root);
        using var input = WindowsFileBoundary.OpenRead(Root, Journal);
        if (input.Stream.Length > 8192) throw new InvalidDataException("JournalBudget");
        using var document = JsonDocument.Parse(input.Stream);
        var payload = document.RootElement.GetProperty("Payload").GetString()!;
        var mac = document.RootElement.GetProperty("Mac").GetString()!;
        var expected = Sign(payload);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(mac), Convert.FromHexString(expected))) throw new InvalidDataException("JournalAuthentication");
        var journal = JsonSerializer.Deserialize<ExecutionJournal>(payload) ?? throw new InvalidDataException("JournalMissing");
        if (journal.Schema is not (1 or 2 or 3) || (journal.Schema == 3 ? !ValidSelectedName(journal.Action.Target) : journal.Action.Target != Original) || journal.Action.Quarantine != Quarantine
            || journal.Action.RiskLevel != "SyntheticOnly" || !Guid.TryParseExact(journal.Action.ActionId, "N", out _)
            || journal.Target.RootId != root.Identity || journal.Target.Length < 4096 || journal.Target.Length > 32 * 1024 * 1024
            || journal.Target.Sha256 != ExpectedHash(journal.Target.Length)
            || journal.Schema == 1 && journal.Target.Length != Fixture.Length)
            throw new InvalidDataException("JournalBinding");
        return journal;
    }

    private static string ExpectedHash(long length)
    {
        if (length == Fixture.Length) return FixtureHash;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (long written = 0; written < length; written += Fixture.Length)
            hash.AppendData(Fixture.AsSpan(0, (int)Math.Min(Fixture.Length, length - written)));
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private string Sign(string text)
    {
        using var keyFile = WindowsFileBoundary.OpenRead(Root, "key.bin");
        if (keyFile.Stream.Length != 32) throw new InvalidDataException("KeyLength");
        var key = new byte[32]; keyFile.Stream.ReadExactly(key);
        try { return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(text))); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private void Save(ExecutionJournal journal)
    {
        using var root = WindowsFileBoundary.PinDirectory(Root);
        var payload = JsonSerializer.Serialize(journal);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Payload = payload, Mac = Sign(payload) });
        var temporary = "journal-" + Guid.NewGuid().ToString("N") + ".tmp";
        using (var file = WindowsFileBoundary.OpenExclusive(Root, temporary, true))
        { file.Stream.Write(bytes); file.Stream.Flush(true); file.Validate(); }
        var destination = Path.Combine(Root, Journal);
        if (File.Exists(destination))
        {
            using (var old = WindowsFileBoundary.OpenRead(Root, Journal)) { if (old.Stream.Length > 8192) throw new InvalidDataException("JournalBudget"); }
            File.Replace(Path.Combine(Root, temporary), destination, null);
        }
        else File.Move(Path.Combine(Root, temporary), destination);
        root.Validate();
    }
}
