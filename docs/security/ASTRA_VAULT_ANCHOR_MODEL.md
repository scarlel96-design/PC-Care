# Astra Vault Anchor Model (Phase E-5 — design only)

**Status:** E-11 harness **candidate** — `ProductionAnchorImplementationCandidate=true`; production route `ProductionAnchorImplemented=false`  
**AV3:** NOT PRODUCTION

## Problem

If an attacker or backup tool restores the **entire vault directory** to an older snapshot, internal cryptographic fields may remain **internally consistent** at that older generation. Without an independent monotonic witness, the system cannot distinguish legitimate old state from rollback attack (R10 limitation).

**Without an anchor, complete full-vault rollback detection is impossible.** This is a documented limitation, not a bug to be silently ignored.

## Role of anchor

- Auxiliary trust point **outside** the mutable vault tree (or TPM/DPAPI-protected local store).
- Stores **no** password, VMK, DEK, filenames, or paths.
- Anchor logs: container id, monotonic counter, optional signature metadata — public classes only.

## Design options

| Option | Notes |
|--------|-------|
| Local trusted monotonic anchor | Counter file on same machine with restricted ACL + durability |
| Windows DPAPI / TPM-backed local anchor | Binding to machine/user; recovery implications |
| Signed local anchor file | Ed25519 signature over (container_id, generation, timestamp) |
| Optional user-controlled external anchor | USB/removable recovery stick; user opt-in |
| Removable recovery anchor | Offline copy for disaster recovery |
| Cloud anchor | **Default disabled** — requires privacy review before any enable |

## Evaluation outcomes (design enum)

`Av3AnchorStatus` in `WriterDesign/Av3AnchorStatus.cs`:

- `AnchorUnavailable` — no anchor configured or readable
- `AnchorMismatch` — anchor disagrees with observed vault generation
- `AnchorRollbackSuspected` — anchor monotonicity violated
- `AnchorFresh` — anchor agrees with authenticated vault head
- `AnchorStale` — anchor behind vault (may be normal during commit)
- `AnchorUnsupported` — platform/policy cannot use requested anchor type
- `AnchorDisabledByUser` — explicit user opt-out
- `AnchorRecoveryRequired` — anchor corrupt; fail-closed until operator action

## Policies

- Anchor corruption → **fail-closed** open or manual recovery per `AnchorRecoveryRequired`.
- Anchor is **not** a second root of trust for secrets — only generation/monotonic witness.
- S-Class target: external anchor **or** trusted local monotonic anchor required (`Av3AnchorPolicy.SClassRequiresExternalOrTrustedLocalAnchor`).

## Relation to writer

- Writer updates anchor **after** post-flush authentication (future phase).
- Read-only unlock may consult anchor when implemented; E-5 has no I/O.

## Related

- `Av3AnchorPolicy` (E-4 harness documentation)
- `ASTRA_VAULT_PRODUCTION_WRITER_DESIGN.md` §14