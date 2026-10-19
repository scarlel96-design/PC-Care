# WO27 - Midcopy Crash Matrix

The synthetic profile observes completed native writes through the final bound handle. RandomAccess.Write submits 4KiB blocks. The parent kills the worker after its durable checkpoint receipt. A separate process reads target and backup identities, lengths, full hashes, ACLs and journals.

Record actual byte counts for every requested boundary. Small files can map several offsets to one block. DuringRollbackCopy remains an after-return checkpoint. Native write completion and Flush do not prove physical power-loss durability.

Reference: [Microsoft API contract](https://learn.microsoft.com/en-us/dotnet/api/system.io.randomaccess.write?view=net-10.0).

The probe records only the *.tmp residue count. Complete residue inventory remains NOT TESTED.

Re-termination inside reconciliation remains NOT TESTED.

## Direct default-copy qualification

The default-copy worker does not configure the observed-copy hook. It executes the existing FileStream.CopyTo path.

The parent reads copied prefix and unchanged tail markers through a read-only handle. It then terminates the worker.

A new reader scans the entire repeated-pattern file. It records the exact leading original-byte extent and verifies the remaining updated-byte pattern.

Requested thresholds are lower bounds. Report actual post-termination extents, not the requested thresholds, as copied byte counts.

Four 64MiB cases supplement the 47 precisely parked observer-profile cases. Process termination does not qualify physical power-loss durability.

## Final automated qualification

Source: `f6d29413a77c7522210de6eba3b1dbae745b2365`.

CI: [39197282336 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39197282336).

App: 1445 PASS / 0 FAIL / 111 SKIP. New WO27: 16 PASS, included.

SecurityLab: 36 PASS, separate. Restore and Release: 0 warnings / 0 errors.

Actual guest deployment, privileged recovery, UAC, reboot, GUI and physical durability remain NOT TESTED.
