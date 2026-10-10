# WO03 runtime trust policy

`RuntimeTrustState` now exposes three independent results: `CryptographicVerificationResult`, `PublisherTrustResult` and `ExecutionPolicyDecision`. The Authenticode evaluator runs even when the consumer warning UI is silent. `RuntimeTrustPolicy.Decide` allows a verified signature from the trusted publisher; Debug builds outside Program Files also have a local unsigned development allowance, without claiming Verified. Release binaries with absent/failed publisher trust stay blocked for sensitive feature gates.

The pinned runtime artifact list includes the new helper. `UnsignedAllowed` remains a descriptive development state rather than a cryptographic claim. User-facing update inspection reports content-verified unsigned packages as inapplicable. Official code-signing availability and installed-package compatibility still need production review and VM testing; this policy does not sign binaries.
