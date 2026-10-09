#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../../../.." && pwd)"
source_file="$script_dir/UpdateInstallerService.original.cs"
target_file="${1:-$repo_root/src/SmartPerformanceDoctor.App/Services/Update/UpdateInstallerService.cs}"
expected_modified="216bfbf21087e1c6ab8835ae0940c32241a4987246b55086ad58975e0583a8e9"
expected_original="75dd62ba1a2ce07cf0e332bac60a9d33e5fb6b26884c4e237514cbd0fb982232"

actual_source="$(sha256sum "$source_file" | awk '{print $1}')"
actual_target="$(sha256sum "$target_file" | awk '{print $1}')"
[[ "$actual_source" == "$expected_original" ]] || { echo "original hash mismatch"; exit 2; }
[[ "$actual_target" == "$expected_modified" ]] || { echo "target hash mismatch"; exit 3; }
cp -- "$source_file" "$target_file"
restored="$(sha256sum "$target_file" | awk '{print $1}')"
[[ "$restored" == "$expected_original" ]] || { echo "restore hash mismatch"; exit 4; }
echo "RESTORED SHA256=$restored"
