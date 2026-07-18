use super::system::ModuleRun;
use super::types::{emit_event, EngineEvent};
use std::process::Stdio;
use tokio::process::Command;

pub async fn run_audio() -> ModuleRun {
    let module = "audio";
    let mut events = Vec::new();
    let mut signals = Vec::new();

    push(
        &mut events,
        EngineEvent::stage(module, 5, "오디오 사전 스냅샷"),
    );
    let preflight = ps(
        r#"$devices=Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.Class -in @('AudioEndpoint','MEDIA','Sound') -or $_.FriendlyName -match 'audio|sound|realtek|nvidia high definition|bluetooth|usb audio' }; $services=Get-Service -Name Audiosrv,AudioEndpointBuilder,PlugPlay -ErrorAction SilentlyContinue; [PSCustomObject]@{AudioDeviceCount=($devices|Measure-Object).Count;ServiceStopped=($services|Where-Object Status -ne 'Running'|Measure-Object).Count} | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    signals.push(format!("audio_preflight_snapshot={preflight}"));
    if let Some(stopped) = extract_i64(&preflight, "ServiceStopped") {
        if stopped > 0 {
            signals.push("audio_service stopped".into());
            push(
                &mut events,
                EngineEvent::signal(module, 15, "warning", "오디오 관련 서비스 중지"),
            );
        }
    }

    push(
        &mut events,
        EngineEvent::stage(module, 22, "오디오 엔드포인트 스캔"),
    );
    let devices = ps(
        r#"Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.Class -in @('AudioEndpoint','MEDIA','Sound') -or $_.FriendlyName -match 'audio|sound|realtek|nvidia high definition|bluetooth|usb audio' } | Select-Object Class,FriendlyName,InstanceId,Status,Problem | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    signals.push(format!("audio_device_scan={devices}"));
    push(
        &mut events,
        EngineEvent::signal(module, 30, "info", "오디오 장치 목록 수집"),
    );

    push(
        &mut events,
        EngineEvent::stage(module, 40, "오디오 서비스 확인"),
    );
    let services = ps(
        r#"Get-Service -Name Audiosrv,AudioEndpointBuilder,PlugPlay -ErrorAction SilentlyContinue | Select-Object Name,DisplayName,Status,StartType | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    signals.push(format!("audio_service_scan={services}"));
    if services.contains("Stopped") {
        signals.push("audio_service".into());
        push(
            &mut events,
            EngineEvent::signal(module, 48, "warning", "오디오 서비스 중 중지 상태 존재"),
        );
    } else {
        push(
            &mut events,
            EngineEvent::signal(module, 48, "info", "오디오 서비스 실행 중"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 55, "재생 경로 건강성"),
    );
    let playback = ps(
        r#"$render=Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.Class -eq 'AudioEndpoint' -and $_.FriendlyName -notmatch 'capture|mic|recording' }; $disabled=$render | Where-Object { $_.Status -ne 'OK' }; [PSCustomObject]@{RenderEndpointCount=($render|Measure-Object).Count;DisabledRenderCount=($disabled|Measure-Object).Count;NoSoundLikely=(($render|Measure-Object).Count -eq 0) -or (($disabled|Measure-Object).Count -gt 0)} | ConvertTo-Json -Compress"#,
    )
    .await;
    signals.push(format!("audio_playback_health={playback}"));
    if playback.contains("\"NoSoundLikely\":true") || playback.contains("\"RenderEndpointCount\":0")
    {
        signals.push("no_render_endpoint".into());
        push(
            &mut events,
            EngineEvent::signal(module, 65, "warning", "재생 엔드포인트 없음/비정상"),
        );
    } else {
        push(
            &mut events,
            EngineEvent::signal(module, 65, "info", "재생 엔드포인트 정상"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 72, "오디오 이벤트 상관 분석"),
    );
    let ev = ps(
        r#"$start=(Get-Date).AddDays(-14); Get-WinEvent -FilterHashtable @{LogName='System';Level=1,2,3;StartTime=$start} -MaxEvents 80 -ErrorAction SilentlyContinue | Where-Object {$_.ProviderName -match 'Audio|Kernel-PnP|Service Control Manager' -or $_.Message -match 'audio|sound|Realtek|Audiosrv|AudioEndpointBuilder'} | Select-Object TimeCreated,ProviderName,Id,LevelDisplayName | ConvertTo-Json -Compress -Depth 4"#,
    )
    .await;
    if ev.contains("ProviderName") {
        signals.push(format!("audio_event_correlation={ev}"));
        push(
            &mut events,
            EngineEvent::signal(module, 82, "info", "오디오 관련 이벤트 수집"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 90, "오디오 복구 계획"),
    );
    let plan = ps(
        r#"$services=Get-Service -Name Audiosrv,AudioEndpointBuilder -ErrorAction SilentlyContinue; $audio=Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { ($_.Class -in @('AudioEndpoint','MEDIA','Sound') -or $_.FriendlyName -match 'audio|sound|realtek') -and $_.Status -ne 'OK' }; $render=Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.Class -eq 'AudioEndpoint' -and $_.FriendlyName -notmatch 'capture|mic' }; $needRepair=$false; if(($services|Where-Object Status -ne 'Running'|Measure-Object).Count -gt 0){$needRepair=$true}; if(($audio|Measure-Object).Count -gt 0){$needRepair=$true}; if(($render|Measure-Object).Count -eq 0){$needRepair=$true}; [PSCustomObject]@{ServiceRestartNeeded=($services | Where-Object {$_.Status -ne 'Running'} | Measure-Object).Count -gt 0; RepairNeeded=$needRepair; RecommendedOrder= if($needRepair){@('restart audio services','restart non-ok audio pnp devices','scan devices','post-scan verify')}else{@('no repair needed - playback path looks healthy')} } | ConvertTo-Json -Compress -Depth 5"#,
    )
    .await;
    signals.push(format!("audio_repair_plan={plan}"));
    if plan.contains("\"RepairNeeded\":true") {
        push(
            &mut events,
            EngineEvent::signal(module, 96, "warning", "오디오 복구 권장"),
        );
    } else {
        push(
            &mut events,
            EngineEvent::signal(module, 96, "info", "오디오 복구 불필요"),
        );
    }

    push(
        &mut events,
        EngineEvent::stage(module, 100, "오디오 복구 진단 완료"),
    );
    ModuleRun {
        events,
        signals,
        status: "ok".into(),
        message: "audio 모듈 실행 완료".into(),
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
