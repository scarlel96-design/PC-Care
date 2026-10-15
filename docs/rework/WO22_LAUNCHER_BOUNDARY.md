# WO22 — Launcher boundary

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo22-recovery-state-product-reliability`.
Baseline: `cc0fa735a4b1faaa7d72595e131372257e87f225`.
Read `evidence/wo22/VERIFICATION.txt` for tested source, commands, outputs and publication evidence.

## Current implementation

The existing managed InstallationLauncher remains the selected prototype.
It checks its deployment location, publisher policy, retained file identity, protected ACL and signed installation declaration.
It checks pending transactions before launching the declared program.
The CLR and some dependencies load before managed inspection.
This is not native pre-CLR attestation.
No production signing root or trusted deployment was added.

## Alternatives

| Option | Benefit | Qualification cost |
|---|---|---|
| Minimal Win32 launcher | Moves checks before CLR startup | Native DLL loading, PE signing, deployment and recovery still require qualification |
| MSIX package | Package signing and integrity policy | Requires deployment and updater integration qualification |
| Signed bootstrap | Retains the existing update trust boundary | Does not attest all pre-main module loading |
| Managed launcher | Reuses current verified policy and engine | CLR startup remains outside managed checks |

No additional wrapper was added without evidence that it closes the deployment P0.
Trusted launcher deployment and actual Authenticode E2E remain NOT TESTED.

## Official references

Read [DLL search order](https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-search-order).
Read [SetDefaultDllDirectories](https://learn.microsoft.com/en-us/windows/win32/api/LibLoaderAPI/nf-libloaderapi-setdefaultdlldirectories).
Read [MSIX tamper protection](https://learn.microsoft.com/en-us/windows/msix/desktop/tamper-protection).
Read [Package signing](https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview).

## Final verification

Source: `14ebea0b79d8df25f17a223cb6730bf5c95a21ee`.
Windows CI: [38768349299](https://github.com/scarlel96-design/PC-Care/actions/runs/38768349299).
App: 1333 PASS,0 FAIL,111 SKIP. SecurityLab: 36 PASS,0 FAIL,0 SKIP.
