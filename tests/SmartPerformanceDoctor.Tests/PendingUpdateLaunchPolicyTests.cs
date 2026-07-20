using SmartPerformanceDoctor.App.Services.Update;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class PendingUpdateLaunchPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("yes")]
    public void NormalStartup_DoesNotAutomaticallyFinalizePendingUpdate(string? value)
    {
        Assert.False(PendingUpdateLaunchPolicy.AllowsAutomaticFinalize(value));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    public void ExplicitHandoff_AllowsPendingUpdateFinalize(string value)
    {
        Assert.True(PendingUpdateLaunchPolicy.AllowsAutomaticFinalize(value));
    }
}
