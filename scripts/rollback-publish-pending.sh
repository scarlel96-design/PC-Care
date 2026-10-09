#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
marker="$(git -C "$repo_root" rev-parse --git-path pc-care-publish-last-commit)"
if [[ "$marker" != /* ]]; then marker="$repo_root/$marker"; fi
if [[ ! -f "$marker" ]]; then
  echo "No successful publish marker found; no changes made." >&2
  exit 2
fi

published_commit="$(<"$marker")"
head_commit="$(git -C "$repo_root" rev-parse HEAD)"
subject="$(git -C "$repo_root" show -s --format=%s "$published_commit")"
if [[ "$head_commit" != "$published_commit" || "$subject" != "feat: publish pending PC-Care security hardening" ]]; then
  echo "HEAD moved or marker is not the publisher commit; no changes made." >&2
  exit 3
fi

git -C "$repo_root" revert --no-edit "$published_commit"
branch="$(git -C "$repo_root" branch --show-current)"
if git -C "$repo_root" remote get-url origin >/dev/null 2>&1; then
  git -C "$repo_root" push origin "HEAD:$branch"
fi
rm -f -- "$marker"
echo "ROLLBACK OK: published changes reverted on $branch"
