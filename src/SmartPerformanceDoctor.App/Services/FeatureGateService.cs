using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.App.Services.Installation;
using SmartPerformanceDoctor.Contracts.Models.Installation;

namespace SmartPerformanceDoctor.App.Services;

public static class FeatureGateService
{
    public static bool EnsureEnabled(
        string featureId,
        string featureTitle,
        InstalledFeaturesService features,
        Action<string>? onBlocked = null)
    {
        if (!Commercial.CommercialPackTrustState.IsFullyTrusted && RequiresPackTrust(featureId))
        {
            onBlocked?.Invoke("Rule/Protocol Pack 검증에 실패하여 지능형 진단 기능을 일시 비활성화했습니다. 설치 복구 또는 공식 업데이트를 적용하세요.");
            return false;
        }

        if (AegisTrustState.IsSafeMode && RequiresAegisTrust(featureId))
        {
            onBlocked?.Invoke(AegisTrustState.BuildSafeModeMessage());
            return false;
        }

        if (!RuntimeTrustState.IsFullyTrusted && RequiresFullTrust(featureId))
        {
            onBlocked?.Invoke(RuntimeTrustState.BuildDegradedFeatureMessage(featureTitle));
            return false;
        }

        if (features.IsEnabled(featureId))
        {
            return true;
        }

        onBlocked?.Invoke($"{featureTitle} 기능이 설치되어 있지 않습니다. 환경설정 → 기능 관리에서 설치 관리자를 실행해 기능을 추가하세요.");
        return false;
    }

    private static bool RequiresPackTrust(string featureId) =>
        featureId is InstallFeatureIds.KnowledgePack
            or InstallFeatureIds.DeepScanIntelligence;

    private static bool RequiresAegisTrust(string featureId) =>
        featureId is InstallFeatureIds.ProgramIntegrity;

    private static bool RequiresFullTrust(string featureId) =>
        featureId is InstallFeatureIds.ProfessionalSecureDelete
            or InstallFeatureIds.SecureVault
            or InstallFeatureIds.DriverAudioRepair
            or InstallFeatureIds.RegistryDoctor
            or InstallFeatureIds.VulnerabilityFix
            or InstallFeatureIds.ProgramIntegrity;
}