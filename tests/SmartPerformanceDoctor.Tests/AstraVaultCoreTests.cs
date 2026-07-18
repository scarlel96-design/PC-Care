using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;
using Xunit;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Policy;

namespace SmartPerformanceDoctor.Tests;

public sealed class AstraVaultCoreTests
{
    [Fact]
    public void Locator_RoundTrip_And_ReservedZero()
    {
        var loc = VaultLocator.CreateNew(1, 1);
        var bytes = loc.Write();
        var parsed = VaultLocator.Parse(bytes);
        Assert.Equal(loc.ContainerId, parsed.ContainerId);
    }

    [Fact]
    public void Locator_Rejects_BadMagic()
    {
        var bytes = VaultLocator.CreateNew(1, 1).Write();
        bytes[0] = 0xFF;
        Assert.ThrowsAny<Exception>(() => VaultLocator.Parse(bytes));
    }

    [Fact]
    public void Aead_ChaCha_RoundTrip_And_TamperFails()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plain = "astra-vault-test"u8.ToArray();
        var aad = "domain:test"u8.ToArray();
        var ct = AstraAead.Encrypt(AstraCipherSuite.XChaCha20Poly1305, key, plain, aad);
        var back = AstraAead.Decrypt(AstraCipherSuite.XChaCha20Poly1305, key, ct, aad);
        Assert.Equal(plain, back);
        ct.Tag[0] ^= 0xFF;
        Assert.ThrowsAny<Exception>(() =>
            AstraAead.Decrypt(AstraCipherSuite.XChaCha20Poly1305, key, ct, aad));
    }

    [Fact]
    public void Policy_LargeExport_RequiresStepUp()
    {
        var d = VaultPolicyEngine.EvaluateExport(600, 0, modelAvailable: true);
        Assert.Equal(SentinelDecision.RequireStepUp, d);
    }
}