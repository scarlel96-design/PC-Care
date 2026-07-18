using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Validation;

/// <summary>Uniform outward failure for unlock validation (no secret/oracle leak).</summary>
public sealed class UnlockValidationException : CryptographicException
{
    public const string PublicMessage = "Unlock validation failed.";

    public UnlockValidationException()
        : base(PublicMessage)
    {
    }

    public UnlockValidationException(Exception inner)
        : base(PublicMessage, inner)
    {
    }
}