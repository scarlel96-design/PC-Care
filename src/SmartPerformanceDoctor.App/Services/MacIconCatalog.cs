using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public static class MacIconCatalog
{
    // v34: SF-Symbols-like semantic catalog using built-in text glyphs.
    // Apple SF Symbols files are not embedded.
    public static IReadOnlyList<MacNavigationItem> NavigationItems { get; } =
    [
        new("quick", "빠른 검사", "⌁", "Diagnostics", "빠른 상태 확인"),
        new("system", "시스템", "◌", "Diagnostics", "CPU, 메모리, 디스크, OS"),
        new("driver", "드라이버 복구", "◇", "Repair", "장치/드라이버 워크벤치"),
        new("audio", "오디오 복구", "◉", "Repair", "오디오 서비스/장치 워크벤치"),
        new("risk", "위험 승인", "◇", "Repair", "복구 실행 승인 게이트"),
        new("reports", "보고서", "□", "Records", "진단 보고서"),
        new("repairlogs", "복구 로그", "≡", "Records", "RepairHelper 로그"),
        new("crashlogs", "크래시 로그", "△", "Records", "비정상 종료 로그"),
        new("appdiag", "앱 진단", "◎", "System", "런타임 구성 검사"),
        new("release", "릴리즈 상태", "✓", "System", "품질 게이트"),
        new("updates", "업데이트", "↻", "System", "업데이트 채널"),
        new("first", "초기 설정", "✦", "System", "첫 실행 설정"),
        new("heal", "자가 복구", "✚", "System", "누락 구성 복구"),
        new("errorbundle", "오류 번들", "⬡", "System", "오류 정보 수집")
    ];

    public static string SymbolFor(string id)
    {
        return NavigationItems.FirstOrDefault(x => x.Id == id)?.Symbol ?? "•";
    }
}
