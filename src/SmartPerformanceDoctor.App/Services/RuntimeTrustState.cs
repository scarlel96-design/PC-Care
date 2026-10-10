namespace SmartPerformanceDoctor.App.Services;

public enum RuntimeTrustLevel
{
    Verified,
    UnsignedAllowed,
    Unverified,
    Failed
}

public enum CryptographicVerificationResult { Verified, Unsigned, Failed, NotChecked }
public enum PublisherTrustResult { Trusted, Untrusted, Unknown }
public enum ExecutionPolicyDecision { Allowed, Blocked }

public static class RuntimeTrustPolicy
{
    public static ExecutionPolicyDecision Decide(CryptographicVerificationResult cryptographic,
        PublisherTrustResult publisher, bool localDevelopment)
    {
        if (cryptographic == CryptographicVerificationResult.Verified &&
            publisher == PublisherTrustResult.Trusted) return ExecutionPolicyDecision.Allowed;
        if (localDevelopment && cryptographic == CryptographicVerificationResult.Unsigned)
            return ExecutionPolicyDecision.Allowed;
        return ExecutionPolicyDecision.Blocked;
    }
}

public static class RuntimeTrustState
{
    private static bool _initialized;
    private static RuntimeTrustLevel _level = RuntimeTrustLevel.Unverified;
    private static string _summary = "not-checked";
    private static IReadOnlyList<string> _unsignedFiles = Array.Empty<string>();
    public static CryptographicVerificationResult CryptographicVerification { get; private set; } =
        CryptographicVerificationResult.NotChecked;
    public static PublisherTrustResult PublisherTrust { get; private set; } = PublisherTrustResult.Unknown;
    public static ExecutionPolicyDecision ExecutionPolicy { get; private set; } = ExecutionPolicyDecision.Blocked;

    public static RuntimeTrustLevel Level => _level;
    public static bool IsFullyTrusted => Level == RuntimeTrustLevel.Verified;
    public static bool IsOperationallyAllowed => ExecutionPolicy == ExecutionPolicyDecision.Allowed;
    public static string Summary => _summary;
    public static IReadOnlyList<string> UnsignedFiles => _unsignedFiles;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var (level, summary, unsigned) = RuntimeAuthenticodeVerifier.EvaluateInstallRoot(RuntimePaths.InstallRoot);
        CryptographicVerification = level switch
        {
            RuntimeTrustLevel.Verified => CryptographicVerificationResult.Verified,
            RuntimeTrustLevel.Failed => CryptographicVerificationResult.Failed,
            _ when unsigned.Any(x => x.EndsWith(":unsigned", StringComparison.Ordinal)) => CryptographicVerificationResult.Unsigned,
            _ when unsigned.Any(x => x.EndsWith(":publisher-unpinned", StringComparison.Ordinal)) => CryptographicVerificationResult.Verified,
            _ => CryptographicVerificationResult.NotChecked
        };
        PublisherTrust = level == RuntimeTrustLevel.Verified ? PublisherTrustResult.Trusted :
            CryptographicVerification == CryptographicVerificationResult.Verified
                ? PublisherTrustResult.Untrusted : PublisherTrustResult.Unknown;
        // Only a Debug build outside Program Files receives the development execution allowance.
#if DEBUG
        var localDevelopment = !IsProgramFiles(RuntimePaths.InstallRoot);
#else
        const bool localDevelopment = false;
#endif
        ExecutionPolicy = RuntimeTrustPolicy.Decide(CryptographicVerification, PublisherTrust, localDevelopment);
        _level = level == RuntimeTrustLevel.Unverified && ExecutionPolicy == ExecutionPolicyDecision.Allowed
            ? RuntimeTrustLevel.UnsignedAllowed : level;
        _summary = _level == RuntimeTrustLevel.UnsignedAllowed
            ? summary + ";development-execution-only" : summary;
        _unsignedFiles = unsigned;
    }

    public static string BuildDegradedFeatureMessage(string featureTitle) =>
        $"{featureTitle} 기능은 실행 파일의 서명과 발행자 신뢰가 확인되지 않아 비활성화되었습니다.\n" +
        "현재 설치본의 신뢰 상태를 확인하고, 검증된 발행자 빌드가 제공된 뒤 다시 시도하세요.";

    private static bool IsProgramFiles(string root)
    {
        var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Any(x => full.StartsWith(Path.GetFullPath(x).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }
}
