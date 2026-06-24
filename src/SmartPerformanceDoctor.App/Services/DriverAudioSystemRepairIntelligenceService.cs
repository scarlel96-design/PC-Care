using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class DriverAudioSystemRepairIntelligenceService
{
    public IReadOnlyList<IntelligentRepairPlan> BuildPlans()
    {
        return
        [
            new(
                "driver-safe-rescan-v39",
                "driver",
                "장치 트리 불일치, 장치 재탐지 지연, 문제 장치 상태 가능성",
                "low",
                "pnputil_scan_devices",
                "Get-PnpDevice/pnputil 결과와 RepairHelper exit code, post-check snapshot 비교",
                true,
                false,
                "대상 불필요",
                [
                    "문제 장치 목록 확인",
                    "pnputil /scan-devices dry-run 계획",
                    "승인 시 장치 재스캔",
                    "post-check로 문제 장치 변화 확인",
                    "복구 로그 저장"
                ],
                [
                    "장치 삭제/드라이버 삭제 없음",
                    "문제 지속 시 정확한 InstanceId 기반 장치 재시작으로 분기"
                ]),

            new(
                "driver-targeted-restart-v39",
                "driver",
                "특정 장치 드라이버 stack stuck 또는 PnP state mismatch 가능성",
                "medium",
                "pnputil_restart_device",
                "대상 InstanceId 검증, RepairHelper exit code, post-check snapshot 비교",
                true,
                true,
                "장치 관리자/진단 결과의 InstanceId 필요",
                [
                    "InstanceId 입력 검증",
                    "dry-run으로 명령 계획 확인",
                    "승인 시 선택 장치만 재시작",
                    "post-check로 상태 변화 확인",
                    "복구 로그 저장"
                ],
                [
                    "전체 드라이버 삭제 금지",
                    "정확한 InstanceId 없이는 실행하지 않음"
                ]),

            new(
                "audio-stack-restart-v39",
                "audio",
                "Audiosrv/AudioEndpointBuilder hang 또는 endpoint enumeration 문제 가능성",
                "medium",
                "audio_restart_stack",
                "서비스 상태 pre/post, RepairHelper exit code, audio device scan 결과 비교",
                true,
                false,
                "대상 불필요",
                [
                    "오디오 서비스 상태 확인",
                    "dry-run 계획 생성",
                    "승인 시 AudioEndpointBuilder/Audiosrv 순차 재시작",
                    "오디오 장치 재스캔",
                    "post-check로 서비스 Running 여부 확인"
                ],
                [
                    "장치 삭제 없음",
                    "Bluetooth는 재페어링 전 서비스 재시작 우선"
                ]),

            new(
                "system-image-health-v39",
                "system",
                "Windows component store 손상 또는 시스템 파일 무결성 문제 가능성",
                "high",
                "dism_checkhealth",
                "DISM/SFC 결과, heartbeat, report/log evidence 비교",
                true,
                false,
                "대상 불필요",
                [
                    "DISM CheckHealth",
                    "필요 시 ScanHealth",
                    "RestoreHealth는 heartbeat guard로 진행",
                    "SFC verifyonly",
                    "필요 시 scannow"
                ],
                [
                    "강제 중단 금지",
                    "63% 정체 시 dism.log heartbeat 확인"
                ])
        ];
    }

    public RepairIntelligenceProfile BuildProfile()
    {
        var plans = BuildPlans();
        return new RepairIntelligenceProfile(
            "driver/audio/system",
            "intelligent-ready",
            "복합 문제는 driver/audio/system 순서로 증거 기반 분기 처리",
            plans.First().PlanId,
            "medium",
            [
                "dry-run-first",
                "pre-post-snapshot",
                "repair-helper-log",
                "exit-code",
                "progress-event-stream",
                "error-bundle-ready"
            ]);
    }
}
