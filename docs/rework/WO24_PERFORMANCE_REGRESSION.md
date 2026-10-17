# WO24 — Performance regression

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

Keep the 512-character initial path buffer and bounded retry.
Keep file ID, ACL, signature, hash and Flush checks.
Run the retained allocation and long-path regressions.
The CI-only strict-apply probe uses a new protected synthetic directory and the existing updater.
It records elapsed time, CPU, total allocation, Working Set and existing phase counters.
This single strict-ACL measurement does not establish a performance improvement.
It does not measure a deployed PE verification cost.
The earlier replacement IOException remains unresolved; its verified rollback evidence is preserved in WO23.

## Measurement boundaries

Elapsed time, CPU time and allocation cover the `Apply` call only.
The history check runs after that timed interval.
Peak Working Set is the process lifetime peak sampled after the history check.
It includes process startup and can include the history check.
Phase measurements can overlap and must not be summed as independent time.
The strict trial uses synthetic package signatures, not deployed Authenticode PE execution.
WinVerifyTrust correctness tests do not establish a separate WinVerifyTrust cost measurement.
The strict-ACL result has one input and one CI host sample.
It is not a matched comparison with the older local allocation benchmark.

## Earlier replacement failure

The preserved WO23 trial returned `ROLLED-BACK` after a file replacement IOException.
Its recovery verified 130 original hashes and the absence of 128 transaction-created files.
The stored error lacks a native error code, stack trace and exact replacement checkpoint.
The preserved evidence does not identify an external lock holder.
It does not establish an antivirus cause.
Later successful trials do not close that root-cause investigation.
Keep the failure open until a traced reproduction identifies the failing operation and native error.
Read `evidence/wo23/PERFORMANCE_TRIAL_FAILURE.json` for the original result.

## CI fixture correction

Run 39002912393 failed before the strict update trial.
The fixture supplied a temporary work directory that strict mode rejects.
The corrected fixture uses the unchanged fixed protected staging contract in disposable CI only.
It rejects an existing product staging root.
No product location, ACL or signature policy was relaxed.
Read `evidence/wo24/CI_FIXTURE_FAILURE.json` for the actual failure.

## Volume boundary

The protected staging root and installation must use the same volume for new-file publication.
The failed CI trial used a D: installation and the default protected staging path.
Its exception and resolved staging root were not retained.
The cross-volume explanation remains a source-supported hypothesis for that failed run.
The existing `PublishOpenedFile` guard rejects that condition before the rename.
The engine reported rollback, but that run did not independently verify rollback bytes.
The corrected normal trial uses a new fixture on the staging volume.
A source-preservation regression verifies rejection without creating the foreign destination.
The test does not establish complete cross-volume update support.
The earlier WO23 replacement IOException remains a separate unresolved failure.

## Actual strict-ACL CI sample

```json
{
  "Code": "COMMITTED",
  "Stage": "Committed",
  "Failure": "",
  "RestoredOriginals": null,
  "NewFilesAbsent": null,
  "InstallVolume": "C:\\",
  "StagingVolume": "C:\\",
  "ElapsedMs": 39356.5157,
  "CpuMs": 14156.25,
  "AllocatedBytes": 843030760,
  "PeakWorkingSetBytes": 218406912,
  "StrictAcl": true,
  "History": {
    "Verified": true,
    "State": "InstallationVerified",
    "Code": "RETAINED-SIGNED-HISTORY-VERIFIED",
    "Transactions": 1,
    "Files": 258
  },
  "Phases": [
    {
      "Name": "NativeIdentityCheck",
      "Calls": 237638,
      "ElapsedMs": 7926.3334,
      "AllocatedBytes": 352402640
    },
    {
      "Name": "FileCreateAndBind",
      "Calls": 1299,
      "ElapsedMs": 4744.1516,
      "AllocatedBytes": 41953312
    },
    {
      "Name": "FileIdentityValidation",
      "Calls": 16324,
      "ElapsedMs": 3820.237,
      "AllocatedBytes": 175831568
    },
    {
      "Name": "AclRead",
      "Calls": 2987,
      "ElapsedMs": 1239.4592,
      "AllocatedBytes": 61905584
    },
    {
      "Name": "AclAndBoundary",
      "Calls": 2734,
      "ElapsedMs": 2886.7811,
      "AllocatedBytes": 143904040
    },
    {
      "Name": "FileOpenAndBind",
      "Calls": 2726,
      "ElapsedMs": 1881.9995,
      "AllocatedBytes": 84049208
    },
    {
      "Name": "PackageVerification",
      "Calls": 1,
      "ElapsedMs": 74.3776,
      "AllocatedBytes": 1621664
    },
    {
      "Name": "JournalSerialization",
      "Calls": 779,
      "ElapsedMs": 266.5422,
      "AllocatedBytes": 102203328
    },
    {
      "Name": "JournalFlush",
      "Calls": 779,
      "ElapsedMs": 7771.8716,
      "AllocatedBytes": 16304
    },
    {
      "Name": "JournalRename",
      "Calls": 779,
      "ElapsedMs": 679.3022,
      "AllocatedBytes": 386160
    },
    {
      "Name": "JournalTotal",
      "Calls": 779,
      "ElapsedMs": 22917.8007,
      "AllocatedBytes": 413632784
    },
    {
      "Name": "PayloadFlush",
      "Calls": 647,
      "ElapsedMs": 2903.2663,
      "AllocatedBytes": 56848
    },
    {
      "Name": "CopyAndFlush",
      "Calls": 389,
      "ElapsedMs": 5421.6219,
      "AllocatedBytes": 52088384
    },
    {
      "Name": "HashAndIdentity",
      "Calls": 1940,
      "ElapsedMs": 1064.0284,
      "AllocatedBytes": 183009552
    },
    {
      "Name": "ExtractionAndVerification",
      "Calls": 258,
      "ElapsedMs": 5203.3856,
      "AllocatedBytes": 107088616
    },
    {
      "Name": "BackupAndIdentity",
      "Calls": 258,
      "ElapsedMs": 3365.8105,
      "AllocatedBytes": 75588008
    },
    {
      "Name": "ReplacementAndVerification",
      "Calls": 258,
      "ElapsedMs": 16728.8786,
      "AllocatedBytes": 262635776
    },
    {
      "Name": "PostApplyFileVerification",
      "Calls": 258,
      "ElapsedMs": 224.5094,
      "AllocatedBytes": 11955824
    }
  ],
  "Scope": "CI protected synthetic files and fresh fixed ProgramData staging; no PE deployment or UAC"
}
```

This one sample verifies Commit and 258 retained-history file checks.
It does not establish comparative performance or physical-hardware suitability.
