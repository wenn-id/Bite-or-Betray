"""Health check for the Bite or Betray workers (owner's laptop, Task Scheduler every hour).

Results go to two issues in the engineering repo so Cue (Manus cron) can report them to the owner:
  - label `bob-alert`  : open only while there is a problem, closed automatically on recovery (a new comment per new problem).
  - label `bob-status` : always open, holds the latest summary (tasks, CI, agent activity).
Checked: VPS agents failing in a row (crash, login/quota expired, conflict), `blocked` tasks, Unity CI failing for long,
a stuck laptop runner, an unreachable VPS, and both agents running out of tasks.

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
FAILS_FOR_ALERT = 3          # agents run every 30 minutes: 3 failures in a row = about 1.5 hours
CI_RED_HOURS = 24            # CI may be red for a while (an agent fixes it through BOB-CI)
RUNNER_QUIET_HOURS = 6       # the runner logs every 2 hours while the laptop is on
CI_GREEN = ("PASSED", "LULUS")   # CI.md status in English, and the Indonesian one from before the translation

# Runs on the VPS over ssh: a summary of each agent's last runs. Matches the English agent.sh log lines and the older
# Indonesian ones (logs from before the translation stay in the window for a few hours).
REMOTE = r'''
import glob, json, os, re
out = {}
for agent in ("claude", "codex"):
    runs = []
    for path in sorted(glob.glob(os.path.expanduser("~/bob/logs/*-%s.log" % agent)))[-8:]:
        text = open(path, errors="replace").read()
        if re.search(r"pushed \(|no new changes after merging|di-push \(|tidak ada perubahan baru setelah digabung", text): kind = "ok"
        elif re.search(r"no task ready|tidak ada tugas siap", text): kind = "idle"
        elif not re.search(r"finished \(exit|produced no commit|push failed|CONFLICT|discarded|selesai \(exit|tidak menghasilkan commit"
                           r"|push gagal|KONFLIK|dibatalkan|Traceback|Error", text): kind = "running"
        else: kind = "fail"
        task = re.search(r"== (BOB-[\w-]+), base", text)
        lines = [l.strip() for l in text.splitlines() if l.strip() and not l.startswith(" ")]
        runs.append(dict(log=os.path.basename(path), kind=kind, task=task.group(1) if task else None, tail=lines[-4:]))
    out[agent] = runs
# ~/bob/PAUSE pauses both agents, ~/bob/PAUSE-<agent> one of them (agent.sh exits before logging).
out["paused"] = [a for a in ("claude", "codex") if os.path.exists(os.path.expanduser("~/bob/PAUSE"))
                 or os.path.exists(os.path.expanduser("~/bob/PAUSE-" + a))]
print(json.dumps(out))
'''

AUTH = re.compile(r"log ?in|logged out|unauthori[sz]ed|401|403|invalid api key|credential|usage limit|rate limit|quota"
                  r"|subscription|expired", re.I)


def run(cmd, inp=None):
    p = subprocess.run(cmd, input=inp, capture_output=True, text=True, encoding="utf-8", creationflags=NO_WINDOW)
    return p.returncode, p.stdout, p.stderr


def gh_file(path):
    for _ in range(3):           # GitHub API hiccups are common; one failed read should not raise an alert
        code, out, _ = run(["gh", "api", "-H", "Accept: application/vnd.github.raw", f"repos/{GAME}/contents/{path}?ref=develop"])
        if code == 0:
            return out
    return None


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
        problems.append(("vps", "**The VPS cannot be reached** over SSH (3 tries). The Claude/Codex agents are probably not running."))
    else:
        paused = data.pop("paused", [])
        for agent, runs in data.items():
            if agent in paused:
                status.append(f"- **{agent}**: paused by the owner (~/bob/PAUSE)")
                continue
            done = [r for r in runs if r["kind"] != "running"]
            last = done[-1] if done else None
            streak = 0
            for r in reversed(done):
                if r["kind"] != "fail":
                    break
                streak += 1
            pushed = sum(r["kind"] == "ok" for r in runs)
            status.append(f"- **{agent}**: last run `{last['kind'] if last else '-'}`"
                          f"{' (' + last['task'] + ')' if last and last['task'] else ''}, pushed {pushed}x in the last {len(runs)} runs")
            if streak >= FAILS_FOR_ALERT:
                tail = " / ".join(last["tail"])[-400:]
                why = "probably **login or quota expired**, the owner needs to log in again on the VPS" if AUTH.search(tail) else "see the VPS log"
                problems.append((f"fail:{agent}", f"**Agent {agent} failed {streak} times in a row** ({why}). Task: {last['task'] or '-'}. "
                                f"Log `{last['log']}`: `{tail}`"))
            if any(re.search("CONFLICT|KONFLIK", " ".join(r["tail"])) for r in runs[-2:]):
                problems.append((f"conflict:{agent}", f"**Agent {agent}: merge conflict main -> develop**, owner needed."))

        # Both agents without a task for their last 8 runs (about 4 hours): the roadmap ran out, the owner should add tasks.
        if data and not paused and all(len(runs) >= 8 and all(r["kind"] == "idle" for r in runs[-8:]) for runs in data.values()):
            problems.append(("idle", "**Both agents are out of tasks** for about 4 hours: add new tasks to "
                                     "Docs/Autopilot/tasks.json (ask Claude Code to plan the next batch)."))

    tasks = gh_file("Docs/Autopilot/tasks.json")
    if tasks is None:
        problems.append(("tasks", "tasks.json on develop cannot be read from GitHub."))
    else:
        doc = json.loads(tasks)["tasks"]
        count = {}
        for t in doc:
            count[t["status"]] = count.get(t["status"], 0) + 1
        status.insert(0, "- Tasks: " + ", ".join(f"{k} {v}" for k, v in sorted(count.items())))
        for t in doc:
            if t["status"] == "blocked":
                note = (t.get("notes") or "").strip().splitlines()
                problems.append((f"blocked:{t['id']}", f"**{t['id']} blocked** ({t['assignee']}): {t['title']}. {note[-1][:300] if note else ''}"))

    ci = gh_file("Docs/Autopilot/CI.md")
    m = re.search(r"Commit `(\w+)` · ([\d-]+ [\d:]+) WIB · \*\*(\w+)\*\*", ci or "")
    if m:
        status.append(f"- Latest Unity CI: **{m.group(3)}** for `{m.group(1)[:7]}` ({m.group(2)} WIB)")
        if m.group(3) in CI_GREEN:
            state["ci_green"] = m.group(2)
        green = datetime.datetime.strptime(state["ci_green"], "%Y-%m-%d %H:%M") if state.get("ci_green") else None
        if m.group(3) not in CI_GREEN and green and now - green > datetime.timedelta(hours=CI_RED_HOURS):
            fails = re.findall(r"^- (.+)$", ci.split("## Budget Audit")[0], re.M)[:5]
            problems.append(("ci", f"**Unity CI has been red for more than {CI_RED_HOURS} hours** (last pass {state['ci_green']} WIB). "
                            "Examples: " + "; ".join(f[:150] for f in fails)))

    log = os.path.join(CI_DIR, "logs", "runner.log")
    if os.path.exists(log):
        age = now - datetime.datetime.fromtimestamp(os.path.getmtime(log))
        _, up, _ = run(["powershell", "-NoProfile", "-Command",
                        "[int]((Get-Date) - (Get-CimInstance Win32_OperatingSystem).LastBootUpTime).TotalSeconds"])
        uptime = datetime.timedelta(seconds=int(up.strip()) if up.strip().isdigit() else 0)
        # A laptop that just booted has no new runner log yet; hours with the laptop off do not count as stuck.
        if age > datetime.timedelta(hours=RUNNER_QUIET_HOURS) and uptime > datetime.timedelta(hours=RUNNER_QUIET_HOURS):
            problems.append(("runner", f"**The laptop CI runner has not run for {age.seconds // 3600 + age.days * 24} hours** "
                            "(Task Scheduler 'BiteOrBetray Unity CI')."))
    os.makedirs(CI_DIR, exist_ok=True)
    json.dump(state, open(STATE, "w"))
    return problems, status


def issue(label):
    code, out, _ = run(["gh", "issue", "list", "-R", ENG, "--label", label, "--state", "open", "--json", "number,body"])
    items = json.loads(out) if code == 0 and out.strip() else []
    return items[0] if items else None


def ensure_labels():
    for name, color, desc in (("bob-alert", "d73a4a", "Bite or Betray workers need attention (read by Cue)"),
                              ("bob-status", "0e8a16", "Bite or Betray worker summary (read by Cue)")):
        run(["gh", "label", "create", name, "-R", ENG, "--color", color, "--description", desc, "--force"])


def publish(problems, status, dry):
    stamp = f"{datetime.datetime.now():%Y-%m-%d %H:%M} WIB"
    body_status = (f"Updated automatically by `Tools/Autopilot/health.py` · {stamp}\n\n" + "\n".join(status) +
                   f"\n\nActive problems: **{len(problems)}**" + (" (see the `bob-alert` issue)" if problems else ""))
    if dry:
        print(body_status, "\n\nPROBLEMS:\n" + "\n".join(f"[{k}] {t}" for k, t in problems))
        return
    ensure_labels()
    st = issue("bob-status")
    if st:
        run(["gh", "issue", "edit", str(st["number"]), "-R", ENG, "--title", "📊 Bite or Betray worker status", "--body", body_status])
    else:
        run(["gh", "issue", "create", "-R", ENG, "--label", "bob-status", "--title", "📊 Bite or Betray worker status",
             "--body", body_status])

    al = issue("bob-alert")
    if problems:
        # Problem keys are kept hidden in the body: a comment (notification) only for genuinely new problems,
        # not every time a log detail changes.
        body = (f"Last checked {stamp}. This issue closes automatically when every problem has recovered.\n\n" +
                "\n".join(f"- {t}" for _, t in problems) + f"\n\n<!-- keys: {','.join(k for k, _ in problems)} -->")
        if not al:
            run(["gh", "issue", "create", "-R", ENG, "--label", "bob-alert", "--title", "🚨 Bite or Betray workers need attention",
                 "--body", body])
            return
        old = re.search(r"<!-- keys: ([^>]*) -->", al["body"] or "")
        old = set(old.group(1).split(",")) if old else set()
        new = [t for k, t in problems if k not in old]
        run(["gh", "issue", "edit", str(al["number"]), "-R", ENG, "--body", body])
        if new:
            run(["gh", "issue", "comment", str(al["number"]), "-R", ENG, "--body",
                 "New problems:\n" + "\n".join(f"- {p}" for p in new)])
    elif al:
        run(["gh", "issue", "close", str(al["number"]), "-R", ENG, "--comment", f"✅ Everything recovered ({stamp})."])


if __name__ == "__main__":
    problems, status = check()
    publish(problems, status, "--dry" in sys.argv)
