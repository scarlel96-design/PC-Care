# WO26 — Transaction Reconciliation

Baseline: `1a4fd0811f263f35a94389791860d374c710759f`.
Verification source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
Branch: `codex/pccare-wo26-transaction-recovery-volume-staging`.

## Read-only observation
`ReconcileRecovery` creates no journal and changes no installed file.
It checks installation and transaction identities, protected ACLs where required, signed package scope, backups and current contents.
It rejects multiple pending transactions and incomplete recovery scope.
A valid original with a pending journal remains `RecoveryRequired`.
An unknown current hash remains `ManualReviewRequired`.
A blocked read remains `RecoveryDeferred`.
A damaged terminal original remains a review result.

`CanRequestRecovery` is an observation. It grants no execution authority.
`ResumeVerifiedRecovery` repeats observation and uses the existing exclusive update gate.
It repeats the recovery-input checks under that gate before calling existing `RollbackCore`.
The helper uses this path for recovery and pre-apply recovery.
The existing legacy-approved restore path retains its separate approval contract.
The low-level legacy `Recover` API remains for existing callers and regression fixtures.
It is not the new verified product entry point.

Expired or missing retained packages cause review rather than unsigned recovery.
This policy can reduce availability for old pending transactions.
Promoted legacy transactions require their legacy approval flow when their package scope differs.
No new approval reservation or automatic approval is created.

Recovery termination checkpoints include AfterRollbackStarted, DuringRollbackCopy and BeforeRestoredJournal.
Each trial starts from a separately terminated apply process.
The next process rechecks status, explicitly resumes and verifies original bytes.
Process termination does not prove power-loss durability.

The inherited DuringRollbackCopy checkpoint runs after FileLease.CopyFrom returns.
It precedes the following target verification and restored-entry journal write.
It does not interrupt a byte copy midway.
Large-file partial-copy interruption remains NOT TESTED in the new process matrix.
Unknown current contents remain blocked for manual review.

## Authentication boundary

The package signature authenticates the update payload.
Recorded original hashes rely on the existing protected journal and backup boundary.
No new cryptographic journal authentication or independent freshness source is added.
Non-strict fixtures test control flow and file identity. They do not prove production ACL isolation.
Strict recovery checks the current protection policy. It does not compare an archived exact DACL snapshot.

## Final automated qualification

Source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
CI: [39166559052 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39166559052).
App: 1429 PASS / 0 FAIL / 111 SKIP.
New WO26: 21 PASS, included in app total.
SecurityLab: 36 PASS, separate.
Restore and Release: 0 warnings / 0 errors.
Actual product GUI, UAC, independent SID and reboot remain NOT TESTED.
