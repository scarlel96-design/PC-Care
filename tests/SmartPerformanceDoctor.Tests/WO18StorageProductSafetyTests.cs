using System.Security.Cryptography;
using SmartPerformanceDoctor.App.Services.Commercial;
using SmartPerformanceDoctor.App.Services.SystemCare;
using SmartPerformanceDoctor.RecoverySandbox;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO18StorageProductSafetyTests : IDisposable
{
    private readonly string parent=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")
        ??throw new InvalidOperationException("ExplicitSyntheticParentRequired"),"wo18-product-"+Guid.NewGuid().ToString("N"));
    public WO18StorageProductSafetyTests()=>Directory.CreateDirectory(parent);
    [Theory][InlineData("selected.tmp")][InlineData("한글.cache")]
    public void SelectedSyntheticTargetRoundTripsThroughExistingRecoveryEngine(string name)
    {
        var transaction=SandboxRecovery.CreateSelectedSynthetic(parent,name,1048576);
        var path=Path.Combine(transaction.Root,name);var before=SHA256.HashData(File.ReadAllBytes(path));
        var result=transaction.Execute(transaction.Approve());Assert.True(result.BytesVerified);
        Assert.False(File.Exists(path));
        var measurement=transaction.MeasureQuarantine(0,0);
        Assert.Equal(0,measurement.VerifiedFreedBytes);Assert.Equal(1048576,measurement.LogicalBytes);
        Assert.Null(measurement.AllocatedBytes);Assert.Equal(measurement.ObservedFreeSpaceDelta,measurement.UnattributedVolumeDelta);
        var reopened=SandboxRecovery.Open(transaction.Root);Assert.True(reopened.Recover().BytesVerified);
        Assert.Equal(before,SHA256.HashData(File.ReadAllBytes(path)));
    }
    [Fact]public void SelectedTargetRestoreCollisionPreservesBothFiles()
    {
        var transaction=SandboxRecovery.CreateSelectedSynthetic(parent,"selected.tmp");
        transaction.Execute(transaction.Approve());
        var path=Path.Combine(transaction.Root,"selected.tmp");File.WriteAllBytes(path,RandomNumberGenerator.GetBytes(4096));
        var before=File.ReadAllBytes(path);Assert.ThrowsAny<IOException>(()=>transaction.Recover());
        Assert.Equal(before,File.ReadAllBytes(path));Assert.True(File.Exists(Path.Combine(transaction.Root,"fixture.quarantine")));
    }
    [Theory][InlineData("../escape.tmp")][InlineData("vault.header")][InlineData("key.bin")]
    public void SelectedSyntheticFactoryRejectsNonFixtureNames(string name)
        =>Assert.Throws<InvalidDataException>(()=>SandboxRecovery.CreateSelectedSynthetic(parent,name));
    [Fact]public void DirectoryNewEntryInvalidatesApprovalSnapshot()
    {
        var directory=Path.Combine(parent,"tree");Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory,"one.tmp"),new byte[4096]);
        using var review=SecureDeleteDirectoryReview.Capture(directory);
        Assert.False(review.ProductionExecutionEnabled);
        File.WriteAllBytes(Path.Combine(directory,"new.tmp"),new byte[4096]);
        Assert.Throws<IOException>(()=>review.ValidateUnchanged());
        Assert.True(File.Exists(Path.Combine(directory,"new.tmp")));
    }
    [Fact]public void DirectoryChangedBytesInvalidatesSnapshot()
    {
        var directory=Path.Combine(parent,"tree");Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"one.tmp");File.WriteAllBytes(path,new byte[4096]);
        using var review=SecureDeleteDirectoryReview.Capture(directory);
        File.WriteAllBytes(path,RandomNumberGenerator.GetBytes(4096));
        Assert.Throws<InvalidDataException>(()=>review.ValidateUnchanged());
    }
    [Fact]public void DirectoryIdentityDetectsOrBlocksRootRename()
    {
        var directory=Path.Combine(parent,"tree");Directory.CreateDirectory(directory);
        using var review=SecureDeleteDirectoryReview.Capture(directory);
        try { Directory.Move(directory,directory+"-moved"); }
        catch (IOException) { review.ValidateUnchanged(); return; }
        Assert.Throws<IOException>(()=>review.ValidateUnchanged());
        Assert.True(Directory.Exists(directory+"-moved"));
    }
    [Fact]public void DirectoryLockedFileCannotBeQualified()
    {
        var directory=Path.Combine(parent,"tree");Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"one.tmp");File.WriteAllBytes(path,new byte[4096]);
        using var locked=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        Assert.Throws<IOException>(()=>SecureDeleteDirectoryReview.Capture(directory));
    }
    [Fact]public void DirectoryBudgetExhaustionFailsClosed()
    {
        var directory=Path.Combine(parent,"tree");Directory.CreateDirectory(directory);
        for(var i=0;i<513;i++)File.WriteAllBytes(Path.Combine(directory,$"{i}.tmp"),[]);
        Assert.Throws<InvalidDataException>(()=>SecureDeleteDirectoryReview.Capture(directory));
    }
    [Fact]public void RegisteredStoreProtectsOrdinaryNamesAndReloads()
    {
        var custom=Path.Combine(parent,"custom-storage");Directory.CreateDirectory(custom);
        var store=Path.Combine(parent,"catalog");var registry=new StorageProtectionRegistry(store);
        registry.Register(custom,"Vault");
        Assert.True(new StorageProtectionRegistry(store).IsProtected(Path.Combine(custom,"ordinary.tmp")));
        Assert.False(registry.IsProtected(Path.Combine(parent,"custom-storage-other","ordinary.tmp")));
        Assert.DoesNotContain(custom,File.ReadAllText(Path.Combine(store,"stores.dpapi")));
    }
    [Fact]public void CorruptRegistryCannotForgetExistingProtectionSilently()
    {
        var custom=Path.Combine(parent,"custom-storage");Directory.CreateDirectory(custom);
        var store=Path.Combine(parent,"catalog");var registry=new StorageProtectionRegistry(store);registry.Register(custom,"User");
        File.WriteAllBytes(Path.Combine(store,"stores.dpapi"),new byte[32]);
        Assert.Throws<CryptographicException>(()=>registry.IsProtected(Path.Combine(custom,"ordinary.tmp")));
        Assert.Throws<CryptographicException>(()=>registry.Register(custom,"User"));
    }
    [Fact]public void RemovedCatalogCannotBeSilentlyReinitialized()
    {
        var custom=Path.Combine(parent,"custom-storage");Directory.CreateDirectory(custom);
        var store=Path.Combine(parent,"catalog");var registry=new StorageProtectionRegistry(store);registry.Register(custom,"User");
        File.Delete(Path.Combine(store,"stores.dpapi"));
        Assert.Throws<InvalidDataException>(()=>registry.IsProtected(Path.Combine(custom,"ordinary.tmp")));
        Assert.Throws<InvalidDataException>(()=>registry.Register(custom,"User"));
    }
    [Fact]public void AllowlistHasNoExecutionCapability()
    {
        Assert.All(CleanupAllowlist.ReadOnlyProviders(),p=>Assert.False(p.ExecutionEnabled));
        var target=Path.Combine(parent,"fixture.tmp");File.WriteAllBytes(target,new byte[4096]);
        Assert.False(CleanupAllowlist.CanObserve(new("OtherProvider",parent,"unsupported"),target));
        Directory.CreateDirectory(Path.Combine(parent,"not-target"));
        Assert.False(CleanupAllowlist.CanObserve(new("UserTemp",Path.Combine(parent,"not-target"),"preview"),target));
    }
    [Fact]public void ExistingProductionStorageGateRemainsFalse()
    {
        var transaction=SandboxRecovery.CreateSelectedSynthetic(parent,"candidate.tmp");
        var inventory=StorageInventory.CaptureIsolated(transaction.Root);
        Assert.All(inventory.Candidates,c=>Assert.False(c.CanExecute));
    }
    public void Dispose(){if(Directory.Exists(parent))Directory.Delete(parent,true);}
}
