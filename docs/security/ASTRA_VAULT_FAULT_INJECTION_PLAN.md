# Astra Vault Fault Injection Test Plan (Phase E-0 — LOCKED)

**When:** Before `ProductionWriterEnabled = true`  
**Harness:** TEST ONLY; no user vaults; isolated temp containers  
**Implementation:** E-2 simulated FI + E-3 **Windows child-process kill** + E-4 **torn write / header / rollback / journal leak** closure harness. **Production writer NOT AUTHORIZED.** Phase E-4 is **not** writer enable.

## 1. Goals

Prove crash-safe commit classification matches `ASTRA_VAULT_CRASH_SAFE_COMMIT_PLAN.md` §5 and that failed commits never destroy readable previous generations.

## 2. Mandatory fault points

Inject at:

1. before object write  
2. after object write, before flush  
3. after object flush  
4. before metadata write  
5. after metadata write, before flush  
6. after metadata flush  
7. before journal write  
8. after journal write, before flush  
9. before activation header write  
10. after activation header write, before flush  
11. after activation flush, before reread  
12. after reread, before authentication  
13. after authentication, before cleanup  
14. during cleanup  

Injection methods: **E-2** `Av3ProcessKillHarness` (simulated kill), `Av3FlushFaultHarness`, disk-full / media-removal I/O simulation, post-flush truncation. **E-3** `Av3ChildProcessKillHarness` + `SmartPerformanceDoctor.AstraVault.KillWorker` (**Windows only**; `Av3KillSupportStatus` reports `UnsupportedPlatform` / `WorkerNotFound` / `Blocked` — not SKIPPED). `Av3ActualKillMatrixRunner` compares simulated vs actual; mismatch → `ManualReviewRequired`.

## 3. Mandatory verifications (each fault)

| Check | Pass criteria |
|-------|----------------|
| No plaintext leak | No cleartext object bytes in logs/temp after abort |
| No filename/path leak | Journal and disk lack user paths |
| No partial generation normal open | Unlock validator rejects unauthenticated new gen |
| No unauthenticated metadata trust | metadata graph not materialized without AEAD |
| Old generation preserved | Previous gen opens with pre-fault password |
| Failed commit survivability | Vault readable; no widened corruption |
| Secret non-leak | Password/VMK/DEK absent from exceptions/logs |
| Deterministic recovery class | Matrix outcome matches expected enum |

## 4. Test corpus structure (future)

```
tests/SmartPerformanceDoctor.Tests/Av3WriterFaultInjection/
  fixtures/          # synthetic mini-containers
  matrix.json        # fault point × expected classification
```

Golden vectors remain **read-only**; writer FI uses separate generated fixtures (not LOCKED Phase C/D corpus).

## 5. Gate linkage

`Av3PhaseGate.FaultInjectionPlanLocked = true` when this document is frozen.  
Passing FI suite is **required** but **not sufficient** for writer enable (see `ASTRA_VAULT_WRITER_GATE.md`).

## 6. Non-goals (E-0)

- No production writer tests  
- No migration fault tests (Phase H)  
- No UI automation