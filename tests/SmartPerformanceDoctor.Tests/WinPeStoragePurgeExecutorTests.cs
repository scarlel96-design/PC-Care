using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WinPeStoragePurgeExecutorTests
{
    [Fact]
    public async Task EligibleWinPeCryptoErase_DispatchesOnlyThroughInjectedTransport()
    {
        var plan = Plan();
        var native = new FakeNative();
        var executor = new WinPeStoragePurgeExecutor(new FakeLocks(), native, new FakeStatus(NvmeSanitizeOperationState.Succeeded), () => true, new FakeEvidence());
        var request = Request(plan, StorageSanitizeMethod.CryptoErase);

        var result = await executor.ExecuteAsync(request);

        Assert.True(result.CommandIssued);
        Assert.True(result.Completed);
        Assert.Equal(1, native.Calls);
        Assert.Equal(StorageSanitizeMethod.CryptoErase, native.Method);
        Assert.True(result.ExecutionEnabled);
        Assert.True(result.EvidenceValid);
    }

    [Fact]
    public async Task InvalidConfirmation_NeverDispatches()
    {
        var plan = Plan();
        var native = new FakeNative();
        var executor = new WinPeStoragePurgeExecutor(new FakeLocks(), native, new FakeStatus(NvmeSanitizeOperationState.Succeeded), () => true, new FakeEvidence());
        var request = Request(plan, StorageSanitizeMethod.CryptoErase, "wrong");

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(request));

        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public async Task NonWinPe_NeverDispatches()
    {
        var plan = Plan();
        var native = new FakeNative();
        var executor = new WinPeStoragePurgeExecutor(new FakeLocks(), native, new FakeStatus(NvmeSanitizeOperationState.Succeeded), () => false, new FakeEvidence());

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(Request(plan, StorageSanitizeMethod.CryptoErase)));

        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public async Task MismatchedPostDispatchStatus_IsNeverReportedAsCompleted()
    {
        var plan = Plan();
        var native = new FakeNative();
        var executor = new WinPeStoragePurgeExecutor(new FakeLocks(), native, new MismatchedStatus(), () => true, new FakeEvidence());

        var result = await executor.ExecuteAsync(Request(plan, StorageSanitizeMethod.CryptoErase));

        Assert.True(result.CommandIssued);
        Assert.False(result.Completed);
        Assert.True(result.EvidenceValid);
        Assert.Contains("동일 장치", result.Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task FailedControllerStatus_IsReportedAfterDispatch()
    {
        var plan = Plan();
        var native = new FakeNative();
        var executor = new WinPeStoragePurgeExecutor(new FakeLocks(), native, new FakeStatus(NvmeSanitizeOperationState.Failed), () => true, new FakeEvidence());

        var result = await executor.ExecuteAsync(Request(plan, StorageSanitizeMethod.BlockErase));

        Assert.True(result.CommandIssued);
        Assert.False(result.Completed);
        Assert.Contains("실패", result.Message, StringComparison.Ordinal);
        Assert.Equal(StorageSanitizeMethod.BlockErase, native.Method);
    }

    [Fact]
    public async Task BootOrSystemDisk_NeverDispatches()
    {
        var plan = Plan(isBootDisk: true);
        var native = new FakeNative();
        var executor = new WinPeStoragePurgeExecutor(new FakeLocks(), native, new FakeStatus(NvmeSanitizeOperationState.Succeeded), () => true, new FakeEvidence());

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(Request(plan, StorageSanitizeMethod.CryptoErase)));

        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public async Task VolumeLockFailure_NeverDispatches()
    {
        var plan = Plan();
        var native = new FakeNative();
        var executor = new WinPeStoragePurgeExecutor(new ThrowingLocks(), native, new FakeStatus(NvmeSanitizeOperationState.Succeeded), () => true, new FakeEvidence());

        var result = await executor.ExecuteAsync(Request(plan, StorageSanitizeMethod.CryptoErase));

        Assert.False(result.CommandIssued);
        Assert.False(result.Completed);
        Assert.True(result.EvidenceValid);
        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public async Task NativeDispatchError_IsRecordedAsSubmissionUncertain()
    {
        var plan = Plan();
        var native = new ThrowingNative();
        var executor = new WinPeStoragePurgeExecutor(new FakeLocks(), native, new FakeStatus(NvmeSanitizeOperationState.Succeeded), () => true, new FakeEvidence());

        var result = await executor.ExecuteAsync(Request(plan, StorageSanitizeMethod.CryptoErase));

        Assert.False(result.CommandIssued);
        Assert.False(result.Completed);
        Assert.Equal(PurgeDispatchState.SubmissionUncertain, result.DispatchState);
        Assert.Contains("알 수 없", result.Message, StringComparison.Ordinal);
        Assert.Equal(1, native.Calls);
    }
    [Fact]
    public async Task MissingOneTimeAuthorization_NeverDispatches()
    {
        var plan = Plan();
        var native = new FakeNative();
        var executor = new WinPeStoragePurgeExecutor(new FakeLocks(), native, new FakeStatus(NvmeSanitizeOperationState.Succeeded), () => true, new FakeEvidence());
        var request = Request(plan, StorageSanitizeMethod.CryptoErase, authorized: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(request));

        Assert.Equal(0, native.Calls);
    }

    [Fact]
    public async Task UnsupportedOverwriteMethod_NeverDispatches()
    {
        var plan = Plan();
        var native = new FakeNative();
        var executor = new WinPeStoragePurgeExecutor(new FakeLocks(), native, new FakeStatus(NvmeSanitizeOperationState.Succeeded), () => true, new FakeEvidence());

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(Request(plan, StorageSanitizeMethod.Overwrite)));

        Assert.Equal(0, native.Calls);
    }
    private static WinPePurgeExecutionRequest Request(StorageSanitizeDryRunResult plan, StorageSanitizeMethod method, string? confirmation = null, bool authorized = true) => new()
    {
        Plan = plan,
        Authorization = new PurgeAuthorizationDecision { AuthorizedForFutureExecutor = authorized, DeviceFingerprint = DeviceFingerprint.Create(plan.Device) },
        Assessment = new PurgePreExecutionAssessment { IsEligibleForFutureExecutor = true, CrossCheck = new NvmePurgeCapabilityCrossCheck { CryptoEraseCrossValidated = true, BlockEraseCrossValidated = true } },
        Method = method,
        ConfirmationPhrase = confirmation ?? WinPeStoragePurgeExecutor.RequiredConfirmation(method, plan.Device.DiskNumber),
        Timeout = TimeSpan.FromMinutes(10),
        StatusPollInterval = TimeSpan.Zero,
        MaximumStatusPolls = 1
    };

    private static StorageSanitizeDryRunResult Plan(bool isBootDisk = false) => new()
    {
        OperationId = "executor-test", RootPath = "D:\\", IsWinPe = true,
        Device = new StorageDeviceIdentity { RootPath = "D:\\", DriveLetter = "D", DiskNumber = 2, UniqueId = "executor-device", SerialNumber = "serial", Model = "Test NVMe", BusType = "NVMe", SizeBytes = 1000, IsBootDisk = isBootDisk },
        BlockingReasons = Array.Empty<string>()
    };

    private sealed class FakeEvidence : IStoragePurgeExecutionEvidence
    {
        public int BeginCalls { get; private set; }
        public int CompleteCalls { get; private set; }
        public PurgeExecutionEvidenceSession Begin(WinPePurgeExecutionRequest request) { BeginCalls++; return new PurgeExecutionEvidenceSession("fake", "fake.pcpurge"); }
        public PurgeExecutionEvidenceReceipt Complete(PurgeExecutionEvidenceSession session, WinPePurgeExecutionResult result) { CompleteCalls++; return new PurgeExecutionEvidenceReceipt(session.Path, true); }
    }
    private sealed class FakeNative : IStorageReinitializeMediaApi
    {
        public int Calls { get; private set; }
        public StorageSanitizeMethod Method { get; private set; }
        public void ReinitializeMedia(int diskNumber, StorageSanitizeMethod method, TimeSpan timeout) { Calls++; Method = method; }
    }
    private sealed class ThrowingNative : IStorageReinitializeMediaApi
    {
        public int Calls { get; private set; }
        public void ReinitializeMedia(int diskNumber, StorageSanitizeMethod method, TimeSpan timeout)
        {
            Calls++;
            throw new TimeoutException("native transport timeout");
        }
    }
    private sealed class FakeLocks : IWinPePurgeVolumeLockCoordinator { public IWinPePurgeVolumeLockLease Acquire(int diskNumber) => new FakeLease(); }
    private sealed class FakeLease : IWinPePurgeVolumeLockLease { public bool IsLocked => true; public void Dispose() { } }
    private sealed class ThrowingLocks : IWinPePurgeVolumeLockCoordinator { public IWinPePurgeVolumeLockLease Acquire(int diskNumber) => throw new IOException("lock failed"); }
    private sealed class MismatchedStatus : INvmeSanitizeStatusReadOnlyReader
    {
        public NvmeSanitizeStatusReadOnlyEvidence Read(StorageSanitizeDryRunResult plan) => new() { ReadSucceeded = true, DeviceFingerprint = "different-device", CollectedAtUtc = DateTimeOffset.UtcNow, State = NvmeSanitizeOperationState.Succeeded };
    }
    private sealed class FakeStatus(NvmeSanitizeOperationState state) : INvmeSanitizeStatusReadOnlyReader
    {
        public NvmeSanitizeStatusReadOnlyEvidence Read(StorageSanitizeDryRunResult plan) => new() { ReadSucceeded = true, DeviceFingerprint = DeviceFingerprint.Create(plan.Device), CollectedAtUtc = DateTimeOffset.UtcNow, State = state };
    }
}
