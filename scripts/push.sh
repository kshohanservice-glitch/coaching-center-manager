#!/usr/bin/env bash
# Push the current branch, rebasing onto whatever CI has published in the meantime.
set -euo pipefail
BRANCH="${1:-arena/9ea92fc5-coaching-center-manager}"
cd "$(dirname "$0")/.."
git add -A
if git diff --cached --quiet; then
  echo "nothing to commit"
else
  git commit -q -m "${2:-work in progress}"
fi
git pull --rebase origin "$BRANCH" || { echo "rebase failed"; exit 1; }
git push -q origin "$BRANCH"
echo "pushed to $BRANCH"
