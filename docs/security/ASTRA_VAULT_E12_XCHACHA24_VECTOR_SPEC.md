# E-12 XChaCha24 Vector Spec

**Classification:** TEST ONLY — fixture key **labels** only; no production secrets in repo.

## Corpus layout

```
test-vectors/av3/xchacha24/
  manifest.json           # corpus metadata + vector id list
  reference-vectors.json  # vector ids and kinds only (no key/nonce/ciphertext bytes)
```

## Vector ids (deterministic factory)

| Vector id | Kind | Factory |
|-----------|------|---------|
| `e12_activation_payload` | activation | `Av3XChaCha24VectorFactory.BuildActivationPayloadVector` |
| `e12_metadata_root` | metadata_root | `Av3XChaCha24VectorFactory.BuildMetadataRootVector` |
| `e12_empty_plaintext` | empty | `Av3XChaCha24VectorFactory.BuildEmptyPlaintextVector` |
| `e12_multi_segment` | multi_segment | `Av3XChaCha24VectorFactory.BuildMultiSegmentPlaintextVector` |

## Verification

- Independent decrypt: `Av3AeadVectorVerifier.VerifyDecryptPass`
- Tamper negatives: wrong key/nonce/AAD, ciphertext/tag flip
- Invariants: `Av3CryptoInvariantValidator.ValidateVectorRoundTrip`
- Phase C–D golden vectors (`test-vectors/av3/reference-output`) remain **LOCKED** and **unchanged** by E-12

## Out of scope

- User vault paths
- On-disk spd-vault mutation
- Production writer / journal / migration