#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || ! -f "$1" ]]; then
  echo 'USAGE: ROLLBACK.sh MODIFIED_COPY' >&2
  exit 2
fi

source_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
baseline="$source_dir/UpdateInstallerService.original.cs"
target="$1"
modified_hash='5EE65CA9852D59FB5706D0283AE270DE0384A2871CC3FBA55466F5BA6D1FA043'
baseline_hash='5AE7614DA1A7E0CF83E37357772D908F51CFA7C886AB63D77F3C343C2339F8B2'

hash_file() { sha256sum -- "$1" | awk '{print toupper($1)}'; }
if [[ "$(hash_file "$baseline")" != "$baseline_hash" ]]; then
  echo 'BASELINE-HASH-MISMATCH' >&2
  exit 3
fi
if [[ "$(hash_file "$target")" != "$modified_hash" ]]; then
  echo 'MODIFIED-HASH-MISMATCH' >&2
  exit 4
fi
cp -- "$baseline" "$target"
if [[ "$(hash_file "$target")" != "$baseline_hash" ]]; then
  echo 'RESTORE-HASH-MISMATCH' >&2
  exit 5
fi
echo "RESTORED $baseline_hash"
