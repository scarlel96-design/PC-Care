using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;

namespace SmartPerformanceDoctor.AstraVault.Validation;

/// <summary>Ordered read-only unlock validation (Phase D: metadata-root AEAD).</summary>
internal static class UnlockValidationPipeline
{
    public static (VaultLocator Locator, VaultHeaderCopy Header, MetadataRootValidationResult? Metadata) Execute(
        ReadOnlySpan<byte> locatorBytes,
        IReadOnlyList<(byte CopyIndex, ReadOnlyMemory<byte> CopyBytes)> headerCopies,
        byte[]? metadataRootBytes,
        string password)
    {
        // 1. locator bounds/version/suite
        VaultLocator locator;
        try
        {
            locator = VaultLocator.Parse(locatorBytes);
        }
        catch (CryptographicException ex)
        {
            throw new UnlockValidationException(ex);
        }

        // 2–3. header copy candidates parse + size/reserved/trailing
        var candidates = HeaderCopySelector.ParseCandidates(locator, headerCopies);
        if (candidates.Count == 0)
        {
            throw new UnlockValidationException();
        }

        GenerationRollbackRules.ValidateEqualGenerationConflictingRoots(candidates.Select(c => c.Copy).ToList());
        GenerationRollbackRules.ValidateCopyConsensus(candidates.Select(c => c.Copy).ToList());

        // 4–14. per-copy crypto path; prefer highest trusted generation
        var ordered = candidates.OrderByDescending(c => c.Copy.Generation).ToList();
        MetadataRootValidationResult? metadataValidation = null;
        UnlockValidationException? lastFailure = null;

        foreach (var candidate in ordered)
        {
            try
            {
                var copy = candidate.Copy;
                foreach (var slot in copy.PasswordSlots)
                {
                    // 4. password slot parse (done in header parse)
                    // 5–6. Argon2id descriptor + minimum policy
                    if (!slot.Kdf.ToParameters().MeetsMinimum)
                    {
                        throw new UnlockValidationException();
                    }

                    if (slot.ContainerId != copy.ContainerId || slot.Generation != copy.Generation)
                    {
                        throw new UnlockValidationException();
                    }

                    byte[]? kek = null;
                    byte[]? vmk = null;
                    try
                    {
                        // 7–8. VMK unwrap AAD + attempt
                        kek = AstraKdf.DeriveKek(password, slot.KdfSalt, slot.Kdf.ToParameters());
                        var vmkAad = VmkUnwrapAad.Build(
                            AstraFormatConstants.MajorVersion,
                            slot.ContainerId,
                            slot.SlotId,
                            slot.Generation);
                        var suite = (AstraCipherSuite)slot.CipherSuiteId;
                        var blob = new AstraCiphertext(slot.WrapNonce, slot.WrapTag, slot.WrappedVmk);
                        vmk = AstraAead.Decrypt(suite, kek, blob, vmkAad);

                        // 9–10. activation AEAD + digest/commitment
                        HeaderActivationAead.AuthenticateAndDecrypt(copy, vmk);

                        // 11–13. metadata.root envelope, digest, AEAD, plaintext + generation (after activation auth)
                        if (metadataRootBytes is not { Length: > 0 })
                        {
                            throw new UnlockValidationException();
                        }

                        metadataValidation = MetadataRootReadOnlyReader.Validate(copy, vmk, metadataRootBytes);
                        return (locator, copy, metadataValidation);
                    }
                    finally
                    {
                        AstraKdf.Zero(kek);
                        AstraKdf.Zero(vmk);
                    }
                }

                throw new UnlockValidationException();
            }
            catch (UnlockValidationException ex)
            {
                lastFailure = ex;
            }
            catch (CryptographicException ex)
            {
                lastFailure = new UnlockValidationException(ex);
            }
        }

        throw lastFailure ?? new UnlockValidationException();
    }
}