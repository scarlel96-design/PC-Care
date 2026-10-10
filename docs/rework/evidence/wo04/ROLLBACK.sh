#!/usr/bin/env bash
set -euo pipefail

here="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
source_file="$here/UpdateTransaction.original.cs"
target="${1:?pass an independent copy of UpdateTransaction.cs}"
repo_source="$here/../../../../src/SmartPerformanceDoctor.UpdateEngine/UpdateTransaction.cs"

[[ -f "$source_file" && -f "$target" ]] || { echo 'MISSING_INPUT' >&2; exit 2; }
[[ -f "$repo_source" ]] || { echo 'MISSING_REPOSITORY_SOURCE' >&2; exit 2; }
[[ ! "$target" -ef "$repo_source" && "$(realpath -- "$target")" != "$(realpath -- "$repo_source")" ]] || {
  echo 'REFUSE_TO_REPLACE_MODIFIED_FILE' >&2
  exit 3
}
cp -- "$source_file" "$target"
actual="$(sha256sum -- "$target" | cut -d ' ' -f 1 | tr '[:lower:]' '[:upper:]')"
expected='5BA25D5372C348F14FEFCAF8C95A7D4290083E5511A7B08256502DBB52B1AAB4'
[[ "$actual" == "$expected" ]] || { echo "HASH_MISMATCH $actual" >&2; exit 4; }
echo "RESTORED $actual"
