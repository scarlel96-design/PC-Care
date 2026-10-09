using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class SecureDeleteHardeningTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spd-secure-delete-hardening-" + Guid.NewGuid().ToString("N"));

    public SecureDeleteHardeningTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Unknown_media_uses_a_conservative_overwrite_pass()
    {
        Assert.Equal(1, SecureDeleteStorageProfiler.GetOverwritePasses("Unknown", SecureDeleteSecurityLevel.Maximum));
        Assert.Equal(1, SecureDeleteStorageProfiler.GetOverwritePasses("USB-FAT", SecureDeleteSecurityLevel.Professional));
    }

    [Fact]
    public async Task Unknown_media_file_is_cleared_before_removal_path_completes()
    {
        var path = Path.Combine(_root, "unknown-media-target.bin");
        await File.WriteAllBytesAsync(path, "forensic-clear-test"u8.ToArray());

        await ForensicSecureDeleteEngine.SecureDeleteFileAsync(
            path,
            "Unknown",
            SecureDeleteSecurityLevel.Standard,
            CancellationToken.None);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Changed_file_after_dry_run_is_not_deleted()
    {
        var path = Path.Combine(_root, "toctou-target.txt");
        await File.WriteAllTextAsync(path, "planned content");
        var service = new ProfessionalSecureDeleteService(_root);
        var plan = service.PlanDryRun([path]);
        Assert.Single(plan.Targets);

        await File.WriteAllTextAsync(path, "replacement content with a different length");
        var result = await service.ApplyAsync(plan, "보안 삭제에 동의합니다");

        Assert.Equal(0, result.Deleted);
        Assert.Equal(1, result.Failed);
        Assert.True(File.Exists(path));
    }
    [Fact]
    public async Task Replaced_file_with_matching_metadata_after_dry_run_is_not_deleted()
    {
        var path = Path.Combine(_root, "file-id-toctou.txt");
        await File.WriteAllTextAsync(path, "same");
        var created = File.GetCreationTimeUtc(path);
        var modified = File.GetLastWriteTimeUtc(path);
        var service = new ProfessionalSecureDeleteService(_root);
        var plan = service.PlanDryRun([path]);
        Assert.Single(plan.Targets);

        File.Delete(path);
        await File.WriteAllTextAsync(path, "same");
        File.SetCreationTimeUtc(path, created);
        File.SetLastWriteTimeUtc(path, modified);
        var result = await service.ApplyAsync(plan, "보안 삭제에 동의합니다");

        Assert.Equal(0, result.Deleted);
        Assert.Equal(1, result.Failed);
        Assert.True(File.Exists(path));
    }
    [Fact]
    public async Task Changed_folder_after_dry_run_is_not_partially_deleted()
    {
        var folder = Path.Combine(_root, "folder-toctou");
        Directory.CreateDirectory(folder);
        var original = Path.Combine(folder, "planned.txt");
        await File.WriteAllTextAsync(original, "planned content");
        var service = new ProfessionalSecureDeleteService(_root);
        var plan = service.PlanDryRun([folder]);
        Assert.Single(plan.Targets);

        var added = Path.Combine(folder, "added-after-plan.txt");
        await File.WriteAllTextAsync(added, "unexpected content");
        var result = await service.ApplyAsync(plan, "보안 삭제에 동의합니다");

        Assert.Equal(0, result.Deleted);
        Assert.Equal(1, result.Failed);
        Assert.True(File.Exists(original));
        Assert.True(File.Exists(added));
    }

    [Fact]
    public void Reparse_point_folder_is_blocked_instead_of_crashing_the_batch_dry_run()
    {
        var normalFile = Path.Combine(_root, "normal.txt");
        var linkedFolder = Path.Combine(_root, "linked-folder");
        var externalTarget = Path.Combine(Path.GetTempPath(), "spd-secure-delete-link-target-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(linkedFolder);
        Directory.CreateDirectory(externalTarget);
        File.WriteAllText(normalFile, "normal");

        try
        {
            Directory.Delete(linkedFolder);
            Directory.CreateSymbolicLink(linkedFolder, externalTarget);
        }
        catch (UnauthorizedAccessException)
        {
            Directory.Delete(externalTarget, true);
            return; // Symlink creation is unavailable in this Windows test environment.
        }
        try
        {
            var plan = new ProfessionalSecureDeleteService().PlanDryRun([normalFile, linkedFolder]);

            Assert.Single(plan.Targets);
            var blocked = Assert.Single(plan.BlockedTargets);
            Assert.Equal(linkedFolder, blocked.Path);
            Assert.Equal("blocked", blocked.Risk);
            Assert.Contains("재분석", blocked.BlockReason);
        }
        finally
        {
            if (Directory.Exists(linkedFolder)) Directory.Delete(linkedFolder, false);
            if (Directory.Exists(externalTarget)) Directory.Delete(externalTarget, true);
        }
    }
    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        catch { /* test cleanup */ }
    }
}
