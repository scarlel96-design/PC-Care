# Astra Vault Security Review Questionnaire

**Phase E-5** — answer before writer enable discussion. Default expectation: **NO-GO**.

## A. Scope and gates

1. Is AV3 explicitly documented as NOT PRODUCTION in all user-facing materials?
2. Are `ProductionWriterEnabled`, `JournalWriterEnabled`, and `MigrationEnabled` provably false in release builds?
3. Is legacy spd-vault on-disk format unchanged by AV3 code paths?

## B. Crash safety and durability

4. Does the state machine in `ASTRA_VAULT_CRASH_SAFE_COMMIT_PLAN.md` cover all single-fault crash points?
5. Is post-flush reread + AEAD authentication the **only** path to trust a new generation?
6. Are torn writes at locator/journal/header/metadata boundaries classified fail-closed (R1 evidence)?

## C. Header and repair

7. Are 3-copy conflicts classified without automatic silent repair?
8. Can equal-generation conflicting metadata roots be detected at unlock?

## D. Rollback and anchor

9. Is the full-vault rollback limitation clearly disclosed?
10. Does the anchor model avoid storing secrets, paths, and filenames?
11. What is the fail-closed behavior for `AnchorMismatch` / `AnchorRecoveryRequired`?

## E. Journal confidentiality

12. Does journal v1 forbid cleartext paths, passwords, VMK, DEK?
13. Are public error surfaces scanned for leak tokens (R11 harness)?

## F. Crypto

14. Is CURRENT 12-byte ChaCha labeled BELOW S-CLASS?
15. Does the XChaCha24 plan preserve LOCKED golden vectors for CURRENT suite?
16. Is nonce uniqueness argument sufficient for TARGET 24-byte nonces?

## G. API and implementation boundary

17. Are production writer APIs interface-only with no accidental DI in App layer?
18. Does `IAv3WritePolicy` forbid default origin deletion and auto migration?

## H. Migration and legacy

19. Is R9 migration isolated to Phase H with no writer collision?
20. Is user data destructive action non-default?

## I. Operational

21. Is there a documented disable/rollback plan for writer flags?
22. Who signs `ASTRA_VAULT_WRITER_ENABLE_DECISION_RECORD.md`?

## Verdict block (reviewer)

| Item | Response |
|------|----------|
| Findings severity count | P0 / P1 / P2 |
| Writer enable recommendation | GO / CONDITIONAL GO / **NO-GO** |
| S-Class blockers remaining | List |
| Sign-off date | |