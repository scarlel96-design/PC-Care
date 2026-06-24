pub fn label_for(stage_id: &str) -> String {
    match stage_id {
        "device_preflight_snapshot" => "문제 장치 사전 점검",
        "pnp_problem_scan" => "장치 상태 스캔",
        "pnputil_problem_devices" => "Windows 문제 장치 목록 확인",
        "pnputil_driver_store_inventory" => "드라이버 저장소 목록 확인",
        "driver_pnp_entity_error_map" => "장치 오류 코드 분석",
        "driver_signed_inventory" => "설치된 드라이버 서명·버전 확인",
        "device_event_correlation" => "장치·드라이버 관련 이벤트 분석",
        "driver_conflict_scan" => "드라이버 충돌·중복 설치 검사",
        "device_repair_plan" => "드라이버 복구 계획 수립",

        "audio_preflight_snapshot" => "오디오 장치·서비스 사전 점검",
        "audio_device_scan" => "오디오 장치 상태 스캔",
        "audio_cim_sounddevice" => "사운드 장치 정보 수집",
        "audio_driver_inventory" => "오디오 드라이버 목록 확인",
        "audio_service_scan" => "오디오 서비스(Audiosrv 등) 확인",
        "audio_event_correlation" => "오디오 관련 시스템 이벤트 분석",
        "audio_playback_health" => "재생 장치·출력 경로 점검",
        "audio_endpoint_mute_volume" => "음소거·볼륨 상태 확인",
        "audio_repair_plan" => "오디오 복구 계획 수립",

        "quick_os_memory" => "운영체제·메모리 상태 확인",
        "quick_disk" => "디스크 여유 공간 확인",
        "quick_problem_devices" => "문제 장치 빠른 스캔",

        "system_overview" => "시스템 기본 정보 수집",
        "storage_health" => "저장장치 건강 상태 확인",
        "service_anomaly_scan" => "자동 시작 서비스 이상 검사",
        "wmi_repository_check" => "WMI 저장소 무결성 확인",
        "pending_reboot_check" => "재부팅 대기 상태 확인",
        "reliability_crash_digest" => "최근 오류·충돌 이벤트 요약",

        "dism_check_health" => "Windows 이미지 건강 빠른 확인",
        "dism_scan_health" => "Windows 이미지 손상 스캔",
        "component_store_analyze" => "구성 요소 저장소 분석",
        "sfc_scannow" => "시스템 파일 무결성 검사(SFC)",

        other => return other.replace('_', " "),
    }
    .to_string()
}

pub fn status_label(status: &str) -> String {
    match status {
        "ok" => "정상",
        "warning" => "주의",
        "timeout" => "시간 초과",
        "spawn_failed" => "실행 실패",
        "wait_failed" => "대기 실패",
        other => return other.to_string(),
    }
    .to_string()
}