using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.App.Services.Commercial;

namespace SmartPerformanceDoctor.App.Services;

public static class StartupTrustStatusService
{
    public static string BuildTitleStatus()
    {
        var parts = new List<string> { $"v{AppInfo.BuildVersion}" };

        if (!CommercialPackTrustState.IsFullyTrusted)
        {
            parts.Add("Pack 제한");
        }

        if (AegisTrustState.IsSafeMode && !AegisTrustPolicy.AllowRelaxedMirrorTrust())
        {
            parts.Add("복구 미러 안전 모드");
        }
        else if (AegisMirrorPaths.UsingUserFallback)
        {
            parts.Add("미러 사용자 폴더");
        }

        if (!RuntimeTrustState.IsFullyTrusted)
        {
            parts.Add("서명 제한");
        }

        return parts.Count == 1
            ? $"준비됨 · {parts[0]}"
            : $"주의 · {string.Join(" · ", parts)}";
    }

    public static IReadOnlyList<string> BuildStartupNotices()
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