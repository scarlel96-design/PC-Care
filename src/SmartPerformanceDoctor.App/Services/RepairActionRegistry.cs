using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public static class RepairActionRegistry
{
    public static IReadOnlyList<RepairActionDescriptor> DriverActions { get; } =
    [
        new(
            "driver_repair_plan_only",
            "드라이버 복구 계획만 생성",
            "문제 장치 목록과 이벤트를 기준으로 복구 계획만 생성합니다. 실제 변경은 없습니다.",
            "driver",
            "low",
            false,
            "대상 불필요",
            "online-image"),

        new(
            "driver_check_problem_devices",
            "문제 장치 재확인",
            "Get-PnpDevice와 pnputil 문제 장치 확인을 실행합니다.",
            "driver",
            "low",
            false,
            "대상 불필요",
            "online-image"),

        new(
            "pnputil_scan_devices",
            "장치 재스캔",
            "Windows 장치 트리를 다시 스캔합니다.",
            "driver",
            "low",
            false,
            "대상 불필요",
            "online-image"),

        new(
            "pnputil_restart_device",
            "선택 장치 재시작",
            "입력한 InstanceId의 장치를 pnputil로 재시작합니다. 잘못된 대상 입력을 주의해야 합니다.",
            "driver",
            "medium",
            true,
            @"예: USB\\VID_xxxx 또는 장치 InstanceId",
            "")
    ];

    public static IReadOnlyList<RepairActionDescriptor> AudioActions { get; } =
    [
        new(
            "audio_repair_plan_only",
            "오디오 복구 계획만 생성",
            "오디오 서비스/장치 상태를 기준으로 복구 순서를 생성합니다. 실제 변경은 없습니다.",
            "audio",
            "low",
            false,
            "대상 불필요",
            "online-image"),

        new(
            "restart_audiosrv",
            "Windows Audio 서비스 재시작",
            "Audiosrv 서비스를 재시작합니다.",
            "audio",
            "medium",
            false,
            "대상 불필요",
            "online-image"),

        new(
            "restart_audioendpointbuilder",
            "Audio Endpoint Builder 재시작",
            "AudioEndpointBuilder 서비스를 재시작합니다.",
            "audio",
            "medium",
            false,
            "대상 불필요",
            "online-image"),

        new(
            "audio_restart_stack",
            "오디오 스택 순차 재시작",
            "AudioEndpointBuilder와 Audiosrv를 순서대로 재시작합니다.",
            "audio",
            "medium",
            false,
            "대상 불필요",
            "online-image"),

        new(
            "audio_scan_devices",
            "오디오 장치 재스캔",
            "장치 스캔을 실행해 오디오 장치 재탐지를 유도합니다.",
            "audio",
            "low",
            false,
            "대상 불필요",
            "online-image")
    ];

    public static IReadOnlyList<RepairActionDescriptor> SystemActions { get; } =
    [
        new(
            "dism_checkhealth",
            "시스템 이미지 상태 확인",
            "DISM CheckHealth로 Windows 구성 요소 저장소 상태를 확인합니다.",
            "system",
            "low",
            false,
            "대상 불필요",
            "online-image"),

        new(
            "sfc_verifyonly",
            "시스템 파일 무결성 확인",
            "SFC로 시스템 파일 상태만 확인합니다. 실제 복구는 별도 승인이 필요합니다.",
            "system",
            "medium",
            false,
            "대상 불필요",
            "online-image")
    ];

    public static IReadOnlyList<RepairActionDescriptor> AllActions =>
        DriverActions.Concat(AudioActions).Concat(SystemActions).ToArray();
}
