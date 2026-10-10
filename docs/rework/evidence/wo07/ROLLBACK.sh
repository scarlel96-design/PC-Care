#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./ROLLBACK.ps1 -CopyPath "${1:?Pass the independent ROLLBACK_COPY.props path}"
