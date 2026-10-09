using SmartPerformanceDoctor.App.Services.Commercial;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class NvmeSanitizeStatusReadOnlyEvidenceTests
{
    [Fact]
    public void InProgressStatus_ReportsProgressAndIsNonExecutable()
    {
        var log = new byte[512];
        BitConverter.GetBytes((ushort)32768).CopyTo(log, 0);
        BitConverter.GetBytes((ushort)(2 | (3 << 3) | (1 << 7))).CopyTo(log, 2);
        BitConverter.GetBytes(120u).CopyTo(log, 16);

        var evidence = NvmeSanitizeStatusEvidenceParser.Parse(log, "fingerprint", DateTimeOffset.UtcNow);

        Assert.True(evidence.ReadSucceeded);
        Assert.Equal(NvmeSanitizeOperationState.InProgress, evidence.State);
        Assert.InRange(evidence.ProgressBasisPoints!.Value, 4999, 5001);
        Assert.Equal(3, evidence.CompletedOverwritePasses);
        Assert.True(evidence.GlobalDataErasedSinceLastSuccess);
        Assert.Equal(120u, evidence.EstimatedCryptoEraseSeconds);
        Assert.False(evidence.CanExecute);
    }

    [Theory]
    [InlineData(0, NvmeSanitizeOperationState.NeverSanitized)]
    [InlineData(1, NvmeSanitizeOperationState.Succeeded)]
    [InlineData(3, NvmeSanitizeOperationState.Failed)]
    [InlineData(4, NvmeSanitizeOperationState.SucceededWithForcedDeallocation)]
    public void StatusCodes_AreNotCollapsed(int code, NvmeSanitizeOperationState expected)
    {
        var log = new byte[512];
        BitConverter.GetBytes((ushort)code).CopyTo(log, 2);

        var evidence = NvmeSanitizeStatusEvidenceParser.Parse(log, "fingerprint", DateTimeOffset.UtcNow);

        Assert.Equal(expected, evidence.State);
    }

    [Fact]
    public void TruncatedStatusLog_IsRejected()
    {
        var evidence = NvmeSanitizeStatusEvidenceParser.Parse(new byte[64], "fingerprint", DateTimeOffset.UtcNow);

        Assert.False(evidence.ReadSucceeded);
        Assert.Empty(evidence.StatusLogSha256);
    }
}