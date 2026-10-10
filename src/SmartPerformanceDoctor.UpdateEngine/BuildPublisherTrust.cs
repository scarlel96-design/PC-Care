using System.Collections.Frozen;

namespace SmartPerformanceDoctor.UpdateEngine;

/// <summary>Select a trust root at compilation. Runtime environment variables do not change this policy.</summary>
public static class BuildPublisherTrust
{
    public static IReadOnlySet<string> ExecutableThumbprints { get; } = CreateExecutablePins();
    private static IReadOnlySet<string> CreateExecutablePins()
    {
        var pins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#if PCCARE_VM_TEST
        pins.Add(TestVmTrustData.SignerThumbprint);
#endif
        return pins.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }
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
