# WO25 — Bridge dependency trust

Baseline: `71d8f3ba85804a61157c0acf553bb37ccf6c07ad`.
Branch: `codex/pccare-wo25-installation-trust-volume-hardening`.

The new signing script requires two authenticated input closures.
One closure contains Bridge, Helper, engine and runtime configuration files.
The second closure contains the verifier and its dependencies.
The script checks raw signatures and hashes before starting the verifier.
It checks verifier PE publishers with Windows Authenticode first.
It then uses the existing native verifier and file boundary APIs.

The signed folder includes `.deps.json`, `.runtimeconfig.json` and all supplied dependencies.
The engine checks referenced runtime, native and runtime-target assets.
Missing, changed, mixed-version or extra files fail the exact authenticated inventory.
Read leases prevent ordinary replacement during validation.
Protected root and child directory ACLs remain required.

`wo24-signed-deployment.ps1` keeps its preparation output.
Its old Execute path now rejects unauthenticated input closure use.
`wo25-signed-deployment.ps1` reuses the existing signer and native verification body behind the stronger gate.
The certificate cleanup uses the existing fail-visible private-key deletion contract.
Actual test certificate creation and removal inside a disposable guest remain NOT TESTED.
Bridge startup with an unsupported verb only checks process startup and rejection.
It does not establish a complete bootstrap transition.

## Qualification boundary

WO25 remains PARTIAL. Phase 1 remains NO-GO.
Use only synthetic files for filesystem and process tests.
Keep actual guest deployment, UAC, independent SID, administrator restore, reboot, and product GUI results separate.
The complete run records are in `evidence/wo25/`.
No main merge or official release is authorized.

The authenticated closure covers the supplied directory tree.
It does not prove every CLR load comes from that tree.
Shared frameworks, additional probing paths, startup hooks and profiler configuration require separate runtime qualification.
No runtime loader trace or external-runtime binding was completed.


## Final automated qualification

Source: `80b00305e96c55064f15734ae932a433a8178f4d`.
Windows CI: [39055483722 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39055483722).
App: 1408 PASS / 0 FAIL / 111 SKIP.
New WO25: 32 PASS, included in the app total.
SecurityLab: 36 PASS, separate from the app.
Restore and Release: 0 warnings / 0 errors.
Actual isolated product GUI, UAC and reboot remain NOT TESTED.
