# WO03 rollback verification

Isolated transaction tests cover a synthetic copy exception after one applied file, staged-file tampering, an installed-version postcondition failure, a simulated interrupted Applying journal, and missing-backup RecoveryRequired. They assert both restoration of the original file and removal of a newly created file where appropriate. The hardlink case asserts that an outside file remains unchanged. These tests do not simulate a real kernel power cut or volume flush ordering.

Source rollback is separately provided by `docs/rework/evidence/wo03/ROLLBACK.sh`: it checks the modified service's hash, restores its baseline bytes into an explicit target path, and checks the restored hash. The test runs against another copy so the modified working file stays modified. Exact command and literal results are in `VERIFICATION.txt`.
