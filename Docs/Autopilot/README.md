# Bite or Betray automated agents

Two agents on the VPS (`devops@64.235.33.194`) work on the tasks in [tasks.json](tasks.json), each on its own
share according to the `assignee` field: **Claude Code** (cron at minutes :00/:30) and **Codex** (minutes :15/:45), one task
per run, in their own repo copies (`~/bob/repo-claude`, `~/bob/repo-codex`). Results are pushed to the
**`develop`** branch. `main` is never touched by the agents; you merge `develop` → `main` when it looks good.
Want to move a task to the other agent? Change its `assignee` in tasks.json.

**Flow per run** ([Tools/Autopilot/agent.sh](../../Tools/Autopilot/agent.sh)):
1. Sync `develop` with `main`.
2. Pick a `lane: agent` task owned by that agent whose dependencies are already `done` or `needs_unity_check`
   (they may belong to the other agent).
3. The agent does the work, then [check.sh](../../Tools/Autopilot/check.sh) runs: compile all game assemblies without Unity +
   Core rule tests.
4. Merged with the other agent's results that came in meanwhile, checked again, then pushed. On failure → rolled back;
   after 3 rejections, the task becomes `blocked`.

## Monitoring from your phone

- [LOG.md](LOG.md) on the `develop` branch: a summary of each task and what you need to check.
- [tasks.json](tasks.json): status of all tasks. You may change `status` or `notes` directly, for example
  reopening a `blocked` task by changing it to `todo`, or marking a `human` task as `done`.
- Status `needs_unity_check`: the code is in, but it needs to be opened in Unity or tried on the Quest.

## Commands on the VPS

| Command | Purpose |
|---|---|
| `touch ~/bob/PAUSE` / `rm ~/bob/PAUSE` | Pause / resume both agents |
| `touch ~/bob/PAUSE-claude` (or `PAUSE-codex`) | Pause one agent only |
| `ls -t ~/bob/logs \| head` then `less ~/bob/logs/<file>` | Log of each run |
| `~/bob/bin/agent.sh claude` (or `codex`) | Run one task for that agent now |
| `cd ~/bob/repo && git pull && bash Tools/Autopilot/install.sh` | Reinstall after the scripts change |
| `cd ~/bob/repo-claude && ~/bob/bin/check.sh` | Manual compile + test check |

The compile-without-Unity kit (`~/bob/compilekit`) is built from the laptop with
[make_compile_kit.py](../../Tools/Autopilot/make_compile_kit.py). Rebuild it after upgrading Unity or packages.

## Notifications to the owner (Cue)

[health.py](../../Tools/Autopilot/health.py) runs hourly on the laptop (Task Scheduler `BiteOrBetray Health`, installed by install_unity_runner.ps1). Problems (an agent failing 3x in a row, login/quota exhausted, `blocked` tasks, CI red for more than 24 hours, a stuck runner, VPS down) are written to an issue labeled `bob-alert` in the engineering repo, and that issue is closed automatically on recovery. A summary is always in the `bob-status` issue. Cue reads both via a Manus cron and reports to the owner.

