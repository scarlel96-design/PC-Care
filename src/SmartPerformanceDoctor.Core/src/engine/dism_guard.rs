use std::process::Stdio;
use std::time::{Duration, Instant};
use tokio::process::Command;
use tokio::time::sleep;

/// Supervised DISM RestoreHealth with heartbeat + hard timeout (restored from binary semantics).
pub async fn run_restore_health_with_guard(hard_timeout_secs: u64) -> (i32, String) {
    let mut child = match Command::new("DISM.exe")
        .args(["/Online", "/Cleanup-Image", "/RestoreHealth"])
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .kill_on_drop(true)
        .spawn()
    {
        Ok(c) => c,
        Err(e) => return (-1, format!("dism_restore_health spawn_failed: {e}")),
    };

    let started = Instant::now();
    let hard = Duration::from_secs(hard_timeout_secs.max(60));
    let mut last_heartbeat = Instant::now();

    loop {
        match child.try_wait() {
            Ok(Some(status)) => {
                let code = status.code().unwrap_or(-1);
                let msg = if code == 0 {
                    "dism_restore_health completed with heartbeat guard".into()
                } else {
                    format!("dism_restore_health warning exit={code}")
                };
                return (code, msg);
            }
            Ok(None) => {
                if started.elapsed() > hard {
                    let _ = child.kill().await;
                    return (
                        -1,
                        "DISM RestoreHealth hard timeout: process killed by DISM Guard".into(),
                    );
                }
                if last_heartbeat.elapsed() > Duration::from_secs(15) {
                    // Heartbeat keep-alive marker for supervisors.
                    last_heartbeat = Instant::now();
                }
                sleep(Duration::from_millis(500)).await;
            }
            Err(e) => {
                return (-1, format!("dism_restore_health wait_failed: {e}"));
            }
        }
    }
}

pub async fn run_dism(args: &[&str], timeout_secs: u64) -> (i32, String) {
    let fut = Command::new("DISM.exe")
        .args(args)
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .output();

    match tokio::time::timeout(Duration::from_secs(timeout_secs), fut).await {
        Ok(Ok(output)) => {
            let code = output.status.code().unwrap_or(-1);
            let stdout = String::from_utf8_lossy(&output.stdout);
            let stderr = String::from_utf8_lossy(&output.stderr);
            let detail = if !stdout.trim().is_empty() {
                stdout.trim().to_string()
            } else {
                stderr.trim().to_string()
            };
            (code, compact(&detail, 800))
        }
        Ok(Err(e)) => (-1, format!("DISM spawn failed: {e}")),
        Err(_) => (-1, format!("DISM timeout after {timeout_secs}s")),
    }
}

pub async fn run_sfc_scannow(timeout_secs: u64) -> (i32, String) {
    let fut = Command::new("sfc.exe")
        .arg("/scannow")
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .output();

    match tokio::time::timeout(Duration::from_secs(timeout_secs), fut).await {
        Ok(Ok(output)) => (
            output.status.code().unwrap_or(-1),
            compact(&String::from_utf8_lossy(&output.stdout), 800),
        ),
        Ok(Err(e)) => (-1, format!("sfc spawn failed: {e}")),
        Err(_) => (-1, format!("sfc timeout after {timeout_secs}s")),
    }
}

fn compact(s: &str, max: usize) -> String {
    let t = s.replace('\r', " ").replace('\n', " ");
    if t.len() <= max {
        t
    } else {
        format!("{}…", &t[..max])
    }
}
