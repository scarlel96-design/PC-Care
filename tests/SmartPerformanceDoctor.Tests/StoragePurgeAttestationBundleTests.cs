using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class StoragePurgeAttestationBundleTests
{
    [Fact]
    public void Attestation_IsTamperEvidentAndNeverExecutable()
    {
        var root = Path.Combine(Path.GetTempPath(), "PccPurgeAttestationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var plan = Plan();
            var now = DateTimeOffset.UtcNow;
            var approval = new PurgeAuthorizationDecision { AuthorizedForFutureExecutor = true, DeviceFingerprint = DeviceFingerprint.Create(plan.Device) };
            var inspection = new PurgeReadOnlyInspection { State = PurgeInspectionState.VerifiedReadOnly, InspectedAtUtc = now, DeviceFingerprint = DeviceFingerprint.Create(plan.Device) };
            var identify = new NvmeIdentifyReadOnlyEvidence { ReadSucceeded = true, CollectedAtUtc = now, DeviceFingerprint = DeviceFingerprint.Create(plan.Device), SupportsCryptoErase = true };
            var effects = new NvmeCommandEffectsReadOnlyEvidence { ReadSucceeded = true, CollectedAtUtc = now, DeviceFingerprint = DeviceFingerprint.Create(plan.Device), SanitizeCommandSupported = true };
            var status = new NvmeSanitizeStatusReadOnlyEvidence { ReadSucceeded = true, CollectedAtUtc = now, DeviceFingerprint = DeviceFingerprint.Create(plan.Device), State = NvmeSanitizeOperationState.NeverSanitized };
            var assessment = new StoragePurgePreExecutionGate().Assess(plan, approval, inspection, identify, effects, status, now);
            var service = new StoragePurgeAttestationBundleService(root);

            var receipt = service.Write(plan, approval, inspection, identify, effects, status, assessment);

            Assert.True(receipt.IsValid);
            Assert.False(receipt.ExecutionEnabled);
            File.AppendAllText(receipt.Path, "tampered");
            Assert.False(service.Verify(receipt.Path));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private static StorageSanitizeDryRunResult Plan() => new()
    {
        OperationId = "attestation-test", RootPath = "D:\\", IsWinPe = true,
        Device = new StorageDeviceIdentity { RootPath = "D:\\", DriveLetter = "D", DiskNumber = 2, UniqueId = "attestation-device", SerialNumber = "serial", Model = "Test NVMe", BusType = "NVMe", SizeBytes = 1000 },
        BlockingReasons = Array.Empty<string>()
    };
}