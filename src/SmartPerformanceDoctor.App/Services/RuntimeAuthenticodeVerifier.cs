namespace SmartPerformanceDoctor.App.Services;

internal static class RuntimeAuthenticodeVerifier
{
    private const int TrustENoSignature = unchecked((int)0x800B0100);

    // Release publisher pins must be provisioned through a reviewed release process.
    // An arbitrary valid Authenticode signer is not the PC-Care publisher.
    private static readonly HashSet<string> TrustedPublisherThumbprints = new(StringComparer.OrdinalIgnoreCase);

#if PCCARE_VM_TEST
    static RuntimeAuthenticodeVerifier()
    {
        TrustedPublisherThumbprints.Add(SmartPerformanceDoctor.UpdateEngine.BuildPublisherTrust.SignerThumbprint);
    }
#endif

    public static (RuntimeTrustLevel Level, string Summary, IReadOnlyList<string> UnsignedFiles)
        EvaluateInstallRoot(string root)
    {
        var unverified = new List<string>();
        var failed = new List<string>();
        var checkedCount = 0;
        foreach (var relative in EnumerateCriticalBinaries())
        {
            var path = Path.Combine(root, relative);
            if (!File.Exists(path))
            {
                if (relative is "PCCare.exe" or "SmartPerformanceDoctor.dll")
                    failed.Add(relative + ":missing");
                continue;
            }
            checkedCount++;
            var result = Verify(path, out var thumbprint);
            if (result == TrustENoSignature)
            {
                unverified.Add(relative + ":unsigned");
            }
            else if (result != 0)
            {
                failed.Add(relative + ":signature-failed");
            }
            else if (thumbprint is null || !TrustedPublisherThumbprints.Contains(thumbprint))
            {
                unverified.Add(relative + ":publisher-unpinned");
            }
        }

        if (failed.Count > 0)
            return (RuntimeTrustLevel.Failed, $"signature-failed:{failed.Count}", failed);
        if (unverified.Count > 0 || checkedCount == 0)
            return (RuntimeTrustLevel.Unverified, $"publisher-unverified:{unverified.Count}", unverified);
#if PCCARE_VM_TEST
        return (RuntimeTrustLevel.Verified, "WO05 VM TEST ONLY: test Authenticode verified; not an official publisher", Array.Empty<string>());
#else
        return (RuntimeTrustLevel.Verified, "authenticode-and-publisher-verified", Array.Empty<string>());
#endif
    }

    public static bool TryVerifyAuthenticode(string path, out string? subject)
    {
        var code = Verify(path, out _);
        subject = code == 0 ? "Windows Authenticode policy passed" : null;
        return code == 0;
    }

    private static int Verify(string path, out string? thumbprint)
    {
        var result = SmartPerformanceDoctor.UpdateEngine.AuthenticodeBoundary.Verify(path);
        thumbprint = result.Thumbprint;
        return result.Code;
    }
    private static IEnumerable<string> EnumerateCriticalBinaries()
    {
        yield return "PCCare.exe";
        yield return "SmartPerformanceDoctor.dll";
        yield return Path.Combine("engine", "smart_performance_doctor_core.exe");
        yield return Path.Combine("engine", "smart_performance_doctor_repair_helper.exe");
        yield return Path.Combine("engine", "AegisRecoveryHelper.exe");
        yield return Path.Combine("engine", "AegisRecoveryService.exe");
        yield return Path.Combine("engine", "SmartPerformanceDoctor.UpdateHelper.exe");
    }
}
