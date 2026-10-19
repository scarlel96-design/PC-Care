# WO26 — Error Diagnostics

Baseline: `1a4fd0811f263f35a94389791860d374c710759f`.
Verification source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
Branch: `codex/pccare-wo26-transaction-recovery-volume-staging`.

## Preserved internal evidence
Primary and recovery diagnostics remain separate.
They retain native Win32 code, HRESULT, phase, journal stage and required trust checks internally.
The primary mutation flag is not replaced by a rollback attempt's mutation flag.
DeletePending remains NOT OBSERVED.
The boundary's FILE_SHARE_READ request is a code contract, not an observation of another process.

## External export
`UpdateDiagnosticExport` uses a fixed field allowlist.
It omits Failure, raw result Code, paths, identities, free-form phases, environment values and keys.
It preserves stage, numeric error, exception-type allowlist, target count and mutation/verification booleans.
Unknown strings become a review classification rather than copied user content.
The product diagnostic-copy action calls this exporter.
The internal legacy Failure contract remains available to existing engine callers.
It must not be exported directly.

[CreateFile](https://learn.microsoft.com/windows/win32/api/fileapi/nf-fileapi-createfilea) defines share/access compatibility.
The actual synthetic conflicts validate the relevant error paths.
They do not resolve the historical WO23 IOException.

Public process evidence uses document-local aliases for native volume and file identities.
The local raw records remain available with a canonical JSON record digest for comparison.
FILE_HASHES.json separately records the exact published artifact bytes.
Error codes, exit codes and byte-verification outcomes remain unchanged.
The legacy AccessContract text is a static summary, not a per-call desired-access trace.

## Final automated qualification

Source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
CI: [39166559052 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39166559052).
App: 1429 PASS / 0 FAIL / 111 SKIP.
New WO26: 21 PASS, included in app total.
SecurityLab: 36 PASS, separate.
Restore and Release: 0 warnings / 0 errors.
Actual product GUI, UAC, independent SID and reboot remain NOT TESTED.
