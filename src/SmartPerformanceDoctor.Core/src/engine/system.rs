use super::command_runner::{run_specs, CommandSpec};
use super::event_sink::EventSink;
use super::dism_guard;

pub async fn run_quick_diagnostics(sink: EventSink) -> Vec<String>
{
    let specs = vec![
        CommandSpec::powershell("quick", "quick_os_memory", r#"$os=Get-CimInstance Win32_OperatingSystem; [PSCustomObject]@{Caption=$os.Caption;Version=$os.Version;LastBootUpTime=$os.LastBootUpTime;TotalGB=[math]::Round($os.TotalVisibleMemorySize/1MB,2);FreeGB=[math]::Round($os.FreePhysicalMemory/1MB,2);UsedPercent=[math]::Round((($os.TotalVisibleMemorySize-$os.FreePhysicalMemory)/$os.TotalVisibleMemorySize)*100,2)} | ConvertTo-Json -Compress"#, 60),
        CommandSpec::powershell("quick", "quick_disk", r#"Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" | ForEach-Object { $freePct=0; if($_.Size -gt 0){$freePct=[math]::Round(($_.FreeSpace/$_.Size)*100,2)}; [PSCustomObject]@{DeviceID=$_.DeviceID;SizeGB=[math]::Round($_.Size/1GB,2);FreeGB=[math]::Round($_.FreeSpace/1GB,2);FreePercent=$freePct} } | ConvertTo-Json -Compress -Depth 4"#, 60),
        CommandSpec::powershell("quick", "quick_problem_devices", r#"Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.Status -ne 'OK' } | Select-Object Class,FriendlyName,InstanceId,Status,Problem | ConvertTo-Json -Compress -Depth 4"#, 90),
    ];

    run_specs(specs, sink).await
}

pub async fn run_system_diagnostics(sink: EventSink) -> Vec<String>
{
    let specs = vec![
        CommandSpec::powershell("system", "system_overview", r#"Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber,LastBootUpTime,TotalVisibleMemorySize,FreePhysicalMemory,InstallDate | ConvertTo-Json -Compress -Depth 4"#, 90),
        CommandSpec::powershell("system", "storage_health", r#"try { Get-PhysicalDisk | Select-Object FriendlyName,MediaType,HealthStatus,OperationalStatus,@{n='SizeGB';e={[math]::Round($_.Size/1GB,2)}} | ConvertTo-Json -Compress -Depth 4 } catch { Get-CimInstance Win32_DiskDrive | Select-Object Model,Status,InterfaceType,Size | ConvertTo-Json -Compress -Depth 4 }"#, 120),
        CommandSpec::powershell("system", "service_anomaly_scan", r#"Get-CimInstance Win32_Service | Where-Object {$_.StartMode -eq 'Auto' -and $_.State -ne 'Running'} | Select-Object Name,DisplayName,State,StartMode,PathName,ExitCode,ServiceSpecificExitCode | ConvertTo-Json -Compress -Depth 4"#, 120),
        CommandSpec::command("system", "wmi_repository_check", "winmgmt.exe", &["/verifyrepository"], 180),
        CommandSpec::powershell("system", "pending_reboot_check", r#"$paths=@('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending','HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired','HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager'); $r=@(); foreach($p in $paths){ $r += [PSCustomObject]@{Path=$p;Exists=(Test-Path $p)} }; $r | ConvertTo-Json -Compress -Depth 4"#, 90),
        CommandSpec::powershell("system", "reliability_crash_digest", r#"$start=(Get-Date).AddDays(-14); Get-WinEvent -FilterHashtable @{LogName='Application';Level=1,2;StartTime=$start} -MaxEvents 120 -ErrorAction SilentlyContinue | Group-Object ProviderName,Id | Sort-Object Count -Descending | Select-Object -First 20 Count,Name | ConvertTo-Json -Compress -Depth 4"#, 180),
    ];

    run_specs(specs, sink).await
}

pub async fn run_system_recovery(sink: EventSink) -> Vec<String>
{
    let mut signals = Vec::new();

    let pre_specs = vec![
        CommandSpec::command("repair", "dism_check_health", "DISM.exe", &["/Online", "/Cleanup-Image", "/CheckHealth"], 900),
        CommandSpec::command("repair", "dism_scan_health", "DISM.exe", &["/Online", "/Cleanup-Image", "/ScanHealth"], 3600),
    ];
    signals.extend(run_specs(pre_specs, sink.clone()).await);
    signals.push(dism_guard::dism_stall_explanation());
    signals.extend(dism_guard::run_restore_health_supervised(sink.clone()).await);

    let post_specs = vec![
        CommandSpec::command("repair", "component_store_analyze", "DISM.exe", &["/Online", "/Cleanup-Image", "/AnalyzeComponentStore"], 2700),
        CommandSpec::command("repair", "sfc_scannow", "sfc.exe", &["/scannow"], 5400),
    ];
    signals.extend(run_specs(post_specs, sink).await);

    signals
}
