#!/bin/bash
# Pasang/perbarui loop agent di VPS (user devops, tanpa sudo): salin script ke ~/bob/bin dan pasang cron.
# Jalankan dari ~/bob/repo setelah git pull:  bash Tools/Autopilot/install.sh [menit_interval]
set -euo pipefail
BOB=$HOME/bob
mkdir -p "$BOB/bin" "$BOB/logs"
cp Tools/Autopilot/agent.sh Tools/Autopilot/check.sh Tools/Autopilot/tasks.py Tools/Autopilot/prompt.md "$BOB/bin/"
chmod +x "$BOB/bin/agent.sh" "$BOB/bin/check.sh"
EVERY=${1:-30}
( crontab -l 2>/dev/null | grep -v 'bob/bin/agent.sh' ; echo "*/$EVERY * * * * $BOB/bin/agent.sh" ) | crontab -
# Log lama dibersihkan setelah 14 hari.
( crontab -l | grep -v 'bob/logs' ; echo "17 3 * * * find $BOB/logs -name '*.log' -mtime +14 -delete" ) | crontab -
crontab -l
