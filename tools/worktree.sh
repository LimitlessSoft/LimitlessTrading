#!/bin/bash
#
# Creates a git worktree for a new branch off a freshly fetched origin/master, so work happens
# outside the main checkout (which may hold someone else's uncommitted changes).
#
# Usage: ./tools/worktree.sh [--from <branch>] <branch> [path]
#
#   --from <branch>  optional; base the new branch on origin/<branch> instead of origin/master
#                    (a chain of stacked PRs, each on top of the previous one)
#   <branch>         e.g. feature/order-ticket-bracket or bugfix/watchlist-rename
#   [path]           optional; defaults to <main worktree>/.claude/worktrees/<branch with / -> ->
#                    (or LT_WORKTREE_DIR)
#
# Examples:
#   ./tools/worktree.sh feature/bottom-dock
#   ./tools/worktree.sh --from feature/bottom-dock feature/bottom-dock-resize

set -euo pipefail

BASE="master"
if [ "${1:-}" = "--from" ]; then
	if [ $# -lt 2 ]; then
		echo "Error: --from needs a branch name." >&2
		exit 1
	fi
	BASE="$2"
	shift 2
fi

if [ $# -lt 1 ]; then
	echo "Usage: $0 [--from <branch>] <branch> [path]" >&2
	exit 1
fi

BRANCH="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# The main worktree is the reference for the default location.
MAIN_WORKTREE="$(git -C "$SCRIPT_DIR" worktree list --porcelain | awk '!found && /^worktree /{print substr($0, 10); found=1}')"
if [ -z "$MAIN_WORKTREE" ]; then
	echo "Error: $SCRIPT_DIR is not in a git repository." >&2
	exit 1
fi

DIR_NAME="${BRANCH//\//-}"
DEST="${2:-${LT_WORKTREE_DIR:-$MAIN_WORKTREE/.claude/worktrees/$DIR_NAME}}"

if [ -e "$DEST" ]; then
	echo "Error: $DEST already exists." >&2
	exit 1
fi

echo "==> git fetch origin $BASE"
# Explicit refspec so origin/<branch> exists for a base other than master too.
git -C "$MAIN_WORKTREE" fetch origin "+refs/heads/$BASE:refs/remotes/origin/$BASE"

echo "==> Creating worktree $DEST"
mkdir -p "$(dirname "$DEST")"
if git -C "$MAIN_WORKTREE" show-ref --verify --quiet "refs/heads/$BRANCH"; then
	echo "    (branch $BRANCH already exists locally)"
	git -C "$MAIN_WORKTREE" worktree add "$DEST" "$BRANCH"
elif git -C "$MAIN_WORKTREE" show-ref --verify --quiet "refs/remotes/origin/$BRANCH"; then
	echo "    (branch $BRANCH already exists on origin)"
	git -C "$MAIN_WORKTREE" worktree add --track -b "$BRANCH" "$DEST" "origin/$BRANCH"
else
	git -C "$MAIN_WORKTREE" worktree add --no-track -b "$BRANCH" "$DEST" "origin/$BASE"
fi

# Gitignored local files worth carrying over, when present.
for rel in ".claude/settings.local.json"; do
	if [ -f "$MAIN_WORKTREE/$rel" ]; then
		mkdir -p "$(dirname "$DEST/$rel")"
		cp "$MAIN_WORKTREE/$rel" "$DEST/$rel"
		echo "    copied: $rel"
	fi
done

echo ""
echo "===== Worktree ready ====="
echo ""
echo "  branch:  $BRANCH"
echo "  base:    origin/$BASE"
echo "  path:    $DEST"
echo ""
echo "Next:"
echo "  dotnet build \"$DEST/LimitlessTrading.slnx\""
echo ""
