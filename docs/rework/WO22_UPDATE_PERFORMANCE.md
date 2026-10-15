# WO22 — Update performance

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo22-recovery-state-product-reliability`.
Baseline: `cc0fa735a4b1faaa7d72595e131372257e87f225`.
Read `evidence/wo22/VERIFICATION.txt` for tested source, commands, outputs and publication evidence.

## Safe changes

The engine reuses the already bound prepared-target dictionary for post-apply validation.
It removes nested repeated collection searches.
SignedPackage reuses one pooled hash buffer across entries.
Every entry still receives a complete SHA-256 check.
Sensitive buffer contents are cleared before pool return.
No file ID, protected ACL, signature, journal or Flush boundary was removed.

## Measurement design

The baseline and modified engines compile against the same isolated performance probe.
The baseline receives only identical opt-in measurement scopes.
The comparison uses one 258-file random package and interleaved repeated trials.
Each trial starts from a copied clean synthetic installation.
Each trial verifies every installed file hash.
CPU and allocation intervals cover Apply. Independent post-verification is excluded from CPU timing.
Whole-process peak memory includes startup.
Nested phase timings overlap and must not be summed.
Strict production ACL cost is not measured in strictAcl=false fixtures.
Read `PERFORMANCE_RESULT.json` for medians, trial values, input hashes and source receipts.
A small elapsed difference alone does not establish a general performance improvement.
Storage capacity and PC speed are separate measurements.

## Final verification

Source: `14ebea0b79d8df25f17a223cb6730bf5c95a21ee`.
Windows CI: [38768349299](https://github.com/scarlel96-design/PC-Care/actions/runs/38768349299).
App: 1333 PASS,0 FAIL,111 SKIP. SecurityLab: 36 PASS,0 FAIL,0 SKIP.

## Matched observations

```json
{
  "Baseline": {
    "ElapsedMs": 21123.8823,
    "CpuMs": 19531.25,
    "AllocatedBytes": 20347427344,
    "PeakWorkingSetBytes": 206381056
  },
  "Modified": {
    "ElapsedMs": 19994.2344,
    "CpuMs": 18859.375,
    "AllocatedBytes": 20280419464,
    "PeakWorkingSetBytes": 208134144
  }
}
```

The final journal still requires 779 durable writes and 779 Flush calls.
No general CPU or memory improvement is established.
