using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.UpdateEngine;
using SmartPerformanceDoctor.SecurityLab.VaultV4;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO05CompatibilityTests
{
    [Fact]
    public void ProductionBuildHasNoVmTrustRoot()
    {
        Assert.False(BuildPublisherTrust.IsVmTest);
        Assert.Same(PublisherTrustPolicy.Production, BuildPublisherTrust.Current);
        var metadata = typeof(BuildPublisherTrust).Assembly.GetCustomAttributesData();
        Assert.DoesNotContain(metadata, item => item.ConstructorArguments.Any(arg =>
            arg.Value is string value && value.Contains("WO05-VM-TEST", StringComparison.Ordinal)));
        using var f = new SignedUpdateTests.Fixture();
        Assert.False(SignedPackage.Verify(f.Package(), "51.0.4", BuildPublisherTrust.Current).CanApply);
    }

    [Theory]
    [InlineData("payload/COM\u00b9.txt")]
    [InlineData("payload/LPT\u00b2.txt")]
    [InlineData("payload/CONIN$")]
    [InlineData("payload/CONOUT$")]
    public void AdditionalWindowsDeviceNamesAreRejected(string path) => Assert.False(SignedPackage.SafeRelativePath(path));

    [Theory]
    [InlineData("valid")]
    [InlineData("owner")]
    [InlineData("inheritance")]
    [InlineData("users-write")]
    [InlineData("system-missing")]
    [InlineData("admins-missing")]
    [InlineData("deny")]
    public void AclPolicyRejectsUnsafeDescriptors(string mutation)
    {
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(mutation != "inheritance", false);
        security.SetOwner(mutation == "owner" ? users : admins);
        if (mutation != "admins-missing") security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, AccessControlType.Allow));
        if (mutation != "system-missing") security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(users, mutation == "users-write" ? FileSystemRights.Modify : FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        if (mutation == "deny") security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.Write, AccessControlType.Deny));
        if (mutation == "valid") UpdateTransaction.ValidateProtectedSecurity(security, true);
        else Assert.Throws<UnauthorizedAccessException>(() => UpdateTransaction.ValidateProtectedSecurity(security, true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VaultAndSettingsSurviveUpdateOrRollbackAndSnapshotExport(bool rollback)
    {
        using var f = new SignedUpdateTests.Fixture();
        var root = Path.Combine(f.Install, "synthetic-vault");
        var source = Path.Combine(f.Work, "secret.txt");
        File.WriteAllText(source, "WO05-SYNTHETIC-COMPATIBILITY");
        File.WriteAllText(Path.Combine(f.Install, "settings.json"), "{\"theme\":\"dark\"}");
        using (var vault = new LabVaultService(root))
        {
            Assert.True(vault.Create("Correct-Horse-Battery-99!", LabKdfProfile.LabFast).Success);
            Assert.True(vault.Unlock("Correct-Horse-Battery-99!").Success);
            Assert.True(vault.ImportFile(source).Success);
        }
        var before = HashTree(root);
        var settingsHash = Hash(Path.Combine(f.Install, "settings.json"));
        var result = f.Transaction.Apply(f.Package(), f.Install, "51.0.4",
            postApplyVersionCheck: _ => !rollback);
        Assert.Equal(rollback ? UpdateStage.Restored : UpdateStage.Committed, result.Stage);
        Assert.Equal(before, HashTree(root));
        Assert.Equal(settingsHash, Hash(Path.Combine(f.Install, "settings.json")));
        var output = Path.Combine(f.Work, "recovery-output");
        Assert.Equal(1, VaultSnapshotRecovery.Export(root, output, "Correct-Horse-Battery-99!"));
        Assert.Equal(before, HashTree(root));
        var exported = Directory.GetFiles(Path.Combine(output, "export"), "*", SearchOption.AllDirectories).Single();
        Assert.Equal(Hash(source), Hash(exported));
    }

    [Fact]
    public void WrongPasswordSnapshotExportPreservesSource()
    {
        using var f = new SignedUpdateTests.Fixture();
        var root = Path.Combine(f.Install, "vault");
        using (var vault = new LabVaultService(root)) Assert.True(vault.Create("Correct-Horse-Battery-99!", LabKdfProfile.LabFast).Success);
        var before = HashTree(root);
        Assert.Throws<CryptographicException>(() => VaultSnapshotRecovery.Export(root, Path.Combine(f.Work, "wrong-password"), "Wrong-Password-00!"));
        Assert.Equal(before, HashTree(root));
        Assert.Empty(Directory.GetFiles(f.Work, "*.txt"));
    }

    private static string[] HashTree(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path) + ":" + Hash(path)).ToArray();
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
