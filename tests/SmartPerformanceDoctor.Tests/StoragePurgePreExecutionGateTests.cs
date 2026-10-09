using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class StoragePurgePreExecutionGateTests
{
    [Fact]
    public void CompleteEvidence_IsOnlyEligibleForFutureExecutor()
    {
        var plan = Plan();
        var now = DateTimeOffset.UtcNow;
        var result = new StoragePurgePreExecutionGate().Assess(plan, Approval(plan), Inspection(plan, now), Identify(plan, now), Effects(plan, now), Status(plan, now, NvmeSanitizeOperationState.NeverSanitized), now);

        Assert.True(result.IsEligibleForFutureExecutor);
        Assert.False(result.ExecutionEnabled);
        Assert.Empty(result.BlockingReasons);
        Assert.True(result.CrossCheck.CryptoEraseCrossValidated);
    }

    [Fact]
    public void InProgressSanitize_AlwaysBlocksFutureExecutor()
    {
        var plan = Plan();
        var now = DateTimeOffset.UtcNow;
        var result = new StoragePurgePreExecutionGate().Assess(plan, Approval(plan), Inspection(plan, now), Identify(plan, now), Effects(plan, now), Status(plan, now, NvmeSanitizeOperationState.InProgress), now);

        Assert.False(result.IsEligibleForFutureExecutor);
        Assert.Contains(result.BlockingReasons, item => item.Contains("진행 중", StringComparison.Ordinal));
        Assert.False(result.ExecutionEnabled);
    }

    [Fact]
    public void StaleOrSwappedEvidence_AlwaysBlocksFutureExecutor()
    {
        var plan = Plan();
        var other = Plan("other-device");
        var now = DateTimeOffset.UtcNow;
        var staleInspection = Inspection(plan, now.AddMinutes(-6));
        var swappedEffects = Effects(other, now);
        var result = new StoragePurgePreExecutionGate().Assess(plan, Approval(plan), staleInspection, Identify(plan, now), swappedEffects, Status(plan, now, NvmeSanitizeOperationState.NeverSanitized), now);

        Assert.False(result.IsEligibleForFutureExecutor);
        Assert.True(result.BlockingReasons.Count >= 2);
        Assert.False(result.ExecutionEnabled);
    }

    [Fact]
    public void MissingWinPeOrAuthorization_AlwaysBlocksFutureExecutor()
    {
        var plan = Plan(isWinPe: false);
        var now = DateTimeOffset.UtcNow;
        var noApproval = new PurgeAuthorizationDecision { AuthorizedForFutureExecutor = false, DeviceFingerprint = "" };
        var result = new StoragePurgePreExecutionGate().Assess(plan, noApproval, Inspection(plan, now), Identify(plan, now), Effects(plan, now), Status(plan, now, NvmeSanitizeOperationState.NeverSanitized), now);

        Assert.False(result.IsEligibleForFutureExecutor);
        Assert.Contains(result.BlockingReasons, item => item.Contains("WinPE", StringComparison.Ordinal));
        Assert.Contains(result.BlockingReasons, item => item.Contains("승인", StringComparison.Ordinal));
    }

    private static StorageSanitizeDryRunResult Plan(string uniqueId = "gate-device", bool isWinPe = true) => new()
    {
        OperationId = "gate", RootPath = "D:\\", IsWinPe = isWinPe,
        Device = new StorageDeviceIdentity { RootPath = "D:\\", DriveLetter = "D", DiskNumber = 2, UniqueId = uniqueId, SerialNumber = "serial", Model = "Test NVMe", BusType = "NVMe", SizeBytes = 1000 },
        BlockingReasons = Array.Empty<string>()
    };
    private static PurgeAuthorizationDecision Approval(StorageSanitizeDryRunResult plan) => new() { AuthorizedForFutureExecutor = true, DeviceFingerprint = DeviceFingerprint.Create(plan.Device) };
    private static PurgeReadOnlyInspection Inspection(StorageSanitizeDryRunResult plan, DateTimeOffset at) => new() { State = PurgeInspectionState.VerifiedReadOnly, InspectedAtUtc = at, DeviceFingerprint = DeviceFingerprint.Create(plan.Device) };
    private static NvmeIdentifyReadOnlyEvidence Identify(StorageSanitizeDryRunResult plan, DateTimeOffset at) => new() { ReadSucceeded = true, CollectedAtUtc = at, DeviceFingerprint = DeviceFingerprint.Create(plan.Device), SupportsCryptoErase = true, SupportsBlockErase = true };
    private static NvmeCommandEffectsReadOnlyEvidence Effects(StorageSanitizeDryRunResult plan, DateTimeOffset at) => new() { ReadSucceeded = true, CollectedAtUtc = at, DeviceFingerprint = DeviceFingerprint.Create(plan.Device), SanitizeCommandSupported = true };
    private static NvmeSanitizeStatusReadOnlyEvidence Status(StorageSanitizeDryRunResult plan, DateTimeOffset at, NvmeSanitizeOperationState state) => new() { ReadSucceeded = true, CollectedAtUtc = at, DeviceFingerprint = DeviceFingerprint.Create(plan.Device), State = state };
}