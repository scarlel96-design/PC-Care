use serde::{Deserialize, Serialize};
use std::io::{BufRead, BufReader, Write};
use std::path::PathBuf;
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use tokio::process::Command;
use tokio::time::timeout;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct RepairRequest {
    id: String,
    action: String,
    target: String,
    dry_run: bool,
    risk_accepted: bool,
    nonce: String,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct RepairResponse {
    id: String,
    status: String,
    message: String,
    exit_code: Option<i32>,
    stdout: String,
    stderr: String,
    elevated: bool,
    nonce: String,
    log_path: String,
}

#[derive(Debug, Clone)]
struct RepairCommand {
    program: &'static str,
    args: Vec<String>,
    risk: &'static str,
    timeout_seconds: u64,
}

#[tokio::main]
async fn main() {
    let pipe_name = parse_pipe_name();

    if let Some(pipe) = pipe_name {
        if let Err(error) = run_pipe_client(&pipe).await {
            eprintln!("pipe error: {error}");
        }
        return;
    }

    let response = RepairResponse {
        id: "no-pipe".into(),
        status: "ready".into(),
        message: "복구 도우미 v44는 --pipe <이름> 연결 모드가 필요합니다.".into(),
        exit_code: Some(0),
        stdout: String::new(),
        stderr: String::new(),
        elevated: is_elevated(),
        nonce: String::new(),
        log_path: String::new(),
    };

    println!("{}", serde_json::to_string(&response).unwrap());
}

async fn run_pipe_client(pipe_name: &str) -> std::io::Result<()> {
    let pipe_path = format!(r"\\.\pipe\{}", pipe_name);

    let mut last_error = None;
    for _ in 0..60 {
        match std::fs::OpenOptions::new().read(true).write(true).open(&pipe_path) {
            Ok(mut pipe) => {
                let mut reader = BufReader::new(pipe.try_clone()?);
                let mut line = String::new();
                reader.read_line(&mut line)?;

                let response = match serde_json::from_str::<RepairRequest>(&line) {
                    Ok(request) => handle(request).await,
                    Err(error) => RepairResponse {
                        id: "parse-error".into(),
                        status: "failed".into(),
                        message: format!("request parse failed: {error}"),
                        exit_code: None,
                        stdout: String::new(),
                        stderr: redact(&error.to_string()),
                        elevated: is_elevated(),
                        nonce: String::new(),
                        log_path: String::new(),
                    },
                };

                let json = serde_json::to_string(&response).unwrap_or_else(|_| "{}".to_string());
                writeln!(pipe, "{json}")?;
                pipe.flush()?;
                return Ok(());
            }
            Err(error) => {
                last_error = Some(error);
                std::thread::sleep(Duration::from_millis(250));
            }
        }
    }

    Err(last_error.unwrap_or_else(|| std::io::Error::new(std::io::ErrorKind::TimedOut, "pipe connect timeout")))
}

async fn handle(req: RepairRequest) -> RepairResponse {
    let log_path = create_log_path(&req.action);
    let mut log_lines = Vec::new();
    log_lines.push(format!("id={}", req.id));
    log_lines.push(format!("action={}", req.action));
    log_lines.push(format!("target={}", req.target));
    log_lines.push(format!("dryRun={}", req.dry_run));
    log_lines.push(format!("riskAccepted={}", req.risk_accepted));
    log_lines.push(format!("elevated={}", is_elevated()));

    let command = match build_command(&req.action, &req.target) {
        Some(command) => command,
        None => {
            let response = RepairResponse {
                id: req.id,
                status: "blocked".into(),
                message: format!("허용되지 않은 복구 작업입니다: {}", req.action),
                exit_code: None,
                stdout: String::new(),
                stderr: String::new(),
                elevated: is_elevated(),
                nonce: req.nonce,
                log_path: log_path.to_string_lossy().to_string(),
            };
            write_log(&log_path, &log_lines, &response);
            return response;
        }
    };

    log_lines.push(format!("program={}", command.program));
    log_lines.push(format!("args={:?}", command.args));
    log_lines.push(format!("risk={}", command.risk));
    log_lines.push(format!("timeoutSeconds={}", command.timeout_seconds));

    if !req.dry_run && !req.risk_accepted {
        let response = RepairResponse {
            id: req.id,
            status: "blocked".into(),
            message: "실제 복구 실행은 riskAccepted=true가 필요합니다.".into(),
            exit_code: None,
            stdout: String::new(),
            stderr: String::new(),
            elevated: is_elevated(),
            nonce: req.nonce,
            log_path: log_path.to_string_lossy().to_string(),
        };
        write_log(&log_path, &log_lines, &response);
        return response;
    }

    if req.dry_run {
        let response = RepairResponse {
            id: req.id,
            status: "dry-run".into(),
            message: format!("허용된 작업입니다: {} {:?} / risk={} / timeout={}s", command.program, command.args, command.risk, command.timeout_seconds),
            exit_code: Some(0),
            stdout: String::new(),
            stderr: String::new(),
            elevated: is_elevated(),
            nonce: req.nonce,
            log_path: log_path.to_string_lossy().to_string(),
        };
        write_log(&log_path, &log_lines, &response);
        return response;
    }

    let response = run_repair_command(req.id, req.nonce, command, log_path.clone()).await;
    write_log(&log_path, &log_lines, &response);
    response
}

async fn run_repair_command(id: String, nonce: String, command: RepairCommand, log_path: PathBuf) -> RepairResponse {
    let mut child_command = Command::new(command.program);
    child_command.args(command.args.clone());
    child_command.kill_on_drop(true);

    #[cfg(windows)]
    {
        child_command.creation_flags(0x08000000 | 0x00000200); // CREATE_NO_WINDOW | CREATE_NEW_PROCESS_GROUP
    }

    let child = match child_command.spawn() {
        Ok(child) => child,
        Err(error) => {
            return RepairResponse {
                id,
                status: "spawn-failed".into(),
                message: format!("작업 실행 실패: {error}"),
                exit_code: None,
                stdout: String::new(),
                stderr: redact(&error.to_string()),
                elevated: is_elevated(),
                nonce,
                log_path: log_path.to_string_lossy().to_string(),
            };
        }
    };

    match timeout(Duration::from_secs(command.timeout_seconds), child.wait_with_output()).await {
        Ok(Ok(output)) => RepairResponse {
            id,
            status: if output.status.success() { "ok".into() } else { "warning".into() },
            message: format!("작업 실행 완료: {}", command.program),
            exit_code: output.status.code(),
            stdout: compact(&redact(&String::from_utf8_lossy(&output.stdout)), 3000),
            stderr: compact(&redact(&String::from_utf8_lossy(&output.stderr)), 3000),
            elevated: is_elevated(),
            nonce,
            log_path: log_path.to_string_lossy().to_string(),
        },
        Ok(Err(error)) => RepairResponse {
            id,
            status: "wait-failed".into(),
            message: format!("작업 대기 실패: {error}"),
            exit_code: None,
            stdout: String::new(),
            stderr: redact(&error.to_string()),
            elevated: is_elevated(),
            nonce,
            log_path: log_path.to_string_lossy().to_string(),
        },
        Err(_) => RepairResponse {
            id,
            status: "timeout".into(),
            message: format!("작업 제한 시간 초과: {}초", command.timeout_seconds),
            exit_code: None,
            stdout: String::new(),
            stderr: "process timeout; child dropped by supervisor".into(),
            elevated: is_elevated(),
            nonce,
            log_path: log_path.to_string_lossy().to_string(),
        },
    }
}

fn build_command(action: &str, target: &str) -> Option<RepairCommand> {
    if !is_safe_target(action, target) {
        return None;
    }

    match action {
        "driver_repair_plan_only" => Some(RepairCommand {
            program: "powershell.exe",
            args: vec!["-NoProfile".into(), "-ExecutionPolicy".into(), "Bypass".into(), "-Command".into(), "Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object {$_.Status -ne 'OK'} | Select-Object Class,FriendlyName,InstanceId,Status,Problem | ConvertTo-Json -Compress -Depth 4".into()],
            risk: "low",
            timeout_seconds: 120,
        }),
        "driver_check_problem_devices" => Some(RepairCommand {
            program: "powershell.exe",
            args: vec!["-NoProfile".into(), "-ExecutionPolicy".into(), "Bypass".into(), "-Command".into(), "Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object {$_.Status -ne 'OK'} | Select-Object Class,FriendlyName,InstanceId,Status,Problem | ConvertTo-Json -Compress -Depth 4".into()],
            risk: "low",
            timeout_seconds: 120,
        }),
        "audio_repair_plan_only" => Some(RepairCommand {
            program: "powershell.exe",
            args: vec!["-NoProfile".into(), "-ExecutionPolicy".into(), "Bypass".into(), "-Command".into(), "$svc=Get-Service -Name Audiosrv,AudioEndpointBuilder -ErrorAction SilentlyContinue; $dev=Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.Class -in @('AudioEndpoint','MEDIA','Sound') -or $_.FriendlyName -match 'audio|sound|realtek|nvidia|bluetooth' }; [PSCustomObject]@{Services=$svc;AudioDevices=$dev;RecommendedOrder=@('restart endpoint builder','restart audiosrv','scan devices','post verify')} | ConvertTo-Json -Compress -Depth 5".into()],
            risk: "low",
            timeout_seconds: 120,
        }),
        "audio_scan_devices" => Some(RepairCommand {
            program: "pnputil.exe",
            args: vec!["/scan-devices".into()],
            risk: "low",
            timeout_seconds: 180,
        }),
        "audio_restart_stack" => Some(RepairCommand {
            program: "powershell.exe",
            args: vec!["-NoProfile".into(), "-ExecutionPolicy".into(), "Bypass".into(), "-Command".into(), "Restart-Service -Name AudioEndpointBuilder -Force; Restart-Service -Name Audiosrv -Force".into()],
            risk: "medium",
            timeout_seconds: 240,
        }),
        "pnputil_scan_devices" => Some(RepairCommand {
            program: "pnputil.exe",
            args: vec!["/scan-devices".into()],
            risk: "low",
            timeout_seconds: 180,
        }),
        "pnputil_restart_device" => Some(RepairCommand {
            program: "pnputil.exe",
            args: vec!["/restart-device".into(), target.into()],
            risk: "medium",
            timeout_seconds: 240,
        }),
        "restart_audiosrv" => Some(RepairCommand {
            program: "powershell.exe",
            args: vec!["-NoProfile".into(), "-ExecutionPolicy".into(), "Bypass".into(), "-Command".into(), "Restart-Service -Name Audiosrv -Force".into()],
            risk: "medium",
            timeout_seconds: 180,
        }),
        "restart_audioendpointbuilder" => Some(RepairCommand {
            program: "powershell.exe",
            args: vec!["-NoProfile".into(), "-ExecutionPolicy".into(), "Bypass".into(), "-Command".into(), "Restart-Service -Name AudioEndpointBuilder -Force".into()],
            risk: "medium",
            timeout_seconds: 180,
        }),
        
        "dism_scanhealth" => Some(RepairCommand {
            program: "dism.exe",
            args: vec!["/Online".into(), "/Cleanup-Image".into(), "/ScanHealth".into()],
            risk: "medium",
            timeout_seconds: 3600,
        }),
        "dism_restorehealth_guarded" => Some(RepairCommand {
            program: "dism.exe",
            args: vec!["/Online".into(), "/Cleanup-Image".into(), "/RestoreHealth".into()],
            risk: "high",
            timeout_seconds: 7200,
        }),
        "sfc_scannow" => Some(RepairCommand {
            program: "sfc.exe",
            args: vec!["/scannow".into()],
            risk: "high",
            timeout_seconds: 7200,
        }),
        "dism_checkhealth" => Some(RepairCommand {
            program: "DISM.exe",
            args: vec!["/Online".into(), "/Cleanup-Image".into(), "/CheckHealth".into()],
            risk: "low",
            timeout_seconds: 900,
        }),
        "sfc_verifyonly" => Some(RepairCommand {
            program: "sfc.exe",
            args: vec!["/verifyonly".into()],
            risk: "low",
            timeout_seconds: 1800,
        }),
        _ => None,
    }
}

fn is_safe_target(action: &str, target: &str) -> bool {
    match action {
        "pnputil_restart_device" => {
            let t = target.trim();
            !t.is_empty()
                && t != "online-image"
                && t.len() <= 512
                && !t.contains('\n')
                && !t.contains('\r')
                && !t.contains('&')
                && !t.contains('|')
                && !t.contains(';')
        }
        "pnputil_scan_devices" | "driver_repair_plan_only" | "driver_check_problem_devices" | "audio_repair_plan_only" | "audio_scan_devices" | "audio_restart_stack" | "restart_audiosrv" | "restart_audioendpointbuilder" | "dism_checkhealth" | "dism_scanhealth" | "dism_restorehealth_guarded" | "sfc_verifyonly" | "sfc_scannow" => true,
        _ => false,
    }
}

fn parse_pipe_name() -> Option<String> {
    let mut args = std::env::args().skip(1);
    while let Some(arg) = args.next() {
        if arg == "--pipe" {
            return args.next();
        }
    }
    None
}

fn is_elevated() -> bool {
    #[cfg(windows)]
    unsafe {
        use windows::Win32::Foundation::{CloseHandle, HANDLE};
        use windows::Win32::Security::{GetTokenInformation, TokenElevation, TOKEN_ELEVATION, TOKEN_QUERY};
        use windows::Win32::System::Threading::{GetCurrentProcess, OpenProcessToken};

        let mut token = HANDLE::default();
        if OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &mut token).is_err() {
            return windows::Win32::UI::Shell::IsUserAnAdmin().as_bool();
        }

        let mut elevation = TOKEN_ELEVATION::default();
        let mut returned = 0u32;
        let ok = GetTokenInformation(
            token,
            TokenElevation,
            Some(&mut elevation as *mut _ as *mut core::ffi::c_void),
            core::mem::size_of::<TOKEN_ELEVATION>() as u32,
            &mut returned,
        ).is_ok();

        let _ = CloseHandle(token);

        if ok {
            elevation.TokenIsElevated != 0
        } else {
            windows::Win32::UI::Shell::IsUserAnAdmin().as_bool()
        }
    }

    #[cfg(not(windows))]
    {
        false
    }
}

fn create_log_path(action: &str) -> PathBuf {
    let root = if let Some(profile) = std::env::var_os("USERPROFILE") {
        PathBuf::from(profile).join("Desktop").join("SmartPerformanceDoctor").join("RepairLogs")
    } else {
        std::env::temp_dir().join("SmartPerformanceDoctor").join("RepairLogs")
    };

    let _ = std::fs::create_dir_all(&root);
    root.join(format!("repair_{}_{}.log", sanitize(action), timestamp()))
}

fn write_log(path: &PathBuf, lines: &[String], response: &RepairResponse) {
    let mut out = String::new();
    out.push_str("Smart Performance Doctor RepairHelper v44\n");
    for line in lines {
        out.push_str(line);
        out.push('\n');
    }
    out.push_str("\nResponse:\n");
    out.push_str(&serde_json::to_string_pretty(response).unwrap_or_else(|_| "{}".into()));
    out.push('\n');
    let _ = std::fs::write(path, out);
}

fn redact(input: &str) -> String {
    let mut out = input.to_string();
    for key in ["password", "passwd", "token", "secret", "api_key", "apikey", "authorization", "bearer"] {
        out = out.replace(key, "[redacted-key]");
        out = out.replace(&key.to_uppercase(), "[redacted-key]");
    }
    out
}

fn compact(input: &str, max: usize) -> String {
    let cleaned = input.split_whitespace().collect::<Vec<_>>().join(" ");
    if cleaned.len() <= max {
        cleaned
    } else {
        format!("{}…", &cleaned[..max.min(cleaned.len())])
    }
}

fn sanitize(input: &str) -> String {
    input.chars().map(|c| if c.is_ascii_alphanumeric() || c == '-' || c == '_' { c } else { '_' }).collect()
}

fn timestamp() -> u64 {
    SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_secs()).unwrap_or(0)
}
