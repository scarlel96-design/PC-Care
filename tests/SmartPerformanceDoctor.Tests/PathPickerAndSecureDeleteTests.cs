using Xunit;
using SmartPerformanceDoctor.App.Models.Security;
using SmartPerformanceDoctor.App.Services.Commercial;
using SmartPerformanceDoctor.App.Services.Pickers;
using SmartPerformanceDoctor.App.Services.Security;

namespace SmartPerformanceDoctor.Tests;

public sealed class PathPickerAndSecureDeleteTests
{
    [Fact]
    public void PickerResult_DistinguishesSuccessCancellationAndFailure()
    {
        var success = PickerResult<string>.Success("C:\\data.bin", "ok");
        var cancelled = PickerResult<string>.Cancelled("cancelled");
        var failed = PickerResult<string>.Failed("failed", unchecked((int)0x80004005), "TRACE123");

        Assert.True(success.IsSuccess);
        Assert.False(success.IsCancelled);
        Assert.True(cancelled.IsCancelled);
        Assert.Null(cancelled.Value);
        Assert.True(failed.IsFailed);
        Assert.Equal(unchecked((int)0x80004005), failed.HResult);
        Assert.Equal("TRACE123", failed.TrackingId);
    }

    [Fact]
    public void PickerOperationGate_RejectsRepeatedEntryAndRecoversAfterExit()
    {
        var gate = new PickerOperationGate();

        Assert.True(gate.TryEnter());
        Assert.True(gate.IsActive);
        Assert.False(gate.TryEnter());

        gate.Exit();

        Assert.False(gate.IsActive);
        Assert.True(gate.TryEnter());
    }

    [Fact]
    public void SecureDeleteTargetSet_NormalizesDuplicatesAndCollapsesChildrenIntoParent()
    {
        var root = CreateTempDirectory();
        try
        {
            var child = Path.Combine(root, "Sub");
            Directory.CreateDirectory(child);
            var file = Path.Combine(child, "sample.txt");
            File.WriteAllText(file, "sample");
            var set = new SecureDeleteTargetSet();

            Assert.True(set.AddFile(file).Added);
            Assert.False(set.AddFile(file.ToUpperInvariant()).Added);

            var parent = set.AddDirectory(root + Path.DirectorySeparatorChar);

            Assert.True(parent.Added);
            Assert.Equal(1, parent.RemovedChildren);
            Assert.Single(set.Items);
            Assert.Equal(SecureDeleteTargetType.Directory, set.Items[0].TargetType);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SecureDeleteTargetSet_RejectsMissingAndProtectedTargets()
    {
        var set = new SecureDeleteTargetSet();
        var missing = set.AddFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.bin"));
        var current = set.AddDirectory(Environment.CurrentDirectory);

        Assert.False(missing.Added);
        Assert.Equal(SecureDeleteValidationStatus.NotFound, missing.Selection!.ValidationStatus);
        Assert.False(current.Added);
        Assert.Equal(SecureDeleteValidationStatus.Blocked, current.Selection!.ValidationStatus);
        Assert.Empty(set.Items);
    }

    [Fact]
    public void PathSafetyGuard_BlocksNetworkDeviceAndProgramDataPaths()
    {
        Assert.False(PathSafetyGuard.Evaluate(@"\\server\share\target.bin").Allowed);
        Assert.False(PathSafetyGuard.Evaluate(@"\\?\C:\target.bin").Allowed);
        Assert.False(PathSafetyGuard.Evaluate(@"C:\ProgramData\Vendor\target.bin").Allowed);
    }

    [Fact]
    public void VaultImportValidator_ValidatesTypesAndRejectsReparsePointsWhenAvailable()
    {
        var root = CreateTempDirectory();
        try
        {
            var file = Path.Combine(root, "input.bin");
            File.WriteAllText(file, "payload");

            Assert.True(VaultImportPathValidator.ValidateFile(file).Allowed);
            Assert.False(VaultImportPathValidator.ValidateDirectory(file).Allowed);

            var link = Path.Combine(root, "file-link.bin");
            try
            {
                File.CreateSymbolicLink(link, file);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return;
            }

            var linked = VaultImportPathValidator.ValidateFile(link);
            Assert.False(linked.Allowed);
            Assert.Contains("재분석", linked.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task PathPickerService_MapsPreCancelledTokenToCancelledResult()
    {
        var service = new PathPickerService(new NullWindowProvider());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await service.PickSingleFileAsync(
            null,
            new PickerRequest("Test", "Test", "Select"),
            cancellation.Token);

        Assert.True(result.IsCancelled);
        Assert.False(result.IsFailed);
    }

    [Fact]
    public async Task PathPickerService_ReportsFailureWhenOwnerWindowIsMissing()
    {
        var service = new PathPickerService(new NullWindowProvider());

        var result = await service.PickFolderAsync(
            null,
            new PickerRequest("Test", "Test", "Select"));

        Assert.True(result.IsFailed);
        Assert.False(result.IsCancelled);
        Assert.NotNull(result.HResult);
        Assert.False(string.IsNullOrWhiteSpace(result.TrackingId));
    }

    private sealed class NullWindowProvider : IWindowProvider
    {
        public Microsoft.UI.Xaml.Window? CurrentWindow => null;
    }
    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pccare-picker-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
