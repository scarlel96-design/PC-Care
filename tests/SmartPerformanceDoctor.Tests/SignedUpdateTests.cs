using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;
using SmartPerformanceDoctor.UpdateEngine;
using SmartPerformanceDoctor.App.Services;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class SignedUpdateTests
{
    private static readonly DateTimeOffset Clock = DateTimeOffset.UtcNow;

    [Fact]
    public void RuntimeExecutionPolicyKeepsThreeTrustDimensionsSeparate()
    {
        Assert.Equal(ExecutionPolicyDecision.Blocked,
            RuntimeTrustPolicy.Decide(CryptographicVerificationResult.Unsigned,
                PublisherTrustResult.Unknown, localDevelopment: false));
        Assert.Equal(ExecutionPolicyDecision.Allowed,
            RuntimeTrustPolicy.Decide(CryptographicVerificationResult.Unsigned,
                PublisherTrustResult.Unknown, localDevelopment: true));
        Assert.Equal(ExecutionPolicyDecision.Blocked,
            RuntimeTrustPolicy.Decide(CryptographicVerificationResult.Failed,
                PublisherTrustResult.Trusted, localDevelopment: true));
        Assert.Equal(ExecutionPolicyDecision.Blocked,
            RuntimeTrustPolicy.Decide(CryptographicVerificationResult.Verified,
                PublisherTrustResult.Untrusted, localDevelopment: true));
        Assert.Equal(ExecutionPolicyDecision.Allowed,
            RuntimeTrustPolicy.Decide(CryptographicVerificationResult.Verified,
                PublisherTrustResult.Trusted, localDevelopment: false));
    }

    [Fact]
    public void DevelopmentKeyIsRejectedByProductionPolicy()
    {
        using var f = new Fixture();
        var package = f.Package();
        Assert.Equal(PublisherVerification.PublisherVerified,
            SignedPackage.Verify(package, "51.0.4", f.Policy, Clock).Trust);
        var production = SignedPackage.Verify(package, "51.0.4", PublisherTrustPolicy.Production, Clock);
        Assert.Equal(PublisherVerification.ContentVerifiedOnly, production.Trust);
        Assert.False(production.CanApply);
    }

    [Theory]
    [InlineData("bad-signature", "SIGNATURE-FAIL")]
    [InlineData("other-publisher", "UNKNOWN-PUBLISHER")]
    [InlineData("content-tamper", "CONTENT-HASH")]
    [InlineData("extra-file", "ALLOWLIST")]
    [InlineData("downgrade", "POLICY")]
    [InlineData("traversal", "ALLOWLIST")]
    [InlineData("duplicate-entry", "ENTRY-PATH")]
    [InlineData("missing-file", "ALLOWLIST")]
    [InlineData("manifest-tamper", "SIGNATURE-FAIL")]
    public void NegativePackagesFailClosed(string mutation, string code)
    {
        using var f = new Fixture();
        var package = f.Package(mutation);
        var result = SignedPackage.Verify(package, "51.0.4", f.Policy, Clock);
        Assert.Equal(code, result.Code);
        Assert.False(result.CanApply);
    }

    [Fact]
    public void TransactionAppliesAllowlistAndPreservesUnlistedData()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        File.WriteAllText(Path.Combine(f.Install, "user-data.txt"), "private");
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4");
        Assert.Equal(UpdateStage.Committed, result.Stage);
        Assert.Equal("new", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(f.Install, "second.txt")));
        Assert.Equal("private", File.ReadAllText(Path.Combine(f.Install, "user-data.txt")));
    }

    [Fact]
    public void CommittedPackageIdCannotBeReplayed()
    {
        using var f = new Fixture();
        var package = f.Package();
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(package, f.Install, "51.0.4").Stage);
        var replay = f.Transaction.Apply(package, f.Install, "51.0.4");
        Assert.Equal(UpdateStage.Prepared, replay.Stage);
        Assert.Equal("REPLAY", replay.Code);
    }

    [Fact]
    public void StagingTamperRollsBackOriginalAndNewFiles()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4", count =>
        {
            if (count == 1)
            {
                var dir = Directory.GetDirectories(f.Work, "tx-*").Single();
                File.WriteAllText(Path.Combine(dir, "stage", "second.txt"), "tampered");
            }
        });
        Assert.Equal(UpdateStage.Restored, result.Stage);
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.False(File.Exists(Path.Combine(f.Install, "second.txt")));
    }

    [Fact]
    public void ConcurrentWorkLockIsRejected()
    {
        using var f = new Fixture();
        using var gate = new FileStream(Path.Combine(f.Work, "update.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(() => f.Transaction.Apply(f.Package(), f.Install, "51.0.4"));
    }

    [Fact]
    public void PartialApplyFailureRestoresOriginal()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4", _ => throw new IOException("synthetic copy failure"));
        Assert.Equal(UpdateStage.Restored, result.Stage);
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
    }

    [Fact]
    public void FailedInstalledVersionPostconditionRestoresOriginal()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4",
            postApplyVersionCheck: _ => false);
        Assert.Equal(UpdateStage.Restored, result.Stage);
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.False(File.Exists(Path.Combine(f.Install, "second.txt")));
    }

    [Fact]
    public void RestartRecoveryReplaysIncompleteApplyingJournal()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        var dir = Directory.GetDirectories(f.Work, "tx-*").Single();
        var journalPath = Path.Combine(dir, "journal.json");
        var journal = JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(journalPath))!;
        journal.Stage = UpdateStage.Applying;
        File.WriteAllText(journalPath, JsonSerializer.Serialize(journal));
        var recovery = f.Transaction.Recover(f.Install);
        Assert.Equal(UpdateStage.Restored, recovery.Stage);
        Assert.Equal("old", File.ReadAllText(Path.Combine(f.Install, "app.txt")));
        Assert.False(File.Exists(Path.Combine(f.Install, "second.txt")));
    }

    [Fact]
    public void MissingBackupLeavesRecoveryRequired()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Install, "app.txt"), "old");
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4", count =>
        {
            if (count != 1) return;
            var dir = Directory.GetDirectories(f.Work, "tx-*").Single();
            File.Delete(Path.Combine(dir, "backup", "app.txt"));
            throw new IOException("synthetic power loss");
        });
        Assert.Equal(UpdateStage.RecoveryRequired, result.Stage);
    }

    [Fact]
    public void ExistingHardlinkOutsideInstallIsNotModified()
    {
        using var f = new Fixture();
        var outside = Path.Combine(f.Work, "outside.txt");
        File.WriteAllText(outside, "old");
        Assert.True(CreateHardLink(Path.Combine(f.Install, "app.txt"), outside, IntPtr.Zero));
        Assert.Equal(UpdateStage.Committed, f.Transaction.Apply(f.Package(), f.Install, "51.0.4").Stage);
        Assert.Equal("old", File.ReadAllText(outside));
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newFileName, string existingFileName, IntPtr securityAttributes);

    private sealed class Fixture : IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly string _root = Path.Combine(Path.GetTempPath(), "pccare-wo03-" + Guid.NewGuid().ToString("N"));
        public string Install => Path.Combine(_root, "install");
        public string Work => Path.Combine(_root, "work");
        public PublisherTrustPolicy Policy => new([new TrustedPublisherKey("WO03-Test-Only", "test-key",
            "stable", _key.ExportSubjectPublicKeyInfo())]);
        public UpdateTransaction Transaction => new(Policy, Work, strictAcl: false);

        public Fixture()
        {
            Directory.CreateDirectory(Install);
            Directory.CreateDirectory(Work);
        }

        public string Package(string mutation = "")
        {
            var first = System.Text.Encoding.UTF8.GetBytes("new");
            var second = System.Text.Encoding.UTF8.GetBytes("second");
            var manifest = new SignedManifest
            {
                Publisher = mutation == "other-publisher" ? "Other" : "WO03-Test-Only",
                KeyId = "test-key", PackageId = Guid.NewGuid().ToString("N"), FromVersion = "51.0.4",
                ToVersion = mutation == "downgrade" ? "51.0.3" : "51.0.5",
                MinimumSupportedVersion = "51.0.0", CreatedAt = Clock.AddDays(-1),
                ExpiresAt = Clock.AddDays(1),
                Files =
                [
                    new SignedFile { Path = mutation == "traversal" ? "payload/../evil.txt" : "payload/app.txt",
                        Length = first.Length, Sha256 = Convert.ToHexString(SHA256.HashData(first)) },
                    new SignedFile { Path = "payload/second.txt", Length = second.Length,
                        Sha256 = Convert.ToHexString(SHA256.HashData(second)) }
                ]
            };
            var signature = _key.SignData(SignedPackage.CanonicalBytes(manifest), HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (mutation == "bad-signature") signature[0] ^= 0x80;
            if (mutation == "manifest-tamper") manifest.ToVersion = "51.0.6";
            var package = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".spdup");
            using var stream = File.Create(package);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            Add(archive, SignedPackage.ManifestName, JsonSerializer.SerializeToUtf8Bytes(manifest));
            Add(archive, SignedPackage.SignatureName,
                JsonSerializer.SerializeToUtf8Bytes(new SignatureEnvelope { Signature = Convert.ToBase64String(signature) }));
            Add(archive, manifest.Files[0].Path, mutation == "content-tamper" ? "bad"u8.ToArray() : first);
            if (mutation != "missing-file") Add(archive, manifest.Files[1].Path, second);
            if (mutation == "extra-file") Add(archive, "payload/extra.txt", "extra"u8.ToArray());
            if (mutation == "duplicate-entry") Add(archive, manifest.Files[0].Path, first);
            return package;
        }

        private static void Add(ZipArchive archive, string name, byte[] bytes)
        {
            using var output = archive.CreateEntry(name).Open();
            output.Write(bytes);
        }

        public void Dispose()
        {
            _key.Dispose();
            Directory.Delete(_root, recursive: true);
        }
    }
}
