# WO-05 journal and recovery

작업 브랜치: `codex/pccare-rework-phase1-filesystem-hardening`
기준 커밋: `155537b721e3eb266b2a0a3fb801a4b35fbbff56`
기준 트리: `581bdaf0bf50a787ea1ca346140065c733e56b12`
검증 소스 커밋·원격 CI: `evidence/wo05/GITHUB_RESULT.txt`.
최종 증거 커밋은 검증 소스 커밋과 구분한다.
실행 위치: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`.
환경: Windows NT 10.0.26200.0, x64, .NET SDK 10.0.401, runtime 10.0.12.
전용 VM 및 스냅샷: 미제공.

Status: PARTIAL. Automated malformed-journal and double-failure tests: PASS.

Schema 2 binds install and transaction directories to measured identities.
Entries bind original, backup and installed files to measured identities.
Readers reject duplicate properties, unknown properties, malformed JSON and oversized files.
Readers use a retained file handle. Protected mode checks the actual opened file's ACL.
Writers create a randomized temporary file, flush it and replace journal.json.
Backups and targets are validated together before any restore action.
The recovery loop retains handles until restore completes.
Retries accept the recorded new content or restored original content on the original object.
Unrelated content on an applied target stops recovery.
FailureResult preserves the primary error and secondary journal failure.
Rollback failure preserves the original failure, rollback failure and secondary recording failure.

Separate-process tests terminate after the first apply write and during rollback copy.
A later process restores old content and preserves vault-marker.
These tests do not prove Windows reboot recovery or physical power-loss durability.
Schema 1 and missing-identity journals fail closed, including terminal old journals.
A reviewed migration or archive path is still required for WO04 journal compatibility.
No old journal is converted automatically into file-write authority.
An ID-less new file remains preserved if creation precedes a crash before journal persistence.

Source rollback uses SourceRollback.ps1 and ROLLBACK.sh on a marked independent copy.
Installation recovery uses UpdateTransaction.Recover through the fixed elevated helper verb.
Source-copy hash restoration does not count as installation recovery.
Evidence: JOURNAL_TEST_RESULT.txt and RECOVERY_TEST_RESULT.txt.
