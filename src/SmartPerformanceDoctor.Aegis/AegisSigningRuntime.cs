namespace SmartPerformanceDoctor.Aegis;

public static class AegisSigningRuntime
{
    public static bool IsSigningConfigured() => AegisManifestBuildSigner.IsSigningConfigured();

    public static string? ResolveSigningKeyPath()
    {
        foreach (var path in AegisManifestBuildSigner.EnumerateSigningKeyPaths())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}