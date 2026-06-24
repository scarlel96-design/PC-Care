use base64::{engine::general_purpose, Engine as _};
use serde::{Deserialize, Serialize};
use std::process::Stdio;
use std::time::Duration;
use tokio::process::Command;
use tokio::time::timeout;

use super::security::redact;
use super::stage_labels;

#[derive(Debug, Clone)]
pub struct CommandSpec {
    pub id: &'static str,
    pub module: &'static str,
    pub program: String,
    pub args: Vec<String>,
    pub timeout_seconds: u64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CommandResult {
    pub id: String,
    pub status: String,
    pub exit_code: Option<i32>,
    pub stdout: String,
    pub stderr: String,
    pub timed_out: bool,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct EngineEvent {
    pub r#type: String,
    pub module: String,
    pub message: String,
    pub progress: u8,
    pub severity: String,
}

impl CommandSpec {
    pub fn powershell(module: &'static str, id: &'static str, script: &str, timeout_seconds: u64) -> Self {
        Self {
            id,
            module,
            program: "powershell.exe".to_string(),
            args: vec![
                "-NoLogo".to_string(),
                "-NoProfile".to_string(),
                "-NonInteractive".to_string(),
                "-ExecutionPolicy".to_string(),
                "Bypass".to_string(),
                "-EncodedCommand".to_string(),
                encode_powershell(script),
            ],
            timeout_seconds,
        }
    }

    pub fn command(module: &'static str, id: &'static str, program: &str, args: &[&str], timeout_seconds: u64) -> Self {
        Self {
            id,
            module,
            program: program.to_string(),
            args: args.iter().map(|x| x.to_string()).collect(),
            timeout_seconds,
        }
    }
}

pub async fn run_command(spec: CommandSpec) -> CommandResult {
    let mut command = Command::new(&spec.program);
    command.args(&spec.args);
    command.stdin(Stdio::null());
    command.stdout(Stdio::piped());
    command.stderr(Stdio::piped());
    command.kill_on_drop(true);

    #[cfg(windows)]
    {
        #[allow(unused_imports)]
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x08000000 | 0x00000200); // CREATE_NO_WINDOW | CREATE_NEW_PROCESS_GROUP
    }

    let child = match command.spawn() {
        Ok(child) => child,
        Err(error) => {
            return CommandResult {
                id: spec.id.to_string(),
                status: "spawn_failed".to_string(),
                exit_code: None,
                stdout: String::new(),
                stderr: redact(&error.to_string()),
                timed_out: false,
            };
        }
    };

    let duration = Duration::from_secs(spec.timeout_seconds.max(10));
    match timeout(duration, child.wait_with_output()).await {
        Ok(Ok(output)) => {
            let exit = output.status.code();
            let stdout = redact(&String::from_utf8_lossy(&output.stdout));
            let stderr = redact(&String::from_utf8_lossy(&output.stderr));
            CommandResult {
                id: spec.id.to_string(),
                status: if output.status.success() { "ok" } else { "warning" }.to_string(),
                exit_code: exit,
                stdout,
                stderr,
                timed_out: false,
            }
        }
        Ok(Err(error)) => CommandResult {
            id: spec.id.to_string(),
            status: "wait_failed".to_string(),
            exit_code: None,
            stdout: String::new(),
            stderr: redact(&error.to_string()),
            timed_out: false,
        },
        Err(_) => CommandResult {
            id: spec.id.to_string(),
            status: "timeout".to_string(),
            exit_code: None,
            stdout: String::new(),
            stderr: format!("timeout after {} seconds", spec.timeout_seconds),
            timed_out: true,
        },
    }
}

pub async fn run_specs(specs: Vec<CommandSpec>, sink: super::event_sink::EventSink) -> Vec<String>
{
    let total = specs.len().max(1);
    let mut signals = Vec::new();

    for (index, spec) in specs.into_iter().enumerate() {
        let stage_label = stage_labels::label_for(spec.id);
        sink.emit(EngineEvent {
            r#type: "stage".into(),
            module: spec.module.into(),
            message: format!("{stage_label} 중…"),
            progress: ((index * 100) / total) as u8,
            severity: "info".into(),
        });

        let module = spec.module;
        let id = spec.id;
        let result = run_command(spec).await;
        let progress = (((index + 1) * 100) / total) as u8;

        let severity = if result.status == "ok" { "info" } else { "warning" };
        let stage_label = stage_labels::label_for(id);
        let status_label = stage_labels::status_label(&result.status);
        sink.emit(EngineEvent {
            r#type: "stage".into(),
            module: module.into(),
            message: format!("{stage_label} 완료 ({status_label})"),
            progress,
            severity: severity.into(),
        });

        let summary = format!(
            "{} status={} exit={:?} stdout={} stderr={}",
            result.id,
            result.status,
            result.exit_code,
            compact(&result.stdout, 700),
            compact(&result.stderr, 500)
        );
        signals.push(summary);
    }

    signals
}

pub fn encode_powershell(script: &str) -> String {
    let mut bytes = Vec::with_capacity(script.len() * 2);
    for unit in script.encode_utf16() {
        bytes.extend_from_slice(&unit.to_le_bytes());
    }
    general_purpose::STANDARD.encode(bytes)
}

fn compact(input: &str, max: usize) -> String {
    let normalized = input.split_whitespace().collect::<Vec<_>>().join(" ");
    if normalized.len() <= max {
        normalized
    } else {
        format!("{}…", &normalized[..max.min(normalized.len())])
    }
}
