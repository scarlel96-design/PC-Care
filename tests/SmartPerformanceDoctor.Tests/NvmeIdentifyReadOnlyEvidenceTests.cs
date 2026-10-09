using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class NvmeIdentifyReadOnlyEvidenceTests
{
    [Fact]
    public void SanicapBits_AreParsedAndBoundToSamePhysicalDevice()
    {
        var plan = Plan("nvme-unique-123456");
        var raw = new byte[4096];
        BitConverter.GetBytes(0b111u | (1u << 30)).CopyTo(raw, 328);
        var evidence = NvmeIdentifyEvidenceParser.Parse(raw, DeviceFingerprint.Create(plan.Device), DateTimeOffset.UtcNow);

        var report = new StoragePurgeReadinessService().Evaluate(plan, nvmeIdentify: evidence);

        Assert.True(evidence.ReadSucceeded);
        Assert.True(evidence.SupportsCryptoErase);
        Assert.True(evidence.SupportsBlockErase);
        Assert.True(evidence.SupportsOverwrite);
        Assert.True(evidence.NoDeallocateInhibited);
        Assert.False(evidence.CanExecute);
        Assert.Equal(PurgeSupportState.VerifiedReadOnly, report.Methods.Single(item => item.Method == StorageSanitizeMethod.CryptoErase).State);
        Assert.Equal(PurgeSupportState.VerifiedReadOnly, report.Methods.Single(item => item.Method == StorageSanitizeMethod.BlockErase).State);
        Assert.False(report.ExecutionEnabled);
    }

    [Fact]
    public void MissingSanicapSupport_IsExplicitlyUnsupportedNotGuessed()
    {
        var plan = Plan("nvme-unique-no-sanicap");
        var evidence = NvmeIdentifyEvidenceParser.Parse(new byte[4096], DeviceFingerprint.Create(plan.Device), DateTimeOffset.UtcNow);

        var report = new StoragePurgeReadinessService().Evaluate(plan, nvmeIdentify: evidence);

        Assert.Equal(PurgeSupportState.Unsupported, report.Methods.Single(item => item.Method == StorageSanitizeMethod.CryptoErase).State);
        Assert.Equal(PurgeSupportState.Unsupported, report.Methods.Single(item => item.Method == StorageSanitizeMethod.BlockErase).State);
    }

    [Fact]
    public void TruncatedIdentifyResponse_IsRejectedAsEvidence()
    {
        var evidence = NvmeIdentifyEvidenceParser.Parse(new byte[512], "fingerprint", DateTimeOffset.UtcNow);

        Assert.False(evidence.ReadSucceeded);
        Assert.Empty(evidence.IdentifySha256);
        Assert.False(evidence.CanExecute);
    }

    [Fact]
    public void IdentifyEvidence_ForDifferentDeviceCannotUpgradeReadiness()
    {
        var plan = Plan("nvme-unique-a");
        var other = Plan("nvme-unique-b");
        var raw = new byte[4096];
        BitConverter.GetBytes(0b011u).CopyTo(raw, 328);
        var evidence = NvmeIdentifyEvidenceParser.Parse(raw, DeviceFingerprint.Create(other.Device), DateTimeOffset.UtcNow);

        var report = new StoragePurgeReadinessService().Evaluate(plan, nvmeIdentify: evidence);

        Assert.Equal(PurgeSupportState.CandidateOnly, report.Methods.Single(item => item.Method == StorageSanitizeMethod.CryptoErase).State);
        Assert.Contains(report.RequiredFutureChecks, item => item.Contains("SANICAP 증적이 없습니다", StringComparison.Ordinal));
    }

    private static StorageSanitizeDryRunResult Plan(string uniqueId) => new()
    {
        OperationId = "nvme-identify-test", RootPath = "D:\\",
        Device = new StorageDeviceIdentity
        {
            RootPath = "D:\\", DriveLetter = "D", DiskNumber = 2, UniqueId = uniqueId,
            SerialNumber = "serial-01", Model = "Test NVMe", BusType = "NVMe", SizeBytes = 1_000_000
        },
        BlockingReasons = Array.Empty<string>()
    };
}