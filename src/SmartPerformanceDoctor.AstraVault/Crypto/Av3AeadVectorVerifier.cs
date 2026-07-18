using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Independent vector verification (no parser dependency).</summary>
public static class Av3AeadVectorVerifier
{
    public static void VerifyDecryptPass(Av3AeadVector vector)
    {
        var cipher = Av3AeadDispatch.Resolve(vector.SuiteId);
        var key = Av3AeadKeyMaterialPolicy.DeriveFixtureKey(System.Text.Encoding.UTF8.GetBytes(vector.KeyLabel));
        var blob = new AstraCiphertext(vector.Nonce, vector.Tag, vector.Ciphertext);
        var plain = cipher.Decrypt(key, blob, vector.Aad);
        if (!plain.AsSpan().SequenceEqual(vector.Plaintext))
        {
            throw new UnlockValidationException();
        }
    }

    public static void VerifyTamperRejected(Av3AeadVector vector, Func<AstraCiphertext, AstraCiphertext> mutate)
    {
        var cipher = Av3AeadDispatch.Resolve(vector.SuiteId);
        var key = Av3AeadKeyMaterialPolicy.DeriveFixtureKey(System.Text.Encoding.UTF8.GetBytes(vector.KeyLabel));
        var blob = mutate(new AstraCiphertext(vector.Nonce, vector.Tag, vector.Ciphertext));
        try
        {
            _ = cipher.Decrypt(key, blob, vector.Aad);
            throw new InvalidOperationException("Expected tamper rejection.");
        }
        catch (UnlockValidationException)
        {
            // expected
        }
        catch (CryptographicException)
        {
            // expected
        }
    }
}