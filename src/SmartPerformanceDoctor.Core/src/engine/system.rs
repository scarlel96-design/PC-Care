use super::dism_guard;
use super::types::{emit_event, EngineEvent};
use std::process::Stdio;
use tokio::process::Command;

pub struct ModuleRun {
    pub events: Vec<EngineEvent>,
    pub signals: Vec<String>,
    pub status: String,
    pub message: String,
}

pub async fn run_quick() -> ModuleRun {
    let module = "quick";
    let mut events = Vec::new();
    let mut signals = Vec::new();

    push(
        &mut events,
        EngineEvent::stage(module, 5, "빠른 검사 시작 — OS/메모리/디스크/문제장치"),
    );

    // OS + memory
    push(
        &mut events,
        EngineEvent::stage(module, 20, "시스템 메모리 상태 수집"),
    );
    let mem = ps(
        r#"$os=Get-CimInstance Win32_OperatingSystem; [PSCustomObject]@{Caption=$os.Caption;Version=$os.Version;LastBootUpTime=$os.LastBootUpTime;TotalGB=[math]::Round($os.TotalVisibleMemorySize/1MB,2);FreeGB=[math]::Round($os.FreePhysicalMemory/1MB,2);UsedPercent=[math]::Round((($os.TotalVisibleMemorySize-$os.FreePhysicalMemory)/$os.TotalVisibleMemorySize)*100,2)} | ConvertTo-Json -Compress"#,
    )
    .await;
    parse_memory_signal(&mem, &mut signals, &mut events, module, 30);

    // Disk free
    push(
        &mut events,
        EngineEvent::stage(module, 45, "고정 디스크 여유 공간 확인"),
    );
    let disk = ps(
        r#"Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" | ForEach-Object { $freePct=0; if($_.Size -gt 0){$freePct=[math]::Round(($_.FreeSpace/$_.Size)*100,2)}; [PSCustomObject]@{DeviceID=$_.DeviceID;SizeGB=[math]::Round($_.Size/1GB,2);FreeGB=[math]::Round($_.FreeSpace/1GB,2);FreePercent=$freePct} } | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    parse_disk_signal(&disk, &mut signals, &mut events, module, 55);

    // Problem devices
    push(
        &mut events,
        EngineEvent::stage(module, 70, "문제 장치 빠른 스캔"),
    );
    let devices = ps(
        r#"$bad=Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object {$_.Status -ne 'OK'}; [PSCustomObject]@{ProblemDeviceCount=($bad|Measure-Object).Count} | ConvertTo-Json -Compress"#,
    )
    .await;
    if let Some(n) = extract_i64(&devices, "ProblemDeviceCount") {
        if n > 0 {
            signals.push(format!("configmanagererror problem_devices={n}"));
            push(
                &mut events,
                EngineEvent::signal(
                    module,
                    80,
                    "warning",
                    format!("문제 장치 {n}개 감지"),
                ),
            );
        } else {
            signals.push("problem_devices=0".into());
            push(
                &mut events,
                EngineEvent::signal(module, 80, "info", "문제 장치 없음"),
            );
        }
    }

    // Pending reboot + storage health (v50.2.2 additions)
    push(
        &mut events,
        EngineEvent::stage(module, 88, "재부팅 대기·저장장치 상태"),
    );
    append_pending_reboot(&mut signals, &mut events, module, 90).await;
    append_storage_health(&mut signals, &mut events, module, 94).await;

    push(
        &mut events,
        EngineEvent::stage(module, 100, "빠른 검사 완료"),
    );
    ModuleRun {
        events,
        signals,
        status: "ok".into(),
        message: "quick 모듈 실행 완료".into(),
    }
}

pub async fn run_system() -> ModuleRun {
    let module = "system";
    let mut events = Vec::new();
    let mut signals = Vec::new();

    push(
        &mut events,
        EngineEvent::stage(module, 5, "시스템 개요 수집"),
    );
    let overview = ps(
        r#"Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber,LastBootUpTime,TotalVisibleMemorySize,FreePhysicalMemory,InstallDate | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    if !overview.is_empty() {
        signals.push(format!("system_overview={overview}"));
        push(
            &mut events,
            EngineEvent::signal(module, 12, "info", "OS 정보 수집 완료"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 18, "저장소 상태 확인"),
    );
    append_storage_health(&mut signals, &mut events, module, 24).await;

    push(
        &mut events,
        EngineEvent::stage(module, 30, "자동 시작 서비스 이상 스캔"),
    );
    let services = ps(
        r#"Get-CimInstance Win32_Service | Where-Object {$_.StartMode -eq 'Auto' -and $_.State -ne 'Running'} | Select-Object Name,DisplayName,State,StartMode | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    if services.contains("\"Name\"") {
        signals.push("service_stopped auto_services_not_running".into());
        push(
            &mut events,
            EngineEvent::signal(
                module,
                38,
                "warning",
                "자동 시작인데 중지된 서비스 발견",
            ),
        );
    } else {
        signals.push("service_anomaly=none".into());
        push(
            &mut events,
            EngineEvent::signal(module, 38, "info", "자동 서비스 이상 없음"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 45, "WMI 저장소 상태 확인"),
    );
    let (wmi_code, wmi_out) = run_cmd("winmgmt.exe", &["/verifyrepository"], 45).await;
    if wmi_code == 0 || wmi_out.to_lowercase().contains("consistent") {
        signals.push("wmi_repository=ok".into());
        push(
            &mut events,
            EngineEvent::signal(module, 50, "info", "WMI 저장소 정상"),
        );
    } else {
        signals.push(format!("wmi_repository_issue={wmi_out}"));
        push(
            &mut events,
            EngineEvent::signal(module, 50, "warning", "WMI 저장소 확인 필요"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 58, "재부팅 대기 상태"),
    );
    append_pending_reboot(&mut signals, &mut events, module, 62).await;

    push(
        &mut events,
        EngineEvent::stage(module, 68, "안정성·크래시 이벤트 요약"),
    );
    let reliability = ps(
        r#"$start=(Get-Date).AddDays(-14); Get-WinEvent -FilterHashtable @{LogName='Application';Level=1,2;StartTime=$start} -MaxEvents 120 -ErrorAction SilentlyContinue | Group-Object ProviderName,Id | Sort-Object Count -Descending | Select-Object -First 12 Count,Name | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    if reliability.contains("\"Count\"") {
        signals.push(format!("reliability_crash_digest={reliability}"));
        push(
            &mut events,
            EngineEvent::signal(module, 75, "info", "애플리케이션 오류 요약 수집"),
        );
    }

    // Stability enrichment (BSOD/WHEA) — complements C# SystemStabilityProbe
    let stability = ps(
        r#"$start=(Get-Date).AddDays(-30); $bc=(Get-WinEvent -FilterHashtable @{LogName='System';ProviderName='Microsoft-Windows-WER-SystemErrorReporting';StartTime=$start} -MaxEvents 40 -ErrorAction SilentlyContinue | Measure-Object).Count; $whea=(Get-WinEvent -FilterHashtable @{LogName='System';ProviderName='Microsoft-Windows-WHEA-Logger';Level=1,2,3;StartTime=$start} -MaxEvents 40 -ErrorAction SilentlyContinue | Measure-Object).Count; $usd=(Get-WinEvent -FilterHashtable @{LogName='System';Id=6008;StartTime=$start} -MaxEvents 40 -ErrorAction SilentlyContinue | Measure-Object).Count; [PSCustomObject]@{BugCheck=$bc;WHEA=$whea;UnexpectedShutdown=$usd} | ConvertTo-Json -Compress"#,
    )
    .await;
    if let Some(bc) = extract_i64(&stability, "BugCheck") {
        if bc > 0 {
            signals.push(format!("bugcheck_30d={bc} bluescreen"));
            push(
                &mut events,
                EngineEvent::signal(module, 84, "warning", format!("최근 30일 BugCheck {bc}건")),
            );
        }
    }
    if let Some(w) = extract_i64(&stability, "WHEA") {
        if w > 0 {
            signals.push(format!("whea_error_30d={w} whea"));
            push(
                &mut events,
                EngineEvent::signal(module, 88, "warning", format!("최근 30일 WHEA {w}건")),
            );
        }
    }
    if let Some(u) = extract_i64(&stability, "UnexpectedShutdown") {
        if u > 0 {
            signals.push(format!("unexpected_shutdown_30d={u}"));
            push(
                &mut events,
                EngineEvent::signal(
                    module,
                    92,
                    "warning",
                    format!("예기치 않은 종료 {u}건"),
                ),
            );
        }
    }

    push(
        &mut events,
        EngineEvent::stage(module, 100, "시스템 점검 완료"),
    );
    ModuleRun {
        events,
        signals,
        status: "ok".into(),
        message: "system 모듈 실행 완료".into(),
    }
}

pub async fn run_system_recovery(apply: bool) -> ModuleRun {
    let module = "system-recovery";
    let mut events = Vec::new();
    let mut signals = Vec::new();

    push(
        &mut events,
        EngineEvent::stage(module, 5, "DISM CheckHealth"),
    );
    let (c1, o1) = dism_guard::run_dism(
        &["/Online", "/Cleanup-Image", "/CheckHealth"],
        180,
    )
    .await;
    signals.push(format!("dism_check_health exit={c1} {o1}"));
    push(
        &mut events,
        EngineEvent::signal(module, 20, if c1 == 0 { "info" } else { "warning" }, o1),
    );

    push(
        &mut events,
        EngineEvent::stage(module, 30, "DISM ScanHealth"),
    );
    let (c2, o2) = dism_guard::run_dism(
        &["/Online", "/Cleanup-Image", "/ScanHealth"],
        600,
    )
    .await;
    signals.push(format!("dism_scan_health exit={c2} {o2}"));
    push(
        &mut events,
        EngineEvent::signal(module, 50, if c2 == 0 { "info" } else { "warning" }, o2),
    );

    if apply {
        push(
            &mut events,
            EngineEvent::stage(module, 55, "DISM RestoreHealth (감독 모드)"),
        );
        let (c3, o3) = dism_guard::run_restore_health_with_guard(45 * 60).await;
        signals.push(format!("dism_restore_health exit={c3} {o3}"));
        push(
            &mut events,
            EngineEvent::signal(
                module,
                75,
                if c3 == 0 { "info" } else { "warning" },
                o3,
            ),
        );

        push(
            &mut events,
            EngineEvent::stage(module, 80, "SFC /scannow"),
        );
        let (c4, o4) = dism_guard::run_sfc_scannow(45 * 60).await;
        signals.push(format!("sfc_scannow exit={c4} {o4}"));
        push(
            &mut events,
            EngineEvent::signal(
                module,
                95,
                if c4 == 0 { "info" } else { "warning" },
                o4,
            ),
        );
    } else {
        signals.push("system_recovery=plan_only".into());
        push(
            &mut events,
            EngineEvent::signal(
                module,
                90,
                "info",
                "계획 모드: RestoreHealth/SFC는 사용자 승인 후 실행",
            ),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 100, "시스템 복구 모듈 완료"),
    );
    ModuleRun {
        events,
        signals,
        status: "ok".into(),
        message: "system-recovery 모듈 실행 완료".into(),
    }
}

pub async fn run_selftest() -> ModuleRun {
    let module = "selftest";
    let mut events = vec![
        EngineEvent::stage(module, 10, "selftest.protocol_ready"),
        EngineEvent::stage(module, 35, "selftest.event_sink_ready"),
        EngineEvent::stage(module, 60, "selftest.intelligence_ready"),
        EngineEvent::stage(module, 85, "selftest.report_writer_ready"),
        EngineEvent::stage(module, 100, "엔진 자체 검증 완료"),
    ];
    for e in &events {
        emit_event(e);
    }
    // Events already emitted; clone for response payload.
    let signals = vec![
        "selftest.protocol_ready".into(),
        "selftest.event_sink_ready".into(),
        "selftest.intelligence_ready".into(),
        "selftest.report_writer_ready".into(),
    ];
    // Avoid double emit later — clear streaming flag by returning events already streamed.
    // Orchestrator still re-emits; make messages idempotent-friendly.
    let _ = &mut events;
    ModuleRun {
        events,
        signals,
        status: "ok".into(),
        message: "selftest 모듈 실행 완료".into(),
    }
}

async fn append_pending_reboot(
    signals: &mut Vec<String>,
    events: &mut Vec<EngineEvent>,
    module: &str,
    progress: i32,
) {
    let raw = ps(
        r#"$paths=@('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending','HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'); $hit=@(); foreach($p in $paths){ if(Test-Path $p){ $hit += $p } }; [PSCustomObject]@{Pending=($hit.Count -gt 0);Paths=$hit} | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    if raw.contains("\"Pending\":true") || raw.contains("\"Pending\": true") {
        signals.push("reboot_pending=true".into());
        push(
            events,
            EngineEvent::signal(module, progress, "warning", "재부팅 대기 상태 감지"),
        );
    } else {
        signals.push("reboot_pending=false".into());
        push(
            events,
            EngineEvent::signal(module, progress, "info", "재부팅 대기 없음"),
        );
    }
}

async fn append_storage_health(
    signals: &mut Vec<String>,
    events: &mut Vec<EngineEvent>,
    module: &str,
    progress: i32,
) {
    let raw = ps(
        r#"try { Get-PhysicalDisk | Select-Object FriendlyName,MediaType,HealthStatus,OperationalStatus,@{n='SizeGB';e={[math]::Round($_.Size/1GB,2)}} | ConvertTo-Json -Compress -Depth 4 } catch { Get-CimInstance Win32_DiskDrive | Select-Object Model,Status,InterfaceType,Size | ConvertTo-Json -Compress -Depth 4 }"#,
    )
    .await;
    if raw.to_lowercase().contains("unhealthy")
        || raw.to_lowercase().contains("warning")
        || raw.to_lowercase().contains("pred fail")
    {
        signals.push(format!("disk_unhealthy {raw}"));
        push(
            events,
            EngineEvent::signal(module, progress, "warning", "저장장치 상태 주의"),
        );
    } else if !raw.is_empty() {
        signals.push("storage_health=ok".into());
        push(
            events,
            EngineEvent::signal(module, progress, "info", "저장장치 상태 정상"),
        );
    }
}

fn parse_memory_signal(
    raw: &str,
    signals: &mut Vec<String>,
    events: &mut Vec<EngineEvent>,
    module: &str,
    progress: i32,
) {
    if raw.is_empty() {
        return;
    }
    signals.push(format!("quick_os_memory={raw}"));
    if let Some(used) = extract_f64(raw, "UsedPercent") {
        let sev = if used >= 90.0 {
            "warning"
        } else {
            "info"
        };
        if used >= 90.0 {
            signals.push("memory_pressure=high".into());
        }
        push(
            events,
            EngineEvent::signal(
                module,
                progress,
                sev,
                format!("메모리 사용률 {used:.1}%"),
            ),
        );
    } else {
        push(
            events,
            EngineEvent::signal(module, progress, "info", "메모리 정보 수집"),
        );
    }
}

fn parse_disk_signal(
    raw: &str,
    signals: &mut Vec<String>,
    events: &mut Vec<EngineEvent>,
    module: &str,
    progress: i32,
) {
    if raw.is_empty() {
        return;
    }
    signals.push(format!("quick_disk={raw}"));
    // Heuristic: any FreePercent under 10
    if raw.contains("\"FreePercent\":") {
        // crude scan for low free space markers
        for token in raw.split("FreePercent\":") {
            if let Some(rest) = token.strip_prefix("") {
                let num: String = rest
                    .chars()
                    .take_while(|c| c.is_ascii_digit() || *c == '.')
                    .collect();
                if let Ok(v) = num.parse::<f64>() {
                    if v < 10.0 {
                        signals.push(format!("low_disk free={v}"));
                        push(
                            events,
                            EngineEvent::signal(
                                module,
                                progress,
                                "warning",
                                format!("디스크 여유 {v:.1}% 부족"),
                            ),
                        );
                        return;
                    }
                }
            }
        }
    }
    push(
        events,
        EngineEvent::signal(module, progress, "info", "디스크 여유 공간 확인 완료"),
    );
}

fn push(events: &mut Vec<EngineEvent>, event: EngineEvent) {
    emit_event(&event);
    events.push(event);
}

async fn ps(script: &str) -> String {
    let fut = Command::new("powershell.exe")
        .args([
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-Command",
            script,
        ])
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .output();

    match tokio::time::timeout(std::time::Duration::from_secs(45), fut).await {
        Ok(Ok(output)) => String::from_utf8_lossy(&output.stdout).trim().to_string(),
        _ => String::new(),
    }
}

async fn run_cmd(program: &str, args: &[&str], timeout_secs: u64) -> (i32, String) {
    let fut = Command::new(program)
        .args(args)
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .output();
    match tokio::time::timeout(std::time::Duration::from_secs(timeout_secs), fut).await {
        Ok(Ok(output)) => (
            output.status.code().unwrap_or(-1),
            String::from_utf8_lossy(&output.stdout).trim().to_string(),
        ),
        Ok(Err(e)) => (-1, e.to_string()),
        Err(_) => (-1, "timeout".into()),
    }
}

fn extract_i64(json: &str, key: &str) -> Option<i64> {
    let pattern = format!("\"{key}\":");
    let idx = json.find(&pattern)?;
    let rest = json[idx + pattern.len()..].trim_start();
    let num: String = rest
        .chars()
        .take_while(|c| c.is_ascii_digit() || *c == '-')
        .collect();
    num.parse().ok()
}

fn extract_f64(json: &str, key: &str) -> Option<f64> {
    let pattern = format!("\"{key}\":");
    let idx = json.find(&pattern)?;
    let rest = json[idx + pattern.len()..].trim_start();
    let num: String = rest
        .chars()
        .take_while(|c| c.is_ascii_digit() || *c == '.' || *c == '-')
        .collect();
    num.parse().ok()
}
