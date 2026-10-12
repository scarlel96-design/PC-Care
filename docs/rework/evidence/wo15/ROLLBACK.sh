#!/usr/bin/env bash
set -eu
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
exec powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$script_dir/ROLLBACK.ps1")" "$@"
