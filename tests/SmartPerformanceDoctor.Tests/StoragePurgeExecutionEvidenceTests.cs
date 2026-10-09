using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class StoragePurgeExecutionEvidenceTests
{
    [Fact]
    public void ExecutionEvidence_BindsPreDispatchAndFinalStatus_AndDetectsTampering()
    {
        var root = Path.Combine(Path.GetTempPath(), "PccPurgeExecutionEvidenceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = new StoragePurgeExecutionEvidenceService(root);
            var plan = new StorageSanitizeDryRunResult
            {
                OperationId = "execution-evidence-test", RootPath = "D:\\", IsWinPe = true,
                Device = new StorageDeviceIdentity { RootPath = "D:\\", DriveLetter = "D", DiskNumber = 3, UniqueId = "evidence-device", SerialNumber = "serial", Model = "Test NVMe", BusType = "NVMe", SizeBytes = 4096 }
            };
            var request = new WinPePurgeExecutionRequest
            {
                Plan = plan,
                Method = StorageSanitizeMethod.CryptoErase,
                ConfirmationPhrase = WinPeStoragePurgeExecutor.RequiredConfirmation(StorageSanitizeMethod.CryptoErase, 3)
            };

            var session = service.Begin(request);
            var receipt = service.Complete(session, new WinPePurgeExecutionResult(
                true, true, "controller reported success",
                new NvmeSanitizeStatusReadOnlyEvidence { ReadSucceeded = true, State = NvmeSanitizeOperationState.Succeeded, StatusLogSha256 = "abc" },
                DateTimeOffset.UtcNow));

            Assert.True(receipt.IsValid);
            File.AppendAllText(receipt.Path, "tampered");
            Assert.False(service.Verify(receipt.Path));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
    [Fact]
    public void TamperedPreDispatchEvidence_CannotBeCompleted()
    {
        var root = Path.Combine(Path.GetTempPath(), "PccPurgeExecutionEvidenceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = new StoragePurgeExecutionEvidenceService(root);
            var plan = new StorageSanitizeDryRunResult
            {
                OperationId = "pre-dispatch-tamper", RootPath = "D:\\", IsWinPe = true,
                Device = new StorageDeviceIdentity { RootPath = "D:\\", DriveLetter = "D", DiskNumber = 3, UniqueId = "tamper-device", SerialNumber = "serial", Model = "Test NVMe", BusType = "NVMe", SizeBytes = 4096 }
            };
            var request = new WinPePurgeExecutionRequest { Plan = plan, Method = StorageSanitizeMethod.BlockErase, ConfirmationPhrase = WinPeStoragePurgeExecutor.RequiredConfirmation(StorageSanitizeMethod.BlockErase, 3) };
            var session = service.Begin(request);
            File.AppendAllText(session.Path, "tampered");

            Assert.Throws<IOException>(() => service.Complete(session, new WinPePurgeExecutionResult(false, false, "blocked", null, DateTimeOffset.UtcNow)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}