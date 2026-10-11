#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
exec powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$here/SOURCE_ROLLBACK.ps1" -TargetRoot "$1"
