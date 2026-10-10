# WO03 publisher trust policy

Package integrity and publisher authority are separate results. `PublisherVerified` requires a valid signature under an exact `(Publisher, KeyId, Channel)` key in `PublisherTrustPolicy`. Valid file hashes with an unknown key yield `ContentVerifiedOnly` and `CanApply=false`; invalid signatures or content yield `VerificationFailed`. A malformed package never becomes trusted via a legacy fallback.

`PublisherTrustPolicy.Production` is deliberately empty. There is no embedded test root, private key, environment override or unsigned release bypass. The test fixture creates a temporary P-256 key in memory, signs test ZIPs and injects only its public half into the test policy; the production policy rejects that package. Official key creation, custody, rotation/revocation, secure public-key insertion and a real signed release are external release gates. The package signer and production publication route are not active yet.

For runtime binaries, the Authenticode result, trusted publisher result, and execution decision remain distinct. Debug builds outside Program Files may run unsigned for local development; Release builds block sensitive execution unless their code signature verifies against the configured publisher policy. Package signing and Windows code signing serve different trust boundaries.
