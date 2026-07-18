use super::system::ModuleRun;
use super::types::{emit_event, EngineEvent};
use std::process::Stdio;
use tokio::process::Command;

pub async fn run_driver() -> ModuleRun {
    let module = "driver";
    let mut events = Vec::new();
    let mut signals = Vec::new();

    push(
        &mut events,
        EngineEvent::stage(module, 5, "장치 사전 스냅샷"),
    );
    let preflight = ps(
        r#"$bad=Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object {$_.Status -ne 'OK'}; [PSCustomObject]@{ProblemDeviceCount=($bad|Measure-Object).Count;ProblemDevices=$bad | Select-Object Class,FriendlyName,InstanceId,Status,Problem} | ConvertTo-Json -Compress -Depth 5"#,
    )
    .await;
    signals.push(format!("device_preflight_snapshot={preflight}"));
    if let Some(n) = extract_i64(&preflight, "ProblemDeviceCount") {
        if n > 0 {
            signals.push(format!("configmanagererror problem_devices={n}"));
            push(
                &mut events,
                EngineEvent::signal(module, 15, "warning", format!("문제 장치 {n}개")),
            );
        } else {
            push(
                &mut events,
                EngineEvent::signal(module, 15, "info", "문제 장치 없음"),
            );
        }
    }

    push(
        &mut events,
        EngineEvent::stage(module, 25, "PnP 문제 장치 스캔"),
    );
    let pnp = ps(
        r#"Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.Status -ne 'OK' } | Select-Object Class,FriendlyName,InstanceId,Status,Problem | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    if pnp.contains("FriendlyName") {
        signals.push(format!("pnp_problem_scan={pnp}"));
        push(
            &mut events,
            EngineEvent::signal(module, 35, "warning", "PnP 문제 장치 목록 수집"),
        );
    } else {
        signals.push("pnp_problem_scan=empty".into());
        push(
            &mut events,
            EngineEvent::signal(module, 35, "info", "PnP 문제 장치 없음"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 42, "ConfigManager 오류 맵"),
    );
    let cm = ps(
        r#"Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object {$_.ConfigManagerErrorCode -ne 0} | Select-Object Name,PNPClass,DeviceID,Manufacturer,Service,Status,ConfigManagerErrorCode | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    if cm.contains("ConfigManagerErrorCode") {
        signals.push(format!("driver_pnp_entity_error_map={cm}"));
        signals.push("configmanagererror".into());
        push(
            &mut events,
            EngineEvent::signal(module, 50, "warning", "ConfigManager 오류 장치 존재"),
        );
    } else {
        signals.push("driver_pnp_entity_error_map=empty".into());
    }

    push(
        &mut events,
        EngineEvent::stage(module, 58, "서명 드라이버 인벤토리"),
    );
    let signed = ps(
        r#"Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue | Select-Object -First 120 DeviceName,DeviceClass,Manufacturer,DriverProviderName,DriverVersion,DriverDate,InfName,IsSigned | Sort-Object DeviceClass,DeviceName | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    if signed.to_lowercase().contains("\"issigned\":false") {
        signals.push("unsigned driver detected".into());
        push(
            &mut events,
            EngineEvent::signal(module, 65, "info", "서명되지 않은 드라이버 포함"),
        );
    } else {
        signals.push("driver_signed_inventory=ok".into());
        push(
            &mut events,
            EngineEvent::signal(module, 65, "info", "드라이버 서명 인벤토리 수집"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 72, "드라이버 충돌 스캔"),
    );
    let conflict = ps(
        r#"$entities=Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object { $_.ConfigManagerErrorCode -ne 0 -or $_.Status -match 'Error|Degraded|Unknown' }; $signed=Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue; $dup=@(); $groups=$signed | Group-Object DeviceName; foreach($g in $groups){ if($g.Count -gt 1){ $dup += [PSCustomObject]@{DeviceName=$g.Name;DriverCount=$g.Count;DriverConflict=$true} } }; [PSCustomObject]@{ProblemEntityCount=($entities|Measure-Object).Count;UnsignedDriverCount=($signed|Where-Object {$_.IsSigned -eq $false}|Measure-Object).Count;DuplicateDriverGroups=$dup;DriverConflict=($dup.Count -gt 0)} | ConvertTo-Json -Compress -Depth 5"#,
    )
    .await;
    signals.push(format!("driver_conflict_scan={conflict}"));
    if conflict.contains("\"DriverConflict\":true") || conflict.contains("\"DriverConflict\": true")
    {
        signals.push("driverconflict".into());
        push(
            &mut events,
            EngineEvent::signal(module, 80, "warning", "중복/충돌 드라이버 그룹 감지"),
        );
    } else {
        push(
            &mut events,
            EngineEvent::signal(module, 80, "info", "드라이버 충돌 신호 없음"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 88, "장치 이벤트 상관 분석"),
    );
    let events_json = ps(
        r#"$start=(Get-Date).AddDays(-14); try { Get-WinEvent -FilterHashtable @{LogName='System';Level=1,2,3;StartTime=$start} -MaxEvents 80 -ErrorAction Stop | Where-Object { $_.ProviderName -match 'Kernel-PnP|DriverFrameworks|DeviceSetupManager' } | Select-Object TimeCreated,ProviderName,Id,LevelDisplayName | ConvertTo-Json -Compress -Depth 4 } catch { '[]' }"#,
    )
    .await;
    if events_json.contains("ProviderName") {
        signals.push(format!("device_event_correlation={events_json}"));
        push(
            &mut events,
            EngineEvent::signal(module, 94, "info", "장치 관련 시스템 이벤트 수집"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 100, "드라이버 복구 진단 완료"),
    );
    ModuleRun {
        events,
        signals,
        status: "ok".into(),
        message: "driver 모듈 실행 완료".into(),
    }
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
    match tokio::time::timeout(std::time::Duration::from_secs(60), fut).await {
        Ok(Ok(output)) => String::from_utf8_lossy(&output.stdout).trim().to_string(),
        _ => String::new(),
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
