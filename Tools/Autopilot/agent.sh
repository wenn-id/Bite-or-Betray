#!/bin/bash
# Bite or Betray agent loop (cron on the VPS, user devops). Two agents, each with its own tasks (assignee field):
#   agent.sh claude   -> Claude Code, repo ~/bob/repo-claude
#   agent.sh codex    -> Codex CLI,   repo ~/bob/repo-codex
# One task per run: sync develop with main -> take this agent's next task -> work -> check.sh ->
# merge with the other agent's work -> check.sh again -> push develop. The copy that runs lives in ~/bob/bin
# (installed by install.sh), so agents cannot change their own loop.
# Pause everything: touch ~/bob/PAUSE   ·   pause one agent: touch ~/bob/PAUSE-claude (or PAUSE-codex)
set -uo pipefail
AGENT=${1:?usage: agent.sh claude|codex}
BOB=$HOME/bob
REPO=$BOB/repo-$AGENT
TASKS=Docs/Autopilot/tasks.json
LOGDIR=$BOB/logs
export PATH=$HOME/.local/node/bin:$HOME/.local/bin:$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet
export GIT_LFS_SKIP_SMUDGE=1
# dotnet runs started by the agent also go without build servers (leftover processes hold the next run's lock).
export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false
mkdir -p "$LOGDIR"
exec 9>"$BOB/agent-$AGENT.lock"; flock -n 9 || exit 0
[ -f "$BOB/PAUSE" ] || [ -f "$BOB/PAUSE-$AGENT" ] && exit 0
exec >>"$LOGDIR/$(date +%Y%m%d-%H%M%S)-$AGENT.log" 2>&1
echo "== $(date -Is) $AGENT started"

cd "$REPO" || exit 1
git fetch -q origin || { echo "fetch failed"; exit 1; }
if git rev-parse -q --verify origin/develop >/dev/null; then
  git checkout -q -B develop origin/develop
else
  git checkout -q -B develop origin/main
fi
git clean -qfd
# The owner's changes on main flow into develop. A conflict = stop and wait for the owner.
if ! git merge -q --no-edit origin/main; then
  git merge --abort; echo "CONFLICT merging main -> develop, owner needed"; exit 1
fi

TASK=$(python3 "$BOB/bin/tasks.py" next "$TASKS" "$AGENT") || { echo "no task ready for $AGENT"; exit 0; }
BASE=$(git rev-parse HEAD)
echo "== $TASK, base $BASE"

# A rejected result counts as an attempt; after 3 the task becomes blocked. An agent that produced nothing
# (not logged in, quota used up) is not counted.
reject() {
  echo "$1, result discarded"
  git rebase --abort 2>/dev/null; git merge --abort 2>/dev/null
  git fetch -q origin && git checkout -q -B develop origin/develop
  N=$(python3 "$BOB/bin/tasks.py" bump "$TASKS" "$TASK")
  [ "$N" -ge 3 ] && python3 "$BOB/bin/tasks.py" set "$TASKS" "$TASK" blocked "$(date +%F): rejected $N times ($1), owner needed."
  git commit -qam "$TASK: attempt $N rejected ($1)" && git push -q origin develop
  exit 1
}

PROMPT=$(python3 "$BOB/bin/tasks.py" prompt "$TASKS" "$TASK" "$BOB/bin/prompt.md")
# Models are pinned here (not taken from CLI defaults): Opus 5.5 high and GPT-6.1 Sol high.
case $AGENT in
  claude) timeout 100m claude -p "$PROMPT" --model claude-opus-5-5 --effort high             --dangerously-skip-permissions --max-turns 250 --output-format text ;;
  codex)  timeout 100m codex exec -m gpt-6.1-sol -c model_reasoning_effort='"high"'             --dangerously-bypass-approvals-and-sandbox -C "$REPO" "$PROMPT" ;;
esac
echo "== $AGENT finished (exit $?)"

# Any leftover uncommitted changes are committed too.
if [ -n "$(git status --porcelain)" ]; then git add -A && git commit -qm "$TASK: remaining $AGENT changes"; fi
[ "$(git rev-parse HEAD)" = "$BASE" ] && { echo "$AGENT produced no commit"; exit 1; }
git diff --name-only "$BASE" HEAD | grep -Eiq '\.(fbx|png|blend|glb|jpg|wav|mp3)$' && reject "changed binary files"
"$BOB/bin/check.sh" || reject "check.sh failed"

# Merge with the other agent's work that landed meanwhile, check again, then push.
for i in 1 2 3; do
  git fetch -q origin
  if ! git rebase -q origin/develop; then
    git rebase --abort
    git merge -q --no-edit origin/develop || reject "conflict with the other agent"
  fi
  [ "$(git rev-parse HEAD)" = "$(git rev-parse origin/develop)" ] && { echo "no new changes after merging"; exit 0; }
  if [ "$(git merge-base HEAD origin/develop)" != "$BASE" ]; then "$BOB/bin/check.sh" || reject "check.sh failed after merging"; fi
  git push -q origin develop && { echo "== $TASK pushed ($(git rev-parse --short HEAD))"; exit 0; }
  sleep 20
done
echo "push failed 3 times, retrying next run"; exit 1
