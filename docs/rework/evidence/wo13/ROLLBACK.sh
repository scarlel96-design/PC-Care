#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
[ "$#" -eq 1 ] || { echo "Independent evidence target required"; exit 2; }
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$script_dir/ROLLBACK.ps1" -Target "$1"
