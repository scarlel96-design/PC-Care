use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use tokio::io::{self, AsyncBufReadExt, AsyncWriteExt};
use tokio::sync::mpsc;

use super::{
    audio,
    command_runner::EngineEvent,
    driver,
    event_sink::{EngineFrame, EventSink},
    intelligence::IntelligenceEngine,
    report_writer::{ReportDocument, ReportWriter, timestamp_human},
    selftest,
    system,
};

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct EngineEnvelope {
    id: String,
    method: String,
    #[serde(default)]
    params: HashMap<String, String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct EngineResponse {
    request_id: String,
    status: String,
    message: String,
    events: Vec<EngineEvent>,
    intelligence: super::intelligence::IntelligenceSummary,
    #[serde(skip_serializing_if = "Option::is_none")]
    report_path: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    html_report_path: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    json_report_path: Option<String>,
}

pub struct EngineOrchestrator;

impl Default for EngineOrchestrator {
    fn default() -> Self {
        Self::new()
    }
}

impl EngineOrchestrator {
    pub fn new() -> Self {
        Self
    }

    pub async fn run_stdio_json_lines(&self) {
        let stdin = io::stdin();
        let mut reader = io::BufReader::new(stdin).lines();
        let mut stdout = io::stdout();

        while let Ok(Some(line)) = reader.next_line().await {
            let request = match serde_json::from_str::<EngineEnvelope>(&line) {
                Ok(request) => request,
                Err(error) => {
                    let response = EngineResponse {
                        request_id: "parse-error".into(),
                        status: "failed".into(),
                        message: format!("request parse failed: {error}"),
                        events: vec![],
                        intelligence: IntelligenceEngine::new().summarize("unknown", &[]),
                        report_path: None,
                        html_report_path: None,
                        json_report_path: None,
                    };
                    write_response_frame(&mut stdout, response).await;
                    continue;
                }
            };

            self.handle_streaming(request, &mut stdout).await;
        }
    }

    async fn handle_streaming(&self, request: EngineEnvelope, stdout: &mut tokio::io::Stdout) {
        let request_id = request.id;
        let method = request.method;
        let module = request.params.get("module").cloned().unwrap_or_else(|| "system".to_string());

        let (tx, mut rx) = mpsc::unbounded_channel::<EngineEvent>();
        let sink = EventSink::new(tx);
        let worker_module = module.clone();
        let worker_method = method.clone();

        let worker = tokio::spawn(async move {
            match (worker_method.as_str(), worker_module.as_str()) {
                ("run_module", "driver") => driver::run_driver_doctor(sink).await,
                ("run_module", "audio") => audio::run_audio_doctor(sink).await,
                ("run_module", "system") => system::run_system_diagnostics(sink).await,
                ("run_module", "quick") => system::run_quick_diagnostics(sink).await,
                ("run_module", "selftest") => selftest::run_self_test(sink).await,
                ("run_module", "system-recovery") | ("run_module", "recovery") => system::run_system_recovery(sink).await,
                _ => vec![format!("unknown route: method={} module={}", worker_method, worker_module)],
            }
        });

        tokio::pin!(worker);
        let mut events = Vec::new();

        loop {
            tokio::select! {
                maybe_event = rx.recv() => {
                    if let Some(event) = maybe_event {
                        events.push(event.clone());
                        write_event_frame(stdout, event).await;
                    } else if worker.is_finished() {
                        break;
                    }
                }
                result = &mut worker => {
                    let signals = match result {
                        Ok(signals) => signals,
                        Err(error) => vec![format!("worker join failed: {error}")],
                    };

                    while let Ok(event) = rx.try_recv() {
                        events.push(event.clone());
                        write_event_frame(stdout, event).await;
                    }

                    let intelligence = IntelligenceEngine::new().summarize(&module, &signals);
                    let bundle = write_report_bundle(&module, &events, &signals, &intelligence);

                    let response = EngineResponse {
                        request_id,
                        status: "ok".into(),
                        message: format!("{module} 모듈 실행 완료"),
                        events: vec![],
                        intelligence,
                        report_path: bundle.as_ref().map(|x| x.html.clone()),
                        html_report_path: bundle.as_ref().map(|x| x.html.clone()),
                        json_report_path: bundle.as_ref().map(|x| x.json.clone()),
                    };

                    write_response_frame(stdout, response).await;
                    return;
                }
            }
        }

        let intelligence = IntelligenceEngine::new().summarize(&module, &[]);
        let response = EngineResponse {
            request_id,
            status: "ok".into(),
            message: format!("{module} 모듈 실행 완료"),
            events: vec![],
            intelligence,
            report_path: None,
            html_report_path: None,
            json_report_path: None,
        };
        write_response_frame(stdout, response).await;
    }
}

fn write_report_bundle(
    module: &str,
    events: &[EngineEvent],
    signals: &[String],
    intelligence: &super::intelligence::IntelligenceSummary,
) -> Option<super::report_writer::ReportBundle> {
    let output_dir = ReportWriter::default_report_dir(module);
    let scan_findings = extract_scan_findings(module, signals);
    let recommended: Vec<String> = intelligence
        .actions
        .iter()
        .map(|x| format!("[{}] {} — {}", x.priority, x.action, x.reason))
        .collect();

    let actions_taken = vec![
        format!("{module} 모듈 정밀 스캔 {}단계 수행", scan_findings.len().max(1)),
        "진단 분석 및 보고서 번들 생성".into(),
        "PC 설정 변경 없음 (진단 단계)".into(),
    ];

    let report = ReportDocument {
        title: format!("스마트 성능 닥터 — {module} 점검 보고서"),
        created_at: timestamp_human(),
        module: module.to_string(),
        status: intelligence.status.clone(),
        summary: intelligence.plain_summary.clone(),
        scan_findings,
        events: events
            .iter()
            .map(|x| format!("[{}] {}", x.severity, x.message))
            .collect(),
        root_causes: intelligence
            .root_causes
            .iter()
            .map(|x| format!("{} / {} / {}", x.area, x.severity, x.explanation))
            .collect(),
        actions: recommended.clone(),
        actions_taken,
        recommended_actions: recommended,
    };

    ReportWriter::write_bundle(&output_dir, &report).ok()
}

fn extract_scan_findings(module: &str, signals: &[String]) -> Vec<String> {
    let mut findings = Vec::new();

    for signal in signals {
        let lower = signal.to_lowercase();
        if lower.contains("problemdevicecount") && lower.contains(":0") {
            findings.push("문제 장치 없음 — 드라이버 충돌 신호 낮음".into());
        }
        if lower.contains("problemdevicecount") && !lower.contains(":0") {
            findings.push("문제 장치 감지 — 드라이버/장치 상태 추가 확인 필요".into());
        }
        if lower.contains("audiodevicecount") && lower.contains(":0") {
            findings.push("오디오 출력 장치 미검출 — 소리 없음 원인 후보".into());
        }
        if lower.contains("servicerestartneeded") && lower.contains("true") {
            findings.push("오디오 서비스 재시작 필요 신호".into());
        }
        if lower.contains("defaultrenderendpoint") && lower.contains("missing") {
            findings.push("기본 재생 장치 없음 — 소리 출력 불가 상태".into());
        }
        if lower.contains("muted") && lower.contains("true") {
            findings.push("시스템 또는 장치 음소거 상태".into());
        }
        if lower.contains("master volume") && (lower.contains(": 0") || lower.contains("\"0\"")) {
            findings.push("마스터 볼륨 0% — 소리 없음 원인 후보".into());
        }
        if lower.contains("unsigned") || lower.contains("issigned\":false") {
            findings.push("서명되지 않은 드라이버 후보 감지".into());
        }
        if lower.contains("configmanagererrorcode") && !lower.contains(":0") {
            findings.push("PnP 구성 오류 코드 감지 — 드라이버 충돌/손상 후보".into());
        }
        if lower.contains("duplicate driver") || lower.contains("driverconflict") {
            findings.push("동일 장치에 중복 드라이버 후보".into());
        }
    }

    if findings.is_empty() {
        findings.push(format!(
            "{module} 모듈 기본 스캔 완료 — 치명 신호는 낮음"
        ));
    }

    findings.sort();
    findings.dedup();
    findings
}

async fn write_event_frame(stdout: &mut tokio::io::Stdout, event: EngineEvent) {
    let frame = EngineFrame {
        frame_type: "event".into(),
        event: Some(event),
        response: None,
    };

    let json = serde_json::to_string(&frame).unwrap_or_else(|_| "{}".to_string());
    let _ = stdout.write_all(json.as_bytes()).await;
    let _ = stdout.write_all(b"\n").await;
    let _ = stdout.flush().await;
}

async fn write_response_frame(stdout: &mut tokio::io::Stdout, response: EngineResponse) {
    let value = serde_json::to_value(response).unwrap_or_else(|_| serde_json::json!({}));
    let frame = EngineFrame {
        frame_type: "response".into(),
        event: None,
        response: Some(value),
    };

    let json = serde_json::to_string(&frame).unwrap_or_else(|_| "{}".to_string());
    let _ = stdout.write_all(json.as_bytes()).await;
    let _ = stdout.write_all(b"\n").await;
    let _ = stdout.flush().await;
}
