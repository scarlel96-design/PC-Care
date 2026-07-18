use serde::{Deserialize, Serialize};
use std::collections::HashMap;

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineEnvelope {
    #[serde(default = "default_jsonrpc")]
    pub jsonrpc: String,
    #[serde(default)]
    pub id: String,
    #[serde(default)]
    pub method: String,
    #[serde(default)]
    pub params: HashMap<String, String>,
}

fn default_jsonrpc() -> String {
    "2.0".into()
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineFrame {
    pub frame_type: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub event: Option<EngineEvent>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub response: Option<EngineResponse>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineEvent {
    #[serde(rename = "type")]
    pub event_type: String,
    pub module: String,
    pub message: String,
    pub progress: i32,
    pub severity: String,
    pub timestamp: String,
}

impl EngineEvent {
    pub fn stage(module: &str, progress: i32, message: impl Into<String>) -> Self {
        Self {
            event_type: "stage".into(),
            module: module.into(),
            message: message.into(),
            progress,
            severity: "info".into(),
            timestamp: chrono_now(),
        }
    }

    pub fn signal(module: &str, progress: i32, severity: &str, message: impl Into<String>) -> Self {
        Self {
            event_type: "signal".into(),
            module: module.into(),
            message: message.into(),
            progress,
            severity: severity.into(),
            timestamp: chrono_now(),
        }
    }
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineResponse {
    pub request_id: String,
    pub status: String,
    pub message: String,
    pub events: Vec<EngineEvent>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub intelligence: Option<IntelligenceSummary>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub report_path: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub html_report_path: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub json_report_path: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct IntelligenceSummary {
    pub score: i32,
    pub status: String,
    pub plain_summary: String,
    pub root_causes: Vec<RootCauseCandidate>,
    pub actions: Vec<ActionPlanItem>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RootCauseCandidate {
    pub area: String,
    pub severity: String,
    pub evidence: String,
    pub explanation: String,
    pub recommendation: String,
    pub confidence: f64,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ActionPlanItem {
    pub priority: String,
    pub area: String,
    pub action: String,
    pub reason: String,
    pub risk: String,
}

fn chrono_now() -> String {
    // RFC3339-ish local timestamp without extra deps.
    use std::time::{SystemTime, UNIX_EPOCH};
    let secs = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs())
        .unwrap_or(0);
    format!("unix:{secs}")
}

pub fn emit_event(event: &EngineEvent) {
    let frame = EngineFrame {
        frame_type: "event".into(),
        event: Some(event.clone()),
        response: None,
    };
    if let Ok(json) = serde_json::to_string(&frame) {
        // Flush every frame — stdout is fully buffered when piped to the app.
        println!("{json}");
        let _ = std::io::Write::flush(&mut std::io::stdout());
    }
}

pub fn emit_response(response: &EngineResponse) {
    let frame = EngineFrame {
        frame_type: "response".into(),
        event: None,
        response: Some(response.clone()),
    };
    if let Ok(json) = serde_json::to_string(&frame) {
        println!("{json}");
        let _ = std::io::Write::flush(&mut std::io::stdout());
    }
}
