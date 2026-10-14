#!/usr/bin/env bash
set -euo pipefail
target="$(realpath -- "$1")"
[[ "$(basename -- "$target")" == RESTORE_COPY ]] || exit 2
[[ "$(basename -- "$(dirname -- "$target")")" == wo17-rollback-* ]] || exit 2
[[ -f "$target" && ! -L "$target" ]] || exit 2
hash="$(sha256sum -- "$target" | cut -d' ' -f1)"
[[ "$hash" == 6a915f84716f861241c35ebf106acdcc6508950172ea9de9799e7b8bb4f5625c || "$hash" == 1c2cf13ca59e650948924e32d38807368a7b33d62017896f2633466b3b26b464 ]] || exit 3
cp -- "$(dirname -- "$0")/ORIGINAL_FILE" "$target"
[[ "$(sha256sum -- "$target" | cut -d' ' -f1)" == 1c2cf13ca59e650948924e32d38807368a7b33d62017896f2633466b3b26b464 ]] || exit 4
echo ROLLBACK_SHA256_RESTORED=1c2cf13ca59e650948924e32d38807368a7b33d62017896f2633466b3b26b464
