#!/usr/bin/env bash
set -euo pipefail
base="$(cd -- "$(dirname -- "$0")" && pwd)"
target="${1:?Pass the independent restored source copy}"
case "$target" in
  */wo27-rollback-*/restored/Engine/UpdateTransaction.cs) ;;
  *) echo ROLLBACK_TARGET_REJECTED >&2; exit 3 ;;
esac
[[ -f "$target" && ! -L "$target" ]]
[[ "$(sha256sum "$base/ORIGINAL_FILE" | cut -d' ' -f1)" == "6ef35e5023aa38f77636a100919d0d9587f7b67da2ab0a2d76bec466f85e7384" ]]
cp -- "$base/ORIGINAL_FILE" "$target"
cmp -- "$base/ORIGINAL_FILE" "$target"
echo SOURCE_COPY_RESTORED
