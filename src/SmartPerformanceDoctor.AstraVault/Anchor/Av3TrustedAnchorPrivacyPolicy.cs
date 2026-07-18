namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Privacy model for trusted anchor witnesses (digest-only external API).</summary>
public static class Av3TrustedAnchorPrivacyPolicy
{
    public static bool ExternalWitnessDigestOnly => Av3TrustedAnchorRuntimePolicy.StoresPublicDigestsOnly;

    public static bool NoPasswordInWitness => !Av3TrustedAnchorPolicy.StoresSecrets;

    public static bool NoVmkOrDekInWitness => !Av3TrustedAnchorPolicy.StoresSecrets;

    public static bool NoUserFilenamesOrPathsInWitness => !Av3TrustedAnchorPolicy.StoresPathsOrFilenames;

    public static bool NoObjectPathInWitness => !Av3TrustedAnchorPolicy.StoresPathsOrFilenames;

    public static bool RedactPublicErrors => true;
}