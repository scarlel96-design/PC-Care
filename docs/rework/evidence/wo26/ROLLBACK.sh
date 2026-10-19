#!/usr/bin/env bash
set -euo pipefail
base="$(cd -- "$(dirname -- "$0")" && pwd)"
target="${1:?Pass the independent restored source copy}"
case "$target" in
  */wo26-rollback-*/restored/Engine/UpdateTransaction.cs) ;;
  *) echo ROLLBACK_TARGET_REJECTED >&2; exit 3 ;;
esac
[[ -f "$target" && ! -L "$target" ]]
[[ "$(sha256sum "$base/ORIGINAL_FILE" | cut -d' ' -f1)" == "225bb7d9dd3a4a8e1a06d034b52f3cb57d5ed0557a0f1929fc550d8b62edc113" ]]
cp -- "$base/ORIGINAL_FILE" "$target"
cmp -- "$base/ORIGINAL_FILE" "$target"
echo SOURCE_COPY_RESTORED
