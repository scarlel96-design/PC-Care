#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
dir="$(mktemp -d)"
trap 'rm -rf -- "$dir"' EXIT
p=src/SmartPerformanceDoctor.UpdateEngine/RecoveryReconciliation.cs
mkdir -p "$dir/$(dirname "$p")"
cp "$here/MODIFIED_FILE" "$dir/$p"
observe() {
  if grep -Fq 'ReadCheckpoint?.Invoke("JournalLoaded"' "$1"; then echo checkpoint-present; else echo checkpoint-absent; fi
}
test "$(observe "$here/ORIGINAL_FILE")" = checkpoint-absent
echo "BASELINE: $(observe "$here/ORIGINAL_FILE")"
test "$(observe "$dir/$p")" = checkpoint-present
echo "MODIFIED: $(observe "$dir/$p")"
(cd "$dir" && patch -s -R -p1 < "$here/DIFF_FILE")
cmp "$here/ORIGINAL_FILE" "$dir/$p"
test "$(observe "$dir/$p")" = checkpoint-absent
echo "RESTORED: $(observe "$dir/$p")"
sha256sum "$dir/$p" | cut -d ' ' -f1
