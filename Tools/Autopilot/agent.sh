#!/bin/bash
# Loop agent Bite or Betray (dijalankan cron di VPS, user devops). Satu tugas per jalan:
# sinkron develop dengan main -> pilih tugas -> Claude Code (cadangan: Codex) mengerjakan -> check.sh -> push develop.
# Salinan yang dijalankan cron ada di ~/bob/bin (disalin dari Tools/Autopilot oleh install.sh), jadi agent tidak
# mengubah loop-nya sendiri. Matikan sementara: touch ~/bob/PAUSE
set -uo pipefail
BOB=$HOME/bob
REPO=$BOB/repo
TASKS=Docs/Autopilot/tasks.json
LOGDIR=$BOB/logs
export PATH=$HOME/.local/node/bin:$HOME/.local/bin:$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet
export GIT_LFS_SKIP_SMUDGE=1
mkdir -p "$LOGDIR"
exec 9>"$BOB/agent.lock"; flock -n 9 || exit 0
[ -f "$BOB/PAUSE" ] && exit 0
TS=$(date +%Y%m%d-%H%M%S)
LOG=$LOGDIR/$TS.log
exec >>"$LOG" 2>&1
echo "== $(date -Is) mulai"

cd "$REPO" || exit 1
git fetch -q origin || { echo "fetch gagal"; exit 1; }
if git rev-parse -q --verify origin/develop >/dev/null; then
  git checkout -q -B develop origin/develop
else
  git checkout -q -B develop origin/main
fi
git clean -qfd
# Perubahan owner di main ikut masuk develop. Konflik = berhenti dan tunggu owner.
if ! git merge -q --no-edit origin/main; then
  git merge --abort; echo "KONFLIK merge main -> develop, butuh owner"; exit 1
fi

git push -q origin develop
TASK=$(python3 "$BOB/bin/tasks.py" next "$TASKS") || { echo "tidak ada tugas siap"; exit 0; }
BASE=$(git rev-parse HEAD)
echo "== $TASK, base $BASE"

# Hasil yang ditolak (check gagal / ubah biner) dihitung sebagai percobaan; setelah 3x tugas jadi blocked.
# Agent yang tidak menghasilkan apa pun (belum login, kuota habis) tidak dihitung.
reject() {
  echo "$1, hasil dibatalkan"; git reset -q --hard "$BASE"
  N=$(python3 "$BOB/bin/tasks.py" bump "$TASKS" "$TASK")
  if [ "$N" -ge 3 ]; then
    python3 "$BOB/bin/tasks.py" set "$TASKS" "$TASK" blocked "$(date +%F): ditolak $N kali ($1), butuh owner."
  fi
  git commit -qam "$TASK: percobaan $N ditolak ($1)" && git push -q origin develop
  exit 1
}

PROMPT=$(python3 "$BOB/bin/tasks.py" prompt "$TASKS" "$TASK" "$BOB/bin/prompt.md")
ok=0
if command -v claude >/dev/null; then
  echo "== claude"
  timeout 100m claude -p "$PROMPT" --dangerously-skip-permissions --max-turns 250 --output-format text && ok=1
fi
if [ $ok -eq 0 ] && command -v codex >/dev/null; then
  echo "== codex (cadangan)"
  git reset -q --hard "$BASE"; git clean -qfd
  timeout 100m codex exec --dangerously-bypass-approvals-and-sandbox -C "$REPO" "$PROMPT" && ok=1
fi

# Sisa perubahan yang belum di-commit agent ikut di-commit.
if [ -n "$(git status --porcelain)" ]; then git add -A && git commit -qm "$TASK: perubahan agent"; fi
if [ "$(git rev-parse HEAD)" = "$BASE" ]; then
  echo "agent tidak menghasilkan commit (ok=$ok)"; exit 1
fi
# Jangan pernah mengubah file biner LFS.
git diff --name-only "$BASE" HEAD | grep -Eiq '\.(fbx|png|blend|glb|jpg|wav|mp3)$' && reject "mengubah file biner"
"$BOB/bin/check.sh" || reject "check.sh gagal"
git push -q origin develop && echo "== $TASK di-push ($(git rev-parse --short HEAD))"
