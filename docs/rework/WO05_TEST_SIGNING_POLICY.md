# WO-05 test signing policy

작업 브랜치: `codex/pccare-rework-phase1-filesystem-hardening`
기준 커밋: `155537b721e3eb266b2a0a3fb801a4b35fbbff56`
기준 트리: `581bdaf0bf50a787ea1ca346140065c733e56b12`
검증 소스 커밋·원격 CI: `evidence/wo05/GITHUB_RESULT.txt`.
최종 증거 커밋은 검증 소스 커밋과 구분한다.
실행 위치: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`.
환경: Windows NT 10.0.26200.0, x64, .NET SDK 10.0.401, runtime 10.0.12.
전용 VM 및 스냅샷: 미제공.

Compilation and production-audit gates: PASS. Authenticode VM execution: BLOCKED. Official signing: BLOCKED.

Production PublisherTrustPolicy remains empty.
Normal Release artifacts retain production feature gates.
Environment variables cannot add publisher roots at runtime.
WO05VMTest=true enables PCCARE_VM_TEST only during compilation.
The build requires Release and an explicit WO05TestTrustSource file.
Each test-profile assembly receives PC-Care.BuildProfile=WO05-VM-TEST-NOT-FOR-DISTRIBUTION.
The generated public source contains an ephemeral package public key and the VM test certificate thumbprint.
VM test runtime verification still calls WinVerifyTrust.
The displayed verified state identifies VM TEST ONLY. It does not identify an official publisher.

fixture-profile generates a public key and discards the private key.
vm-profile stores a private PKCS8 key only under a marked disposable-VM evidence directory.
The VM harness restricts that directory to the VM operator.
The test Authenticode certificate has a non-exportable private key.
Test certificates and roots are installed only through the guarded VM prepare action.
The repository contains no generated private key or test certificate.
The generated public-key build source is not committed.

audit-production rejects test metadata or TestVmTrustData with exit 6.
It rejects IsVmTest=true with exit 7.
Actual normal app, helper and engine audits return exit 0.
Actual VM app and engine audits return exit 6 as expected.
The complete VM Release application profile compiles without errors or warnings.
No Authenticode signing, certificate trust registration or UAC test ran on this host.

Keep package-signature tests separate from executable Authenticode tests.
Delete test keys by restoring the dedicated VM snapshot after evidence export.
Do not copy test artifacts into user distributions.
