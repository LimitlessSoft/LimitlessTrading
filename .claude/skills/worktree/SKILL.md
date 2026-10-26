---
name: worktree
description: Create a git worktree off origin/master and work, build, run, commit and push from it. Use whenever new work starts on a feature/bugfix/docs branch, or when the user mentions worktrees.
allowed-tools: Bash, Read, Edit, Write, Glob, Grep
---

# Worktree Skill

**Every change starts in its own worktree.** The main checkout often holds another session's
uncommitted work; nothing done in a worktree disturbs it.

## Create it

From the main checkout or any worktree (the script finds the main one itself):

```bash
./tools/worktree.sh feature/<short-name>      # or bugfix/<short-name>, docs/<short-name>
```

It fetches `origin/master`, creates the branch from it and prints the path — **use that path for
everything after this point**.

| | |
|---|---|
| Default location | `<main checkout>/.claude/worktrees/<branch>` with `/` → `-` (`feature/bottom-dock` → `.claude/worktrees/feature-bottom-dock`); ignored by git |
| Explicit path | `./tools/worktree.sh <branch> /some/path` or `LT_WORKTREE_DIR=/some/path ./tools/worktree.sh <branch>` |
| Base | freshly fetched `origin/master` — never the current branch. `--from <branch>` (first argument) bases it on `origin/<branch>` instead; only for stacked PRs |
| Existing branch | checked out as-is (local) or tracked from `origin/<branch>`; not rebased |
| Copied files | `.claude/settings.local.json` if present. App settings live in the user's profile, not the repo, so every worktree runs with the same IB connection settings |
| Aborts if | the destination already exists |

## Work in it

Anchor every command to the worktree path:

```bash
W=/home/parpil/source/LimitlessTrading/.claude/worktrees/feature-bottom-dock
git -C "$W" status
dotnet build "$W/LimitlessTrading.slnx"
dotnet test --solution "$W/LimitlessTrading.slnx"
dotnet run --project "$W/src/LT.App"
```

A fresh worktree has no `bin/`/`obj/` — the first build restores. Format only the files you
changed: `csharpier format <files>` (from `$W`).

## Commit, push, PR

Only when the user asked for it (CLAUDE.md, *Git*). Inside the worktree:

```bash
git -C "$W" add <files>
git -C "$W" commit -F - <<'EOF'
<subject>

<body>

Co-Authored-By: ...
EOF
git -C "$W" push -u origin <branch>
gh pr create --repo LimitlessSoft/LimitlessTrading --base master --head <branch> --title "..." --body "..."
```

The `code` agent (`.claude/agents/code.md`) has the message and PR conventions.

**Stacked PRs:** a change too big for one PR is split into branches that each build on the
previous one — `./tools/worktree.sh --from feature/a feature/b`, and the PR for `feature/b` targets
`feature/a`. The first one targets `master`.

## Clean up

Once the PR is merged or the work is abandoned — never while the user may still want the changes:

```bash
git worktree remove <path>       # --force only if the user accepts losing uncommitted work
git branch -D <branch>
```

`git worktree list` shows what exists; `git worktree prune` clears entries whose directory was
deleted by hand.
