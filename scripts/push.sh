#!/usr/bin/env bash
# Commit, rebase onto whatever CI published, and push the session branch.
set -euo pipefail
BRANCH="${1:-arena/9ea92fc5-coaching-center-manager}"
cd "$(dirname "$0")/.."
python3 scripts/generate-solution.py > /dev/null
git add -A
if git diff --cached --quiet; then
  echo "nothing to commit"
else
  git commit -q -m "${2:-work in progress}"
fi
git pull --rebase origin "$BRANCH" > /dev/null || { echo "rebase failed"; exit 1; }
git push -q origin "$BRANCH"
echo "pushed to $BRANCH"
