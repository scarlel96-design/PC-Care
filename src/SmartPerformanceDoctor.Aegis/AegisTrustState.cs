namespace SmartPerformanceDoctor.Aegis;

public enum AegisOperatingMode
{
    Normal,
    SafeMode,
    RecoveryOnly
}

public static class AegisTrustState
{
    private static bool _initialized;
    private static AegisOperatingMode _mode = AegisOperatingMode.Normal;
    private static string _reason = "";

    public static AegisOperatingMode Mode => _initialized ? _mode : AegisOperatingMode.Normal;
    public static bool IsSafeMode => Mode != AegisOperatingMode.Normal;
    public static bool AllowAutoRepair => Mode == AegisOperatingMode.Normal;
    public static bool AllowCapsuleApply => Mode == AegisOperatingMode.Normal;
    public static string Reason => _reason;

    public static void Initialize(AegisMirrorStatus status)
    {
        _initialized = true;
        _mode = AegisOperatingMode.Normal;
        _reason = status.RepairedFiles > 0 ? "auto-repaired" : "trusted";
    }

    public static string BuildSafeModeMessage() =>
        "복구 미러가 안전 모드입니다. 서명·캡슐 검증에 실패하여 자동 복구 적용을 중단했습니다. " +
        "설치 복구 또는 공식 업데이트로 복구 미러를 재구성하세요.";
}