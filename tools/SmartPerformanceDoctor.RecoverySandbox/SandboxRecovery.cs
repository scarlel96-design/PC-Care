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

/// <summary>Execute only fixed synthetic fixtures. This tool is absent from the product dependency graph.</summary>
public sealed class SandboxRecovery
{
    public string Root { get; }
    private const string Original = "fixture.dat", Quarantine = "fixture.quarantine", Journal = "execution.json";
    private static readonly byte[] Fixture = Enumerable.Range(0, 4096).Select(i => (byte)(i * 17 % 251)).ToArray();
    private static readonly string FixtureHash = Convert.ToHexString(SHA256.HashData(Fixture));
    private SandboxRecovery(string root) => Root = Path.GetFullPath(root);

    public static SandboxRecovery Create(string parent)
    {
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
        File.WriteAllBytes(Path.Combine(root, Original), Fixture);
        using var target = WindowsFileBoundary.OpenRead(root, Original);
        var identity = new TargetIdentity(pin.Identity, target.Identity, target.Stream.Length, target.Hash());
        var action = new ActionDescriptor(Guid.NewGuid().ToString("N"), Original, Quarantine, "SyntheticOnly");
        var sandbox = new SandboxRecovery(root);
        sandbox.Save(new(1, action, identity, "Ready"));
        return sandbox;
    }

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

    public RecoveryResult Execute(ConsentRecord consent, CancellationToken cancellation = default, Action<string>? checkpoint = null)
    {
        using var transactionLock = WindowsFileBoundary.OpenExclusive(Root, "operation.lock", !File.Exists(Path.Combine(Root, "operation.lock")));
        using var root = WindowsFileBoundary.PinDirectory(Root);
        var journal = Read();
        if (journal.State != "Ready") throw new InvalidOperationException("DuplicateOrPendingExecution");
        using var file = WindowsFileBoundary.OpenWrite(Root, Original, deletable: true);
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
        if (journal.State == "Ready")
        {
            using var ready = WindowsFileBoundary.OpenRead(Root, Original);
            Verify(ready, journal.Target); return new("Ready", true, 0);
        }
        if (journal.State is not ("Prepared" or "Quarantined" or "RollingBack" or "Restored")) throw new InvalidDataException("InvalidState");
        var originalExists = File.Exists(Path.Combine(Root, Original));
        var quarantineExists = File.Exists(Path.Combine(Root, Quarantine));
        if (journal.State == "Restored" && quarantineExists) throw new IOException("RecoveryRequired:RestoredStateMismatch");
        if (originalExists == quarantineExists) throw new IOException("RecoveryRequired:MissingOrConflictingTargets");
        using var file = WindowsFileBoundary.OpenWrite(Root, originalExists ? Original : Quarantine, deletable: true);
        Verify(file, journal.Target);
        Save(journal with { State = "RollingBack" }); checkpoint?.Invoke("RollingBack");
        if (!originalExists) file.RenameOpenedFile(Path.Combine(Root, Original));
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
        if (journal.Schema != 1 || journal.Action.Target != Original || journal.Action.Quarantine != Quarantine
            || journal.Action.RiskLevel != "SyntheticOnly" || !Guid.TryParseExact(journal.Action.ActionId, "N", out _)
            || journal.Target.RootId != root.Identity || journal.Target.Sha256 != FixtureHash || journal.Target.Length != Fixture.Length)
            throw new InvalidDataException("JournalBinding");
        return journal;
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
