using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public static class DashboardMetricCatalog
{
    public static IReadOnlyList<DashboardMetricCard> Cards { get; } =
    [
        new("시스템 상태", "대기", "Ready", "◌", "빠른 검사 또는 시스템 진단을 실행하세요."),
        new("드라이버", "워크벤치", "Dry-run first", "◇", "장치 재스캔과 선택 장치 재시작을 분리했습니다."),
        new("오디오", "워크벤치", "Safe apply", "◉", "오디오 서비스와 장치 재스캔을 분리했습니다."),
        new("보고서", "자동 생성", "Report bundle", "□", "Core 응답과 report.html/report.json을 연결합니다."),
        new("복구 로그", "추적 가능", "RepairHelper", "≡", "RepairHelper 실행 로그를 확인할 수 있습니다."),
        new("릴리즈", "RC Gate", "v35", "✓", "빌드/패키징/체크섬/업데이트 게이트를 유지합니다.")
    ];
}
