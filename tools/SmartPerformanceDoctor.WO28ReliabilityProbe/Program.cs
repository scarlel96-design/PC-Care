using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;

try { return Execute(args); }
catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Status = "INPUT_OR_INVENTORY_REJECTED", ErrorType = ex.GetType().Name,
        Code = ex is InvalidDataException ? ex.Message : "READONLY_BOUNDARY_REJECTED"
    }));
    return 3;
}

static int Execute(string[] args)
{
if (!OperatingSystem.IsWindows() || args.Length != 3) return 2;
var verb = args[0];
if (verb is not ("inspect" or "hold-inventory")) return 2;
var root = SyntheticRoot(args[1], "wo27-");
var control = SyntheticRoot(args[2], "wo28-control-");
if (root.Equals(control, StringComparison.OrdinalIgnoreCase) ||
    control.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
    root.StartsWith(control + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException("CONTROL_MUST_BE_SEPARATE");
using var rootPin = WindowsFileBoundary.PinDirectory(root);
using var controlPin = WindowsFileBoundary.PinDirectory(control);
RequireMarker(root, "fixture.marker", "WO27_SYNTHETIC_ONLY");
RequireMarker(root, "wo28.marker", "WO28_READONLY_RELIABILITY");
RequireMarker(control, "control.marker", "WO28_CONTROL_ONLY");
using var publicKey = WindowsFileBoundary.OpenRead(root, "public.bin");
if (publicKey.Stream.Length > 4096) throw new InvalidDataException("PUBLIC_KEY_LIMIT");
using var keyData = new MemoryStream();
publicKey.Stream.CopyTo(keyData);
var engine = new UpdateTransaction(new PublisherTrustPolicy([
    new TrustedPublisherKey("WO25-Fixture", "ephemeral", "stable", keyData.ToArray())
]), Path.Combine(root, "work"), false);
var reconciliation = engine.ReconcileRecovery(Path.Combine(root, "install"));
var interrupted = false;
var inventory = Inventory(root, (relative, consumed, total) =>
{
    if (verb != "hold-inventory" || interrupted || relative != "install/app.txt") return;
    interrupted = true;
    // Park after a completed read. The recovery engine has already returned.
    var checkpoint = JsonSerializer.SerializeToUtf8Bytes(new
    {
        Phase = "ResidualInventoryHashRead", EngineReconciliationCompleted = true,
        RelativePath = relative, BytesHashed = consumed, TotalBytes = total,
        ReconciliationState = reconciliation.State, ReconciliationCode = reconciliation.Code,
        InsideRecoveryEngine = false, AnyMutationRequested = false
    });
    var pending = Path.Combine(control, "ready.pending");
    using (var signal = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    {
        signal.Write(checkpoint);
        signal.Flush(true);
    }
    File.Move(pending, Path.Combine(control, "ready.json"));
    Thread.Sleep(Timeout.Infinite);
});
var digest = Hash(JsonSerializer.SerializeToUtf8Bytes(inventory));
var repeated = Inventory(root, (_, _, _) => { });
if (Hash(JsonSerializer.SerializeToUtf8Bytes(repeated)) != digest)
    throw new InvalidDataException("INVENTORY_CHANGED_DURING_INSPECTION");
publicKey.Validate();
rootPin.Validate();
controlPin.Validate();
Console.WriteLine(JsonSerializer.Serialize(new
{
    Status = "READ_ONLY_INSPECTION_COMPLETED", StrictAcl = false,
    Reconciliation = reconciliation, Inventory = inventory,
    InventoryDigest = digest, StableAcrossTwoInventoryPasses = true,
    InventoryAtomicAcrossFiles = false, AnyMutationRequested = false,
    ExcludedFileCount = 0, PlaintextContentExported = false
}));
return 0;
}

static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

static string SyntheticRoot(string value, string prefix)
{
    var configured = Environment.GetEnvironmentVariable("RUNNER_TEMP");
    if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured) || !Path.IsPathFullyQualified(value))
        throw new InvalidDataException("ABSOLUTE_RUNNER_TEMP_REQUIRED");
    var parent = Path.GetFullPath(configured).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    var full = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar);
    if (!full.StartsWith(parent, StringComparison.OrdinalIgnoreCase) ||
        !Path.GetFileName(full).StartsWith(prefix, StringComparison.Ordinal) ||
        !Directory.Exists(full)) throw new InvalidDataException("SYNTHETIC_ROOT_REQUIRED");
    return full;
}

static void RequireMarker(string root, string relative, string expected)
{
    using var marker = WindowsFileBoundary.OpenRead(root, relative);
    if (marker.Stream.Length > 128) throw new InvalidDataException("SYNTHETIC_MARKER_REQUIRED");
    using var reader = new StreamReader(marker.Stream, Encoding.UTF8, leaveOpen: true);
    if (reader.ReadToEnd() != expected) throw new InvalidDataException("SYNTHETIC_MARKER_REQUIRED");
    marker.Validate();
}

static IReadOnlyList<InventoryItem> Inventory(string root, Action<string, long, long> onRead)
{
    var records = new List<InventoryItem>();
    var pending = new Stack<string>();
    pending.Push(root);
    long totalBytes = 0;
    var buffer = new byte[64 * 1024];
    while (pending.Count != 0)
    {
        if (records.Count > 16384) throw new InvalidDataException("INVENTORY_ENTRY_LIMIT");
        var directory = pending.Pop();
        using var directoryPin = WindowsFileBoundary.PinDirectory(directory);
        var dirRelative = directory == root ? "." : Path.GetRelativePath(root, directory).Replace('\\', '/');
        var acl = new DirectoryInfo(directory).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access | AccessControlSections.Owner);
        records.Add(new(dirRelative, "Directory", 0, "", Hash(Encoding.UTF8.GetBytes(directoryPin.Identity)), Hash(Encoding.UTF8.GetBytes(acl)), 0, 0));
        foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
        {
            if (records.Count > 16384) throw new InvalidDataException("INVENTORY_ENTRY_LIMIT");
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("INVENTORY_REPARSE_REJECTED");
            if ((attributes & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            using var file = WindowsFileBoundary.OpenRead(root, relative);
            var length = file.Stream.Length;
            totalBytes = checked(totalBytes + length);
            if (totalBytes > 1024L * 1024 * 1024) throw new InvalidDataException("INVENTORY_BYTE_LIMIT");
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long consumed = 0;
            int read;
            while ((read = file.Stream.Read(buffer)) != 0)
            {
                digest.AppendData(buffer, 0, read);
                consumed += read;
                onRead(relative, consumed, length);
            }
            var fileAcl = file.ReadSecurity().GetSecurityDescriptorSddlForm(AccessControlSections.Access | AccessControlSections.Owner);
            file.Validate();
            if (consumed != length || file.Stream.Length != length) throw new InvalidDataException("INVENTORY_LENGTH_CHANGED");
            records.Add(new(relative, "File", length, Convert.ToHexString(digest.GetHashAndReset()),
                Hash(Encoding.UTF8.GetBytes(file.Identity)), Hash(Encoding.UTF8.GetBytes(fileAcl)),
                File.GetLastWriteTimeUtc(path).Ticks, (int)attributes));
        }
        directoryPin.Validate();
    }
    CryptographicOperations.ZeroMemory(buffer);
    return records.OrderBy(x => x.RelativePath, StringComparer.Ordinal).ToArray();
}

sealed record InventoryItem(string RelativePath, string Kind, long Length, string Sha256,
    string IdentityDigest, string AclDigest, long LastWriteUtcTicks, int Attributes);
