# AV3 Format Test Vectors (Phase D — LOCKED)

## Status

| Item | Value |
|------|--------|
| Phase | **D — metadata.root ciphertext vectors locked** |
| Classification | **TEST ONLY** (never production secrets) |
| AV3 writer/journal/migration | **NOT AUTHORIZED** |
| Legacy spd-vault | **BELOW S-CLASS** |
| S-Class target | **NOT YET SATISFIED** |

## Layout

```
test-vectors/av3/
  xchacha24/                # E-12 XChaCha24 corpus (vector ids only — see ASTRA_VAULT_E12_XCHACHA24_VECTOR_SPEC.md)
    manifest.json
    reference-vectors.json
  reference-input.json      # deterministic fixture (TEST ONLY secrets)
  reference-output/
    manifest.json           # SHA-256 locks + provenance
    locator.bin
    header-copy-{0,1,2}.bin
    password-slot-*.bin
    activation-payload-*.bin
    metadata-root-descriptor.bin
    metadata-root-aad.bin
    metadata-root-plaintext.bin
    metadata-root-ciphertext.bin
    metadata-root-tag.bin
    metadata-root-commitment-preimage.bin
    metadata-root-commitment.bin
    metadata-root-expected-result.json
    vmk-unwrap-aad.bin
    expected-errors.json
    provenance.json
```

## Generator

- Tool: `tools/astra-vault-vector-gen/` (`astra-vault-vector-gen`)
- **Does not** reference `SmartPerformanceDoctor.AstraVault` or production writers/parsers.
- **Does not** read user vaults or network.
- Writes **only** under `reference-output/`.
- **Not** shipped in release packages.

Regenerate (maintainers only):

```bash
dotnet run --project tools/astra-vault-vector-gen/AstraVaultVectorGen.csproj -c Release -- test-vectors/av3
```

## Validation

- `Av3GoldenVectorTests` / `Av3MetadataRootTests` — manifest hash lock, metadata AEAD path, tamper negatives, determinism.
- Production `ReadOnlyUnlockValidator` validates golden output (read-only, no graph/manifest materialization).

## Out of scope (Phase D)

- metadata graph / manifest / filename materialization → **later phases**
- production writer / journal / migration → **NOT AUTHORIZED**