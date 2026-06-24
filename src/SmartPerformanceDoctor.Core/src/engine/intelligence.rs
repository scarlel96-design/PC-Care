use super::rules_loader::{load_rules, resolve_rules_base_dir, RulesCatalog};
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RootCause {
    pub area: String,
    pub severity: String,
    pub evidence: String,
    pub explanation: String,
    pub recommendation: String,
    pub confidence: f32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ActionPlanItem {
    pub priority: String,
    pub area: String,
    pub action: String,
    pub reason: String,
    pub risk: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IntelligenceSummary {
    pub score: u8,
    pub status: String,
    pub plain_summary: String,
    pub root_causes: Vec<RootCause>,
    pub actions: Vec<ActionPlanItem>,
}

pub struct IntelligenceEngine {
    rules: RulesCatalog,
}

impl Default for IntelligenceEngine {
    fn default() -> Self {
        Self {
            rules: load_rules(&resolve_rules_base_dir()),
        }
    }
}

impl IntelligenceEngine {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn summarize(&self, module: &str, signals: &[String]) -> IntelligenceSummary {
        let mut root_causes = Vec::new();
        let mut actions = Vec::new();
        let mut score = 94u8;

        for signal in signals {
            let s = signal.to_lowercase();

            if let Some(code) = extract_problem_code(&s)
                && let Some(rule) = self.rules.driver_codes.get(&code)
            {
                    score = score.saturating_sub(risk_penalty(&rule.risk));
                    root_causes.push(Self::cause(
                        module,
                        map_risk_to_severity(&rule.risk),
                        signal,
                        &rule.title,
                        "규칙 DB에 등록된 장치 문제 코드가 감지되었습니다.",
                        &rule.recommendation,
                        0.82,
                    ));
                    actions.push(ActionPlanItem {
                        priority: "1".into(),
                        area: module.to_string(),
                        action: rule.recommendation.clone(),
                        reason: format!("문제 코드 {code} 규칙 매칭"),
                        risk: rule.risk.clone(),
                    });
                    continue;
            }

            if (s.contains("low disk") || s.contains("disk free"))
                && let Some(rule) = self.rules.system_rules.get("low_disk")
            {
                    score = score.saturating_sub(10);
                    root_causes.push(Self::cause(
                        module,
                        "warning",
                        signal,
                        "저장 공간 부족",
                        "시스템 규칙에 따라 디스크 여유가 낮습니다.",
                        &rule.action,
                        0.78,
                    ));
                    continue;
            }

            if s.contains("memory") && (s.contains("high") || s.contains("used"))
                && let Some(rule) = self.rules.system_rules.get("high_memory")
            {
                    score = score.saturating_sub(8);
                    root_causes.push(Self::cause(
                        module,
                        "warning",
                        signal,
                        "메모리 사용량 높음",
                        "시스템 규칙에 따라 메모리 사용률이 높습니다.",
                        &rule.action,
                        0.74,
                    ));
                    continue;
            }

            if s.contains("state") && (s.contains("stopped") || s.contains("not running"))
                && let Some(rule) = self.rules.system_rules.get("service_stopped")
            {
                    score = score.saturating_sub(14);
                    root_causes.push(Self::cause(
                        module,
                        "warning",
                        signal,
                        "자동 시작 서비스 중단",
                        "자동 시작으로 설정된 서비스가 실행 중이 아닙니다.",
                        &rule.action,
                        0.80,
                    ));
                    actions.push(ActionPlanItem {
                        priority: "1".into(),
                        area: module.to_string(),
                        action: rule.action.clone(),
                        reason: "서비스 상태 이상 규칙 매칭".into(),
                        risk: "medium".into(),
                    });
                    continue;
            }

            if (s.contains("reboot") || s.contains("pending reboot"))
                && let Some(rule) = self.rules.system_rules.get("pending_reboot")
            {
                    score = score.saturating_sub(6);
                    root_causes.push(Self::cause(
                        module,
                        "info",
                        signal,
                        "재부팅 필요",
                        "시스템 업데이트 또는 복구 작업 후 재부팅이 필요할 수 있습니다.",
                        &rule.action,
                        0.70,
                    ));
                    continue;
            }

            if let Some((rule_name, rule)) = self.match_audio_rule(&s) {
                if rule_name == "repair_not_needed" {
                    score = score.saturating_add(2).min(99);
                    continue;
                }

                score = score.saturating_sub(10);
                root_causes.push(Self::cause(
                    module,
                    "warning",
                    signal,
                    &format!("오디오 규칙: {rule_name}"),
                    "오디오 정밀 스캔 신호가 규칙 DB와 일치했습니다.",
                    &rule.action,
                    0.84,
                ));
                actions.push(ActionPlanItem {
                    priority: "1".into(),
                    area: module.to_string(),
                    action: rule.action.clone(),
                    reason: format!("audio_rules/{rule_name}"),
                    risk: "medium".into(),
                });
                continue;
            }

            if s.contains("driverconflict\":true") || s.contains("duplicate driver") {
                score = score.saturating_sub(16);
                root_causes.push(Self::cause(
                    module,
                    "warning",
                    signal,
                    "드라이버 충돌 후보",
                    "동일 장치에 복수 드라이버 또는 PnP 충돌 신호가 감지되었습니다.",
                    "문제 장치만 선별 재시작 후 공식 드라이버 재설치를 검토하세요.",
                    0.86,
                ));
                actions.push(ActionPlanItem {
                    priority: "1".into(),
                    area: module.to_string(),
                    action: "문제 장치 선별 재시작 및 드라이버 재설치 검토".into(),
                    reason: "driver_conflict_scan".into(),
                    risk: "medium".into(),
                });
                continue;
            }

            if s.contains("repairneeded\":false") || s.contains("no repair needed") || s.contains("무작정 복구 불필요") {
                score = score.saturating_add(3).min(99);
                continue;
            }

            if s.contains("timeout") {
                score = score.saturating_sub(15);
                root_causes.push(Self::cause(
                    module,
                    "warning",
                    signal,
                    "작업 제한 시간 초과",
                    "해당 단계가 지연되거나 응답하지 않았습니다.",
                    "단독 재실행 후 반복되면 관련 이벤트 로그를 우선 확인하세요.",
                    0.70,
                ));
            } else if s.contains("spawn_failed") || s.contains("wait_failed") {
                score = score.saturating_sub(12);
                root_causes.push(Self::cause(
                    module,
                    "warning",
                    signal,
                    "진단 명령 실행 실패",
                    "Windows 구성 요소, 권한, 명령 지원 여부에 문제가 있을 수 있습니다.",
                    "관리자 권한과 Windows 11 기본 구성 요소 상태를 확인하세요.",
                    0.65,
                ));
            } else if s.contains("problem") || s.contains("error") || s.contains("stopped") || s.contains("warning") {
                score = score.saturating_sub(8);
                root_causes.push(Self::cause(
                    module,
                    "info",
                    signal,
                    "문제 신호 후보",
                    "진단 출력에 문제 후보 문자열이 포함되어 있습니다.",
                    "보고서 원본 로그와 모듈 사후 검증을 함께 확인하세요.",
                    0.55,
                ));
            }
        }

        if root_causes.is_empty() {
            root_causes.push(Self::cause(
                module,
                "info",
                "no critical signal",
                "치명 신호 없음",
                "현재 수집된 신호에서 치명적인 오류 후보는 낮습니다.",
                "문제가 계속되면 해당 모듈을 단독 실행하고 사후 검증을 확인하세요.",
                0.60,
            ));
        }

        if actions.is_empty() {
            actions.push(ActionPlanItem {
                priority: "1".into(),
                area: module.to_string(),
                action: "사후 검증 포함 모듈 실행".into(),
                reason: "복구 성공 여부는 재스캔으로만 확정할 수 있습니다.".into(),
                risk: "low".into(),
            });
        }

        let rule_count = self.rules.driver_codes.len()
            + self.rules.system_rules.len()
            + self.rules.audio_rules.len();

        IntelligenceSummary {
            score,
            status: if score >= 85 {
                "양호".into()
            } else if score >= 65 {
                "주의".into()
            } else {
                "위험".into()
            },
            plain_summary: format!(
                "{module} 모듈에서 {}개 신호를 분석했습니다. 규칙 {rule_count}개가 적용되었습니다.",
                signals.len(),
                rule_count = rule_count
            ),
            root_causes,
            actions,
        }
    }

    fn match_audio_rule(&self, signal: &str) -> Option<(&str, &super::rules_loader::AudioRuleEntry)> {
        for (name, rule) in &self.rules.audio_rules {
            for marker in &rule.signals {
                if signal.contains(&marker.to_lowercase()) {
                    return Some((name.as_str(), rule));
                }
            }
        }
        None
    }

    fn cause(
        module: &str,
        severity: &str,
        evidence: &str,
        title: &str,
        explanation: &str,
        recommendation: &str,
        confidence: f32,
    ) -> RootCause {
        RootCause {
            area: module.to_string(),
            severity: severity.to_string(),
            evidence: compact(evidence, 380),
            explanation: format!("{title}: {explanation}"),
            recommendation: recommendation.to_string(),
            confidence,
        }
    }
}

fn extract_problem_code(signal: &str) -> Option<String> {
    for token in signal.split(|c: char| !c.is_ascii_digit()) {
        if (token.len() == 1 || token.len() == 2) && token.chars().all(|c| c.is_ascii_digit()) {
            return Some(token.to_string());
        }
    }
    None
}

fn risk_penalty(risk: &str) -> u8 {
    match risk.to_lowercase().as_str() {
        "high" | "critical" => 18,
        "medium" => 12,
        _ => 8,
    }
}

fn map_risk_to_severity(risk: &str) -> &'static str {
    match risk.to_lowercase().as_str() {
        "high" | "critical" => "warning",
        "medium" => "info",
        _ => "info",
    }
}

fn compact(input: &str, max: usize) -> String {
    if input.len() <= max {
        input.to_string()
    } else {
        format!("{}…", &input[..max.min(input.len())])
    }
}