#!/usr/bin/env bash
set -euo pipefail
if [ "$#" -ne 1 ]; then
  echo 'Usage: ROLLBACK.sh SOURCE_FIXTURE_DIRECTORY' >&2
  exit 2
fi
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
exec powershell.exe -NoProfile -File "$script_dir/SourceRollback.ps1" "$1"
