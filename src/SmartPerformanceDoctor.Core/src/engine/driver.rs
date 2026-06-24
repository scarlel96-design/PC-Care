use super::command_runner::{run_specs, CommandSpec};
use super::event_sink::EventSink;

pub async fn run_driver_doctor(sink: EventSink) -> Vec<String>
{
    let specs = vec![
        CommandSpec::powershell("driver", "device_preflight_snapshot", r#"$bad=Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object {$_.Status -ne 'OK'}; [PSCustomObject]@{ProblemDeviceCount=($bad|Measure-Object).Count;ProblemDevices=$bad | Select-Object Class,FriendlyName,InstanceId,Status,Problem} | ConvertTo-Json -Compress -Depth 5"#, 120),
        CommandSpec::powershell("driver", "pnp_problem_scan", r#"Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.Status -ne 'OK' } | Select-Object Class,FriendlyName,InstanceId,Status,Problem | ConvertTo-Json -Compress -Depth 4"#, 120),
        CommandSpec::command("driver", "pnputil_problem_devices", "pnputil.exe", &["/enum-devices", "/problem"], 120),
        CommandSpec::command("driver", "pnputil_driver_store_inventory", "pnputil.exe", &["/enum-drivers"], 90),
        CommandSpec::powershell("driver", "driver_pnp_entity_error_map", r#"Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object {$_.ConfigManagerErrorCode -ne 0} | Select-Object Name,PNPClass,DeviceID,Manufacturer,Service,Status,ConfigManagerErrorCode | ConvertTo-Json -Compress -Depth 4"#, 180),
        CommandSpec::powershell("driver", "driver_signed_inventory", r#"Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue | Select-Object -First 120 DeviceName,DeviceClass,Manufacturer,DriverProviderName,DriverVersion,DriverDate,InfName,IsSigned | Sort-Object DeviceClass,DeviceName | ConvertTo-Json -Compress -Depth 4"#, 90),
        CommandSpec::powershell("driver", "device_event_correlation", r#"$start=(Get-Date).AddDays(-14); $logs=@('System','Microsoft-Windows-DeviceSetupManager/Admin'); foreach($log in $logs){ try { Get-WinEvent -FilterHashtable @{LogName=$log;Level=1,2,3;StartTime=$start} -MaxEvents 80 -ErrorAction Stop | Select-Object @{n='Log';e={$log}},TimeCreated,ProviderName,Id,LevelDisplayName,Message } catch {} } | ConvertTo-Json -Compress -Depth 4"#, 90),
        CommandSpec::powershell("driver", "driver_conflict_scan", r#"$entities=Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object { $_.ConfigManagerErrorCode -ne 0 -or $_.Status -match 'Error|Degraded|Unknown' }; $signed=Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue; $dup=@(); $groups=$signed | Group-Object DeviceName; foreach($g in $groups){ if($g.Count -gt 1){ $dup += [PSCustomObject]@{DeviceName=$g.Name;DriverCount=$g.Count;DriverConflict=$true} } }; [PSCustomObject]@{ProblemEntityCount=($entities|Measure-Object).Count;UnsignedDriverCount=($signed|Where-Object {$_.IsSigned -eq $false}|Measure-Object).Count;DuplicateDriverGroups=$dup;DriverConflict=($dup.Count -gt 0)} | ConvertTo-Json -Compress -Depth 5"#, 180),
        CommandSpec::powershell("driver", "device_repair_plan", r#"$bad=Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object {$_.Status -ne 'OK'}; if(($bad|Measure-Object).Count -eq 0){ [PSCustomObject]@{RepairNeeded=$false;Message='문제 장치 없음 — 무작정 복구 불필요'} | ConvertTo-Json -Compress; return }; $plan=@(); foreach($d in $bad){ $action='restart-and-rescan'; if($d.Problem -match '28|31|39|43'){ $action='restart-rescan-then-official-driver-reinstall-if-repeated' }; $plan += [PSCustomObject]@{FriendlyName=$d.FriendlyName;Class=$d.Class;InstanceId=$d.InstanceId;Problem=$d.Problem;RecommendedAction=$action;Risk='medium'} }; [PSCustomObject]@{RepairNeeded=$true;Plan=$plan} | ConvertTo-Json -Compress -Depth 4"#, 120),
    ];

    run_specs(specs, sink).await
}
