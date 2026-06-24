using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public static class ModuleRegistry
{
    public static ModuleDescriptor Get(string id)
    {
        return id switch
        {
            "driver" => new ModuleDescriptor(
                "driver",
                "드라이버 복구",
                "문제 장치, 드라이버 저장소, ConfigManager 오류, Kernel-PnP 이벤트를 상관 분석합니다.",
                "#7AA7FF",
                "medium",
                ["사전 스냅샷", "문제 장치 스캔", "드라이버 인벤토리", "이벤트 상관 분석", "복구 계획", "안전 재시작", "사후 검증"]),

            "audio" => new ModuleDescriptor(
                "audio",
                "오디오 복구",
                "오디오 엔드포인트, Audiosrv, AudioEndpointBuilder, Realtek/Bluetooth/NVIDIA 오디오 스택을 분석합니다.",
                "#B48CFF",
                "medium",
                ["오디오 스냅샷", "엔드포인트 스캔", "서비스 확인", "오디오 이벤트 상관 분석", "서비스 재구동", "장치 재스캔", "사후 검증"]),

            "system-recovery" => new ModuleDescriptor(
                "system-recovery",
                "시스템 복구",
                "DISM/SFC 복구를 감독 모드로 실행하고 63% 정체를 별도 감시합니다.",
                "#FFB86B",
                "high",
                ["CheckHealth", "ScanHealth", "RestoreHealth 감독", "Component Store 분석", "SFC", "보고서"]),

            "selftest" => new ModuleDescriptor(
                "selftest",
                "엔진 자체 검증",
                "Windows 명령 없이 Core/JSON/스트리밍/인텔리전스 연결만 검증합니다.",
                "#65D6A6",
                "low",
                ["Core 시작", "JSON Lines", "EventSink", "Intelligence", "ReportWriter"]),

            "quick" => new ModuleDescriptor(
                "quick",
                "빠른 검사",
                "시스템 핵심 상태를 빠르게 스캔하고 보고서를 생성합니다.",
                "#65D6A6",
                "low",
                ["시스템 정보", "서비스", "이벤트 요약", "인텔리전스", "보고서"]),

            "system" => new ModuleDescriptor(
                "system",
                "시스템 점검",
                "OS, 저장소, 서비스, WMI, 재부팅 대기, 안정성 이벤트를 분석합니다. 드라이버·오디오는 전체 점검에서만 수행됩니다.",
                "#65D6A6",
                "low",
                ["시스템 개요", "저장소 상태", "서비스 이상", "WMI 저장소", "재부팅 대기", "안정성 이벤트", "AI 추론"]),

            _ => new ModuleDescriptor(
                id,
                "시스템 진단",
                "Windows 11 시스템 상태를 분석합니다.",
                "#65D6A6",
                "low",
                ["시스템 정보", "저장소", "서비스", "이벤트", "보고서"])
        };
    }
}
