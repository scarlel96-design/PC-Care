# WO-05 ACL validation

작업 브랜치: `codex/pccare-rework-phase1-filesystem-hardening`
기준 커밋: `155537b721e3eb266b2a0a3fb801a4b35fbbff56`
기준 트리: `581bdaf0bf50a787ea1ca346140065c733e56b12`
검증 소스 커밋·원격 CI: `evidence/wo05/GITHUB_RESULT.txt`.
최종 증거 커밋은 검증 소스 커밋과 구분한다.
실행 위치: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`.
환경: Windows NT 10.0.26200.0, x64, .NET SDK 10.0.401, runtime 10.0.12.
전용 VM 및 스냅샷: 미제공.

Policy tests: PASS. File-handle ACL retrieval: PASS. Installed ACL measurements: BLOCKED.

Protected roots require Administrators or SYSTEM ownership and protected inheritance.
Both Administrators and SYSTEM require FullControl.
Non-administrative identities cannot receive write, append, delete, ACL-change or ownership-change rights.
The policy rejects unexpected deny entries.
Users ReadAndExecute remains permitted.
Opened package copies, stages, backups, journals and update.lock receive file ACL checks.
Protected ancestors below the work root receive separate descriptor checks.
FileSecurity comes from the retained FileStream handle with READ_CONTROL access.

Seven descriptor cases test valid policy, wrong owner, inherited root, Users write, missing SYSTEM, missing Administrators and deny entries.
These cases validate descriptors. They do not prove effective access for a different Windows token.
The local FileAclIsReadFromTheRetainedHandle test compares actual file descriptors obtained through both access paths.

Run wo05-vm-harness.ps1 capture in the dedicated VM.
Record root and descendant owner, SDDL, inheritance and file hashes.
Run acl-write-probe from a non-elevated VM session.
Record actual denied writes and administrative helper access.
Repeat capture after the VM reboots.
Test malformed existing ACLs before allowing writes.
Restore the VM snapshot after each destructive fixture scenario.

No host ProgramData or Program Files ACL was changed.
VM identity, snapshot, actual ACL, effective token access and reboot persistence remain BLOCKED.
