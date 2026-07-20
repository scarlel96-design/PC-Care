using SmartPerformanceDoctor.App.Services.Update;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class UpdateArtifactCleanupTests
{
    [Fact]
    public void CompletedUpdate_DeletesManagedPackageAndStagingDirectory()
    {
        var root = CreateRoot();
        try
        {
            var inbox = Path.Combine(root, "inbox");
            var stagingRoot = Path.Combine(root, "staging");
            var package = Path.Combine(inbox, "PCCare_Update_v51.0.1.spdup");
            var staging = Path.Combine(stagingRoot, "apply_1");
            Directory.CreateDirectory(inbox);
            Directory.CreateDirectory(staging);
            File.WriteAllText(package, "package");
            File.WriteAllText(Path.Combine(staging, "payload.bin"), "payload");

            var result = UpdateArtifactCleanup.TryCleanupCompletedUpdate(
                package,
                staging,
                inbox,
                stagingRoot);

            Assert.True(result.PackageDeleted);
            Assert.True(result.StagingDeleted);
            Assert.Empty(result.Warnings);
            Assert.False(File.Exists(package));
            Assert.False(Directory.Exists(staging));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void CompletedUpdate_PreservesUserSelectedPackageOutsideInbox()
    {
        var root = CreateRoot();
        try
        {
            var inbox = Path.Combine(root, "inbox");
            var stagingRoot = Path.Combine(root, "staging");
            var package = Path.Combine(root, "user-selected.spdup");
            var staging = Path.Combine(stagingRoot, "apply_2");
            Directory.CreateDirectory(inbox);
            Directory.CreateDirectory(staging);
            File.WriteAllText(package, "package");

            var result = UpdateArtifactCleanup.TryCleanupCompletedUpdate(
                package,
                staging,
                inbox,
                stagingRoot);

            Assert.False(result.PackageDeleted);
            Assert.True(File.Exists(package));
            Assert.True(result.StagingDeleted);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void BoundaryCheck_DoesNotTreatSiblingPrefixAsManagedPath()
    {
        var root = CreateRoot();
        try
        {
            var inbox = Path.Combine(root, "inbox");
            var sibling = Path.Combine(root, "inbox-backup", "update.spdup");
            Assert.False(UpdateArtifactCleanup.IsManagedDescendant(sibling, inbox));
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "pccare-update-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }
}