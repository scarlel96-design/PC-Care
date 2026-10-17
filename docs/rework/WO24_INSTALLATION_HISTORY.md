# WO24 — Installation history qualification

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

InspectInstallation retains its existing current-declaration scope.
The new InspectInstallationHistory API is read-only and grants no update authority.
It validates retained declaration signatures, package IDs, transaction and installation identities.
It verifies journal file lengths and hashes against signed declarations.
It requires a continuous signed version chain.
It checks every retained path against its last signed declaration and actual file identity.
Restored attempts do not override a later committed generation.
Missing declarations and discontinuous history require manual review.
An older disjoint file can be outside the current package declaration.
The new qualifier detects its alteration without modifying that file.
This API does not prove independent freshness or files outside retained declarations.
Product startup still uses the existing trust policy.
Production integration of complete history coverage remains a separate decision.

## Classification boundary

The new qualifier preserves the existing installation inspection code when the current declaration is unverified.
Its outer history state uses a conservative `RecoveryRequired` fallback.
That fallback does not prove corruption or authorize repair.
Use `InstallationTrustPolicy` to distinguish normal fresh or legacy unattested installations.
Product integration must resolve this state mapping before using the qualifier for startup decisions.
This work does not claim completion of every requested installation-state category.
