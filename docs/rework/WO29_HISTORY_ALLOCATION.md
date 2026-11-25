# History inspection allocation experiment

The isolated experiment reuses a path buffer for identity checks during history inspection. Production does not select this experiment. File identity, ACL, signatures and complete hashes remain required.

See HISTORY_ALLOCATION_RESULT.json for the paired observations, source revision, sample counts and dispersion. Do not infer product startup time from history inspection.

## 4,096 transactions

| Metric | Baseline median | Experiment median | Baseline range | Experiment range |
|---|---:|---:|---:|---:|
| ElapsedMs | 88,533.91 | 80,346.98 | 46,169.58 to 103,052.81 | 58,944.44 to 97,309.47 |
| CpuMs | 91,867.19 | 82,484.38 | 47,093.75 to 105,734.38 | 61,265.62 to 101,828.12 |
| AllocatedBytes | 3,012,904,692.00 | 1,908,865,628.00 | 3,004,639,688.00 to 3,016,888,184.00 | 1,904,504,984.00 to 1,912,412,096.00 |
| PeakSampledHandles | 12,574.00 | 12,574.00 | 12,574.00 to 12,574.00 | 12,574.00 to 12,574.00 |
| PeakSampledWorkingSetBytes | 145,647,616.00 | 143,800,320.00 | 141,238,272.00 to 146,288,640.00 | 142,381,056.00 to 144,154,624.00 |
| HandlesAfter | 272.00 | 272.00 | 272.00 to 272.00 | 272.00 to 273.00 |
| NativeIdentityCalls | 1,003,935.00 | 1,003,935.00 | 1,003,935.00 to 1,003,935.00 | 1,003,935.00 to 1,003,935.00 |
| NativeIdentityElapsedMs | 62,712.86 | 57,222.85 | 32,902.06 to 73,625.44 | 42,014.10 to 69,530.39 |
| NativeIdentityAllocatedBytes | 1,557,909,256.00 | 457,759,480.00 | 1,557,385,512.00 to 1,559,400,568.00 | 457,317,576.00 to 460,370,280.00 |

Allocated bytes changed by -36.64%. Native identity calls remained unchanged. Reused directory handles still need validation against their expected paths. Signed history metadata also stays pinned until final verification. Reuse avoids repeated allocation of the initial path buffer; it does not eliminate those checks. Phase totals overlap. Do not sum their allocations.

Each arm ran six times on the same input. Elapsed times fell in both arms during the run. This trend limits inference about speed. The experiment was not enabled in production. See the JSON for raw order, per-phase calls, standard deviations and smaller inputs.
