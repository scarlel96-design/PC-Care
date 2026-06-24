param(
    [string]$OutputDir = "",
    [string]$Version = "50.0.0"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDir) {
    $OutputDir = Join-Path $ProjectRoot "content\data\commercial"
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$categories = @{
    "system_performance" = 900
    "memory" = 700
    "disk_storage_smart" = 800
    "filesystem_ntfs" = 500
    "windows_update" = 550
    "dism_component" = 500
    "sfc_integrity" = 400
    "driver_pnp" = 900
    "audio_bluetooth" = 800
    "network" = 550
    "startup_logon" = 550
    "scheduled_tasks" = 400
    "services" = 750
    "event_log_patterns" = 1400
    "reliability" = 450
    "power_thermal" = 450
    "gpu_display" = 400
    "usb_hid" = 400
    "security_defender" = 400
    "app_crash_hang" = 600
    "webview_runtime" = 300
    "registry_risk" = 400
    "perf_cpu_thermal" = 450
    "perf_ram_pressure" = 450
    "perf_startup_bloat" = 400
    "perf_background_apps" = 350
    "perf_visual_effects" = 300
    "perf_power_plan" = 300
    "perf_search_index" = 250
    "perf_delivery_opt" = 250
    "perf_game_dvr" = 200
    "perf_pagefile" = 250
    "perf_hibernation" = 200
    "perf_browser_cache" = 350
    "perf_temp_cleanup" = 450
    "perf_dns_latency" = 250
    "perf_disk_fragment" = 250
    "perf_superfetch" = 250
    "perf_telemetry" = 200
    "perf_wmi_health" = 350
    "perf_event_storm" = 400
    "perf_boot_time" = 350
}

$rules = New-Object System.Collections.Generic.List[object]
$ruleIndex = 0
foreach ($entry in $categories.GetEnumerator()) {
    $cat = $entry.Key
    $count = $entry.Value
    for ($i = 0; $i -lt $count; $i++) {
        $ruleIndex++
        $rules.Add([PSCustomObject]@{
            ruleId = "$cat.rule_$($i.ToString('0000'))"
            version = "1.0.0"
            area = $cat.Split('_')[0]
            category = $cat
            symptoms = @("symptom_$($i % 20)")
            signals = @("signal.$cat.$($i % 50)")
            conditions = @("confidence >= 0.5")
            severity = if ($i % 7 -eq 0) { "critical" } elseif ($i % 3 -eq 0) { "high" } else { "medium" }
            confidenceBase = [math]::Round(0.55 + ($i % 40) * 0.01, 2)
            risk = if ($i % 5 -eq 0) { "high" } elseif ($i % 2 -eq 0) { "medium" } else { "low" }
            protocolId = "$cat.protocol_$($i % 12)"
            dryRunAction = "dry_run_only"
            applyAction = "user_approval_required"
            postCheck = @("post_verify_scan")
            userMessage = "[$cat] pattern $($i) detected"
        })
    }
}

$protocolIds = @(
    "driver.pnp.safe_rescan", "driver.pnp.targeted_restart", "audio.stack.safe_restart",
    "audio.bluetooth.endpoint_recover", "system.dism.checkhealth", "system.dism.scanhealth",
    "system.dism.restorehealth_guarded", "system.sfc.verifyonly", "system.sfc.scannow_guarded",
    "memory.pressure.analysis", "disk.free_space.recovery_plan", "disk.smart.warning_protocol",
    "startup.heavy_items_review", "service.not_running_safe_restart", "eventlog.recent_critical_triage",
    "reliability.crash_correlation", "network.stack.reset_plan", "webview.runtime_repair_plan",
    "windows_update.repair_plan", "registry.safe_cleanup", "privacy.clean_preview",
    "junk.clean_quarantine", "shortcut.repair_safe", "internet.acceleration_plan",
    "vulnerability.fix_guided", "secure_delete.hdd_single_pass", "secure_delete.ssd_best_effort",
    "secure_delete.vault_crypto_erase", "secure_delete.free_space_wipe_hdd"
)

$protocols = New-Object System.Collections.Generic.List[object]
foreach ($protoId in $protocolIds) {
    $protocols.Add([PSCustomObject]@{
        protocolId = "$protoId.v1"
        area = ($protoId -split '\.')[0]
        risk = if ($protoId -match 'restorehealth|scannow|sanitize|crypto') { "high" } else { "low" }
        requiresElevation = $true
        requiresTarget = $protoId -match 'targeted'
        preChecks = @("dry_run_snapshot", "risk_gate")
        dryRun = @("plan_only")
        apply = @("user_approval_apply")
        postChecks = @("post_verify_scan")
        successCriteria = @("exitCode == 0", "postCheck passed")
        failureHandling = @("collect evidence", "suggest rollback")
        rollback = @("restore from snapshot")
    })
}

for ($i = 0; $i -lt 90; $i++) {
    $protocols.Add([PSCustomObject]@{
        protocolId = "commercial.protocol_$($i.ToString('000')).v1"
        area = "commercial"
        risk = "medium"
        requiresElevation = ($i % 3 -eq 0)
        requiresTarget = $false
        preChecks = @("readiness_scan")
        dryRun = @("simulate")
        apply = @("guarded_apply")
        postChecks = @("verification_scan")
        successCriteria = @("evidence_saved")
        failureHandling = @("audit_log")
        rollback = @("no_destructive_default")
    })
}

$rulesPack = [PSCustomObject]@{
    format = "spd-rules-pack-v1"
    version = $Version
    packVersion = $Version
    productVersion = $Version
    ruleCount = $rules.Count
    checksum = ""
    rules = $rules
}
$protocolsPack = [PSCustomObject]@{
    format = "spd-protocols-pack-v1"
    version = $Version
    packVersion = $Version
    productVersion = $Version
    protocolCount = $protocols.Count
    checksum = ""
    protocols = $protocols
}

$rulesPath = Join-Path $OutputDir "rules.pack.json"
$protocolsPath = Join-Path $OutputDir "protocols.pack.json"
$rulesPack | ConvertTo-Json -Depth 6 -Compress | Set-Content $rulesPath -Encoding UTF8
$protocolsPack | ConvertTo-Json -Depth 6 -Compress | Set-Content $protocolsPath -Encoding UTF8

Write-Host "[OK] Rules: $($rules.Count) -> $rulesPath ($((Get-Item $rulesPath).Length) bytes)" -ForegroundColor Green
Write-Host "[OK] Protocols: $($protocols.Count) -> $protocolsPath" -ForegroundColor Green