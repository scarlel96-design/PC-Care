# WO-13 security review

Status: **PARTIAL**. Phase 1: **NO-GO**.

Branch: `codex/pccare-vault-crash-safe-recovery`.
Baseline SHA: `de7563d76f79bbe563e09ed385fcadcd5b87928e`.
Source manifest SHA-256: `140E28B09FCD66016A0F77F2AAA808CF96ACA4E8FD1536F36B8B958BB4116E7F`.
Execution environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic fixtures only.
Final source commit and CI receipt: `evidence/wo13/GITHUB_RESULT.txt`.


## Reused technology

The implementation retains .NET cryptographic APIs and the existing Argon2id dependency.
Konscious.Security.Cryptography.Argon2 1.3.0 remains unchanged. Its repository uses the MIT license.
ProtectedData 9.0.0 remains unchanged.
The existing XChaCha20-Poly1305 object format and AES-GCM key wrapping remain compatible.
No new cryptographic algorithm was designed.
The HChaCha20 implementation matched the public XChaCha draft vector.
The Internet-Draft is expired. Vector agreement is not external cryptographic certification.
Live libsodium ciphertext interoperability remains NOT TESTED.
Dependency version upgrades and a complete vulnerability inventory were not part of the executed verification.

Primary references:

- [XChaCha draft vector](https://datatracker.ietf.org/doc/html/draft-irtf-cfrg-xchacha-03)
- [libsodium XChaCha construction](https://doc.libsodium.org/secret-key_cryptography/aead/chacha20-poly1305/xchacha20-poly1305_construction)
- [.NET File.Replace contract](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0)
- [Konscious implementation](https://github.com/kmaragon/Konscious.Security.Cryptography)

## Approval consistency

The actual AV3 production gates remain false.
Stale Lab mirror approval flags and migration GO claims were corrected.
Historical package/design milestones do not grant current production authorization.
The legacy explicit source-sealing and Lab source-wipe request paths return failure before processing originals.
Tests verify original file and folder bytes remain unchanged.
Encrypted orphan objects are preserved after unauthenticated journal errors.

## Remaining P0

1. Qualify a compatible replacement for the product v5 metadata overwrite path.
2. Qualify v5 password and recovery-key transition durability.
3. Add a qualified freshness anchor for recovery-store replay prevention.
4. Extend interrupted recovery and all-operation concurrency tests.
5. Complete hostile path replacement and storage failure matrices.

The v6 used-code bitmap has HMAC protection. Four recovery-store interruptions passed.
A live two-process race rejected the second consumer. These tests do not prove power-loss durability.

External security review and power-loss durability remain uncompleted.


## Verification and restoration

Read `evidence/wo13/VERIFICATION.txt` for commands, literal output, exit codes, and artifact paths.
Read `evidence/wo13/SOURCE_HASHES.json` for the tested source bytes.
Source-copy restoration only changes an independent evidence copy.
Vault recovery opens an authenticated generation and verifies exported bytes in a separate process.
Retain the fixture before further recovery attempts.
Do not use the source rollback script on vault data.
