use super::audio;
use super::driver;
use super::intelligence;
use super::report;
use super::system;
use super::types::{emit_response, EngineEnvelope, EngineResponse};
use std::io::{self, Write};
use tokio::io::{AsyncBufReadExt, BufReader};

pub struct EngineOrchestrator;

impl EngineOrchestrator {
    pub fn new() -> Self {
        Self
    }

    pub async fn run_stdio_json_lines(&self) {
        // Prefer async stdin for cancellation-friendly builds; fallback path uses blocking.
        let stdin = tokio::io::stdin();
        let mut reader = BufReader::new(stdin);
        let mut line = String::new();

        match reader.read_line(&mut line).await {
            Ok(0) => return,
            Ok(_) => {}
            Err(e) => {
                let _ = writeln!(io::stderr(), "stdin read error: {e}");
                return;
            }
        }

        let request = match parse_request(line.trim()) {
            Ok(r) => r,
            Err(msg) => {
                emit_response(&EngineResponse {
                    request_id: "parse-error".into(),
                    status: "failed".into(),
                    message: msg,
                    events: vec![],
                    intelligence: None,
                    report_path: None,
                    html_report_path: None,
                    json_report_path: None,
                });
                return;
            }
        };

        let response = self.handle(request).await;
        emit_response(&response);
    }

    async fn handle(&self, request: EngineEnvelope) -> EngineResponse {
        let module = request
            .params
            .get("module")
            .map(|s| s.as_str())
            .unwrap_or("system");
        let apply = request
            .params
            .get("apply")
            .map(|v| v == "1" || v.eq_ignore_ascii_case("true"))
            .unwrap_or(false)
            || request.method.eq_ignore_ascii_case("apply");

        let run = match module {
            "selftest" => system::run_selftest().await,
            "quick" => system::run_quick().await,
            "system" => system::run_system().await,
            "system-recovery" => system::run_system_recovery(apply).await,
            "driver" => driver::run_driver().await,
            "audio" => audio::run_audio().await,
            "full" => run_full().await,
            other => {
                // Unknown module → system baseline with marker.
                let mut base = system::run_system().await;
                base.signals
                    .push(format!("unknown_module_fallback={other}"));
                base.message = format!("{other} 모듈을 system 파이프라인으로 처리했습니다.");
                base
            }
        };

        let intelligence = intelligence::analyze(module, &run.signals, &run.events);
        let paths = report::write_report(
            module,
            &run.status,
            &run.message,
            &run.events,
            &intelligence,
            &run.signals,
        );

        EngineResponse {
            request_id: if request.id.is_empty() {
                uuid::Uuid::new_v4().to_string().replace('-', "")
            } else {
                request.id
            },
            status: run.status,
            message: run.message,
            events: run.events,
            intelligence: Some(intelligence),
            report_path: paths
                .as_ref()
                .map(|p| p.html.to_string_lossy().to_string()),
            html_report_path: paths
                .as_ref()
                .map(|p| p.html.to_string_lossy().to_string()),
            json_report_path: paths
                .as_ref()
                .map(|p| p.json.to_string_lossy().to_string()),
        }
    }
}

async fn run_full() -> system::ModuleRun {
    let mut events = Vec::new();
    let mut signals = Vec::new();

    let quick = system::run_quick().await;
    events.extend(quick.events);
    signals.extend(quick.signals);

    let system = system::run_system().await;
    events.extend(system.events);
    signals.extend(system.signals);

    let driver = driver::run_driver().await;
    events.extend(driver.events);
    signals.extend(driver.signals);

    let audio = audio::run_audio().await;
    events.extend(audio.events);
    signals.extend(audio.signals);

    system::ModuleRun {
        events,
        signals,
        status: "ok".into(),
        message: "full 모듈 실행 완료".into(),
    }
}

fn parse_request(line: &str) -> Result<EngineEnvelope, String> {
    if line.is_empty() {
        return Err("empty request".into());
    }
    serde_json::from_str::<EngineEnvelope>(line).map_err(|e| format!("invalid request json: {e}"))
}
