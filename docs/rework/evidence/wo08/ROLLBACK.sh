#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./ROLLBACK.ps1 -CopyPath "${1:?Pass an independent EventBuffer.cs fixture path}"
