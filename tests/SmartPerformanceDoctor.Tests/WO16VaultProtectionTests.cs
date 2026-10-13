using SmartPerformanceDoctor.App.Services.SystemCare;
using SmartPerformanceDoctor.AstraVault.Target;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO16VaultProtectionTests
{
    [Theory]
    [InlineData("state.dpapi")]
    [InlineData("state.dpapi.previous")]
    [InlineData("vault.header.tmp")]
    [InlineData("vault.metadata.cache")]
    [InlineData("recovery_slots.v6.json.previous")]
    [InlineData("object.avpack")]
    public void Vault_and_anchor_files_are_protected_from_cleanup(string name)
    {
        var now = DateTime.UtcNow;
        Assert.Equal(CleanupDisposition.Protected, StorageInventory.Classify(name, true,
            FileAttributes.Normal, now.AddDays(-30), now));
    }
    [Fact] public void All_production_approval_gates_remain_closed()
    {
        Assert.False(Av3PhaseGate.ProductionWriterEnabled);
        Assert.False(Av3PhaseGate.JournalWriterEnabled);
        Assert.False(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3PhaseGate.WriterEnableReady);
        Assert.False(Av3PhaseGate.ExternalReviewCompleted);
        Assert.False(Av3PhaseGate.ProductionEnableAuthorized);
    }
}
