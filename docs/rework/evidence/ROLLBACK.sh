#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
source_file="$script_dir/UpdatePackageInspector.original.cs"
target_file="${1:?usage: ROLLBACK.sh TARGET_COPY}"
expected="b0624cb57c6df8580aa87041b018b01e14e9c29de270f06e844a7ab5a743360c"

test -f "$source_file"
test -f "$target_file"
actual="$(sha256sum "$source_file" | awk '{print $1}')"
test "$actual" = "$expected"
cp -- "$source_file" "$target_file"
restored="$(sha256sum "$target_file" | awk '{print $1}')"
test "$restored" = "$expected"
printf 'restored_sha256=%s\n' "$restored"
