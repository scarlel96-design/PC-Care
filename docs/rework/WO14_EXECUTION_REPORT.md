# WO-14 execution report

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: PARTIAL. Phase 1 NO-GO.

## Implemented result

An explicit synthetic transition now preserves the complete v5 source.
It reuses authenticated v6 generations in a separate candidate.
Read-only salvage recovers authenticated MetadataReady content without changing the original commit.
Streaming decryption now reuses pooled chunk buffers.
Cancellation, chunk authentication, concealed sizes, trailing-data rejection, and whole-content verification remain enforced.
The vault page now explains its compatibility limit and disables source sealing.
Windows CI runs the new tests, child-process probes, memory measurement, and existing regression suites.

## Local validation

| Run | PASS | FAIL | SKIP | Exit |
|---|---:|---:|---:|---:|
| Full application | 1085 | 0 | 111 | 0 |
| WO-14 new tests, included above | 30 | 0 | 0 | 0 |
| SecurityLab rebuilt | 36 | 0 | 0 | 0 |
| Release build | 0 warnings, 0 errors | | | 0 |
| Transition probe | Ten termination points | 0 unexpected failures | | 0 |
| Converted password probe | Ten termination points | 0 both-password failures | | 0 |
| Streaming fresh-process probe | Four sizes | 0 hash mismatches | | 0 |
| GUI profile guard | No GUI execution | | | 4 expected |
| Independent source-copy rollback | Exact original hash | | | 0 |

The initial SecurityLab `--no-build` run used a stale standalone test assembly.
It returned 35 PASS and 1 FAIL at the old readiness assertion.
Current source uses `Assert.False(gaps.LabCodeComplete)`.
The stack line and literal readiness output contradicted that current assertion.
The project was rebuilt and rerun. Both attempts are retained in evidence.

## Changed files

Read `evidence/wo14/CHANGED_FILES.txt` for the ten implementation files.
The thirteen reports and evidence files are committed separately.
The validated source commit and final evidence commit are distinct.
CI validates the implementation SHA shown above.

## Local details unavailable from source alone

The local Windows host has 24 logical processors and is not representative low-spec hardware.
Baseline and modified fixture containers live in private temporary directories.
Public JSON includes hashes and synthetic relative paths only.
The original locked v5 fixture remains unchanged.
No independent GUI test account or power-cut VM was supplied.
Memory trials retained prior create/import/KDF allocations within each fresh size process.
The transition and memory probes overlapped during local execution.
Sandbox file-sharing and Bash startup errors were rerun with authorized host access.
Those environment errors are not product success evidence.
No private fixture location, password, recovery code, or plaintext payload was uploaded.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.

## Local measured streaming cost

| Bytes | Unlock ms | Export ms | Export CPU ms | Allocated bytes | Export peak WS bytes |
|---:|---:|---:|---:|---:|---:|
| 4096 | 423.5148 | 92.7536 | 109.375 | 15478632 | 377552896 |
| 4194304 | 572.8109 | 303.2424 | 359.375 | 15501760 | 376487936 |
| 67108864 | 401.4736 | 3139.6089 | 3015.625 | 15799208 | 127324160 |
| 268435456 | 403.3819 | 10978.0803 | 9718.75 | 16592048 | 326078464 |

Each size uses a fresh process. Each process also creates and imports its fixture before export.
Working set includes retained KDF/import memory. Allocation is cumulative within the export phase.
Export allocation stays approximately 15.5–16.6 MB across these sizes.
This is not a low-spec qualification or a controlled before/after improvement claim.
ArrayPool buffers are zeroed and returned with clearArray=true.
Managed memory can retain other plaintext copies; complete process-memory erasure is not guaranteed.

## Completed Windows CI

[Run 38228910926](https://github.com/scarlel96-design/PC-Care/actions/runs/38228910926) completed successfully.
It validates `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab` and tree `c95434b61ae67ca361945b9caa74dddea477db8f`.
The full application returned 1085 PASS, 111 SKIP, and 0 FAIL.
SecurityLab returned 36 PASS and 0 FAIL.
All thirty new WO14 tests passed.
The Release build reported zero warnings and zero errors.
Restore reported three NU1510 package warnings.
Read `evidence/wo14/CI_RESULT.txt` for exact commands, literal outputs, and downloaded evidence hashes.
Actual GUI and power-loss trials remain NOT TESTED.
