namespace SmartPerformanceDoctor.App.Branding;

/// <summary>
/// PC 케어 제품 명칭 — UI·설치·사용자 표시 문자열의 단일 기준.
/// 실행 파일·폴더 등 기술 식별자는 하위 호환을 위해 legacy 이름을 유지합니다.
/// </summary>
public static class AstraCareBranding
{
    public const string Product = "PC 케어";
    public const string ProductFormal = "PC 케어 프로";
    public const string Tagline = "PC Care — integrated check, repair, and security";
    public const string TaglineKorean = "PC 통합 점검·복구·보안 관리 프로그램";
    public const string Description = TaglineKorean;

    public const string Engine = "최신 진단 엔진";
    public const string EngineExe = "smart_performance_doctor_core.exe";
    public const string RepairHelper = "케어 복구 도우미";
    public const string RepairHelperExe = "smart_performance_doctor_repair_helper.exe";
    public const string AegisRecoveryHelper = "복구 미러 도우미";
    public const string AegisRecoveryHelperExe = "AegisRecoveryHelper.exe";
    public const string AegisRecoveryService = "복구 미러 서비스";
    public const string AegisRecoveryServiceExe = "AegisRecoveryService.exe";
    public const string MainExe = "SmartPerformanceDoctor.exe";
    public const string BrandedExe = "AstraCare.exe";
    public const string LegacyCoreExe = "AstraCore.exe";
    public const string LegacyRepairHelperExe = "AstraRepairHelper.exe";
    public const string LegacyAegisRecoveryHelperExe = "aegis_recovery_helper.exe";

    public const string BetaBadge = "Beta";

    public const string Vault = "보안 금고";
    public const string Shred = "보안 삭제";
    public const string VaultNav = "보안 금고";
    public const string ShredNav = "보안 삭제";

    public const string VaultBetaNotice =
        "보안 금고는 암호화 보관·복원·무결성 검사를 제공합니다. " +
        "대용량 폴더·엣지 케이스·UI는 개선 중이며, 중요 데이터는 별도 백업을 권장합니다.";

    public const string ShredBetaNotice =
        "보안 삭제는 포렌식 저항 풀체인을 제공합니다. " +
        "SSD 물리 한계·클라우드 동기화·MFT 슬랙 등은 완전 제거를 보장하지 않으니 삭제 전 대상을 반드시 확인하세요.";

    public const string UnifiedCare = "통합 점검";
    public const string Repair = UnifiedCare;
    public const string Clean = "시스템 케어";
    public const string Scan = "정밀 점검";
    public const string DeviceRepair = "장치 복구";

    public const string AegisMirror = "복구 미러";
    public const string AegisMirrorKorean = "복구 미러";
    public const string MirrorGuardEngine = "복구 미러 엔진";
    public const string RecoveryCapsule = "오프라인 복구 캡슐";

    public const string ShredConfirmation = "보안 삭제에 동의합니다";
    /// <summary>Commercial-grade irreversible confirm phrase (preferred for v4+).</summary>
    public const string ShredIrreversibleConfirmation = "이 작업은 되돌릴 수 없습니다";
    public const string LegacyShredConfirmation = ShredConfirmation;

    public const string UserDataFolder = "PCCare";
    public const string LocalAppDataFolder = "PCCare";
    public const string LegacyUserDataFolder = "AstraCare";
    public const string LegacyUserDataFolder2 = "SmartPerformanceDoctor";
    public const string ProgramDataFolder = "PCCare";
    public const string LegacyProgramDataFolder = "AstraCare";
    public const string LegacyProgramDataFolder2 = "SmartPerformanceDoctor";
    public const string InstallFolderName = "PCCare";
    public const string BrandedExePreferred = "PCCare.exe";

    public const string AegisDisclaimer =
        "복구 미러는 프로그램 손상 시 자동 복구를 수행하기 위한 보호 기능입니다. " +
        "사용자 데이터, 보안 금고 데이터, 개인 파일을 자동 복제하지 않습니다. " +
        "프로그램 제거 시 복구 미러도 함께 제거되며, 보안 프로그램이나 Windows 정책을 우회하지 않습니다.";

    public const string ShredSsdLimitation =
        "SSD/NVMe 파일 단위 삭제는 최대 강도 체인(난독화·랜덤 덮어쓰기·TRIM·볼륨 retrim)을 적용합니다. " +
        "다만 플래시 물리·섀도 복사본·클라우드 동기화 등으로 잔존 가능성이 있어 공인 복구 저항 Level 5 보증은 표기하지 않습니다.";
}