# E-13 Trusted Anchor Provider Contract

## Types

- `IAv3TrustedAnchorProvider`
- `Av3TrustedAnchorProviderKind`
- `Av3TrustedAnchorWitness`
- `Av3TrustedAnchorRequest`
- `Av3TrustedAnchorVerification`
- `Av3TrustedAnchorCommitResult`
- `Av3TrustedAnchorFailureReason`
- `Av3TrustedAnchorPolicy`
- `Av3TrustedAnchorPrivacyPolicy`
- `Av3TrustedAnchorOfflinePolicy`
- `Av3TrustedAnchorRecoveryPolicy`

## Witness fields (digest-only)

`VaultId`, `AnchorId`, `Generation`, `MonotonicCounter`, `PreviousWitnessDigest`, `CurrentWitnessDigest`, `HeaderRootDigest`, `MetadataCiphertextDigest`, `ActivationDigest`, `ProviderKind`, `MachineBindingState`, `ExternalWitnessState`, `OfflineGraceState`, `RecoveryState`.

## Prohibited storage

- No passwords, VMK/DEK, user filenames/paths, or object paths in witness stores or public errors.

## Production route

`Av3TrustedAnchorRuntimePolicy.ProductionTrustedAnchorRouteEnabled = false` until future explicit gate.

**Contract status:** **LOCKED / SIGNED** (E-13.1 sign-off); production enable and live witness still **OPEN**.