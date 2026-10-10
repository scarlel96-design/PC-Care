using System.Diagnostics;
using System.Security.AccessControl;
using System.Text.Json;

namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record BootstrapFile(string Name, string Identity, long Length, string Hash);
public sealed record BootstrapBinding(string Id, string InstallRoot, string InstallIdentity,
    string ExecutionIdentity, string Verb, string Package, int CallerPid, int ParentPid, List<BootstrapFile> Files);

public static class UpdateBootstrap
{
    private static readonly string[] RuntimeFiles = ["SmartPerformanceDoctor.UpdateHelper.exe",
        "SmartPerformanceDoctor.UpdateHelper.dll", "SmartPerformanceDoctor.UpdateEngine.dll",
        "SmartPerformanceDoctor.UpdateHelper.deps.json", "SmartPerformanceDoctor.UpdateHelper.runtimeconfig.json"];
    public static string WorkRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "PC-Care", "UpdateTransactions");

    public static string Prepare(string installRoot, string verb, string package, int callerPid)
    {
        if (verb is not ("--apply" or "--recover") || callerPid <= 0) throw new InvalidDataException("BOOTSTRAP-COMMAND");
        ValidateInstallRoot(installRoot);
        new UpdateTransaction(BuildPublisherTrust.Current, WorkRoot, true).InitializeProtectedRoot();
        using var rootPin = WindowsFileBoundary.PinDirectory(installRoot);
        var engine = Path.Combine(installRoot, "engine");
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(WorkRoot, "bootstrap-" + id);
        Directory.CreateDirectory(directory);
        using var executionPin = WindowsFileBoundary.PinDirectory(directory);
        ValidateAcl(directory);
        var files = new List<BootstrapFile>();
        foreach (var name in RuntimeFiles)
        {
            using var source = WindowsFileBoundary.OpenRead(engine, name);
            UpdateTransaction.ValidateProtectedSecurity(source.ReadSecurity(), false);
            VerifyPublisher(engine, name, source);
            using var target = WindowsFileBoundary.OpenWrite(directory, name, true);
            target.CopyFrom(source);
            target.Verify(source.Stream.Length, source.Hash());
            files.Add(new(name, target.Identity, target.Stream.Length, target.Hash()));
        }
        var binding = new BootstrapBinding(id, Path.GetFullPath(installRoot), rootPin.Identity,
            executionPin.Identity, verb, package, callerPid, Environment.ProcessId, files);
        using (var output = WindowsFileBoundary.OpenWrite(directory, "binding.json", true))
        { output.Stream.Write(JsonSerializer.SerializeToUtf8Bytes(binding)); output.Stream.Flush(true); }
        Validate(directory, id);
        return directory;
    }

    public static BootstrapBinding Validate(string directory, string id)
    {
        if (!Guid.TryParseExact(id, "N", out _) || !string.Equals(Path.GetFullPath(directory),
            Path.Combine(WorkRoot, "bootstrap-" + id), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("BOOTSTRAP-LOCATION");
        UpdateTransaction.ValidateProtectedSecurity(new DirectoryInfo(WorkRoot).GetAccessControl(), true);
        ValidateAcl(directory);
        using var execution = WindowsFileBoundary.PinDirectory(directory);
        using var record = WindowsFileBoundary.OpenRead(directory, "binding.json");
        UpdateTransaction.ValidateProtectedSecurity(record.ReadSecurity(), false);
        if (record.Stream.Length > 16384) throw new InvalidDataException("BOOTSTRAP-BINDING-SIZE");
        using var document = JsonDocument.Parse(record.Stream);
        RejectDuplicateFields(document.RootElement);
        var binding = document.RootElement.Deserialize<BootstrapBinding>(
            new JsonSerializerOptions { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("BOOTSTRAP-BINDING");
        if (string.IsNullOrWhiteSpace(binding.InstallRoot) || binding.Package is null ||
            binding.Files is null || binding.Files.Any(f => f is null || string.IsNullOrWhiteSpace(f.Name) ||
                string.IsNullOrWhiteSpace(f.Identity) || f.Length < 0 || f.Hash is null ||
                f.Hash.Length != 64 || !f.Hash.All(Uri.IsHexDigit)))
            throw new InvalidDataException("BOOTSTRAP-BINDING-FIELDS");
        ValidateInstallRoot(binding.InstallRoot);
        using var install = WindowsFileBoundary.PinDirectory(binding.InstallRoot);
        if (binding.Id != id || binding.ExecutionIdentity != execution.Identity || binding.InstallIdentity != install.Identity ||
            binding.Verb is not ("--apply" or "--recover") || binding.CallerPid <= 0 || binding.ParentPid <= 0 ||
            binding.Files is null || binding.Files.Count != RuntimeFiles.Length ||
            !binding.Files.Select(f => f.Name).Order().SequenceEqual(RuntimeFiles.Order()))
            throw new InvalidDataException("BOOTSTRAP-BINDING-MISMATCH");
        foreach (var item in binding.Files)
        {
            using var file = WindowsFileBoundary.OpenRead(directory, item.Name);
            UpdateTransaction.ValidateProtectedSecurity(file.ReadSecurity(), false);
            file.Verify(item.Length, item.Hash, item.Identity);
            VerifyPublisher(directory, item.Name, file);
        }
        return binding;
    }

    public static void ValidateInstallRoot(string installRoot)
    {
        var full = Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (!new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }
            .Any(root => !string.IsNullOrWhiteSpace(root) && full.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("BOOTSTRAP-INSTALL-LOCATION");
        using var pin = WindowsFileBoundary.PinDirectory(full);
        ValidateAcl(full);
        ValidateAcl(Path.Combine(full, "engine"));
    }

    public static Process? Launch(string directory, string id)
    {
        var binding = Validate(directory, id);
        using var install = WindowsFileBoundary.PinDirectory(binding.InstallRoot);
        using var execution = WindowsFileBoundary.PinDirectory(directory);
        var leases = PinRuntime(directory, binding);
        try
        {
        var start = new ProcessStartInfo(Path.Combine(directory, RuntimeFiles[0]))
            { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory };
        start.ArgumentList.Add("--bootstrap"); start.ArgumentList.Add(id);
        return Process.Start(start);
        }
        finally { foreach (var file in leases) file.Dispose(); }
    }

    public static Process? LaunchSnapshotRecovery(string installRoot, int callerPid)
    {
        if (callerPid <= 0) throw new InvalidDataException("BOOTSTRAP-COMMAND");
        ValidateInstallRoot(installRoot);
        if (!Directory.Exists(WorkRoot)) return null;
        ValidateAcl(WorkRoot);
        using var install = WindowsFileBoundary.PinDirectory(installRoot);
        using var work = WindowsFileBoundary.PinDirectory(WorkRoot);
        foreach (var directory in Directory.EnumerateDirectories(WorkRoot, "bootstrap-*").OrderByDescending(p => p, StringComparer.Ordinal))
        {
            BootstrapBinding binding;
            try { binding = Validate(directory, Path.GetFileName(directory)[10..]); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { continue; }
            if (binding.InstallIdentity != install.Identity ||
                !string.Equals(binding.InstallRoot, Path.GetFullPath(installRoot), StringComparison.OrdinalIgnoreCase)) continue;
            using var execution = WindowsFileBoundary.PinDirectory(directory);
            var leases = PinRuntime(directory, binding);
            try
            {
                var start = new ProcessStartInfo(Path.Combine(directory, RuntimeFiles[0]))
                    { UseShellExecute = true, Verb = "runas", WorkingDirectory = directory };
                start.ArgumentList.Add("--bootstrap-recover"); start.ArgumentList.Add(binding.Id);
                start.ArgumentList.Add("--wait-pid"); start.ArgumentList.Add(callerPid.ToString());
                return Process.Start(start);
            }
            finally { foreach (var file in leases) file.Dispose(); }
        }
        return null;
    }

    public static void RecordOutcome(string directory, string id, TransactionResult result, int exitCode)
    {
        var binding = Validate(directory, id);
        using var execution = WindowsFileBoundary.PinDirectory(directory);
        using var record = WindowsFileBoundary.OpenWrite(directory, "outcome-" + Guid.NewGuid().ToString("N") + ".json", true);
        UpdateTransaction.ValidateProtectedSecurity(record.ReadSecurity(), false);
        record.Stream.Write(JsonSerializer.SerializeToUtf8Bytes(new { binding.Id, binding.InstallRoot,
            binding.InstallIdentity, Utc = DateTimeOffset.UtcNow, result.Stage, StageName = result.Stage.ToString(), result.Code, result.TransactionId,
            result.FilesApplied, result.Failure, ExitCode = exitCode }));
        record.Stream.Flush(true);
    }

    private static List<WindowsFileBoundary.FileLease> PinRuntime(string directory, BootstrapBinding binding)
    {
        var files = new List<WindowsFileBoundary.FileLease>();
        try
        {
            foreach (var name in RuntimeFiles)
            {
                var file = WindowsFileBoundary.OpenRead(directory, name); files.Add(file);
                UpdateTransaction.ValidateProtectedSecurity(file.ReadSecurity(), false);
                var expected = binding.Files.Single(item => item.Name == name);
                file.Verify(expected.Length, expected.Hash, expected.Identity);
                VerifyPublisher(directory, name, file);
            }
            return files;
        }
        catch { foreach (var file in files) file.Dispose(); throw; }
    }

    public static Process? LaunchInstalled(string installRoot, string verb, string package, int callerPid)
    {
        if (verb is not ("--apply" or "--recover") || callerPid <= 0)
            throw new InvalidDataException("BOOTSTRAP-COMMAND");
        ValidateInstallRoot(installRoot);
        var engine = Path.Combine(installRoot, "engine");
        using var installPin = WindowsFileBoundary.PinDirectory(installRoot);
        var leases = new List<WindowsFileBoundary.FileLease>();
        try
        {
            foreach (var name in RuntimeFiles)
            {
                var lease = WindowsFileBoundary.OpenRead(engine, name);
                leases.Add(lease);
                UpdateTransaction.ValidateProtectedSecurity(lease.ReadSecurity(), false);
                VerifyPublisher(engine, name, lease);
            }
            var start = new ProcessStartInfo(Path.Combine(engine, RuntimeFiles[0]))
                { UseShellExecute = true, Verb = "runas", WorkingDirectory = engine };
            start.ArgumentList.Add(verb);
            if (verb == "--apply") start.ArgumentList.Add(Path.GetFullPath(package));
            start.ArgumentList.Add("--wait-pid"); start.ArgumentList.Add(callerPid.ToString());
            installPin.Validate();
            foreach (var lease in leases) lease.Validate();
            return Process.Start(start);
        }
        finally { foreach (var lease in leases) lease.Dispose(); }
    }
    private static void ValidateAcl(string path) => UpdateTransaction.ValidateProtectedSecurity(new DirectoryInfo(path).GetAccessControl(), false);
    private static void RejectDuplicateFields(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("BOOTSTRAP-DUPLICATE-FIELD");
                RejectDuplicateFields(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateFields(item);
    }
    private static void VerifyPublisher(string root, string name, WindowsFileBoundary.FileLease file)
    {
        if (!name.EndsWith(".exe", StringComparison.Ordinal) && !name.EndsWith(".dll", StringComparison.Ordinal)) return;
        var signature = AuthenticodeBoundary.Verify(Path.Combine(root, name));
        if (!AuthenticodeBoundary.IsBuildPublisher(signature) || signature.Identity != file.Identity || signature.Hash != file.Hash())
            throw new InvalidDataException("BOOTSTRAP-PUBLISHER-UNVERIFIED:" + name);
    }
}
