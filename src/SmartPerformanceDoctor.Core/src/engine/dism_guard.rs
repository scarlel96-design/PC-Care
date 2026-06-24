use std::path::{Path, PathBuf};
use std::time::{Duration, SystemTime};

use tokio::process::Command;
use tokio::time::{interval, timeout};

use super::event_sink::EventSink;

#[derive(Debug, Clone)]
pub struct DismGuardPolicy {
    pub plateau_hint_percent: u8,
    pub hard_timeout_seconds: u64,
    pub stall_without_log_seconds: u64,
    pub heartbeat_interval_seconds: u64,
}

impl Default for DismGuardPolicy {
    fn default() -> Self {
        Self {
            plateau_hint_percent: 63,
            hard_timeout_seconds: 7200,
            stall_without_log_seconds: 1800,
            heartbeat_interval_seconds: 20,
        }
    }
}

pub async fn run_restore_health_supervised(sink: EventSink) -> Vec<String> {
    let policy = DismGuardPolicy::default();
    let dism_log = default_dism_log_path();

    sink.stage(
        "repair",
        "DISM RestoreHealth 감독 모드 시작: 63% 정체와 DISM 로그 heartbeat를 분리 감시합니다.",
        1,
    );

    sink.stage(
        "repair",
        format!("DISM 로그 감시 대상: {}", dism_log.display()),
        2,
    );

    let before_modified = file_modified(&dism_log);

    let mut command = Command::new("DISM.exe");
    command.args(["/Online", "/Cleanup-Image", "/RestoreHealth"]);
    command.kill_on_drop(true);

    #[cfg(windows)]
    {
        #[allow(unused_imports)]
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x08000000 | 0x00000200); // CREATE_NO_WINDOW | CREATE_NEW_PROCESS_GROUP
    }

    let mut child = match command.spawn() {
        Ok(child) => child,
        Err(error) => {
            sink.warning("repair", format!("DISM RestoreHealth 시작 실패: {error}"), 5);
            return vec![format!("dism_restore_health spawn_failed: {error}")];
        }
    };

    let mut tick = interval(Duration::from_secs(policy.heartbeat_interval_seconds));
    let started = SystemTime::now();
    let mut last_log_change = before_modified.unwrap_or(started);
    let mut last_reported_log_change = last_log_change;

    let result = timeout(Duration::from_secs(policy.hard_timeout_seconds), async {
        loop {
            tokio::select! {
                _ = tick.tick() => {
                    let elapsed = started.elapsed().map(|d| d.as_secs()).unwrap_or(0);
                    let modified = file_modified(&dism_log);
                    if let Some(m) = modified
                        && m > last_log_change
                    {
                        last_log_change = m;
                    }

                    let log_alive = last_log_change > last_reported_log_change;
                    if log_alive {
                        last_reported_log_change = last_log_change;
                        sink.stage("repair", format!("DISM 진행 중: 진행률이 멈춰 보여도 dism.log가 갱신되고 있습니다. 경과 {}초", elapsed), 63);
                    } else {
                        let silent = last_log_change.elapsed().map(|d| d.as_secs()).unwrap_or(0);
                        if silent >= policy.stall_without_log_seconds {
                            sink.warning("repair", format!("DISM 정체 후보: 진행률/로그 갱신 없이 {}초 경과", silent), 64);
                        } else {
                            sink.stage("repair", format!("DISM 감시 중: 63% 부근 장시간 작업 가능성. 로그 무변화 {}초", silent), 63);
                        }
                    }
                }
                status = child.wait() => {
                    return status;
                }
            }
        }
    }).await;

    match result {
        Ok(Ok(status)) if status.success() => {
            sink.stage("repair", "DISM RestoreHealth 완료", 80);
            vec!["dism_restore_health completed with heartbeat guard".into()]
        }
        Ok(Ok(status)) => {
            sink.warning("repair", format!("DISM RestoreHealth 경고 종료: {:?}", status.code()), 75);
            vec![format!("dism_restore_health warning exit={:?}", status.code())]
        }
        Ok(Err(error)) => {
            sink.warning("repair", format!("DISM RestoreHealth wait 실패: {error}"), 75);
            vec![format!("dism_restore_health wait_failed: {error}")]
        }
        Err(_) => {
            let _ = child.kill().await;
            sink.warning("repair", "DISM RestoreHealth hard timeout: 무한 대기 방지를 위해 종료했습니다.", 70);
            vec!["dism_restore_health hard_timeout: process killed by DISM Guard".into()]
        }
    }
}

pub fn dism_stall_explanation() -> String {
    "DISM은 62~63% 부근에서 오래 멈춰 보일 수 있습니다. v20은 dism.log 수정 시간 heartbeat를 감시해 실제 작업 중인지 정체 후보인지 분리합니다.".into()
}

fn default_dism_log_path() -> PathBuf {
    if cfg!(windows) {
        PathBuf::from(r"C:\Windows\Logs\DISM\dism.log")
    } else {
        PathBuf::from("/tmp/dism.log")
    }
}

fn file_modified(path: &Path) -> Option<SystemTime> {
    std::fs::metadata(path).ok()?.modified().ok()
}
