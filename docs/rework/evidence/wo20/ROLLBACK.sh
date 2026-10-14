#!/usr/bin/env bash
set -euo pipefail
test "$#" -eq 1
python "$(dirname "$0")/rollback_copy.py" "$1"
