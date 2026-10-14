#!/usr/bin/env bash
set -euo pipefail
target="$(realpath -- "$1")"
[[ "$(basename -- "$target")" == RESTORE_COPY ]] || exit 2
[[ "$(basename -- "$(dirname -- "$target")")" == wo18-rollback-* ]] || exit 2
[[ -f "$target" && ! -L "$target" ]] || exit 2
hash="$(sha256sum -- "$target" | cut -d' ' -f1)"
[[ "$hash" == 6a915f84716f861241c35ebf106acdcc6508950172ea9de9799e7b8bb4f5625c || "$hash" == 0e117981c393bc0e9822328047d6f9fbbd4a2b4f4fded5b91c48a0c00ce47ae3 ]] || exit 3
cp -- "$(dirname -- "$0")/ORIGINAL_FILE" "$target"
[[ "$(sha256sum -- "$target" | cut -d' ' -f1)" == 6a915f84716f861241c35ebf106acdcc6508950172ea9de9799e7b8bb4f5625c ]] || exit 4
echo ROLLBACK_SHA256_RESTORED=6a915f84716f861241c35ebf106acdcc6508950172ea9de9799e7b8bb4f5625c
