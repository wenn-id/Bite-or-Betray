"""Pengecek kesehatan worker Bite or Betray (laptop owner, Task Scheduler tiap jam).

Hasilnya ditulis ke dua issue di repo engineering supaya Cue (cron Manus) bisa melaporkannya ke owner:
  - label `bob-alert`  : terbuka hanya saat ada masalah, ditutup otomatis saat pulih (komentar baru tiap masalah baru).
  - label `bob-status` : selalu terbuka, isinya ringkasan terbaru (tugas, CI, aktivitas agent).
Yang dicek: agent VPS gagal beruntun (crash, login/kuota habis, konflik), tugas `blocked`, CI Unity gagal lama,
runner laptop macet, dan VPS tidak bisa dihubungi.

    python Tools/Autopilot/health.py [--dry]
"""
import datetime
import json
import os
import re
import subprocess
import sys

GAME = "wenn-id/Bite-or-Betray"
ENG = "wenn-id/bite-autopilot-engineering"
VPS = ["ssh", "-o", "ConnectTimeout=20", "-o", "BatchMode=yes", "-i",
       os.path.expanduser(r"~\.ssh\irengcloud_ed25519"), "devops@64.235.33.194"]
CI_DIR = os.path.expanduser(r"~\bob-ci")
STATE = os.path.join(CI_DIR, "health.json")
NO_WINDOW = 0x08000000 if os.name == "nt" else 0
FAILS_FOR_ALERT = 3          # agent jalan tiap 30 menit: 3 gagal beruntun = sekitar 1,5 jam
CI_RED_HOURS = 24            # CI boleh merah sebentar (agent memperbaikinya lewat BOB-CI)
RUNNER_QUIET_HOURS = 6       # runner menulis log tiap 2 jam selama laptop menyala

# Dijalankan di VPS lewat ssh: ringkasan beberapa jalan terakhir tiap agent.
REMOTE = r'''
import glob, json, os, re
out = {}
for agent in ("claude", "codex"):
    runs = []
    for path in sorted(glob.glob(os.path.expanduser("~/bob/logs/*-%s.log" % agent)))[-8:]:
        text = open(path, errors="replace").read()
        if re.search(r"di-push \(|tidak ada perubahan baru setelah digabung", text): kind = "ok"
        elif "tidak ada tugas siap" in text: kind = "idle"
        elif not re.search(r"selesai \(exit|tidak menghasilkan commit|push gagal|KONFLIK|dibatalkan|Traceback|Error", text): kind = "running"
        else: kind = "fail"
        task = re.search(r"== (BOB-[\w-]+), base", text)
        lines = [l.strip() for l in text.splitlines() if l.strip() and not l.startswith(" ")]
        runs.append(dict(log=os.path.basename(path), kind=kind, task=task.group(1) if task else None, tail=lines[-4:]))
    out[agent] = runs
print(json.dumps(out))
'''

AUTH = re.compile(r"log ?in|logged out|unauthori[sz]ed|401|403|invalid api key|credential|usage limit|rate limit|quota"
                  r"|subscription|expired", re.I)


def run(cmd, inp=None):
    p = subprocess.run(cmd, input=inp, capture_output=True, text=True, encoding="utf-8", creationflags=NO_WINDOW)
    return p.returncode, p.stdout, p.stderr


def gh_file(path):
    code, out, _ = run(["gh", "api", "-H", "Accept: application/vnd.github.raw", f"repos/{GAME}/contents/{path}?ref=develop"])
    return out if code == 0 else None


def agents():
    for _ in range(3):
        code, out, _ = run(VPS + ["python3", "-"], REMOTE)
        if code == 0:
            return json.loads(out)
    return None


def check():
    now = datetime.datetime.now()
    state = json.load(open(STATE)) if os.path.exists(STATE) else {}
    problems, status = [], []

    data = agents()
    if data is None:
        problems.append(("vps", "**VPS tidak bisa dihubungi** lewat SSH (3x). Agent Claude/Codex kemungkinan tidak jalan."))
    else:
        for agent, runs in data.items():
            done = [r for r in runs if r["kind"] != "running"]
            last = done[-1] if done else None
            streak = 0
            for r in reversed(done):
                if r["kind"] != "fail":
                    break
                streak += 1
            pushed = sum(r["kind"] == "ok" for r in runs)
            status.append(f"- **{agent}**: jalan terakhir `{last['kind'] if last else '-'}`"
                          f"{' (' + last['task'] + ')' if last and last['task'] else ''}, push {pushed}x dari {len(runs)} jalan terakhir")
            if streak >= FAILS_FOR_ALERT:
                tail = " / ".join(last["tail"])[-400:]
                why = "kemungkinan **login atau kuota habis**, owner perlu login ulang di VPS" if AUTH.search(tail) else "lihat log di VPS"
                problems.append((f"fail:{agent}", f"**Agent {agent} gagal {streak}x berturut-turut** ({why}). Tugas: {last['task'] or '-'}. "
                                f"Log `{last['log']}`: `{tail}`"))
            if any("KONFLIK" in " ".join(r["tail"]) for r in runs[-2:]):
                problems.append((f"conflict:{agent}", f"**Agent {agent}: konflik merge main -> develop**, butuh owner."))

    tasks = gh_file("Docs/Autopilot/tasks.json")
    if tasks is None:
        problems.append(("tasks", "tasks.json di develop tidak bisa dibaca dari GitHub."))
    else:
        doc = json.loads(tasks)["tasks"]
        count = {}
        for t in doc:
            count[t["status"]] = count.get(t["status"], 0) + 1
        status.insert(0, "- Tugas: " + ", ".join(f"{k} {v}" for k, v in sorted(count.items())))
        for t in doc:
            if t["status"] == "blocked":
                note = (t.get("notes") or "").strip().splitlines()
                problems.append((f"blocked:{t['id']}", f"**{t['id']} blocked** ({t['assignee']}): {t['title']}. {note[-1][:300] if note else ''}"))

    ci = gh_file("Docs/Autopilot/CI.md")
    m = re.search(r"Commit `(\w+)` · ([\d-]+ [\d:]+) WIB · \*\*(\w+)\*\*", ci or "")
    if m:
        status.append(f"- CI Unity terakhir: **{m.group(3)}** untuk `{m.group(1)}` ({m.group(2)} WIB)")
        if m.group(3) == "LULUS":
            state["ci_green"] = m.group(2)
        green = datetime.datetime.strptime(state["ci_green"], "%Y-%m-%d %H:%M") if state.get("ci_green") else None
        if m.group(3) != "LULUS" and green and now - green > datetime.timedelta(hours=CI_RED_HOURS):
            fails = re.findall(r"^- (.+)$", ci.split("## Budget Audit")[0], re.M)[:5]
            problems.append(("ci", f"**CI Unity merah lebih dari {CI_RED_HOURS} jam** (lulus terakhir {state['ci_green']} WIB). "
                            "Contoh: " + "; ".join(f[:150] for f in fails)))

    log = os.path.join(CI_DIR, "logs", "runner.log")
    if os.path.exists(log):
        age = now - datetime.datetime.fromtimestamp(os.path.getmtime(log))
        _, up, _ = run(["powershell", "-NoProfile", "-Command",
                        "[int]((Get-Date) - (Get-CimInstance Win32_OperatingSystem).LastBootUpTime).TotalSeconds"])
        uptime = datetime.timedelta(seconds=int(up.strip()) if up.strip().isdigit() else 0)
        # Laptop yang baru menyala wajar belum ada log runner baru; jam laptop mati tidak dihitung macet.
        if age > datetime.timedelta(hours=RUNNER_QUIET_HOURS) and uptime > datetime.timedelta(hours=RUNNER_QUIET_HOURS):
            problems.append(("runner", f"**Runner CI di laptop tidak jalan {age.seconds // 3600 + age.days * 24} jam** "
                            "(Task Scheduler 'BiteOrBetray Unity CI')."))
    os.makedirs(CI_DIR, exist_ok=True)
    json.dump(state, open(STATE, "w"))
    return problems, status


def issue(label):
    code, out, _ = run(["gh", "issue", "list", "-R", ENG, "--label", label, "--state", "open", "--json", "number,body"])
    items = json.loads(out) if code == 0 and out.strip() else []
    return items[0] if items else None


def ensure_labels():
    for name, color, desc in (("bob-alert", "d73a4a", "Worker Bite or Betray bermasalah (dibaca Cue)"),
                              ("bob-status", "0e8a16", "Ringkasan worker Bite or Betray (dibaca Cue)")):
        run(["gh", "label", "create", name, "-R", ENG, "--color", color, "--description", desc, "--force"])


def publish(problems, status, dry):
    stamp = f"{datetime.datetime.now():%Y-%m-%d %H:%M} WIB"
    body_status = (f"Diperbarui otomatis oleh `Tools/Autopilot/health.py` · {stamp}\n\n" + "\n".join(status) +
                   f"\n\nMasalah aktif: **{len(problems)}**" + (" (lihat issue `bob-alert`)" if problems else ""))
    if dry:
        print(body_status, "\n\nMASALAH:\n" + "\n".join(f"[{k}] {t}" for k, t in problems))
        return
    ensure_labels()
    st = issue("bob-status")
    if st:
        run(["gh", "issue", "edit", str(st["number"]), "-R", ENG, "--body", body_status])
    else:
        run(["gh", "issue", "create", "-R", ENG, "--label", "bob-status", "--title", "📊 Status worker Bite or Betray",
             "--body", body_status])

    al = issue("bob-alert")
    if problems:
        # Kunci masalah disimpan tersembunyi di body: komentar (notifikasi) hanya untuk masalah yang benar-benar baru,
        # bukan setiap kali detail log berubah.
        body = (f"Terakhir dicek {stamp}. Issue ini ditutup otomatis saat semua masalah pulih.\n\n" +
                "\n".join(f"- {t}" for _, t in problems) + f"\n\n<!-- keys: {','.join(k for k, _ in problems)} -->")
        if not al:
            run(["gh", "issue", "create", "-R", ENG, "--label", "bob-alert", "--title", "🚨 Worker Bite or Betray bermasalah",
                 "--body", body])
            return
        old = re.search(r"<!-- keys: ([^>]*) -->", al["body"] or "")
        old = set(old.group(1).split(",")) if old else set()
        new = [t for k, t in problems if k not in old]
        run(["gh", "issue", "edit", str(al["number"]), "-R", ENG, "--body", body])
        if new:
            run(["gh", "issue", "comment", str(al["number"]), "-R", ENG, "--body",
                 "Masalah baru:\n" + "\n".join(f"- {p}" for p in new)])
    elif al:
        run(["gh", "issue", "close", str(al["number"]), "-R", ENG, "--comment", f"✅ Semua pulih ({stamp})."])


if __name__ == "__main__":
    problems, status = check()
    publish(problems, status, "--dry" in sys.argv)
