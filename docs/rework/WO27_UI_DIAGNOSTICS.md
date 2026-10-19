# WO27 - Ui Diagnostics

The administrator recovery button starts disabled. Read-only reconciliation enables it only when CanRequestRecovery is true. The click handler repeats reconciliation before helper launch. The helper retains independent execution checks.

Original-state preservation and actual restoration have separate messages. Diagnostic export uses fixed boolean mutation fields. Exclude paths, file identities, secrets, transaction IDs and free-form failure text. Source-contract tests do not qualify actual clicking, focus, DPI or screen readers.

## Final automated qualification

Source: `f6d29413a77c7522210de6eba3b1dbae745b2365`.

CI: [39197282336 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39197282336).

App: 1445 PASS / 0 FAIL / 111 SKIP. New WO27: 16 PASS, included.

SecurityLab: 36 PASS, separate. Restore and Release: 0 warnings / 0 errors.

Actual guest deployment, privileged recovery, UAC, reboot, GUI and physical durability remain NOT TESTED.
