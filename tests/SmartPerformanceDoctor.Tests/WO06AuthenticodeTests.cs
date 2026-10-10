using SmartPerformanceDoctor.UpdateEngine;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO06AuthenticodeTests
{
    [Fact]
    public void UnsignedBinaryIsNotVerified()
    {
        var result = AuthenticodeBoundary.Verify(typeof(UpdateTransaction).Assembly.Location);
        Assert.NotEqual(0, result.Code);
        Assert.Null(result.Thumbprint);
        Assert.False(AuthenticodeBoundary.IsBuildPublisher(result));
    }

    [Fact]
    public void MissingFileAndExtractionFailureRemainUnverified()
    {
        var result = AuthenticodeBoundary.Verify(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"));
        Assert.NotEqual(0, result.Code);
        Assert.False(AuthenticodeBoundary.IsBuildPublisher(result));
    }

    [Fact]
    public void PinnedVerificationRejectsPathReplacementAndDeletion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pccare-wo06-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "signed.exe");
            File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"), path);
            var blocked = 0;
            var result = AuthenticodeBoundary.Verify(path, () =>
            {
                try { File.Delete(path); } catch (IOException) { blocked++; }
                try { File.WriteAllBytes(path, [0]); } catch (IOException) { blocked++; }
            });
            Assert.Equal(2, blocked);
            Assert.Equal(0, result.Code);
            Assert.NotNull(result.Thumbprint);
            Assert.Equal(49, result.Identity.Length);
            Assert.Equal(64, result.Hash.Length);
            Assert.False(AuthenticodeBoundary.IsBuildPublisher(result));
            using (var edit = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            { edit.Position = 512; var value = edit.ReadByte(); edit.Position = 512; edit.WriteByte((byte)(value ^ 1)); }
            Assert.NotEqual(0, AuthenticodeBoundary.Verify(path).Code);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("--exec")]
    [InlineData("--apply")]
    [InlineData("--recover")]
    public void ExternalBootstrapAndInstallLocationsAreRejected(string verb)
    {
        Assert.Throws<InvalidDataException>(() => UpdateBootstrap.Validate(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.Throws<InvalidDataException>(() => UpdateBootstrap.LaunchInstalled(Path.GetTempPath(), verb, "synthetic.spdup", 1));
    }

    [Fact]
    public void ProductionExecutablePinsRemainImmutableAndEmpty()
    {
        Assert.Empty(BuildPublisherTrust.ExecutableThumbprints);
        Assert.False(BuildPublisherTrust.ExecutableThumbprints is HashSet<string>);
    }
}
