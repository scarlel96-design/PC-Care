using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

[Collection("VaultEnvironment")]
public sealed class LabVaultHeaderAuthenticationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spd-vault-header-auth-" + Guid.NewGuid().ToString("N"));
    public LabVaultHeaderAuthenticationTests()
    {
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("SPD_TEST_VAULT_ROOT", _root);
    }

    [Fact]
    public void Tampered_authenticated_headers_are_rejected_after_password_unlock()
    {
        const string password = "HeaderAuthVault99!";
        ProductFeatureFlags.EnableProductHost();
        try
        {
            using var vault = new SecureVaultService();
            Assert.True(vault.CreateVault(password).Success);
            vault.Lock();
            foreach (var name in new[] { "vault.header.json", "vault.header.copy1.json", "vault.header.backup.json" })
            {
                var path = Path.Combine(SecureVaultPaths.Root, name);
                File.WriteAllText(path, File.ReadAllText(path).Replace("XChaCha20-Poly1305-v2", "AES-256-GCM", StringComparison.Ordinal));
            }
            var unlock = vault.Unlock(password);
            Assert.False(unlock.Success);
            Assert.Contains("헤더 인증", unlock.Message, StringComparison.Ordinal);
        }
        finally { ProductFeatureFlags.ResetForTests(); }
    }

    [Fact]
    public async Task Authenticated_old_header_copy_is_rejected_when_metadata_generation_has_advanced()
    {
        const string password = "RollbackAuthVault99!";
        ProductFeatureFlags.EnableProductHost();
        try
        {
            using var vault = new SecureVaultService();
            Assert.True(vault.CreateVault(password).Success);
            var headerCopies = new Dictionary<string, string>();
            foreach (var name in new[] { "vault.header.json", "vault.header.copy1.json", "vault.header.backup.json" })
            {
                headerCopies[name] = File.ReadAllText(Path.Combine(SecureVaultPaths.Root, name));
            }

            var source = Path.Combine(Path.GetTempPath(), "spd-vault-generation-" + Guid.NewGuid().ToString("N") + ".txt");
            await File.WriteAllTextAsync(source, "advance metadata generation");
            Assert.True((await vault.AddFileAsync(source, sealOrigin: false)).Success);
            vault.Lock();

            foreach (var pair in headerCopies)
            {
                File.WriteAllText(Path.Combine(SecureVaultPaths.Root, pair.Key), pair.Value);
            }

            var unlock = vault.Unlock(password);
            Assert.False(unlock.Success);
            Assert.Contains("rollback", unlock.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { ProductFeatureFlags.ResetForTests(); }
    }

    [Fact]
    public void Recovery_code_unlocks_through_the_same_authenticated_header_gate()
    {
        const string password = "RecoveryHeaderGate99!";
        ProductFeatureFlags.EnableProductHost();
        try
        {
            using var vault = new SecureVaultService();
            var created = vault.CreateVault(password);
            Assert.True(created.Success);
            Assert.NotEmpty(created.RecoveryCodes!);
            var code = created.RecoveryCodes![0];
            vault.Lock();
            var unlock = vault.UnlockWithRecoveryKey(code);
            Assert.True(unlock.Success, unlock.Message);
        }
        finally { ProductFeatureFlags.ResetForTests(); }
    }
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SPD_TEST_VAULT_ROOT", null);
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
