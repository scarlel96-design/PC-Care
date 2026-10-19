# WO26 — Recovery State Classification

Baseline: `1a4fd0811f263f35a94389791860d374c710759f`.
Verification source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
Branch: `codex/pccare-wo26-transaction-recovery-volume-staging`.

## Root cause and correction
The old rollback path requested write access before checking the current original bytes.
A read-sharing handle could block that request although installed bytes were unchanged.
The corrected path verifies the original through a read lease first.
It retains that lease when identity, length, hash and required ACL checks match.
It requests a write lease only when restoration needs a write.
It rechecks identity and content after changing lease type.

## Result contract
`PreMutationRejected` means the apply path did not start installation-content mutation.
`OriginalStateVerified` means recovery checked recorded originals without copying their contents.
`RecoveryCompleted` means the apply path changed content and rollback completed, or rollback performed a required mutation.
`RecoveryDeferred` retains an incomplete result when recovery cannot finish.
`OriginalBytesVerified` covers recorded journal targets. It does not attest the entire product.
An empty recorded scope does not receive that boolean.
`AnyMutationStarted` records installation-content mutation. Staging file creation is a separate activity.
Resume results describe changes during that recovery invocation.
They do not reconstruct every byte write from earlier terminated processes.
The existing `ROLLED-BACK` and terminal `Restored` API values remain compatible.
The additive disposition distinguishes the actual activity.

The required journal records retain `PRE-MUTATION-REJECTED` for original-state closure.
`InspectRecovery` reports `ORIGINAL-STATE-VERIFIED` when all relevant terminal transactions match that reason.
The product UI does not describe that result as a performed file restoration.

## Final automated qualification

Source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
CI: [39166559052 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39166559052).
App: 1429 PASS / 0 FAIL / 111 SKIP.
New WO26: 21 PASS, included in app total.
SecurityLab: 36 PASS, separate.
Restore and Release: 0 warnings / 0 errors.
Actual product GUI, UAC, independent SID and reboot remain NOT TESTED.
