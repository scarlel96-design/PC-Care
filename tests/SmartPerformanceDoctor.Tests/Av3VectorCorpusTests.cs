using System.Buffers.Binary;
using System.Diagnostics;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Validation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3VectorCorpusTests
{
    private const string TestPassword = "Corpus-Valid-Passw0rd!";
    private const string SecretMarker = "X-SECRET-MARKER-9f3c2a1b";

    private static void AssertUniformUnlockFailure(Action action, params string[] secrets)
    {
        UnlockValidationException? ex = null;
        try
        {
            action();
        }
        catch (UnlockValidationException caught)
        {
            ex = caught;
        }

        Assert.NotNull(ex);
        Assert.Equal(UnlockValidationException.PublicMessage, ex!.Message);
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, ex.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Vector_ValidLocatorAndThreeCopyCandidates_ReadOnlyUnlock()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 2);
        var copy2 = bundle.HeaderCopy.ToArray();
        copy2[32] = 1;
        var result = ReadOnlyUnlockValidator.Validate(
            bundle.Locator,
            [(0, bundle.HeaderCopy), (1, copy2)],
            bundle.MetadataRoot,
            TestPassword);
        Assert.Equal(SmartPerformanceDoctor.AstraVault.Session.VaultSecurityState.ReadOnlyUnlocked, result.State);
    }

    [Fact]
    public void Vector_InvalidHeaderCopy_IsIgnored()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 1);
        var garbage = new byte[VaultHeaderCopy.ExpectedCopySize(1)];
        var result = ReadOnlyUnlockValidator.Validate(
            bundle.Locator,
            [(0, bundle.HeaderCopy), (2, garbage)],
            bundle.MetadataRoot,
            TestPassword);
        Assert.Equal(1ul, result.SelectedHeader.Generation);
    }

    [Fact]
    public void Vector_StaleAuthenticatedCopy_RedundantLowerCopyIgnoredWhenCurrentValid()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 3);
        var redundant = bundle.HeaderCopy.ToArray();
        redundant[32] = 2;
        var result = ReadOnlyUnlockValidator.Validate(
            bundle.Locator,
            [(0, bundle.HeaderCopy), (2, redundant)],
            bundle.MetadataRoot,
            TestPassword);
        Assert.Equal(3ul, result.SelectedHeader.Generation);
    }

    [Fact]
    public void Vector_ConflictingAuthenticatedCurrentCopy_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 3);
        var other = bundle.HeaderCopy.ToArray();
        other[32] = 1;
        other[124] ^= 0x22;
        AssertUniformUnlockFailure(() =>
            ReadOnlyUnlockValidator.Validate(
                bundle.Locator,
                [(0, bundle.HeaderCopy), (1, other)],
                bundle.MetadataRoot,
                TestPassword));
    }

    [Fact]
    public void Vector_UnauthenticatedHigherGeneration_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 2);
        var high = Av3TestVectors.RekeyHeaderGeneration(bundle, TestPassword, 9);
        AssertUniformUnlockFailure(() =>
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(1, high)], bundle.MetadataRoot, TestPassword));
    }

    [Fact]
    public void Vector_EqualGenerationConflictingRoot_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 4);
        var twin = bundle.HeaderCopy.ToArray();
        twin[32] = 1;
        twin[92] ^= 0x11;
        twin[60] = 0;
        AssertUniformUnlockFailure(() =>
            ReadOnlyUnlockValidator.Validate(
                bundle.Locator,
                [(0, bundle.HeaderCopy), (1, twin)],
                bundle.MetadataRoot,
                TestPassword));
    }

    [Fact]
    public void Vector_VmkUnwrapAadMismatch_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 1);
        var header = bundle.HeaderCopy.ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(
            header.AsSpan(VaultHeaderCopy.FixedRegionSize + 12),
            999ul);
        AssertUniformUnlockFailure(() =>
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, header)], bundle.MetadataRoot, TestPassword));
    }

    [Fact]
    public void Vector_ActivationDigestMismatch_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 1);
        var header = bundle.HeaderCopy.ToArray();
        header[61] ^= 0x55;
        AssertUniformUnlockFailure(() =>
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, header)], bundle.MetadataRoot, TestPassword));
    }

    [Fact]
    public void Vector_ActivationAeadTagTamper_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 1);
        var header = bundle.HeaderCopy.ToArray();
        header[204] ^= 0xAA;
        AssertUniformUnlockFailure(() =>
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, header)], bundle.MetadataRoot, TestPassword));
    }

    [Fact]
    public void Vector_MetadataRootGenerationRollback_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 3, metadataGeneration: 1, parentMetadataGeneration: 5);
        AssertUniformUnlockFailure(() =>
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, bundle.HeaderCopy)], bundle.MetadataRoot, TestPassword));
    }

    [Fact]
    public void Vector_MetadataRootContainerIdMismatch_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 1);
        var meta = bundle.MetadataRoot.ToArray();
        meta[30] ^= 0x01;
        AssertUniformUnlockFailure(() =>
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, bundle.HeaderCopy)], meta, TestPassword));
    }

    [Fact]
    public void Vector_MetadataRootDigestMismatch_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 1);
        var meta = bundle.MetadataRoot.ToArray();
        meta[72] ^= 0x02;
        AssertUniformUnlockFailure(() =>
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, bundle.HeaderCopy)], meta, TestPassword));
    }

    [Fact]
    public void Vector_HeaderReservedNonZero_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 1);
        var header = bundle.HeaderCopy.ToArray();
        header[300] = 1;
        Assert.ThrowsAny<Exception>(() =>
            VaultHeaderCopy.Parse(header, (uint)header.Length));
    }

    [Fact]
    public void Vector_HeaderTrailingBytes_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 1);
        var header = bundle.HeaderCopy.ToArray();
        Array.Resize(ref header, header.Length + 4);
        Assert.ThrowsAny<Exception>(() =>
            VaultHeaderCopy.Parse(header, (uint)header.Length));
    }

    [Fact]
    public void Vector_UnsupportedKdfSuite_Reject()
    {
        var buf = new byte[Argon2idKdfDescriptor.Size];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, 77);
        Assert.ThrowsAny<Exception>(() => Argon2idKdfDescriptor.Parse(buf));
    }

    [Fact]
    public void Vector_KdfBelowMinimum_Reject()
    {
        var buf = new byte[Argon2idKdfDescriptor.Size];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, AstraSuiteIds.KdfArgon2id);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4), 1024);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(8), 1);
        Assert.ThrowsAny<Exception>(() => Argon2idKdfDescriptor.Parse(buf));
    }

    [Fact]
    public void Vector_MetadataDescriptorOversized_Reject()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(TestPassword, 1);
        var meta = bundle.MetadataRoot.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(132), MetadataRootDescriptor.MaxCiphertextLength + 1);
        Assert.ThrowsAny<Exception>(() => MetadataRootDescriptor.Parse(meta));
    }

    [Fact]
    public void Vector_WrongPassword_MalformedSlot_ActivationFailure_SamePublicError()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(SecretMarker + "-pw", 1);
        var header = bundle.HeaderCopy.ToArray();
        header[VaultHeaderCopy.FixedRegionSize] = 0xEE;
        header[204] ^= 0x0F;

        string? wrongPass = null;
        string? malformed = null;
        string? activation = null;
        try
        {
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, bundle.HeaderCopy)], bundle.MetadataRoot, "not-the-password");
        }
        catch (UnlockValidationException ex)
        {
            wrongPass = ex.Message;
        }

        try
        {
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, header)], bundle.MetadataRoot, SecretMarker + "-pw");
        }
        catch (UnlockValidationException ex)
        {
            malformed = ex.Message;
        }

        try
        {
            ReadOnlyUnlockValidator.Validate(bundle.Locator, [(0, header)], bundle.MetadataRoot, SecretMarker + "-pw");
        }
        catch (UnlockValidationException ex)
        {
            activation = ex.Message;
        }

        Assert.Equal(UnlockValidationException.PublicMessage, wrongPass);
        Assert.Equal(wrongPass, malformed);
        Assert.Equal(wrongPass, activation);
        Assert.DoesNotContain(SecretMarker, wrongPass ?? "", StringComparison.Ordinal);
        if (Debugger.IsAttached)
        {
            Debug.WriteLine(UnlockValidationException.PublicMessage);
        }
    }
}