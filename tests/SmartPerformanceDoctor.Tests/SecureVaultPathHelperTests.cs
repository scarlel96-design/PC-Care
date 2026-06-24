using SmartPerformanceDoctor.App.Services.Security;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class SecureVaultPathHelperTests
{
    [Fact]
    public void LooksLikeEncryptedRelativePath_DetectsPipeSeparatedBlob()
    {
        var encrypted = "YWJj/de/Zg==|bm9uY2U=|dGFn";
        Assert.True(SecureVaultPathHelper.LooksLikeEncryptedRelativePath(encrypted));
    }

    [Fact]
    public void NormalizeRelative_RejectsEncryptedBlob()
    {
        var encrypted = "YWJj/de/Zg==|bm9uY2U=|dGFn";
        Assert.Throws<ArgumentException>(() => SecureVaultPathHelper.NormalizeRelative(encrypted));
    }

    [Fact]
    public void CombineUnderRoot_BuildsNestedPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "spd-vault-test-root");
        var target = SecureVaultPathHelper.CombineUnderRoot(root, "nested/file.txt");
        Assert.EndsWith($"{Path.DirectorySeparatorChar}nested{Path.DirectorySeparatorChar}file.txt", target);
    }

    [Fact]
    public void CombineUnderRoot_BlocksTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "spd-vault-test-root");
        Assert.Throws<ArgumentException>(() => SecureVaultPathHelper.CombineUnderRoot(root, "../outside.txt"));
    }

    [Fact]
    public void TryDeriveRelativeFromOriginal_ReturnsRelativePath()
    {
        var root = @"C:\vault\folder";
        var original = @"C:\vault\folder\sub\clip.mp4";
        var relative = SecureVaultPathHelper.TryDeriveRelativeFromOriginal(root, original);
        Assert.Equal("sub/clip.mp4", relative);
    }
}