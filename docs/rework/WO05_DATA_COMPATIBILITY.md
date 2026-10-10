# WO-05 data compatibility

작업 브랜치: `codex/pccare-rework-phase1-filesystem-hardening`
기준 커밋: `155537b721e3eb266b2a0a3fb801a4b35fbbff56`
기준 트리: `581bdaf0bf50a787ea1ca346140065c733e56b12`
검증 소스 커밋·원격 CI: `evidence/wo05/GITHUB_RESULT.txt`.
최종 증거 커밋은 검증 소스 커밋과 구분한다.
실행 위치: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`.
환경: Windows NT 10.0.26200.0, x64, .NET SDK 10.0.401, runtime 10.0.12.
전용 VM 및 스냅샷: 미제공.

Synthetic Lab-vault/settings tests: PASS. Installed-version compatibility: PARTIAL. AV3 recovery: NOT TESTED.

Tests create a Lab v4/v5 vault with synthetic content and LabFast KDF settings.
Tests preserve vault file hashes and settings.json hashes after update and rollback.
Snapshot export returns plaintext with the original synthetic content hash.
Wrong-password export throws and preserves source hashes.
SecurityLab tests retain corruption and password error coverage.
These fixtures are not actual user data or independent old-version installed binaries.

VaultSnapshotRecovery opens every source file through a read-only retained handle.
It rejects links, hardlinks, existing output directories and output below the source.
It limits snapshots to 10,000 files, 10,000 directories and 2 GiB.
It copies to a new snapshot and verifies source and snapshot hashes.
It exports entries into separate fresh directories.
The original vault never receives unlock, metadata repair or migration writes.

The standalone VaultRecovery tool rejects elevated execution and redirected password input.
It requests a masked interactive password. Passwords are not passed in arguments or environment variables.
The managed password string is not guaranteed to be wiped from memory.
The output contains plaintext recovered data by the explicit export operation.
The tool supports Lab v4/v5. It does not claim AV3 export support.
AV3 production Writer and migration remain gated.

Build command:
`dotnet build tools/SmartPerformanceDoctor.VaultRecovery/SmartPerformanceDoctor.VaultRecovery.csproj -c Release -p:Platform=x64 -p:NuGetAudit=false --no-restore -m:1 -v:minimal`

Run the recovery tool from a non-elevated session with a source directory and a new output directory.
Compare source-hashes.json with independent pre-export source hashes.
Compare exported plaintext hashes with the expected synthetic records.
Interactive CLI execution, actual old installed vaults and production signing of this tool remain NOT TESTED or BLOCKED.
