using System.Security.Cryptography;
using SmartPerformanceDoctor.UpdateEngine;
using Xunit;
namespace SmartPerformanceDoctor.Tests;
public sealed class WO23PreparationCopyCancellationTests
{
    [Fact] public void PartialPreparationCopyPreservesSourceAndExposesIncompleteDestination()
    {
        var root=Path.Combine(Path.GetTempPath(),"wo23-copy-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var bytes=RandomNumberGenerator.GetBytes(3*1024*1024);File.WriteAllBytes(Path.Combine(root,"source.bin"),bytes);
            var hash=Convert.ToHexString(SHA256.HashData(bytes));using var cancellation=new CancellationTokenSource();
            using(var source=WindowsFileBoundary.OpenRead(root,"source.bin"))
            using(var destination=WindowsFileBoundary.OpenWrite(root,"preparation.bin",create:true))
            {
                Assert.Throws<OperationCanceledException>(()=>destination.CopyFrom(source,()=>cancellation.Cancel(),cancellation.Token));
                Assert.InRange(destination.Stream.Position,1,bytes.Length-1);
                Assert.Equal(hash,source.Hash());source.Validate();destination.Validate();
            }
            Assert.Equal(bytes,File.ReadAllBytes(Path.Combine(root,"source.bin")));
            Assert.True(new FileInfo(Path.Combine(root,"preparation.bin")).Length<bytes.Length);
        }
        finally{Directory.Delete(root,true);}
    }
}
