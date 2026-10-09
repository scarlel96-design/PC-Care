using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class StoragePurgeReadOnlyInspectionTests
{
    [Fact]
    public void VerifiedInspection_IsTimeBoundAndRemainsNonExecutable()
    {
        var plan = Plan("disk-inspection-123456");
        var inspection = new PurgeReadOnlyInspection
        {
            State = PurgeInspectionState.VerifiedReadOnly,
            InspectedAtUtc = DateTimeOffset.UtcNow,
            DeviceFingerprint = DeviceFingerprint.Create(plan.Device),
            Detail = "synthetic read-only observation"
        };

        var report = new StoragePurgeReadinessService().Evaluate(plan, inspection);

        Assert.True(inspection.IsFresh(DateTimeOffset.UtcNow));
        Assert.False(inspection.CanExecute);
        Assert.False(report.ExecutionEnabled);
        Assert.Same(inspection, report.ReadOnlyInspection);
        Assert.Contains(report.RequiredFutureChecks, item => item.Contains("읽기 전용 재식별 완료", StringComparison.Ordinal));
    }

    [Fact]
    public void SwappedOrExpiredInspection_IsNotAcceptedAsCurrentEvidence()
    {
        var plan = Plan("disk-inspection-a");
        var other = Plan("disk-inspection-b");
        var swapped = new PurgeReadOnlyInspection
        {
            State = PurgeInspectionState.VerifiedReadOnly,
            InspectedAtUtc = DateTimeOffset.UtcNow,
            DeviceFingerprint = DeviceFingerprint.Create(other.Device)
        };
        var expired = new PurgeReadOnlyInspection
        {
            State = PurgeInspectionState.VerifiedReadOnly,
            InspectedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-6),
            DeviceFingerprint = DeviceFingerprint.Create(plan.Device)
        };

        var swappedReport = new StoragePurgeReadinessService().Evaluate(plan, swapped);
        var expiredReport = new StoragePurgeReadinessService().Evaluate(plan, expired);

        Assert.Contains(swappedReport.RequiredFutureChecks, item => item.Contains("증적이 없습니다", StringComparison.Ordinal));
        Assert.Contains(expiredReport.RequiredFutureChecks, item => item.Contains("증적이 없습니다", StringComparison.Ordinal));
        Assert.False(swappedReport.ExecutionEnabled);
        Assert.False(expiredReport.ExecutionEnabled);
    }

    [Fact]
    public void FailedInspection_CannotBecomeFresh()
    {
        var inspection = new PurgeReadOnlyInspection
        {
            State = PurgeInspectionState.ProbeFailed,
            InspectedAtUtc = DateTimeOffset.UtcNow,
            Detail = "read-only query failed"
        };

        Assert.False(inspection.IsFresh(DateTimeOffset.UtcNow));
        Assert.False(inspection.CanExecute);
    }

    private static StorageSanitizeDryRunResult Plan(string uniqueId) => new()
    {
        OperationId = "purge-read-only-test",
        RootPath = "D:\\",
        Device = new StorageDeviceIdentity
        {
            RootPath = "D:\\", DriveLetter = "D", DiskNumber = 2, UniqueId = uniqueId,
            SerialNumber = "serial-01", Model = "Test NVMe", BusType = "NVMe", SizeBytes = 1_000_000
        },
        BlockingReasons = Array.Empty<string>()
    };
}