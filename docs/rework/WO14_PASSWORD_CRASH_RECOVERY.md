# WO-14 password crash recovery

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PASS for the explicitly converted v6 fixture; PARTIAL for raw v5.

Ten separate-process termination points were tested.
Each recovered fixture exported both original files with byte and SHA-256 equality.
At least one authenticated password remained valid at every point.

| Boundary | Coverage |
|---|---|
| KDF parameters prepared | PASS |
| Salt prepared | PASS |
| VMK wrapping prepared | PASS |
| Header prepared | PASS |
| Generation metadata written | PASS |
| Generation header written | PASS |
| Generation reread and verified | PASS |
| Before activation replacement | PASS |
| After activation replacement | PASS |
| Password commit completed | PASS |

The JSON records old and new reader exit codes separately.
An invalid password returns exit 4; that result is not data-loss failure when the valid password verifies all bytes.
This matrix uses `explicitly-converted-v6`.
Raw default v5 password rewriting was not changed or qualified by this matrix.
Production conversion remains disabled until compatibility, recovery, and authorization requirements are satisfied.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
