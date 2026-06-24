use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum EngineKind {
    System,
    Driver,
    Audio,
    Security,
    Storage,
    Update,
    Report,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TaskSpec {
    pub id: String,
    pub engine: EngineKind,
    pub description: String,
    pub timeout_seconds: u64,
    pub risk: String,
    pub dry_run_first: bool,
}
