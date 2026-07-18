# E-13 Trusted Anchor Privacy Model

## External witness API

- Digest-only request/response (`Av3ExternalWitnessStubContract`).
- No live network calls in E-13 (stub/mock only).

## Privacy policies

- `Av3TrustedAnchorPrivacyPolicy.ExternalWitnessDigestOnly = true`
- No password / VMK / DEK / paths / filenames in witnesses or public reports.
- Public errors redacted via `Av3TrustedAnchorPublicSurface`.

## Offline / recovery

- Offline grace: read-only posture; writer trusted promotion denied without online external confirmation.
- Recovery required on full-vault rollback suspicion; **no automatic repair**.