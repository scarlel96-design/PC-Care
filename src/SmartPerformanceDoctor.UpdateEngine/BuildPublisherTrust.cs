namespace SmartPerformanceDoctor.UpdateEngine;

/// <summary>Select a trust root at compilation. Runtime environment variables do not change this policy.</summary>
public static class BuildPublisherTrust
{
#if PCCARE_VM_TEST
    public const bool IsVmTest = true;
    public static string SignerThumbprint => TestVmTrustData.SignerThumbprint;
    public static PublisherTrustPolicy Current { get; } = new([
        new TrustedPublisherKey("WO05-VM-TEST", "wo05-ephemeral", "stable", TestVmTrustData.PublicKey)]);
#else
    public const bool IsVmTest = false;
    public static PublisherTrustPolicy Current => PublisherTrustPolicy.Production;
#endif
}
