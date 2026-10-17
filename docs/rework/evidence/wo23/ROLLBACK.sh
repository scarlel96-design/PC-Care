#!/usr/bin/env bash
set -euo pipefail
: "${RESTORE_COPY:?set independent copy path}"
target="$(realpath "$RESTORE_COPY")"
case "$target" in */wo23-rollback-*/Rollback-engine/WindowsFileBoundary.cs) ;; *) echo INDEPENDENT_COPY_REQUIRED; exit 2;; esac
here="$(cd "$(dirname "$0")" && pwd)"
printf '%s  %s\n' 'b8b683bf5311b674fec05a9bb16810e8ee365292275f421d83831d9c8539d9da' "$here/ORIGINAL_FILE" | sha256sum -c - >/dev/null
test ! -L "$RESTORE_COPY"
cp "$here/ORIGINAL_FILE" "$target"
printf '%s  %s\n' 'b8b683bf5311b674fec05a9bb16810e8ee365292275f421d83831d9c8539d9da' "$RESTORE_COPY" | sha256sum -c - >/dev/null
echo SOURCE_COPY_RESTORED
