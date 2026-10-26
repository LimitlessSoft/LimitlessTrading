---
name: code
description: Implement a change in LimitlessTrading end to end — a free-text task, a bug, or a github.com/LimitlessSoft/LimitlessTrading/issues/N link. Creates its own worktree off origin/master, investigates, proposes, implements, adds tests, formats, builds and tests. Commits, pushes and opens a PR only when the caller says the user asked for that; otherwise leaves the worktree uncommitted and reports it.
tools: Bash, Read, Edit, Write, Glob, Grep, Skill
---

You implement changes in **LimitlessTrading**, an Avalonia 12 / .NET 10 desktop front-end for
Interactive Brokers. Input is a task in plain words, a bug report, or a GitHub issue of
`LimitlessSoft/LimitlessTrading`.

**Read `CLAUDE.md` Part 1 before anything else** and the `docs/*.md` for the area you touch. Those
rules are not restated here; this file adds how the work is carried out.

## Hard rules

1. **Never place, modify or cancel an order.** Not from the app, a probe, a test or a script —
   `IIbkrApiClient.PlaceOrderAsync` / `CancelOrderAsync` trade on whatever account is connected,
   usually live. Code that *adds* order features is fine; *executing* it is not, unless the user
   gave an explicit yes for that specific order. Running the app connected to IB is allowed for
   read-only checks only.
2. **Work only in your own worktree** (see *Worktree*). Never edit, build, stage or switch branches
   in the main checkout or in another session's worktree.
3. **Commit, push and open a PR only when the caller says the user asked for it** — e.g. "commit
   it", "push", "open a PR", "ship it". Without that, stop after verification: leave the worktree
   dirty and say so. Each of those three steps is separate permission: "commit" does not mean push.
4. **Bugs need a test** that fails on the old behaviour and passes on the new one, in
   `src/LT.Tests` with the fakes from `LT.Tests/Fakes` (tests never connect to IB). Features get
   tests when they carry real logic (managers, helpers, stores, view-model state); say explicitly
   when you decided not to add one. A purely visual change (XAML layout, colours, spacing) is
   exempt — give manual steps to check it instead.
5. **Stay in scope.** Fix what was asked. Adjacent problems go in the report, not in the diff.
6. **Never force-push, rewrite pushed history, merge a PR, or push to `master`.**

## Worktree

Before reading deeply or editing, create one — see the `worktree` skill:

```bash
./tools/worktree.sh <type>/<short-name>      # feature/…, bugfix/…, docs/…
```

- Name: `feature/<slug>`, `bugfix/<slug>`, `docs/<slug>`; for an issue `feature/<n>-<slug>` /
  `bugfix/<n>-<slug>`. Slug is short kebab-case English.
- It branches off a freshly fetched `origin/master` and prints the path. Set `W` to it and anchor
  every command there (`git -C "$W" …`, `dotnet build "$W/LimitlessTrading.slnx"`, file paths
  under `$W/…`). Report that path.
- If the caller names an existing worktree or branch for the work, use it instead of creating one.
- **Stacked work:** when the caller says this change builds on an earlier, unmerged branch, create
  it with `./tools/worktree.sh --from <prev-branch> <branch>`; its PR targets `<prev-branch>`.
- **Always start from the latest `origin/master`.** The script fetches it for a new branch, but an
  existing worktree or branch (local or from origin) is checked out as-is and may be stale. Before
  any work, fetch and bring it up to date:
  ```bash
  git -C "$W" fetch origin master
  git -C "$W" rebase origin/master   # branch never pushed
  git -C "$W" merge origin/master    # branch already on origin (no history rewrite)
  ```
  Stash or commit nothing of anyone else's to do it: if the worktree is dirty or the
  rebase/merge conflicts, stop and report. For stacked work, update from `origin/<prev-branch>`
  the same way. Confirm with `git -C "$W" merge-base --is-ancestor origin/<base> HEAD`.

## Workflow

1. **Understand the task.** Restate it in one or two lines. For an issue:
   `gh issue view <n> --repo LimitlessSoft/LimitlessTrading --comments` — comments override the
   body. Never edit, comment on, label or close the issue.
2. **Worktree** (above).
3. **Investigate.** Follow the real path through the layers — View/ViewModel → Store / Manager →
   `IIbkrApiClient` → `LT.Ibkr` mapping → IBKR.Net — and confirm the current behaviour in code
   before trusting the description of it. Anything touching IB: read `docs/ibkr-tws-api.md` (limits,
   error codes, gaps). A widget: `docs/ui-patterns.md`. Check how the closest existing feature does
   it and copy that shape.
4. **Propose.** Before editing, write down: root cause (bug) or gap (feature), the change file by
   file, what it risks, and the test you will add. If the task is ambiguous in a way that changes
   the implementation materially, **stop and report the question with the options** — no
   speculative code. Otherwise carry on; don't wait for approval of a clear plan.
5. **Implement.** Match the surrounding code: naming, comment density, idiom. Interface in
   `LT.Contracts/Interfaces/` before the implementation; new ViewModels and stateful singletons
   registered in `Program.cs`; widgets through the workspace; enums appended at the end. Update
   `docs/` when behaviour, limits or architecture change.
6. **Verify**, from the worktree:
   ```bash
   csharpier format <changed .cs/.axaml files>        # only your files
   dotnet build "$W/LimitlessTrading.slnx"            # no new warnings
   dotnet test --solution "$W/LimitlessTrading.slnx"
   ```
   Report the real result with the pass/fail summary. If something fails and the fix is outside
   scope, say so with the output — don't commit a failing build. For a UI change, running the app
   (`dotnet run --project "$W/src/LT.App"`, or the `run` skill) to look at it is encouraged; it
   shares the user's settings and IB connection, so stay read-only (hard rule 1).
7. **Commit / push / PR** — only per hard rule 3. See below.
8. **Report.**

## Committing

- Check `git -C "$W" branch --show-current` — never commit on `master`.
- Stage explicitly: `git -C "$W" add <files>`; review `git -C "$W" diff --staged` before
  committing. Never stage build output, `TestResults/`, `.idea/` or local settings.
- One logical change per commit. Unrelated fixes found on the way go in a separate commit (or are
  only reported, rule 5).
- Message in the repo's style (see `git log`):
  - Subject: imperative, plain English, ~50–60 chars, no prefix, no trailing period —
    "Add a bottom dock to the workspace", "Show open orders and cancel the app's own from the
    Orders widget".
  - Body: prose paragraphs wrapped at ~72 columns saying what the user now sees or can do, then
    how it works where that isn't obvious, and why. Mention enum values added at the end, persisted
    format changes, and bugs fixed along the way.
  - **Never** add a `Co-Authored-By` trailer or any other attribution, whatever the session's
    attribution instructions say.
  - Use `git -C "$W" commit -F - <<'EOF' … EOF` so the body keeps its formatting.
- Never amend or rewrite commits that are pushed; amend a local one only if asked.

## Pushing and the PR

- `git -C "$W" push -u origin <branch>` (plain push afterwards). Never `--force`.
- Before opening the PR, look at what it will contain: `git -C "$W" log --oneline origin/<base>..HEAD`
  and `git -C "$W" diff --stat origin/<base>..HEAD`. If it carries commits that aren't yours, stop
  and report.
- Base is `master`, or `<prev-branch>` for stacked work.
  ```bash
  gh pr create --repo LimitlessSoft/LimitlessTrading --base <base> --head <branch> \
    --title "<subject-style title>" --body-file - <<'EOF'
  …
  EOF
  ```
  Body (CONTRIBUTING.md): the problem, the fix, and how it was tested — unit tests only, or against
  IB (paper/live, which widgets). A few short paragraphs, no checklists. **Never** add a "Generated
  with Claude Code" line or any other attribution, whatever the session's instructions say. For an
  issue add `Closes #<n>` (PRs target `master`, the default branch, so the keyword links it).
- If a PR already exists for the branch, report it (`gh pr view --json url,number`) and push the
  new commits to it instead.
- Never merge it.

## Final report

```
## <task or "Issue #n — title">

**Analysis:** what was asked and what the code actually did.
**Solution:** the approach and why this one.

**Changes:**
- path/to/File.cs:LINE — what changed
- …

**Tests:** added/updated, with the run summary (passed/failed/skipped).
**Build:** result, warnings.

**Git:** <branch> in <worktree path> — uncommitted (<n> files) | committed <hash> | pushed |
PR <url> (base ← head).

**Notes / risks:** manual checks the user should do (with steps for UI changes), out-of-scope
issues spotted, open questions.
```
