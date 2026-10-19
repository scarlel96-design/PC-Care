# WO27 - Recovery Disposition

ApplyMutationStarted reports installation-content mutation attempted by this Apply invocation. RecoveryMutationStarted reports installation-content mutation attempted by rollback or explicit recovery. AnyMutationStarted is their logical union. These per-invocation flags exclude private staging and journal writes. They do not count durable bytes.

Transaction metadata writes and read-only backup checks do not set file-mutation flags. Apply error composition now retains rollback mutations. PreMutationRejected requires neither mutation flag. OriginalStateVerified confirms preserved bytes, not a successful update.

The old pre-mutation marker also affected restart classification. Clear and persist that marker before restoration can mutate installation contents.

If that journal write fails, do not start the installation write. A new reader must distinguish RESTORED from ORIGINAL-STATE-VERIFIED.

## Final automated qualification

Source: `f6d29413a77c7522210de6eba3b1dbae745b2365`.

CI: [39197282336 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39197282336).

App: 1445 PASS / 0 FAIL / 111 SKIP. New WO27: 16 PASS, included.

SecurityLab: 36 PASS, separate. Restore and Release: 0 warnings / 0 errors.

Actual guest deployment, privileged recovery, UAC, reboot, GUI and physical durability remain NOT TESTED.
