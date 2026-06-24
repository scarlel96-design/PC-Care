use serde::Serialize;
use tokio::sync::mpsc;

use super::command_runner::EngineEvent;

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineFrame {
    pub frame_type: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub event: Option<EngineEvent>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub response: Option<serde_json::Value>,
}

#[derive(Clone)]
pub struct EventSink {
    tx: mpsc::UnboundedSender<EngineEvent>,
}

impl EventSink {
    pub fn new(tx: mpsc::UnboundedSender<EngineEvent>) -> Self {
        Self { tx }
    }

    pub fn emit(&self, event: EngineEvent) {
        let _ = self.tx.send(event);
    }

    pub fn stage(&self, module: &str, message: impl Into<String>, progress: u8) {
        self.emit(EngineEvent {
            r#type: "stage".into(),
            module: module.into(),
            message: message.into(),
            progress,
            severity: "info".into(),
        });
    }

    pub fn warning(&self, module: &str, message: impl Into<String>, progress: u8) {
        self.emit(EngineEvent {
            r#type: "stage".into(),
            module: module.into(),
            message: message.into(),
            progress,
            severity: "warning".into(),
        });
    }
}
