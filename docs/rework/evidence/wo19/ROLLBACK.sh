#!/usr/bin/env bash
set -euo pipefail
target="$(realpath -- "$1")"
[[ "$(basename -- "$target")" == RESTORE_COPY ]] || exit 2
[[ "$(basename -- "$(dirname -- "$target")")" == wo19-rollback-* ]] || exit 2
[[ -f "$target" && ! -L "$target" ]] || exit 2
hash="$(sha256sum -- "$target" | cut -d' ' -f1)"
[[ "$hash" == 0e04527306ffe4a0d8512e11bd4b2eb889a09b8002996bc8e26903e884b5d79f || "$hash" == 2889d76b5b72ec10df152318104c6116bbf4b68a6f2cf8ad6a62ae2bafbde400 ]] || exit 3
[[ "$(sha256sum < "$(dirname -- "$0")/ORIGINAL_FILE" | cut -d' ' -f1)" == 0e04527306ffe4a0d8512e11bd4b2eb889a09b8002996bc8e26903e884b5d79f ]] || exit 4
cp -- "$(dirname -- "$0")/ORIGINAL_FILE" "$target"
[[ "$(sha256sum -- "$target" | cut -d' ' -f1)" == 0e04527306ffe4a0d8512e11bd4b2eb889a09b8002996bc8e26903e884b5d79f ]] || exit 4
echo ROLLBACK_SHA256_RESTORED=0e04527306ffe4a0d8512e11bd4b2eb889a09b8002996bc8e26903e884b5d79f