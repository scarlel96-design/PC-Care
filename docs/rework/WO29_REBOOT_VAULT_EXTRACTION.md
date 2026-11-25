# Reopen and extract the synthetic v5 vault

The Windows process probe reopened the vault in 33 checks. Each check compared four authenticated outputs. Fixture sizes were 0 B, 4 KiB, 1 MiB and 4 MiB. All 132 comparisons matched. These are four files checked repeatedly. See VAULT_EXTRACTION_RESULT.json.

Windows reboot, physical power loss and recovery-code consumption were not tested. Keep them separate from process termination.
