# WO-05 VM E2E runbook

작업 브랜치: `codex/pccare-rework-phase1-filesystem-hardening`
기준 커밋: `155537b721e3eb266b2a0a3fb801a4b35fbbff56`
기준 트리: `581bdaf0bf50a787ea1ca346140065c733e56b12`
검증 소스 커밋·원격 CI: `evidence/wo05/GITHUB_RESULT.txt`.
최종 증거 커밋은 검증 소스 커밋과 구분한다.
실행 위치: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`.
환경: Windows NT 10.0.26200.0, x64, .NET SDK 10.0.401, runtime 10.0.12.
전용 VM 및 스냅샷: 미제공.

Harness parse and host guard: PASS. Actual VM E2E: BLOCKED.

| Evidence class | Actual status |
|---|---|
| Engine unit/integration tests | PASS on local Windows synthetic folders |
| Separate-process apply termination and recovery | PASS |
| Separate-process rollback termination and retry | PASS |
| Installed application update and UAC | BLOCKED |
| Windows VM reboot recovery | BLOCKED |
| Official production package and publisher | BLOCKED |

## Prerequisites

Create a disposable Windows 11 VM.
Record its hypervisor identity and a restorable snapshot ID.
Install .NET 10 SDK and project dependencies in that VM.
Create an evidence directory and its .wo05-disposable-vm marker.
Use Program Files/PC-Care-WO05-Lab as the dedicated installation directory.
Prepare only synthetic settings and Lab vault data.
Use a filtered administrator VM account for same-account UAC and test certificate access.
Use a separate standard-user account for effective ACL measurements.
Record certificate trust visibility separately for each account.

## Prepare the profile

Run these commands inside the VM repository.

```powershell
dotnet build tools/SmartPerformanceDoctor.VMTestKit/SmartPerformanceDoctor.VMTestKit.csproj -c Release -p:Platform=x64
./scripts/wo05-vm-harness.ps1 -EvidenceDirectory C:\WO05-Evidence -SnapshotId VM_SNAPSHOT_ID -Action prepare
$trustSource = 'C:\WO05-Evidence\trust\TestVmTrust.g.cs'
dotnet build src/SmartPerformanceDoctor.App/SmartPerformanceDoctor.App.csproj -c Release -p:Platform=x64 -p:WO05VMTest=true "-p:WO05TestTrustSource=$trustSource" -p:Version=51.0.4 -m:1 -o C:\WO05-Baseline
dotnet build src/SmartPerformanceDoctor.App/SmartPerformanceDoctor.App.csproj -c Release -p:Platform=x64 -p:WO05VMTest=true "-p:WO05TestTrustSource=$trustSource" -p:Version=51.0.5 -m:1 -o C:\WO05-Next
```

Package the baseline layout using the existing installer pipeline in the VM.
Record the installer command, artifact hash and installation result.
Confirm that the layout contains PCCare.exe and engine/SmartPerformanceDoctor.UpdateHelper.exe.
Sign first-party runtime binaries with the recorded VM certificate using Set-AuthenticodeSignature.
Verify Get-AuthenticodeSignature returns Valid for each signed executable and required assembly.
Record actual FileVersion for both builds before package generation.
Copy only approved update files into C:\WO05-Payload.
Do not include settings or vault files in the payload.

```powershell
$kit = 'tools/SmartPerformanceDoctor.VMTestKit/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.VMTestKit.exe'
& $kit sign-package C:\WO05-Payload C:\WO05-Evidence\trust\test-private.p8 C:\WO05-Evidence\update.spdup 51.0.4 51.0.5
./scripts/wo05-vm-harness.ps1 -EvidenceDirectory C:\WO05-Evidence -SnapshotId VM_SNAPSHOT_ID -Action capture -InstallRoot 'C:\Program Files\PC-Care-WO05-Lab' -Outcome 'BEFORE'
./scripts/wo05-vm-harness.ps1 -EvidenceDirectory C:\WO05-Evidence -SnapshotId VM_SNAPSHOT_ID -Action apply -InstallRoot 'C:\Program Files\PC-Care-WO05-Lab' -PackagePath C:\WO05-Evidence\update.spdup
```

## Execute each scenario from a restored snapshot

1. Run the installed baseline and record its UI and version.
2. Cancel UAC and record code 1223 with unchanged installation hashes.
3. Approve UAC and record the helper result and installed version.
4. Compare every approved payload hash with the installed file hash.
5. Hold a later target read handle and repeat apply.
6. Verify earlier existing files remain unchanged after preflight failure.
7. Release the lock and run the fixed recover action.
8. Terminate the helper during apply and preserve its journal.
9. Restart the installed application and record pending recovery detection.
10. Terminate recovery during copy and run recover again.
11. Reboot only the VM through its console.
12. Record startup detection, UAC recovery and final hashes after reboot.
13. Compare synthetic vault and settings hashes after each scenario.
14. Open, unlock and export the synthetic vault.
15. Run capture and acl-write-probe as the standard user.
16. Repeat ACL measurements after VM reboot.

The harness never reboots the host and never treats an expected cancellation as installation success.
Capture stores file hashes, ACL descriptors, account elevation, VM identity, snapshot ID and outcome.
Baseline installer packaging and human UAC/reboot actions require recorded manual steps.
The helper cannot currently replace its own locked image safely. Treat this expected finding as an open P0.
An engine-only payload can test file replacement but does not qualify the complete application update.
No scenario in this runbook has been executed in a dedicated VM during WO05.
