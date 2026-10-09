using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class StorageSanitizerDryRunTests
{
    [Fact]
    public void DataDisk_ProducesIdentityAndKeepsExecutionDisabled()
    {
        var sanitizer = new StorageSanitizerDryRun(new FakeProbe(new StorageDeviceIdentity
        {
            RootPath = "D:\\", DriveLetter = "D", DiskNumber = 2, UniqueId = "disk-unique-123456", SerialNumber = "serial-01",
            Model = "Test NVMe", BusType = "NVMe", FileSystem = "NTFS", HealthStatus = "Healthy", SizeBytes = 1_000_000
        }));

        var plan = sanitizer.PlanDryRun("D:\\");

        Assert.True(plan.Device.IsResolved);
        Assert.False(plan.IsSystemDisk);
        Assert.False(plan.RequiresWinPe);
        Assert.False(plan.ExecutionEnabled);
        Assert.Contains(plan.Capabilities, capability => capability.Code == "CAP_NVME_SANITIZE_CANDIDATE" && capability.Supported);
        Assert.Contains(plan.Capabilities, capability => capability.Code == "CAP_EXECUTION_DISABLED" && capability.Supported);
    }

    [Fact]
    public void BootDisk_IsBlockedAndRequiresWinPe()
    {
        var sanitizer = new StorageSanitizerDryRun(new FakeProbe(new StorageDeviceIdentity
        {
            RootPath = "C:\\", DriveLetter = "C", DiskNumber = 0, UniqueId = "disk-system-123456", Model = "System Disk",
            BusType = "NVMe", FileSystem = "NTFS", IsBootDisk = true, IsSystemDisk = true
        }));

        var plan = sanitizer.PlanDryRun("C:\\");

        Assert.True(plan.IsSystemDisk);
        Assert.True(plan.RequiresWinPe);
        Assert.False(plan.ExecutionEnabled);
        Assert.Contains(plan.BlockingReasons, reason => reason.Contains("부팅/시스템 디스크", StringComparison.Ordinal));
    }

    [Fact]
    public void Authorization_BindsOneTimePhraseToTheSamePhysicalDisk()
    {
        var plan = CreateAuthorizablePlan("disk-unique-123456");
        var service = new StoragePurgeAuthorizationService();
        var challenge = service.Begin(plan);

        var approved = service.Confirm(plan, challenge.OperationId, challenge.RequiredPhrase);
        var replay = service.Confirm(plan, challenge.OperationId, challenge.RequiredPhrase);

        Assert.True(approved.AuthorizedForFutureExecutor);
        Assert.False(approved.ExecutionEnabled);
        Assert.False(replay.AuthorizedForFutureExecutor);
    }

    [Fact]
    public void Authorization_RejectsDeviceSwap()
    {
        var service = new StoragePurgeAuthorizationService();
        var challenge = service.Begin(CreateAuthorizablePlan("disk-unique-123456"));

        var decision = service.Confirm(CreateAuthorizablePlan("disk-other-654321"), challenge.OperationId, challenge.RequiredPhrase);

        Assert.False(decision.AuthorizedForFutureExecutor);
        Assert.Contains("식별값이 바뀌", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Evidence_IsLocallyTamperEvidentAndNeverEnablesExecution()
    {
        var root = Path.Combine(Path.GetTempPath(), "PccEvidenceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var evidence = new StoragePurgeEvidenceService(root);
            var receipt = evidence.Write(CreateAuthorizablePlan("disk-evidence-123456"));

            Assert.True(receipt.IsValid);
            Assert.False(receipt.ExecutionEnabled);
            File.AppendAllText(receipt.EvidencePath, "tampered");
            Assert.False(evidence.Verify(receipt.EvidencePath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
    [Fact]
    public void WinPePreparation_IsNonExecutableAndRequiresFreshHardwareChecks()
    {
        var plan = CreateAuthorizablePlan("disk-prep-123456");
        var authorizationService = new StoragePurgeAuthorizationService();
        var challenge = authorizationService.Begin(plan);
        var approval = authorizationService.Confirm(plan, challenge.OperationId, challenge.RequiredPhrase);
        var readiness = new StoragePurgeReadinessService().Evaluate(plan);
        var root = Path.Combine(Path.GetTempPath(), "PccWinPePreparationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var receipt = new StoragePurgeWinPePreparationService().Create(plan, approval, root);

            Assert.Contains(readiness.Methods, method => method.Method == StorageSanitizeMethod.CryptoErase && method.State == PurgeSupportState.CandidateOnly);
            Assert.All(readiness.Methods, method => Assert.False(method.CanExecute));
            Assert.False(receipt.ExecutionEnabled);
            Assert.False(receipt.DeviceCommandsIncluded);
            Assert.Contains("\"DeviceCommandsIncluded\": false", File.ReadAllText(receipt.Path), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
    private static StorageSanitizeDryRunResult CreateAuthorizablePlan(string uniqueId) => new()
    {
        OperationId = "dry-run-test", RootPath = "D:\\",
        Device = new StorageDeviceIdentity { RootPath = "D:\\", DriveLetter = "D", DiskNumber = 2, UniqueId = uniqueId, SerialNumber = "serial-01", Model = "Test NVMe", BusType = "NVMe", SizeBytes = 1_000_000 },
        IsSystemDisk = false, IsWinPe = false, BlockingReasons = Array.Empty<string>()
    };
    private sealed class FakeProbe(StorageDeviceIdentity identity) : IStorageDeviceProbe
    {
        public StorageDeviceIdentity Probe(string rootPath) => identity;
    }
}
