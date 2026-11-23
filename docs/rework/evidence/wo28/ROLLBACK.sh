#!/usr/bin/env bash
set -eu
evidence="$(cd "$(dirname "$0")" && pwd)"
probe="$(mktemp -d)"
trap 'rm -f "$probe/math.mjs"; rmdir "$probe"' EXIT
observe() {
  (cd "$probe" && node --input-type=module -e "import {double} from './math.mjs'; try{console.log(double(Infinity))}catch(e){console.log(e.name)}")
}
cp "$evidence/ORIGINAL_FILE" "$probe/math.mjs"
before="$(observe)"
[ "$before" = Infinity ]
cp "$evidence/MODIFIED_FILE" "$probe/math.mjs"
after="$(observe)"
[ "$after" = RangeError ]
cp "$evidence/ORIGINAL_FILE" "$probe/math.mjs"
cmp "$evidence/ORIGINAL_FILE" "$probe/math.mjs"
restored="$(observe)"
[ "$restored" = Infinity ]
printf 'BASELINE=%s\nMODIFIED=%s\nROLLBACK=%s\nRESTORED_BYTES_IDENTICAL\n' "$before" "$after" "$restored"
