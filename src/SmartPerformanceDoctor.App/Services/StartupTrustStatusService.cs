using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.App.Services.Commercial;

namespace SmartPerformanceDoctor.App.Services;

public static class StartupTrustStatusService
{
    public static string BuildTitleStatus() => $"준비됨 · v{AppInfo.BuildVersion}";

    public static IReadOnlyList<string> BuildStartupNotices()
    {
        if (!SmartProtectionDefaults.SilentConsumerMode)
        {
            return BuildLegacyStartupNotices();
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> BuildLegacyStartupNotices()
    {
        var notices = new List<string>();

        if (AegisTrustState.IsSafeMode && !AegisTrustPolicy.AllowRelaxedMirrorTrust())
        {
            notices.Add(AegisTrustState.BuildSafeModeMessage());
        }

        if (!CommercialPackTrustState.IsFullyTrusted)
        {
            notices.Add(
                "규칙·프로토콜 Pack의 checksum·서명 검증에 실패했습니다. " +
                "규칙 기반 기능이 제한 모드로 동작합니다. 설치를 복구하거나 공식 업데이트를 적용하세요.");
        }

        if (!RuntimeTrustState.IsFullyTrusted && RuntimeTrustState.Level != RuntimeTrustLevel.Failed)
        {
            notices.Add(
                "일부 실행 파일의 코드 서명을 확인하지 못했습니다. " +
                "보안 삭제·보안 금고·장치 복구 등 고위험 기능이 제한됩니다.");
        }

        return notices;
    }
}