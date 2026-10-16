#!/bin/bash
# Pasang/perbarui dua agent di VPS (user devops, tanpa sudo): salin script ke ~/bob/bin, siapkan repo per agent,
# dan pasang cron. Jalankan dari ~/bob/repo setelah git pull:  bash Tools/Autopilot/install.sh
set -euo pipefail
BOB=$HOME/bob
mkdir -p "$BOB/bin" "$BOB/logs" "$BOB/coretest"
# Salin lalu rename (bukan timpa di tempat): bash membaca script sedikit demi sedikit, jadi agent yang sedang
# berjalan tetap memakai versi lama sampai selesai.
for f in agent.sh check.sh tasks.py prompt.md; do
  install -m 755 "Tools/Autopilot/$f" "$BOB/bin/.$f.new" && mv -f "$BOB/bin/.$f.new" "$BOB/bin/$f"
done
cp Tools/Autopilot/CoreTests.csproj.template "$BOB/coretest/CoreTests.csproj"
URL=$(git remote get-url origin)
for a in claude codex; do
  if [ ! -d "$BOB/repo-$a" ]; then
    GIT_LFS_SKIP_SMUDGE=1 git clone -q "$URL" "$BOB/repo-$a"
    git -C "$BOB/repo-$a" config lfs.fetchexclude "*"
  fi
  git -C "$BOB/repo-$a" config user.name "Bite Agent ($a)"
  git -C "$BOB/repo-$a" config user.email "252014216+wenn-id@users.noreply.github.com"
done
# Claude di menit :00/:30, Codex di :15/:45. Baris cron lain dibiarkan. Log lama dibersihkan setelah 14 hari.
{ crontab -l 2>/dev/null || true; } | { grep -v -e 'bob/bin/agent.sh' -e 'bob/logs' || true; } > "$BOB/cron.tmp"
echo "0,30 * * * * $BOB/bin/agent.sh claude" >> "$BOB/cron.tmp"
echo "15,45 * * * * $BOB/bin/agent.sh codex" >> "$BOB/cron.tmp"
echo "17 3 * * * find $BOB/logs -name '*.log' -mtime +14 -delete" >> "$BOB/cron.tmp"
crontab "$BOB/cron.tmp" && rm "$BOB/cron.tmp"
crontab -l
