#!/usr/bin/env bash
set -euo pipefail
base="$(cd -- "$(dirname -- "$0")" && pwd)"
target="${1:?Pass the independent restored source copy}"
case "$target" in
  */wo25-rollback-*/restored/Engine/InstallationHistoryQualification.cs) ;;
  *) echo ROLLBACK_TARGET_REJECTED >&2; exit 3 ;;
esac
[[ -f "$target" && ! -L "$target" ]]
[[ "$(sha256sum "$base/ORIGINAL_FILE" | cut -d' ' -f1)" == "56dda72a5f3f0573e9a92f26787e34d41131f3c54b9463162c68431ea90e9a4d" ]]
cp -- "$base/ORIGINAL_FILE" "$target"
cmp -- "$base/ORIGINAL_FILE" "$target"
echo SOURCE_COPY_RESTORED
