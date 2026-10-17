#!/usr/bin/env bash
set -euo pipefail
: "${RESTORE_COPY:?independent source copy required}"
test ! -L "$RESTORE_COPY"
target="$(realpath "$RESTORE_COPY")"
case "$target" in */wo24-rollback-*/restored.ps1) ;; *) echo INDEPENDENT_COPY_REQUIRED; exit 2;; esac
here="$(cd "$(dirname "$0")" && pwd)"
printf '%s  %s\n' '39a9fbd7b9953da817048c638ea51c89d7e35fe509951c1610eef26e4bbdb377' "$here/ORIGINAL_FILE" | sha256sum -c - >/dev/null
cp "$here/ORIGINAL_FILE" "$target"
printf '%s  %s\n' '39a9fbd7b9953da817048c638ea51c89d7e35fe509951c1610eef26e4bbdb377' "$target" | sha256sum -c - >/dev/null
echo SOURCE_COPY_RESTORED
