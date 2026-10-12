# WO-13 streaming export

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `140E28B09FCD66016A0F77F2AAA808CF96ACA4E8FD1536F36B8B958BB4116E7F`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Implemented output

The exporter decrypts authenticated chunks into an exclusive unique temporary file.
The parser validates chunk order, sizes, truncation, and trailing ciphertext.
The exporter verifies complete length and SHA-256 before handle-based publication.
An existing destination is preserved.
Cancellation removes only this operation's temporary file.
Authentication failure does not publish a final plaintext file.
Sensitive buffers and unwrapped keys are cleared after use.
Managed runtime copies are not proven completely erased.
Packed input is bounded before allocation. Loose input streams from a pinned read handle.

## Local measurement

Command: `dotnet run --project tools/SmartPerformanceDoctor.VaultProbe -c Release -- --crash-safe artifacts/wo13/auth-final`. Exit code: 0.
All five output hashes matched their synthetic inputs.

| Bytes | Export ms | CPU ms | Cumulative allocated bytes | Maximum sampled working set bytes | Hash |
|---:|---:|---:|---:|---:|---|
| 0 | 80.9186 | 78.125 | 12452992 | 257740800 | PASS |
| 4096 | 72.6311 | 140.625 | 12463520 | 421539840 | PASS |
| 4194304 | 108.4448 | 171.875 | 20849848 | 294793216 | PASS |
| 67108864 | 723.5300 | 1078.125 | 146932968 | 277483520 | PASS |
| 268435456 | 2853.2486 | 4468.750 | 550402440 | 376844288 | PASS |

Export timing excludes KDF. Working-set samples can include retained KDF and previous-test memory.
The sampler runs at 10 ms intervals. It does not prove the true peak.
Cumulative allocations rise with file size because chunk arrays are repeatedly allocated.
No full-file plaintext array is required on the loose-object export path.
Low-spec hardware, midstream cancellation latency, and long-run memory behavior remain NOT TESTED.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
