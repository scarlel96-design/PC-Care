namespace SmartPerformanceDoctor.SecurityLab.Migration;

/// <summary>
/// Design notes for v3 → Lab v4. Dry-run API: <see cref="V3MigrationDryRun"/>.
/// </summary>
public static class V3ToV4MigrationNotes
{
    public const string ProductFormat = "spd-vault-v3";
    public const string LabFormat = "spd-vault-v5-lab";

    public static IReadOnlyList<string> DryRunChecklist { get; } =
    [
        "1. 제품 금고 백업(전체 폴더 복사)",
        "2. V3MigrationDryRun.Analyze(productRoot) 구조 스캔",
        "3. 제품 앱에서 v3 잠금 해제 후 entry 목록·해시 스냅샷",
        "4. V3ToLabMigrator / UI 'v3→v4 마이그레이션' re-encrypt import",
        "5. Lab export 해시 비교",
        "6. 원본 v3 유지(자동 삭제 금지) · 경로 교체는 사용자 승인 후"
    ];

    public static string Status =>
        "dry-run + execute re-import implemented (V3ToLabMigrator) · Lab→AV3 execute still denied · package deferred";
}
