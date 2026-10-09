using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class NvmeCommandEffectsReadOnlyEvidenceTests
{
    [Fact]
    public void SanitizeOpcodeSupport_IsReadFromAdminEffectsEntry()
    {
        var log = new byte[4096];
        BitConverter.GetBytes(0b1u | (1u << 4)).CopyTo(log, 0x84 * 4);

        var evidence = NvmeCommandEffectsEvidenceParser.Parse(log, "fingerprint", DateTimeOffset.UtcNow);

        Assert.True(evidence.ReadSucceeded);
        Assert.True(evidence.SanitizeCommandSupported);
        Assert.True(evidence.SanitizeMayChangeControllerCapabilities);
        Assert.NotEmpty(evidence.EffectsLogSha256);
        Assert.False(evidence.CanExecute);
    }

    [Fact]
    public void CrossCheck_RequiresBothFreshReportsForSameDevice()
    {
        var plan = Plan("nvme-cross-check");
        var identifyRaw = new byte[4096];
        BitConverter.GetBytes(0b111u).CopyTo(identifyRaw, 328);
        var effectsRaw = new byte[4096];
        BitConverter.GetBytes(0b1u).CopyTo(effectsRaw, 0x84 * 4);
        var now = DateTimeOffset.UtcNow;
        var identify = NvmeIdentifyEvidenceParser.Parse(identifyRaw, DeviceFingerprint.Create(plan.Device), now);
        var effects = NvmeCommandEffectsEvidenceParser.Parse(effectsRaw, DeviceFingerprint.Create(plan.Device), now);

        var result = NvmePurgeCapabilityCrossCheck.Evaluate(plan, identify, effects, now);

        Assert.True(result.IsCurrent);
        Assert.True(result.SanitizeCommandSupported);
        Assert.True(result.CryptoEraseCrossValidated);
        Assert.True(result.BlockEraseCrossValidated);
        Assert.True(result.OverwriteCrossValidated);
        Assert.False(result.CanExecute);
    }

    [Fact]
    public void CrossCheck_RejectsEffectsEvidenceForDifferentDevice()
    {
        var plan = Plan("nvme-cross-a");
        var other = Plan("nvme-cross-b");
        var raw = new byte[4096];
        BitConverter.GetBytes(0b111u).CopyTo(raw, 328);
        var effects = new byte[4096];
        BitConverter.GetBytes(0b1u).CopyTo(effects, 0x84 * 4);
        var now = DateTimeOffset.UtcNow;

        var result = NvmePurgeCapabilityCrossCheck.Evaluate(
            plan,
            NvmeIdentifyEvidenceParser.Parse(raw, DeviceFingerprint.Create(plan.Device), now),
            NvmeCommandEffectsEvidenceParser.Parse(effects, DeviceFingerprint.Create(other.Device), now),
            now);

        Assert.False(result.IsCurrent);
        Assert.False(result.SanitizeCommandSupported);
        Assert.False(result.CryptoEraseCrossValidated);
    }

    [Fact]
    public void TruncatedEffectsLog_IsNotAccepted()
    {
        var evidence = NvmeCommandEffectsEvidenceParser.Parse(new byte[512], "fingerprint", DateTimeOffset.UtcNow);

        Assert.False(evidence.ReadSucceeded);
        Assert.Empty(evidence.EffectsLogSha256);
    }

    private static StorageSanitizeDryRunResult Plan(string uniqueId) => new()
    {
        OperationId = "nvme-effects-test", RootPath = "D:\\",
        Device = new StorageDeviceIdentity
        {
            RootPath = "D:\\", DriveLetter = "D", DiskNumber = 2, UniqueId = uniqueId,
            SerialNumber = "serial-01", Model = "Test NVMe", BusType = "NVMe", SizeBytes = 1_000_000
        },
        BlockingReasons = Array.Empty<string>()
    };
}