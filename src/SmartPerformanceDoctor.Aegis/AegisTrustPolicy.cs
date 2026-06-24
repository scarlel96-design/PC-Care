namespace SmartPerformanceDoctor.Aegis;

public static class AegisTrustPolicy
{
    public static bool IsPortableInstall()
    {
        var root = AegisRuntimeContext.InstallRoot;
        return !root.Contains(@"\Program Files\", StringComparison.OrdinalIgnoreCase)
            && !root.Contains(@"\Program Files (x86)\", StringComparison.OrdinalIgnoreCase);
    }

    public static bool AllowRelaxedMirrorTrust() =>
        IsPortableInstall() || AegisMirrorPaths.UsingUserFallback || AegisMirrorPaths.UsesTestOverride;

    public static bool ShouldEnterSafeMode(bool signatureValid, bool capsuleValid, bool capsuleExists) =>
        !AllowRelaxedMirrorTrust() && (!signatureValid || (!capsuleValid && capsuleExists));
}