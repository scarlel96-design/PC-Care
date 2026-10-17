# WO24 — Bootstrap E2E

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

Reuse BootstrapDispatch, UpdateBootstrap, Bridge and Helper.
Keep production Publisher Root empty.
Do not substitute a test publisher into production policy.
The prepared deployment harness qualifies startup separately from transition.
Full legacy application UI, UAC, protected snapshot, application and relaunch remain NOT TESTED.
A source-bound isolated publisher build profile is still required before this E2E can pass.
