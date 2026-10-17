# WO23 — Windows security qualification

Baseline: `db8defce0764dd2421ceb78b9e3ce6b851fa9e6b`.
Branch: `codex/pccare-wo23-windows-security-update-hardening`.

## Execution boundary
Run the security script only on a GitHub-hosted Windows runner.
The script checks GITHUB_ACTIONS, RUNNER_ENVIRONMENT and RUNNER_OS before creating test resources.
It records administrator context, filesystem type and signing capabilities.
Public evidence redacts the runner SID.

## Actual checks
The Windows probe creates an isolated directory under RUNNER_TEMP.
It verifies an Administrators owner, protected DACL and inherited file access rules.
It rejects a non-administrator write ACE through the production ACL validator.
It tests retained-handle replacement rejection, file ID replacement, hardlinks, symbolic links, junctions and delete disposition.
A symbolic-link privilege failure remains NOT TESTED.

## Limits
These checks execute Windows APIs on a disposable runner.
They do not establish an independent standard-user security boundary.
Restricted-token execution, interactive UAC, installed product qualification and physical power loss remain separate requirements.
Read `evidence/wo23/WINDOWS_ACL_RESULT.json` and `ENVIRONMENT_RESULT.json`.
