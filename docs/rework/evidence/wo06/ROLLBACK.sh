#!/usr/bin/env bash
set -euo pipefail
# Restore only the marked independent source copy.
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec pwsh -NoProfile -File "$script_dir/SourceRollback.ps1" -FixtureDirectory "${1:?Provide the independent source-copy directory}"
