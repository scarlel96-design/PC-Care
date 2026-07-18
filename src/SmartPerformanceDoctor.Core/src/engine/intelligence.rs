use super::types::{ActionPlanItem, EngineEvent, IntelligenceSummary, RootCauseCandidate};

/// Rule-based intelligence fusion restored from v50 binary signal heuristics.
pub fn analyze(module: &str, signals: &[String], events: &[EngineEvent]) -> IntelligenceSummary {
    let mut root_causes = Vec::new();
    let mut actions = Vec::new();
    let mut score: i32 = 94;
    let joined = signals.join(" | ").to_lowercase();

    let critical_markers = [
        ("bluescreen", 18, "critical", "블루스크린 신호가 감지되었습니다."),
        ("bugcheck", 16, "critical", "BugCheck 이벤트가 확인되었습니다."),
        ("whea", 12, "warning", "하드웨어 오류(WHEA) 신호가 있습니다."),
        ("unexpected_shutdown", 10, "warning", "예기치 않은 종료 기록이 있습니다."),
        ("configmanagererror", 12, "warning", "PnP 장치 구성 오류가 있습니다."),
        ("driverconflict", 10, "warning", "드라이버 충돌 가능성이 있습니다."),
        ("service_stopped", 8, "warning", "자동 시작 서비스가 중지 상태입니다."),
        ("disk_unhealthy", 14, "critical", "디스크 상태가 불량입니다."),
        ("low_disk", 8, "warning", "디스크 여유 공간이 부족합니다."),
        ("reboot_pending", 6, "info", "재부팅 대기 상태가 있습니다."),
        ("audio_service", 10, "warning", "오디오 서비스 이상이 있습니다."),
        ("no_render_endpoint", 12, "warning", "재생 엔드포인트가 없습니다."),
        ("unsigned driver", 6, "info", "서명되지 않은 드라이버가 있습니다."),
    ];

    for (marker, penalty, severity, explanation) in critical_markers {
        if joined.contains(marker) {
            score -= penalty;
            root_causes.push(RootCauseCandidate {
                area: module.into(),
                severity: severity.into(),
                evidence: marker.into(),
                explanation: explanation.into(),
                recommendation: recommendation_for(marker).into(),
                confidence: if severity == "critical" { 0.86 } else { 0.72 },
            });
            actions.push(ActionPlanItem {
                priority: if severity == "critical" {
                    "1".into()
                } else {
                    "2".into()
                },
                area: module.into(),
                action: action_for(marker).into(),
                reason: explanation.into(),
                risk: if severity == "critical" {
                    "medium".into()
                } else {
                    "low".into()
                },
            });
        }
    }

    // Count warning-level streaming events.
    let warn_events = events
        .iter()
        .filter(|e| e.severity == "warning" || e.severity == "error")
        .count() as i32;
    score -= warn_events * 2;

    if root_causes.is_empty() {
        root_causes.push(RootCauseCandidate {
            area: module.into(),
            severity: "info".into(),
            evidence: "no critical signal".into(),
            explanation: "치명 신호 없음: 현재 수집된 신호에서 치명적인 오류 패턴이 없습니다.".into(),
            recommendation: "문제가 계속되면 해당 모듈을 단독 실행하고 사후 검증을 확인하세요.".into(),
            confidence: 0.6,
        });
        actions.push(ActionPlanItem {
            priority: "1".into(),
            area: module.into(),
            action: "사후 검증 포함 모듈 재실행".into(),
            reason: "복구 성공 여부는 리스캔으로만 확정할 수 있습니다.".into(),
            risk: "low".into(),
        });
    }

    score = score.clamp(5, 100);
    let status = match score {
        90..=100 => "양호",
        75..=89 => "주의",
        55..=74 => "경고",
        _ => "위험",
    };

    IntelligenceSummary {
        score,
        status: status.into(),
        plain_summary: format!(
            "{module} 모듈에서 {}개 신호를 분석했습니다. 규칙 {}개를 적용했습니다.",
            signals.len().max(events.len()),
            root_causes.len()
        ),
        root_causes,
        actions,
    }
}

fn recommendation_for(marker: &str) -> &'static str {
    match marker {
        "bluescreen" | "bugcheck" => "최근 드라이버 업데이트·메모리 진단·미니덤프를 확인하세요.",
        "whea" => "RAM/CPU/SSD 하드웨어 상태를 점검하고 과열·전원 이상을 확인하세요.",
        "unexpected_shutdown" => "전원 공급·과열·강제 종료 원인을 확인하세요.",
        "configmanagererror" | "driverconflict" => "문제 장치를 재시작·재스캔하고 공식 드라이버를 재설치하세요.",
        "service_stopped" => "자동 시작 서비스를 재시작하고 시작 유형을 확인하세요.",
        "disk_unhealthy" | "low_disk" => "여유 공간 확보 후 chkdsk/저장장치 상태 점검을 권장합니다.",
        "reboot_pending" => "업데이트가 완료되도록 재부팅을 권장합니다.",
        "audio_service" | "no_render_endpoint" => "Audiosrv/AudioEndpointBuilder 재시작 후 장치 재스캔을 실행하세요.",
        _ => "관련 모듈을 다시 실행하고 사후 검증 보고서를 확인하세요.",
    }
}

fn action_for(marker: &str) -> &'static str {
    match marker {
        "bluescreen" | "bugcheck" => "안정성 이벤트·미니덤프 분석",
        "whea" => "하드웨어 진단 실행",
        "configmanagererror" | "driverconflict" => "문제 장치 재스캔",
        "service_stopped" => "중지된 자동 서비스 재시작",
        "disk_unhealthy" | "low_disk" => "디스크 정리 및 상태 점검",
        "reboot_pending" => "재부팅 후 재검증",
        "audio_service" | "no_render_endpoint" => "오디오 스택 재시작",
        _ => "사후 검증 포함 모듈 재실행",
    }
}
