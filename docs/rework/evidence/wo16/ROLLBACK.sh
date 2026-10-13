#!/usr/bin/env bash
set -euo pipefail
base="$(cd -- "$(dirname -- "$0")" && pwd)"
target="${1:?Pass an independent RESTORE_COPY file}"
[[ "$(basename -- "$target")" == "RESTORE_COPY" ]] || exit 2
[[ "$(basename -- "$(dirname -- "$target")")" == wo16-rollback-* ]] || exit 2
[[ -f "$target" && ! -L "$target" ]] || exit 2
current="$(sha256sum -- "$target" | cut -d' ' -f1)"
[[ "$current" == "c6f0a26a4733b3b19997065e2f5053aa1e9cfa10dd8a7da61c09c1c7bddeda62" || "$current" == "5d1aa7f0c56fc811e184e0311063cd2c81028846350c77b56917971998465bff" ]] || exit 3
cp -- "$base/ORIGINAL_FILE" "$target"
[[ "$(sha256sum -- "$target" | cut -d' ' -f1)" == "5d1aa7f0c56fc811e184e0311063cd2c81028846350c77b56917971998465bff" ]]
printf 'ROLLBACK_SOURCE_SHA256=%s;SOURCE_COPY_ONLY=PASS\n' "5d1aa7f0c56fc811e184e0311063cd2c81028846350c77b56917971998465bff"