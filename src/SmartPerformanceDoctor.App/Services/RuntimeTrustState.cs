namespace SmartPerformanceDoctor.App.Services;

public enum RuntimeTrustLevel
{
    Verified,
    UnsignedAllowed,
    Unverified,
    Failed
}

public static class RuntimeTrustState
{
    private static bool _initialized;
    private static RuntimeTrustLevel _level = RuntimeTrustLevel.Unverified;
    private static string _summary = "not-checked";
    private static IReadOnlyList<string> _unsignedFiles = Array.Empty<string>();

    public static RuntimeTrustLevel Level => _level;
    public static bool IsFullyTrusted => Level == RuntimeTrustLevel.Verified;
    public static bool IsOperationallyAllowed =>
        Level is RuntimeTrustLevel.Verified or RuntimeTrustLevel.UnsignedAllowed;
    public static string Summary => _summary;
    public static IReadOnlyList<string> UnsignedFiles => _unsignedFiles;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (ShouldSkipSignatureCheck())
        {
            _level = RuntimeTrustLevel.UnsignedAllowed;
            _summary = "unsigned-allowed-signature-not-verified";
            return;
        }

        var (level, summary, unsigned) = RuntimeAuthenticodeVerifier.EvaluateInstallRoot(RuntimePaths.InstallRoot);
        _level = level;
        _summary = summary;
        _unsignedFiles = unsigned;
    }

    public static string BuildDegradedFeatureMessage(string featureTitle) =>
        $"{featureTitle} 기능은 실행 파일 코드 서명 검증이 완료되지 않아 일시 비활성화되었습니다.\n" +
        "설치를 복구하거나 공식 설치 패키지로 재설치한 뒤 다시 시도하세요.";

    private static bool ShouldSkipSignatureCheck()
    {
        if (SmartProtectionDefaults.SilentConsumerMode)
        {
            return true;
        }

        var flag = Environment.GetEnvironmentVariable("PCCARE_SKIP_SIGNATURE_CHECK");
        if (string.Equals(flag, "1", StringComparison.Ordinal)
            || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var root = RuntimePaths.InstallRoot;
        return !root.Contains(@"\Program Files\", StringComparison.OrdinalIgnoreCase)
            && !root.Contains(@"\Program Files (x86)\", StringComparison.OrdinalIgnoreCase);
    }
}
