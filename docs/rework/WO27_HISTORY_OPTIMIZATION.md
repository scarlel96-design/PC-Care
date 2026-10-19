# WO27 - History Optimization

Select deduplicated directory handles within the existing retain-all design. Do not release verified objects before final validation. Batch and two-pass alternatives need equivalent final identity, hash and ACL evidence. Protected snapshots need independent creation and retention validation. Neither alternative is enabled.

| Alternative | Resource effect | Trust requirement | Decision |
|---|---|---|---|
| A: Retain all objects | Linear file leases; repeated ancestor handles | Keep every accepted object stable through final approval | Retain this model; share duplicate ancestor handles |
| B: Bounded batches | Fewer concurrent file handles | Prevent replacement after a completed batch | Not enabled; equivalent final stability remains unproved |
| C: Two-phase verification | Fewer first-phase handles; repeated final reads | Revalidate signatures, identities, hashes, ACLs and transaction order without an approval gap | Not enabled; no validated replacement for retained leases |
| D: Protected snapshot | Snapshot storage and creation cost | Authenticate snapshot creation, identity, access and retention | Not enabled; no qualified snapshot provider |

The selected change bounds duplicate ancestor handles within one synchronous inspection. It does not bound all file handles independently of history size.

The thread-local scope restores its predecessor after disposal. File leases close before shared directory handles. Compare complete baseline and modified engines on identical synthetic inputs. Use ABBAAB process order. Report medians and dispersion separately for local and CI measurements.

## Final local component comparison

Local source: `cc02c95d000e352d1cf6f3fbced46a68f24fda45`.

The history implementation and reused WO25 probe match this source. The local measurement predates the final rollback-disposition fix. Final CI measures the complete final source.

Both variants use the same constructed history. Three processes per variant run in ABBAAB order. No regression process runs concurrently.

| Metric | Baseline median | Modified median |
|---|---:|---:|
| ElapsedMs | 47566.12 | 42538.99 |
| CpuMs | 73750.00 | 62906.25 |
| AllocatedBytes | 2803470504.00 | 3082823224.00 |
| PeakSampledWorkingSetBytes | 155684864.00 | 146235392.00 |
| PeakSampledHandles | 49480.00 | 12587.00 |
| HandlesAfter | 285.00 | 285.00 |

The cumulative managed allocation increases. This result is not an allocation improvement.

The final design also pins primary and previous journal candidates through approval. It retains more handles than the exploratory candidate.

Both variants return 285 process handles after inspection. This count includes runtime and sampler handles.

The resource fixture uses strictAcl=false. Separate ACL tests do not qualify full 4,096-entry protected-root performance.

Actual WinUI cold start and low-end physical hardware remain NOT TESTED.

## Final automated qualification

Source: `f6d29413a77c7522210de6eba3b1dbae745b2365`.

CI: [39197282336 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39197282336).

App: 1445 PASS / 0 FAIL / 111 SKIP. New WO27: 16 PASS, included.

SecurityLab: 36 PASS, separate. Restore and Release: 0 warnings / 0 errors.

Actual guest deployment, privileged recovery, UAC, reboot, GUI and physical durability remain NOT TESTED.

## Final CI component comparison

Final source: `f6d29413a77c7522210de6eba3b1dbae745b2365`. Three samples per variant use ABBAAB order.

| Metric | Baseline median | Modified median | Baseline range | Modified range |
|---|---:|---:|---|---|
| ElapsedMs | 21811.03 | 21763.59 | 21436.51..21933.97 | 21689.68..22774.90 |
| CpuMs | 24625.00 | 24156.25 | 23828.12..24718.75 | 23859.38..25656.25 |
| AllocatedBytes | 1982848768.00 | 2141241968.00 | 1981706592.00..1983256328.00 | 2140828944.00..2146168936.00 |
| PeakSampledWorkingSetBytes | 134533120.00 | 133218304.00 | 134434816.00..134676480.00 | 133128192.00..133779456.00 |
| PeakSampledHandles | 28958.00 | 12563.00 | 28958.00..28961.00 | 12560.00..12563.00 |
| HandlesAfter | 263.00 | 266.00 | 263.00..266.00 | 263.00..266.00 |

CI elapsed-time ranges overlap. A repeatable elapsed-time improvement is not established.
Managed cumulative allocation increases in both environments. Handle reduction is verified within this constructed fixture.
The fixture uses strictAcl=false. Guest installation and full protected-root performance remain NOT TESTED.
Phase timers overlap. Do not sum their elapsed times or allocation counts.
NativeIdentityCheck counts boundary checks, not every operating-system API call. A dedicated ACL-query count is NOT TESTED.
