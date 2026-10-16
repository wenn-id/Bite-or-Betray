#!/bin/bash
# Pasang/perbarui loop agent di VPS (user devops, tanpa sudo): salin script ke ~/bob/bin dan pasang cron.
# Jalankan dari ~/bob/repo setelah git pull:  bash Tools/Autopilot/install.sh [menit_interval]
set -euo pipefail
BOB=$HOME/bob
mkdir -p "$BOB/bin" "$BOB/logs"
cp Tools/Autopilot/agent.sh Tools/Autopilot/check.sh Tools/Autopilot/tasks.py Tools/Autopilot/prompt.md "$BOB/bin/"
chmod +x "$BOB/bin/agent.sh" "$BOB/bin/check.sh"
EVERY=${1:-30}
# Baris lama milik agent diganti; baris cron lain dibiarkan. Log lama dibersihkan setelah 14 hari.
{ crontab -l 2>/dev/null || true; } | { grep -v -e 'bob/bin/agent.sh' -e 'bob/logs' || true; } > "$BOB/cron.tmp"
echo "*/$EVERY * * * * $BOB/bin/agent.sh" >> "$BOB/cron.tmp"
echo "17 3 * * * find $BOB/logs -name '*.log' -mtime +14 -delete" >> "$BOB/cron.tmp"
crontab "$BOB/cron.tmp" && rm "$BOB/cron.tmp"
crontab -l
