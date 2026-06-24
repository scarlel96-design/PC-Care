using SmartPerformanceDoctor.Aegis;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class AegisMirrorTests : IDisposable
{
    private readonly string? _previousSigningKeyPath;

    public AegisMirrorTests()
    {
        const string signingKeyVariable = "AEGIS_SIGNING_KEY_PATH";
        _previousSigningKeyPath = Environment.GetEnvironmentVariable(signingKeyVariable);
        var testKey = TestEphemeralSigningKey.CreatePemFile();
        Environment.SetEnvironmentVariable(signingKeyVariable, testKey);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AEGIS_SIGNING_KEY_PATH", _previousSigningKeyPath);
    }

    [Fact]
    public void Baseline_manifest_roundtrip_succeeds()
    {
        var temp = Path.Combine(Path.GetTempPath(), "aegis-test-" + Guid.NewGuid().ToString("N"));
        var mirror = Path.Combine(Path.GetTempPath(), "aegis-mirror-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        File.WriteAllText(Path.Combine(temp, "SmartPerformanceDoctor.exe"), "test-main");
        File.WriteAllText(Path.Combine(temp, "SmartPerformanceDoctor.dll"), "test-dll");

        AegisMirrorPaths.SetMirrorRootOverride(mirror);
        try
        {
            AegisBaselineService.RebuildBaseline(temp, "47.0.0-test");
            var verifier = new AegisIntegrityVerifier();
            var (manifest, valid, source) = AegisManifestQuorum.TryLoadWithQuorum();
            Assert.NotNull(manifest);
            Assert.True(valid);
            Assert.NotEqual("backup-quorum", source);
            Assert.True(File.Exists(AegisMirrorPaths.CapsuleFile));
            Assert.True(AegisSlotManager.BackupSlotReady);
        }
        finally
        {
            AegisMirrorPaths.SetMirrorRootOverride(null);
            AegisRuntimeContext.ResetInstallRoot();
            TryDeleteDirectory(temp);
            TryDeleteDirectory(mirror);
        }
    }

    [Fact]
    public void Audit_chain_appends_and_verifies()
    {
        var mirror = Path.Combine(Path.GetTempPath(), "aegis-audit-" + Guid.NewGuid().ToString("N"));
        AegisMirrorPaths.SetMirrorRootOverride(mirror);
        try
        {
            var id = "test-" + Guid.NewGuid().ToString("N");
            AegisAuditChain.Append(id, "unit-test", "chain", "success");
            Assert.True(AegisAuditChain.VerifyChain());
        }
        finally
        {
            AegisMirrorPaths.SetMirrorRootOverride(null);
            TryDeleteDirectory(mirror);
        }
    }

    [Fact]
    public void Offline_capsule_export_import_roundtrip()
    {
        var temp = Path.Combine(Path.GetTempPath(), "aegis-offline-" + Guid.NewGuid().ToString("N"));
        var mirror = Path.Combine(Path.GetTempPath(), "aegis-mirror-offline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        File.WriteAllText(Path.Combine(temp, "SmartPerformanceDoctor.exe"), "offline-main");
        AegisMirrorPaths.SetMirrorRootOverride(mirror);
        try
        {
            AegisBaselineService.RebuildBaseline(temp, "47.0.0-offline");
            var pack = AegisOfflineCapsule.ExportLatestPack();
            Assert.True(File.Exists(pack));

            File.Delete(AegisMirrorPaths.CapsuleFile);
            Assert.True(AegisOfflineCapsule.TryImportPack(pack));
            Assert.True(File.Exists(AegisMirrorPaths.CapsuleFile));
        }
        finally
        {
            AegisMirrorPaths.SetMirrorRootOverride(null);
            AegisRuntimeContext.ResetInstallRoot();
            TryDeleteDirectory(temp);
            TryDeleteDirectory(mirror);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}