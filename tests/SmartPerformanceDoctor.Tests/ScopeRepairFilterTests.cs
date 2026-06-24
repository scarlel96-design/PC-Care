using SmartPerformanceDoctor.Contracts.Services;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class ScopeRepairFilterTests
{
    [Theory]
    [InlineData("system", "dism_checkhealth", true)]
    [InlineData("system", "sfc_verifyonly", true)]
    [InlineData("system", "driver_check_problem_devices", false)]
    [InlineData("system", "audio_scan_devices", false)]
    [InlineData("driver", "driver_check_problem_devices", true)]
    [InlineData("audio", "audio_restart_stack", true)]
    [InlineData("full", "audio_restart_stack", true)]
    public void IsAllowedForScope_EnforcesScopeGates(string scope, string actionId, bool expected) =>
        Assert.Equal(expected, ScopeRepairFilter.IsAllowedForScope(actionId, scope));

    [Fact]
    public void ResolveModuleIds_SystemScope_ExcludesDriverAndAudio()
    {
        var modules = ScopeRepairFilter.ResolveModuleIds("system");
        Assert.Single(modules);
        Assert.Equal("system", modules[0]);
        Assert.DoesNotContain(modules, m => m is "driver" or "audio");
    }

    [Fact]
    public void ResolveModuleIds_FullScope_IncludesAllModules()
    {
        var modules = ScopeRepairFilter.ResolveModuleIds("full");
        Assert.Contains(modules, m => m == "driver");
        Assert.Contains(modules, m => m == "audio");
        Assert.Contains(modules, m => m == "system");
    }

    [Fact]
    public void DefaultActionsForScope_System_HasNoDriverAudio()
    {
        var actions = ScopeRepairFilter.DefaultActionsForScope("system");
        Assert.All(actions, id => Assert.True(ScopeRepairFilter.IsAllowedForScope(id, "system")));
        Assert.DoesNotContain(actions, id => id.Contains("driver", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(actions, id => id.Contains("audio", StringComparison.OrdinalIgnoreCase));
    }
}