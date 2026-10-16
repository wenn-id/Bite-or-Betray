#!/bin/bash
# Loop agent Bite or Betray (cron di VPS, user devops). Dua agent dengan tugas masing-masing (kolom assignee):
#   agent.sh claude   -> Claude Code, repo ~/bob/repo-claude
#   agent.sh codex    -> Codex CLI,   repo ~/bob/repo-codex
# Satu tugas per jalan: sinkron develop dengan main -> ambil tugas milik agent ini -> kerjakan -> check.sh ->
# gabung dengan kerja agent lain -> check.sh lagi -> push develop. Salinan yang dijalankan ada di ~/bob/bin
# (disalin install.sh), jadi agent tidak mengubah loop-nya sendiri.
# Jeda semua: touch ~/bob/PAUSE   ·   jeda satu agent: touch ~/bob/PAUSE-claude (atau PAUSE-codex)
set -uo pipefail
AGENT=${1:?pakai: agent.sh claude|codex}
BOB=$HOME/bob
REPO=$BOB/repo-$AGENT
TASKS=Docs/Autopilot/tasks.json
LOGDIR=$BOB/logs
export PATH=$HOME/.local/node/bin:$HOME/.local/bin:$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet
export GIT_LFS_SKIP_SMUDGE=1
mkdir -p "$LOGDIR"
exec 9>"$BOB/agent-$AGENT.lock"; flock -n 9 || exit 0
[ -f "$BOB/PAUSE" ] || [ -f "$BOB/PAUSE-$AGENT" ] && exit 0
exec >>"$LOGDIR/$(date +%Y%m%d-%H%M%S)-$AGENT.log" 2>&1
echo "== $(date -Is) $AGENT mulai"

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

TASK=$(python3 "$BOB/bin/tasks.py" next "$TASKS" "$AGENT") || { echo "tidak ada tugas siap untuk $AGENT"; exit 0; }
BASE=$(git rev-parse HEAD)
echo "== $TASK, base $BASE"

# Hasil yang ditolak dihitung sebagai percobaan; setelah 3x tugas jadi blocked. Agent yang tidak menghasilkan
# apa pun (belum login, kuota habis) tidak dihitung.
reject() {
  echo "$1, hasil dibatalkan"
  git rebase --abort 2>/dev/null; git merge --abort 2>/dev/null
  git fetch -q origin && git checkout -q -B develop origin/develop
  N=$(python3 "$BOB/bin/tasks.py" bump "$TASKS" "$TASK")
  [ "$N" -ge 3 ] && python3 "$BOB/bin/tasks.py" set "$TASKS" "$TASK" blocked "$(date +%F): ditolak $N kali ($1), butuh owner."
  git commit -qam "$TASK: percobaan $N ditolak ($1)" && git push -q origin develop
  exit 1
}

PROMPT=$(python3 "$BOB/bin/tasks.py" prompt "$TASKS" "$TASK" "$BOB/bin/prompt.md")
case $AGENT in
  claude) timeout 100m claude -p "$PROMPT" --dangerously-skip-permissions --max-turns 250 --output-format text ;;
  codex)  timeout 100m codex exec --dangerously-bypass-approvals-and-sandbox -C "$REPO" "$PROMPT" ;;
esac
echo "== $AGENT selesai (exit $?)"

# Sisa perubahan yang belum di-commit ikut di-commit.
if [ -n "$(git status --porcelain)" ]; then git add -A && git commit -qm "$TASK: perubahan $AGENT"; fi
[ "$(git rev-parse HEAD)" = "$BASE" ] && { echo "$AGENT tidak menghasilkan commit"; exit 1; }
git diff --name-only "$BASE" HEAD | grep -Eiq '\.(fbx|png|blend|glb|jpg|wav|mp3)$' && reject "mengubah file biner"
"$BOB/bin/check.sh" || reject "check.sh gagal"

# Gabung dengan hasil agent lain yang masuk selama tugas ini dikerjakan, cek lagi, lalu push.
for i in 1 2 3; do
  git fetch -q origin
  if ! git rebase -q origin/develop; then
    git rebase --abort
    git merge -q --no-edit origin/develop || reject "konflik dengan agent lain"
  fi
  [ "$(git rev-parse HEAD)" = "$(git rev-parse origin/develop)" ] && { echo "tidak ada perubahan baru setelah digabung"; exit 0; }
  if [ "$(git merge-base HEAD origin/develop)" != "$BASE" ]; then "$BOB/bin/check.sh" || reject "check.sh gagal setelah digabung"; fi
  git push -q origin develop && { echo "== $TASK di-push ($(git rev-parse --short HEAD))"; exit 0; }
  sleep 20
done
echo "push gagal 3x, dicoba lagi di jalan berikutnya"; exit 1
