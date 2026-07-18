# E-11 Anchor Provider Design

## Interfaces (`Anchor/`)

- `IAv3RollbackAnchor` — Read, Verify, Prepare, Commit, Abort, ClassifyAnchorFailure
- DTOs: `Av3AnchorSnapshot`, `Av3AnchorUpdateRequest/Result`, `Av3AnchorVerificationResult`
- `Av3AnchorProviderKind`, `Av3AnchorFailureReason`, `Av3AnchorRuntimePolicy`

## Snapshot fields (public witness)

ContainerId, Generation, MonotonicCounter, WitnessDigestHex (header/root commitment witness), ProviderKind. **No** VMK/DEK/password/paths. CreatedAt/UpdatedAt **not** used for security decisions.

## Harness provider

- `Av3HarnessRollbackAnchor` — sibling `{vault}-anchor/` store, `av3-e11-` temp roots only
- `Av3AnchorGuardRegistry` — per-root concurrent update denial
- `Av3AnchorDryRunBridge` — post-commit anchor update on E-11 roots only

## Principles

- No trusted promotion before commit + post-auth
- Anchor failure ⇒ not committed for anchor posture
- Stale/rollback ⇒ `AnchorStale` / `AnchorRollbackSuspected` / `AnchorRecoveryRequired`

**Limits (E-11.1 locked):** same-disk untrusted anchor alone cannot prove full-vault rollback resistance; full vault rollback closure requires trusted monotonic anchor.