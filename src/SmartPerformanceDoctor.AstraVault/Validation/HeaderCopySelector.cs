using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;

namespace SmartPerformanceDoctor.AstraVault.Validation;

public sealed record HeaderCandidate(byte CopyIndex, VaultHeaderCopy Copy, bool CryptographicValid);

/// <summary>
/// 3-copy candidate selection: structural parse, consensus, crypto-valid VMK unwrap; never trust max generation alone.
/// </summary>
public static class HeaderCopySelector
{
    public static IReadOnlyList<HeaderCandidate> ParseCandidates(
        VaultLocator locator,
        IReadOnlyList<(byte CopyIndex, ReadOnlyMemory<byte> Bytes)> rawCopies)
    {
        var parsed = new List<HeaderCandidate>();
        foreach (var (index, mem) in rawCopies)
        {
            try
            {
                var copy = VaultHeaderCopy.Parse(mem.Span, locator.HeaderCopySize);
                if (copy.ContainerId != locator.ContainerId)
                {
                    continue;
                }

                parsed.Add(new HeaderCandidate(index, copy, CryptographicValid: false));
            }
            catch (CryptographicException)
            {
                // drop invalid copy
            }
        }

        return parsed;
    }

    public static bool TryValidateCopyCrypto(VaultHeaderCopy copy, string password, out byte[]? vmk)
    {
        vmk = null;
        foreach (var slot in copy.PasswordSlots)
        {
            if (slot.ContainerId != copy.ContainerId || slot.Generation != copy.Generation)
            {
                continue;
            }

            if (!slot.Kdf.ToParameters().MeetsMinimum)
            {
                continue;
            }

            byte[]? kek = null;
            try
            {
                kek = AstraKdf.DeriveKek(password, slot.KdfSalt, slot.Kdf.ToParameters());
                var aad = VmkUnwrapAad.Build(
                    AstraFormatConstants.MajorVersion,
                    slot.ContainerId,
                    slot.SlotId,
                    slot.Generation);
                var suite = (AstraCipherSuite)slot.CipherSuiteId;
                var blob = new AstraCiphertext(slot.WrapNonce, slot.WrapTag, slot.WrappedVmk);
                vmk = AstraAead.Decrypt(suite, kek, blob, aad);
                HeaderActivationAead.AuthenticateAndDecrypt(copy, vmk);
                return true;
            }
            catch (Exception)
            {
                // uniform failure path
            }
            finally
            {
                AstraKdf.Zero(kek);
            }
        }

        return false;
    }
}