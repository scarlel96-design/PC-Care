using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;
namespace SmartPerformanceDoctor.Tests;

public sealed class WO25InstallationHistoryIntegrationTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void FreshAndLegacyRemainUnverifiedCompatible(bool existing)
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture();
        if (existing) File.WriteAllText(Path.Combine(f.Install, "notes.txt"), "synthetic");
        var result = f.Engine.InspectStartupTrust(f.Install);
        Assert.Equal(existing ? InstallationTrustState.LegacyUnattestedInstallation : InstallationTrustState.FreshInstallation, result.State);
        Assert.False(result.Verified); Assert.False(result.RequiresReview);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void StartupVerifiesThreeSignedUpdates(bool samePath)
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture();
        for (var i = 4; i < 7; i++) Assert.Equal(UpdateStage.Committed, f.Engine.Apply(f.Package($"51.0.{i}", $"51.0.{i+1}", samePath ? "same.bin" : $"file{i}.bin"), f.Install, $"51.0.{i}").Stage);
        var result = f.Engine.InspectStartupTrust(f.Install); Assert.True(result.Verified); Assert.Equal(InstallationTrustState.SignedHistoryVerified, result.State);
    }
    [Fact] public void OlderTamperedContentBlocksStartupAndNextUpdateWithoutMutation()
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture();
        f.Engine.Apply(f.Package("51.0.4", "51.0.5", "older.bin"), f.Install, "51.0.4");
        f.Engine.Apply(f.Package("51.0.5", "51.0.6", "newer.bin"), f.Install, "51.0.5");
        var path = Path.Combine(f.Install, "older.bin"); File.WriteAllText(path, "changed"); var bytes = File.ReadAllBytes(path);
        Assert.True(f.Engine.InspectInstallation(f.Install).Verified);
        var startup = f.Engine.InspectStartupTrust(f.Install); Assert.False(startup.Verified); Assert.True(startup.RequiresReview);
        Assert.Equal(InstallationTrustState.InstallationHistoryMismatch, startup.State);
        Assert.Equal(UpdateStage.RecoveryRequired, f.Engine.Apply(f.Package("51.0.6", "51.0.7", "third.bin"), f.Install, "51.0.6").Stage);
        Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Equal(2, Directory.GetDirectories(f.Work, "tx-*").Length);
    }
    [Theory][InlineData("missing")][InlineData("signature")][InlineData("identity")]
    public void HistoricalEvidenceFailureIsVisible(string kind)
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture();
        f.Engine.Apply(f.Package("51.0.4", "51.0.5", "older.bin"), f.Install, "51.0.4");
        var record = Directory.GetFiles(f.Work, "committed-attestation.json", SearchOption.AllDirectories).Single();
        f.Engine.Apply(f.Package("51.0.5", "51.0.6", "newer.bin"), f.Install, "51.0.5");
        if (kind == "missing") File.Delete(record);
        else if (kind == "signature") { var saved = JsonSerializer.Deserialize<CommittedInstallation>(File.ReadAllText(record))!; File.WriteAllText(record, JsonSerializer.Serialize(saved with { Signature = Convert.ToBase64String(new byte[64]) })); }
        else { var path = Path.Combine(f.Install, "older.bin"); File.Move(path, path + ".old"); File.WriteAllBytes(path, File.ReadAllBytes(path + ".old")); }
        var result = f.Engine.InspectStartupTrust(f.Install); Assert.False(result.Verified); Assert.True(result.RequiresReview);
        Assert.Equal(kind == "missing" ? InstallationTrustState.InstallationHistoryIncomplete : InstallationTrustState.InstallationHistoryMismatch, result.State);
    }
    [Fact] public void UnlistedExecutableRequiresReviewButOrdinaryDataIsPreserved()
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture();
        f.Engine.Apply(f.Package("51.0.4", "51.0.5", "known.bin"), f.Install, "51.0.4");
        var data = Path.Combine(f.Install, "notes.txt"); File.WriteAllText(data, "synthetic data");
        Assert.True(f.Engine.InspectStartupTrust(f.Install).Verified);
        File.WriteAllText(Path.Combine(f.Install, "unlisted.dll"), "synthetic");
        Assert.Equal("INSTALLATION-UNATTESTED-EXECUTABLE", f.Engine.InspectStartupTrust(f.Install).Code);
        Assert.Equal("synthetic data", File.ReadAllText(data));
    }
    [Fact] public void EnrollmentStillProtectsOlderDisjointFilesAfterUpdate()
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "enrolled.bin"), "enrolled.bin");
        Assert.True(f.Engine.EnrollInstallation(f.Install, f.Package("51.0.3", "51.0.4", "enrolled.bin"), "51.0.3", true).Verified);
        Assert.Equal(UpdateStage.Committed, f.Engine.Apply(f.Package("51.0.4", "51.0.5", "new.bin"), f.Install, "51.0.4").Stage);
        Assert.True(f.Engine.InspectStartupTrust(f.Install).Verified);
        File.WriteAllText(Path.Combine(f.Install, "enrolled.bin"), "changed");
        Assert.False(f.Engine.InspectStartupTrust(f.Install).Verified);
    }
    [Fact] public void RestoredFirstAttemptPreservesEnrollmentTrust()
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture();
        File.WriteAllText(Path.Combine(f.Install, "enrolled.bin"), "enrolled.bin");
        f.Engine.EnrollInstallation(f.Install, f.Package("51.0.3", "51.0.4", "enrolled.bin"), "51.0.3", true);
        var result = f.Engine.Apply(f.Package("51.0.4", "51.0.5", "new.bin"), f.Install, "51.0.4", faultInjector: p => { if (p == UpdateFaultPoint.BeforeCommittedJournal) throw new IOException("synthetic"); });
        Assert.Equal(UpdateStage.Restored, result.Stage); Assert.True(f.Engine.InspectStartupTrust(f.Install).Verified);
    }
    [Theory][InlineData(UpdateStage.Committed)][InlineData(UpdateStage.Restored)]
    public void ArchivedLegacyTransitionRemainsUnverifiedAndCanAcceptSignedUpdate(UpdateStage terminal)
    {
        using var f = new SignedUpdateTests.Fixture(); File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");
        f.Transaction.Apply(f.Package(),f.Install,"51.0.4",faultInjector:p=>{if(terminal==UpdateStage.Restored&&p==UpdateFaultPoint.BeforeCommittedJournal)throw new IOException("synthetic");});
        typeof(WO06RecoveryTests).GetMethod("MakeLegacy",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,[f,terminal]);
        f.Transaction.Recover(f.Install);
        var before=f.Transaction.InspectStartupTrust(f.Install);
        var result=f.Transaction.Apply(f.Package(),f.Install,"51.0.4");
        Assert.True(result.Stage==UpdateStage.Committed,JsonSerializer.Serialize(new{Before=before,Result=result}));
        Assert.False(before.Verified);
    }
    [Fact] public void ArchivedLegacyRestorationWithChangedBytesRequiresReview()
    {
        using var f = new SignedUpdateTests.Fixture(); File.WriteAllText(Path.Combine(f.Install,"app.txt"),"old");
        f.Transaction.Apply(f.Package(),f.Install,"51.0.4",faultInjector:p=>{if(p==UpdateFaultPoint.BeforeCommittedJournal)throw new IOException("synthetic");});
        typeof(WO06RecoveryTests).GetMethod("MakeLegacy",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,[f,UpdateStage.Restored]);
        f.Transaction.Recover(f.Install);
        File.WriteAllText(Path.Combine(f.Install,"app.txt"),"unexpected bytes");
        var result=f.Transaction.InspectStartupTrust(f.Install);
        Assert.False(result.Verified); Assert.True(result.RequiresReview);
        Assert.Equal("RESTORATION-CONTENT-REVIEW",result.Code);
        Assert.Equal("unexpected bytes",File.ReadAllText(Path.Combine(f.Install,"app.txt")));
    }
    [Fact] public void ActualProductAndLauncherUseStartupPolicy()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
        Assert.NotNull(root);
        foreach (var path in new[] { "src/SmartPerformanceDoctor.App/Services/Update/UpdateRecoveryCoordinator.cs", "tools/SmartPerformanceDoctor.InstallationLauncher/Program.cs" })
            Assert.Contains(".InspectStartupTrust(", File.ReadAllText(Path.Combine(root!.FullName, path)));
        Assert.DoesNotContain(".InspectInstallationTrust(", File.ReadAllText(Path.Combine(root!.FullName, "src/SmartPerformanceDoctor.App/Services/Update/UpdateRecoveryCoordinator.cs")));
    }
}

public sealed class WO25SignedDeploymentManifestTests
{
    [Theory][InlineData("unchanged")][InlineData("dll")][InlineData("deps")][InlineData("runtime")][InlineData("missing")][InlineData("extra")][InlineData("key")][InlineData("manifest")]
    public void SignatureAndEntireInputClosureAreRequired(string change)
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var names = new[] { "helper.dat", "helper.deps.json", "helper.runtimeconfig.json" };
        foreach (var name in names) File.WriteAllText(Path.Combine(f.Install, name), name.EndsWith(".json") ? "{}" : "synthetic binary");
        var rows = names.Select(name => new DeploymentInputFile(name, new FileInfo(Path.Combine(f.Install, name)).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(f.Install, name)))), false)).ToArray();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new DeploymentInputManifest("pccare-test-deployment-v1", "1.0.0", new string('A', 40), rows));
        var signature = key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation); var publicKey = key.ExportSubjectPublicKeyInfo(); var pin = Convert.ToHexString(SHA256.HashData(publicKey));
        if (change == "dll") File.WriteAllText(Path.Combine(f.Install, "helper.dat"), "different");
        if (change == "deps") File.WriteAllText(Path.Combine(f.Install, "helper.deps.json"), "{\"changed\":true}");
        if (change == "runtime") File.WriteAllText(Path.Combine(f.Install, "helper.runtimeconfig.json"), "{\"changed\":true}");
        if (change == "missing") File.Delete(Path.Combine(f.Install, names[0]));
        if (change == "extra") File.WriteAllText(Path.Combine(f.Install, "unlisted.dat"), "extra");
        if (change == "key") pin = new string('0', 64);
        if (change == "manifest") bytes[^2] ^= 1;
        if (change != "unchanged") { var error = Record.Exception(() => SignedDeploymentClosure.Bind(f.Install, bytes, signature, publicKey, pin, false)); Assert.True(error is IOException or InvalidDataException or CryptographicException); return; }
        using var bound = SignedDeploymentClosure.Bind(f.Install, bytes, signature, publicKey, pin, false);
        Assert.Equal(3, bound.Files.Count); Assert.False(bound.AuthenticodeVerified);
        Assert.Throws<IOException>(() => File.WriteAllText(Path.Combine(f.Install, names[0]), "mutation"));
        Assert.Throws<InvalidDataException>(() => bound.VerifyAuthenticode()); bound.Validate();
    }
    [Fact] public void SignedDepsCannotReferToMissingRuntimeAsset()
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var path = Path.Combine(f.Install, "app.deps.json"); File.WriteAllText(path, "{\"targets\":{\"net10.0\":{\"app/1.0\":{\"runtime\":{\"missing.dll\":{}}}}}}");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new DeploymentInputManifest("pccare-test-deployment-v1", "1.0.0", new string('A',40), [new("app.deps.json",new FileInfo(path).Length,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),false)]));
        var publicKey = key.ExportSubjectPublicKeyInfo();
        var error = Assert.Throws<InvalidDataException>(() => SignedDeploymentClosure.Bind(f.Install, bytes, key.SignData(bytes,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation),publicKey,Convert.ToHexString(SHA256.HashData(publicKey)),false));
        Assert.Equal("DEPLOYMENT-DEPENDENCY-MISSING",error.Message);
    }
}

public sealed class WO25ReplacementErrorDiagnosticsTests
{
    [Theory][InlineData("PRIMARY-ERROR","PRIMARY-ERROR")][InlineData("Access denied: C:/synthetic/private/file.bin","EXCEPTION-DETAIL-REDACTED")]
    public void DiagnosticsPreserveMachineCodesWithoutRecordingRawPaths(string message,string expected)
    {
        var result=UpdateFailureDiagnostic.Capture(new IOException(message),"ExistingFileCopy",UpdateStage.Applying,"synthetic-id","volume-a","volume-a",false,true);
        Assert.Equal(expected,result.ErrorCode); Assert.DoesNotContain("private",JsonSerializer.Serialize(result));
    }
    [Fact] public void SharingConflictPreservesBytesAndReportsNativeError()
    {
        using var f = new WO23LegacyPublicationGapTests.Fixture(); var path=Path.Combine(f.Install,"target.bin");File.WriteAllText(path,"old bytes");
        using var other = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var result=f.Engine.Apply(f.Package("51.0.4","51.0.5","target.bin"),f.Install,"51.0.4");
        Assert.NotNull(result.Diagnostic); Assert.Equal(32,result.Diagnostic!.NativeErrorCode); Assert.False(result.AnyMutationStarted);
        Assert.Equal("DestinationHandleAcquisition",result.Diagnostic.Phase);Assert.Equal("old bytes",File.ReadAllText(path));
    }
    [Theory][InlineData("SIGNED-HISTORY-VERIFIED")][InlineData("INSTALLATION-HISTORY-INCOMPLETE")][InlineData("INSTALLATION-HISTORY-SIGNATURE")][InlineData("INSTALLATION-UNATTESTED-EXECUTABLE")][InlineData("PUBLISH-SAME-VOLUME-REQUIRED")]
    public void ProductStatusHasKoreanExplanation(string code) { var text=UpdateStateText.Installation(code);Assert.DoesNotContain(code,text);Assert.True(text.Length>20); }
}
