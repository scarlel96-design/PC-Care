#!/usr/bin/env bash
set -euo pipefail
# Restore only an independent source copy. This script does not recover vault data.
exec powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(dirname "$0")/SOURCE_ROLLBACK.ps1" -CopyRoot "${1:?Independent copy path required}"
