using SmartPerformanceDoctor.RecoverySandbox;
using Xunit;

namespace SmartPerformanceDoctor.Tests;
public sealed class WO10SandboxTests : IDisposable
{
    private readonly string parent = Path.Combine(Path.GetTempPath(), "wo10-fixtures-" + Guid.NewGuid().ToString("N"));
    public WO10SandboxTests() => Directory.CreateDirectory(parent);
    private SandboxRecovery New() => SandboxRecovery.Create(parent);
    [Fact] public void ActualFileRoundTripPreservesBytes() {var s=New();var before=File.ReadAllBytes(Path.Combine(s.Root,"fixture.dat"));Assert.Equal("Quarantined",s.Execute(s.Approve()).State);Assert.False(File.Exists(Path.Combine(s.Root,"fixture.dat")));Assert.Equal("Restored",s.Recover().State);Assert.Equal(before,File.ReadAllBytes(Path.Combine(s.Root,"fixture.dat")));Assert.Equal(0,s.Recover().Mutations);}
    [Fact] public void ReadyRecoveryVerifiesFile() {var s=New();File.WriteAllText(Path.Combine(s.Root,"fixture.dat"),"changed");Assert.Throws<InvalidDataException>(()=>s.Recover());}
    [Fact] public void ChangedContentBlocksExecution() {var s=New();var c=s.Approve();File.WriteAllText(Path.Combine(s.Root,"fixture.dat"),"changed");Assert.Throws<InvalidDataException>(()=>s.Execute(c));}
    [Fact] public void ReplacedFileIdentityBlocksExecution() {var s=New();var c=s.Approve();var path=Path.Combine(s.Root,"fixture.dat");var bytes=File.ReadAllBytes(path);File.Move(path,path+".old");File.WriteAllBytes(path,bytes);Assert.Throws<IOException>(()=>s.Execute(c));}
    [Theory][InlineData(false)][InlineData(true)] public void MissingOrExpiredConsentBlocks(bool expired) {var s=New();var c=s.Approve() with {Approved=expired,ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(-1)};Assert.Throws<InvalidOperationException>(()=>s.Execute(c));Assert.True(File.Exists(Path.Combine(s.Root,"fixture.dat")));}
    [Fact] public void ConsentFromAnotherActionBlocks() {var a=New();var b=New();Assert.Throws<InvalidOperationException>(()=>b.Execute(a.Approve()));}
    [Fact] public void LockBlocksExecution() {var s=New();var c=s.Approve();using var locked=File.Open(Path.Combine(s.Root,"fixture.dat"),FileMode.Open,FileAccess.Read,FileShare.None);Assert.Throws<IOException>(()=>s.Execute(c));}
    [Fact] public void DuplicateExecutionBlocks() {var s=New();var c=s.Approve();s.Execute(c);Assert.Throws<InvalidOperationException>(()=>s.Execute(c));}
    [Fact] public void OriginalCollisionIsPreserved() {var s=New();s.Execute(s.Approve());var original=Path.Combine(s.Root,"fixture.dat");File.WriteAllText(original,"user-collision");Assert.Throws<IOException>(()=>s.Recover());Assert.Equal("user-collision",File.ReadAllText(original));Assert.True(File.Exists(Path.Combine(s.Root,"fixture.quarantine")));}
    [Fact] public void TamperedJournalBlocksRecovery() {var s=New();s.Execute(s.Approve());var path=Path.Combine(s.Root,"execution.json");File.WriteAllText(path,File.ReadAllText(path).Replace("Quarantined","Restored"));Assert.Throws<InvalidDataException>(()=>s.Recover());Assert.True(File.Exists(Path.Combine(s.Root,"fixture.quarantine")));}
    [Fact] public void CorruptJournalBlocks() {var s=New();File.WriteAllText(Path.Combine(s.Root,"execution.json"),"{");Assert.ThrowsAny<System.Text.Json.JsonException>(()=>SandboxRecovery.Open(s.Root));}
    [Fact] public void ChangedQuarantineBlocksRollback() {var s=New();s.Execute(s.Approve());File.WriteAllText(Path.Combine(s.Root,"fixture.quarantine"),"changed");Assert.Throws<InvalidDataException>(()=>s.Recover());}
    [Fact] public void CancellationBeforeMovePreservesOriginal() {var s=New();using var c=new CancellationTokenSource();c.Cancel();Assert.Throws<OperationCanceledException>(()=>s.Execute(s.Approve(),c.Token));Assert.True(File.Exists(Path.Combine(s.Root,"fixture.dat")));}
    [Theory][InlineData("Prepared")][InlineData("Moved")][InlineData("Quarantined")]
    public void ExceptionCheckpointRecovers(string point) {var s=New();var bytes=File.ReadAllBytes(Path.Combine(s.Root,"fixture.dat"));Assert.Throws<IOException>(()=>s.Execute(s.Approve(),checkpoint:p=>{if(p==point)throw new IOException("injected");}));Assert.Equal("Restored",s.Recover().State);Assert.Equal(bytes,File.ReadAllBytes(Path.Combine(s.Root,"fixture.dat")));}
    [Theory][InlineData("RollingBack")][InlineData("RestoredMove")][InlineData("Restored")]
    public void InterruptedRollbackIsIdempotent(string point) {var s=New();s.Execute(s.Approve());Assert.Throws<IOException>(()=>s.Recover(p=>{if(p==point)throw new IOException("injected");}));Assert.Equal("Restored",s.Recover().State);}
    [Fact] public void ArbitraryUserDirectoryIsRejected() => Assert.Throws<InvalidDataException>(()=>SandboxRecovery.Open(parent));
    [Fact] public void OversizeJournalBlocks() {var s=New();File.WriteAllText(Path.Combine(s.Root,"execution.json"),new string('x',9000));Assert.Throws<InvalidDataException>(()=>SandboxRecovery.Open(s.Root));}
    [Fact] public void ConcurrentExecutionIsBlocked() {var s=New();var c=s.Approve();s.Execute(c,checkpoint:p=>{if(p=="Prepared")Assert.Throws<IOException>(()=>s.Execute(c));});s.Recover();}
    [Fact] public void ConsentExpiryIsCheckedAfterPreflight()
    {
        var s=New();var consent=s.Approve() with {ExpiresUtc=DateTimeOffset.UtcNow.AddMilliseconds(100)};
        Assert.Throws<InvalidOperationException>(()=>s.Execute(consent,checkpoint:p=>{if(p=="PreflightValidated")Thread.Sleep(150);}));
        Assert.True(File.Exists(Path.Combine(s.Root,"fixture.dat")));
    }
    [Fact] public void ProductAssemblyHasNoSandboxExecutionDependency()
    {
        var app=System.Reflection.Assembly.Load("SmartPerformanceDoctor");
        Assert.DoesNotContain(app.GetReferencedAssemblies(),a=>a.Name!.Contains("RecoverySandbox"));
        Assert.DoesNotContain(app.GetTypes(),t=>t.FullName!.Contains("SandboxRecovery"));
    }
    [Fact] public void HeldTargetRejectsPathReplacementBeforeMove()
    {
        var s=New();var consent=s.Approve();var path=Path.Combine(s.Root,"fixture.dat");
        s.Execute(consent,checkpoint:p=>{if(p=="PreflightValidated")Assert.Throws<IOException>(()=>File.Move(path,path+".replaced"));});
        Assert.Equal("Restored",s.Recover().State);
    }
    [Fact] public void HeldRootRejectsDirectoryReplacementBeforeMove()
    {
        var s=New();var consent=s.Approve();
        s.Execute(consent,checkpoint:p=>{if(p=="PreflightValidated")Assert.Throws<IOException>(()=>Directory.Move(s.Root,s.Root+"-replaced"));});
        Assert.Equal("Restored",s.Recover().State);
    }
    public void Dispose() {if(Directory.Exists(parent))Directory.Delete(parent,true);}
}
