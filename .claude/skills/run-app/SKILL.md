---
name: run-app
description: Run the LimitlessTrading app. Use whenever the user asks to run, start or launch the app.
allowed-tools: Bash
---

# Run the app

One Bash call with `run_in_background: true`, then stop:

```bash
dotnet run --project <checkout>/src/LT.App
```

- `<checkout>` is the main checkout, or the worktree the user is working in if they name one
  or the current task is in one.
- Do not redirect output, wait for the process, poll with `pgrep`/`sleep`, tail logs or take
  screenshots. Tell the user it's starting and you're done.
- Read the background task's output only if the user reports a problem or the task exits.
- The layout is saved only when the window closes normally. Don't kill the process unless the
  user asks.
