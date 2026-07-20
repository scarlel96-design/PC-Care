using SmartPerformanceDoctor.App.Views;
using SmartPerformanceDoctor.Contracts.Models.Installation;

namespace SmartPerformanceDoctor.App.Platform;

internal sealed class CoreExperienceModule : IProductModule
{
    public string Id => "pccare.core";
    public Version ContractVersion => new(1, 0);

    public IEnumerable<ProductFeatureDescriptor> Features =>
    [
        new("home", "홈", "PC 상태와 다음 권장 작업", "⌂", ProductArea.Home, typeof(MacDashboardPage), 10, true,
            SearchTerms: ["상태", "점수", "권장"]),
        new("care", "PC 최적화", "점검, 정리, 드라이버와 오디오 진단", "✦", ProductArea.Care, typeof(CareCenterPage), 20, true,
            InstallFeatureId: InstallFeatureIds.CoreDiagnostics, SearchTerms: ["점검", "정리", "속도", "드라이버", "오디오"]),
        new("security", "보안", "보안 금고와 복구하기 어려운 삭제", "◇", ProductArea.Security, typeof(SecurityCenterPage), 30, true,
            SearchTerms: ["금고", "암호화", "보안 삭제"]),
        new("history", "기록 및 복구", "보고서, 작업 내역과 되돌리기", "↶", ProductArea.History, typeof(HistoryCenterPage), 40, true,
            InstallFeatureId: InstallFeatureIds.ReportAudit, SearchTerms: ["보고서", "로그", "복구", "되돌리기"]),
        new("settings", "설정", "업데이트와 프로그램 동작 설정", "⚙", ProductArea.Settings, typeof(SettingsPage), 90, true,
            SearchTerms: ["업데이트", "시작", "백그라운드"])
    ];

    public IEnumerable<EngineDescriptor> Engines =>
    [
        new("engine.core-diagnostics", "Core 진단 엔진", EngineKind.Diagnostics, "v16",
            ["시스템 스냅샷", "이벤트 상관 분석", "JSON Lines 스트리밍"], RunsOutOfProcess: true),
        new("engine.knowledge", "로컬 규칙 분석", EngineKind.Intelligence, "1",
            ["지식 팩", "원인 후보 점수화", "복구 순서 추천"]),
        new("engine.update", "업데이트 엔진", EngineKind.Infrastructure, "2",
            ["릴리즈 확인", "무결성 검증", "보류 업데이트 적용"], RequiresElevation: true, RunsOutOfProcess: true)
    ];
}

internal sealed class CareProductModule : IProductModule
{
    public string Id => "pccare.care";
    public Version ContractVersion => new(1, 0);

    public IEnumerable<ProductFeatureDescriptor> Features =>
    [
        new("care.quick", "빠른 점검", "PC 핵심 상태를 빠르게 확인", "⌁", ProductArea.Care, typeof(UnifiedCarePage), 110,
            InstallFeatureId: InstallFeatureIds.CoreDiagnostics),
        new("care.system", "시스템 케어", "불필요한 항목 검사와 안전 정리", "◉", ProductArea.Care, typeof(SystemCareCenterPage), 120,
            InstallFeatureId: InstallFeatureIds.SystemCare),
        new("care.driver-audio", "드라이버·오디오", "장치와 오디오 문제의 원인 진단", "◌", ProductArea.Care, typeof(UnifiedCarePage), 130,
            InstallFeatureId: InstallFeatureIds.CoreDiagnostics)
    ];

    public IEnumerable<EngineDescriptor> Engines =>
    [
        new("engine.system-care", "시스템 케어 엔진", EngineKind.Optimization, "2",
            ["정크·캐시", "개인정보 흔적", "바로가기", "레지스트리", "시작 프로그램"], RequiresElevation: true),
        new("engine.driver-audio", "드라이버·오디오 진단", EngineKind.Diagnostics, "2",
            ["ConfigManager", "Kernel-PnP", "오디오 엔드포인트", "서비스 상태"])
    ];
}

internal sealed class SecurityProductModule : IProductModule
{
    public string Id => "pccare.security";
    public Version ContractVersion => new(1, 0);

    public IEnumerable<ProductFeatureDescriptor> Features =>
    [
        new("security.vault", "보안 금고", "중요 파일을 암호화해 보관", "◇", ProductArea.Security, typeof(SecureVaultCenterPage), 210,
            InstallFeatureId: InstallFeatureIds.SecureVault),
        new("security.delete", "보안 삭제", "복구하기 어렵도록 안전하게 삭제", "⌫", ProductArea.Security, typeof(SecureDeleteCenterPage), 220,
            InstallFeatureId: InstallFeatureIds.ProfessionalSecureDelete)
    ];

    public IEnumerable<EngineDescriptor> Engines =>
    [
        new("engine.vault", "AstraVault", EngineKind.Security, "5",
            ["Argon2id 키 파생", "인증 암호화", "복구 코드", "자동 잠금"]),
        new("engine.secure-delete", "Forensic Secure Delete", EngineKind.Security, "2",
            ["Dry-run", "저장 장치 감지", "다중 패스 삭제"], RequiresElevation: true),
        new("engine.aegis", "Aegis 복구", EngineKind.Recovery, "2",
            ["실행 파일 무결성", "복구 미러", "시작 실패 복구"], RequiresElevation: true, RunsOutOfProcess: true)
    ];
}

internal static class ProductComposition
{
    private static readonly Lazy<ProductCatalog> DefaultCatalog = new(CreateDefault);

    public static ProductCatalog Current => DefaultCatalog.Value;

    internal static ProductCatalog CreateDefault()
    {
        var catalog = new ProductCatalog();
        catalog.Register(new CoreExperienceModule());
        catalog.Register(new CareProductModule());
        catalog.Register(new SecurityProductModule());
        return catalog;
    }
}
