# WO26 — Deployment Dependencies

Baseline: `1a4fd0811f263f35a94389791860d374c710759f`.
Verification source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
Branch: `codex/pccare-wo26-transaction-recovery-volume-staging`.

## Reused trust chain
The implementation retains .NET ECDSA-P256/SHA-256, exact manifest bytes and the pinned public-key digest.
It retains complete file inventory, length, hash, identity and native publisher checks.
No cryptographic format or production publisher root is changed.

## Conservative asset policy
Managed runtime assets resolve to their app-local leaf beside the deps declaration.
Native and runtimeTargets assets require their declared relative path beside that declaration.
An identically named file in another directory does not satisfy the asset.
This is a restricted qualification policy. It is not a new CLR dependency resolver.
The policy can reject valid published layouts outside these path rules.
Qualify actual SDK publish output before using this restricted policy for production deployment.
External additionalProbingPaths require review even when the input configuration is signed.
The PowerShell pre-verifier uses the same restricted asset path rules.
The isolated deployment harness rejects active startup-hook, additional-deps, shared-store and profiler overrides.
Tests change only a process-local synthetic environment value and restore it.

## Independent verifier bootstrap
The trusted operator script verifies the verifier's signature and independently pinned input manifest before execution.
The native verifier then binds file IDs and protected ACLs.
The pin and expected publisher must come from outside the supplied input tree.
Accepting a public key merely because it accompanies a manifest does not establish trust.
PowerShell, CLR host, shared frameworks and their machine trust remain outside this input-tree attestation.
Different shared-framework versions are not qualified by matching input DLL names.
Actual guest PE deployment remains PREPARED / NOT TESTED.

## Official source basis
[Default probing](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/default-probing) documents deps and external probing inputs.
[Runtime configuration](https://github.com/dotnet/sdk/blob/main/documentation/specs/runtime-configuration-file.md) describes runtimeTargets and additionalProbingPaths.
The adapter fails closed outside its restricted policy.

## Final automated qualification

Source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
CI: [39166559052 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39166559052).
App: 1429 PASS / 0 FAIL / 111 SKIP.
New WO26: 21 PASS, included in app total.
SecurityLab: 36 PASS, separate.
Restore and Release: 0 warnings / 0 errors.
Actual product GUI, UAC, independent SID and reboot remain NOT TESTED.
