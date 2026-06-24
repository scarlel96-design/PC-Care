using SmartPerformanceDoctor.Contracts.Models.Installation;

namespace SmartPerformanceDoctor.Contracts.Services.Installation;

public static class FeatureCatalog
{
    public static IReadOnlyList<FeatureDefinition> All { get; } =
    [
        Req(InstallFeatureIds.CoreDiagnostics, "Core Diagnostic Engine", "기본 시스템 진단, 상태 수집, 보고서 생성", "Feature_CoreDiagnostics"),
        Req(InstallFeatureIds.ReportAudit, "Report / Audit Engine", "HTML/JSON/TXT 보고서와 감사 로그 저장", "Feature_ReportAudit"),
        Req(InstallFeatureIds.ProgramIntegrity, "Program Integrity Core", "프로그램 무결성, 엔진 해시, 설정 변조 확인", "Feature_ProgramIntegrity"),
        Req(InstallFeatureIds.ConfigManager, "Configuration Manager", "설치된 기능 목록과 사용자 설정 관리", "Feature_ConfigManager"),
        Req(InstallFeatureIds.UpdateManifest, "Update / Manifest Verifier", "업데이트 채널, 체크섬, 서명 검증", "Feature_UpdateManifest"),

        Opt(InstallFeatureIds.SystemCare, "System Care Center", "개인정보·정크·시작 프로그램·시스템 최적화",
            "Feature_SystemCare", riskLevel: "medium", includedInRecommended: true, includedInMinimal: false, requiresElevation: false),
        Opt(InstallFeatureIds.DriverAudioRepair, "Driver & Audio Device Repair", "드라이버/PnP/오디오 스택 진단·복구",
            "Feature_DriverAudioRepair", riskLevel: "high", includedInRecommended: true, includedInMinimal: false, requiresElevation: true),
        Opt(InstallFeatureIds.SecureVault, "Secure Vault", "민감 파일·폴더 암호화 금고 (Argon2id)",
            "Feature_SecureVault", riskLevel: "high", includedInRecommended: true, includedInMinimal: false, requiresElevation: false),
        Opt(InstallFeatureIds.ProfessionalSecureDelete, "Professional Secure Delete", "복구 매우 어려운 보안 삭제",
            "Feature_ProfessionalSecureDelete", riskLevel: "very-high", includedInRecommended: true, includedInMinimal: false, requiresElevation: false),
        Opt(InstallFeatureIds.RegistryDoctor, "Registry Doctor", "레지스트리 백업·정리",
            "Feature_RegistryDoctor", riskLevel: "medium", includedInRecommended: true, includedInMinimal: false, requiresElevation: true),
        Opt(InstallFeatureIds.DiskDoctor, "Disk Doctor", "디스크 검사·최적화",
            "Feature_DiskDoctor", riskLevel: "medium", includedInRecommended: true, includedInMinimal: false, requiresElevation: true),
        Opt(InstallFeatureIds.PrivacyCleaner, "Privacy Cleaner", "개인정보·사용 흔적 정리",
            "Feature_PrivacyCleaner", riskLevel: "medium", includedInRecommended: false, includedInMinimal: false, requiresElevation: false),
        Opt(InstallFeatureIds.JunkCleaner, "Junk Cleaner", "불필요 파일 정리",
            "Feature_JunkCleaner", riskLevel: "low", includedInRecommended: true, includedInMinimal: false, requiresElevation: false),
        Opt(InstallFeatureIds.ShortcutRepair, "Shortcut Repair", "바로가기 복구",
            "Feature_ShortcutRepair", riskLevel: "low", includedInRecommended: true, includedInMinimal: false, requiresElevation: false),
        Opt(InstallFeatureIds.InternetAcceleration, "Internet Acceleration Center", "인터넷 가속·네트워크 점검",
            "Feature_InternetAcceleration", riskLevel: "medium", includedInRecommended: false, includedInMinimal: false, requiresElevation: false),
        Opt(InstallFeatureIds.VulnerabilityFix, "Vulnerability Fix Center", "취약점 수정 제안",
            "Feature_VulnerabilityFix", riskLevel: "high", includedInRecommended: true, includedInMinimal: false, requiresElevation: true),
        Opt(InstallFeatureIds.DeepScanIntelligence, "Deep Scan / Intelligence Pack", "심층 점검·문제 분석",
            "Feature_DeepScanIntelligence", riskLevel: "medium", includedInRecommended: true, includedInMinimal: false, requiresElevation: false),
        Opt(InstallFeatureIds.KnowledgePack, "Knowledge Pack / Rule DB", "규칙 데이터 관리",
            "Feature_KnowledgePack", riskLevel: "low", includedInRecommended: true, includedInMinimal: false, requiresElevation: false),
        Opt(InstallFeatureIds.PortableTools, "Portable Tools", "포터블 유틸리티",
            "Feature_PortableTools", riskLevel: "low", includedInRecommended: false, includedInMinimal: false, requiresElevation: false)
    ];

    public static InstalledFeaturesManifest CreateManifest(InstallMode mode, string version, IEnumerable<string> selectedOptionalIds)
    {
        var selected = new HashSet<string>(selectedOptionalIds, StringComparer.OrdinalIgnoreCase);
        var manifest = new InstalledFeaturesManifest
        {
            Version = version,
            InstallMode = mode switch
            {
                InstallMode.Recommended => "recommended",
                InstallMode.Minimal => "minimal",
                _ => "custom"
            },
            InstalledAt = DateTimeOffset.Now.ToString("o")
        };

        foreach (var feature in All)
        {
            var enabled = feature.IsRequired
                || (mode == InstallMode.Recommended && feature.IncludedInRecommended)
                || (mode == InstallMode.Minimal && feature.IncludedInMinimal)
                || selected.Contains(feature.Id);
            manifest.Features[feature.Id] = enabled;
        }

        return manifest;
    }

    public static InstalledFeaturesManifest CreateAllEnabled(string version) =>
        CreateManifest(InstallMode.Custom, version, InstallFeatureIds.Optional);

    public static InstalledFeaturesManifest CreateMinimalRuntimeDefault(string version) =>
        CreateManifest(InstallMode.Minimal, version, []);

    /// <summary>v49 포터블/개발 실행 기본 — 주요 기능(시스템 케어·보안 금고·보안 삭제) 포함.</summary>
    public static InstalledFeaturesManifest CreateRuntimeDefault(string version) =>
        CreateManifest(InstallMode.Recommended, version, InstallFeatureIds.Optional);

    public static void EnsureV49PrimaryFeatures(InstalledFeaturesManifest manifest)
    {
        foreach (var featureId in new[]
                 {
                     InstallFeatureIds.SystemCare,
                     InstallFeatureIds.SecureVault,
                     InstallFeatureIds.ProfessionalSecureDelete,
                     InstallFeatureIds.RegistryDoctor,
                     InstallFeatureIds.DiskDoctor,
                     InstallFeatureIds.JunkCleaner,
                     InstallFeatureIds.ShortcutRepair,
                     InstallFeatureIds.PrivacyCleaner
                 })
        {
            manifest.Features[featureId] = true;
        }
    }

    private static FeatureDefinition Req(string id, string en, string ko, string msi) =>
        new()
        {
            Id = id,
            DisplayName = en,
            Description = ko,
            Category = "required",
            RiskLevel = "low",
            MsiFeatureName = msi,
            IsRequired = true,
            IncludedInRecommended = true,
            IncludedInMinimal = true
        };

    private static FeatureDefinition Opt(
        string id,
        string displayName,
        string description,
        string msiFeatureName,
        string riskLevel,
        bool includedInRecommended,
        bool includedInMinimal,
        bool requiresElevation) =>
        new()
        {
            Id = id,
            DisplayName = displayName,
            Description = description,
            Category = "optional",
            RiskLevel = riskLevel,
            RequiresElevation = requiresElevation,
            MsiFeatureName = msiFeatureName,
            IsRequired = false,
            IncludedInRecommended = includedInRecommended,
            IncludedInMinimal = includedInMinimal
        };
}