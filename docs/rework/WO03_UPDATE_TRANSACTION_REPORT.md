# WO03 update transaction

The coordinator records Prepared → PublisherVerified → Staged → BackupVerified → Applying → PostApplyVerification → Committed. Exceptions enter RollbackPending → RollingBack → Restored or RecoveryRequired. Each job has a GUID and a JSON journal written to a temporary file, flushed with `Flush(true)`, then moved over the prior journal. A single exclusive work-root lock serializes apply/recover. An incomplete prior job prevents direct new application.

Before mutation, it records each target's existence, length and SHA-256 and verifies copied backups. It creates a temporary target file, verifies it, then replaces the target and updates the journal. It verifies all installed payload hashes and the helper checks version plus required components before writing Committed. Files absent from the signed list, including user settings and vault data, are not traversed or copied by this engine.

Committed journals retain the signed PackageId. An application attempt using an already committed PackageId returns `REPLAY` before staging or touching installed files. This protects the package identity check even if a caller supplies an obsolete current-version value.

`Recover` ignores committed/restored jobs, marks untouched pre-apply jobs restored, and rolls back an interrupted applying job. A damaged or missing backup yields RecoveryRequired. The helper runs recovery before a new apply and stops for review when recovery actually restored files. Power loss between filesystem mutation and durable journal writes, directory durability, target path races and Windows file locks still require disposable-VM testing; this is not a claim of atomic multi-file install.
