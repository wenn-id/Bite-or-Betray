"""Runner Unity otomatis di laptop owner (Windows Task Scheduler, tiap 2 jam selama laptop menyala).

Mengecek branch develop yang dikerjakan agent VPS dengan Unity sungguhan: EditMode tests, Budget Audit, dan build APK
Quest Development (termasuk error shader Vulkan). Hasil ditulis ke Docs/Autopilot/CI.md dan tugas BOB-CI di
tasks.json pada branch develop, jadi agent memperbaiki kalau ada yang gagal.

Project CI terpisah dari project yang dibuka owner: C:\\Users\\acer\\bob-ci\\repo (lihat install_unity_runner.ps1).

    python unity_runner.py            # satu putaran (dipanggil Task Scheduler)
    python unity_runner.py --force    # jalankan walaupun commit develop sudah pernah dicek
"""
import datetime
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

CI = os.path.join(os.path.expanduser("~"), "bob-ci")
REPO = os.path.join(CI, "repo")
LOGS = os.path.join(CI, "logs")
UNITY = r"C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe"
STATE = os.path.join(CI, "last_sha.txt")
LOCK = os.path.join(CI, "runner.lock")
OWNER = r"C:\Users\acer\Bite or Betray"   # project yang dibuka owner sendiri
TASKS = "Docs/Autopilot/tasks.json"
# Crash alat build (biasanya RAM habis saat dua Unity jalan): bukan salah kode, dicoba lagi di jalan berikutnya.
CRASH = ("Lost connection with shader compiler", "Suspected crash", "PLEASE submit a bug report", "Stack dump:",
         "out of memory", "OutOfMemory", "std::bad_alloc")
REPORT = "Docs/Autopilot/CI.md"
NO_WINDOW = 0x08000000                   # CREATE_NO_WINDOW: tanpa jendela console saat dijalankan Task Scheduler


def log(msg):
    line = f"{datetime.datetime.now():%Y-%m-%d %H:%M:%S} {msg}"
    print(line, flush=True)
    with open(os.path.join(LOGS, "runner.log"), "a", encoding="utf-8") as f:
        f.write(line + "\n")


def git(*args, check=True):
    return subprocess.run(["git", "-C", REPO, *args], capture_output=True, text=True, encoding="utf-8", check=check,
                          creationflags=NO_WINDOW).stdout.strip()


def unity(log_path, *args, timeout=3 * 3600):
    cmd = [UNITY, "-batchmode", "-projectPath", REPO, "-buildTarget", "Android", "-logFile", log_path, *args]
    try:
        return subprocess.run(cmd, timeout=timeout, creationflags=NO_WINDOW).returncode
    except subprocess.TimeoutExpired:
        return -1


def unity_cmdlines():
    out = subprocess.run(["powershell", "-NoProfile", "-Command",
                          "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Select-Object -ExpandProperty CommandLine"],
                         capture_output=True, text=True, creationflags=NO_WINDOW).stdout.lower()
    return out


def unity_busy():
    """Unity lain yang membuka project CI (bukan project owner) tidak boleh ditabrak."""
    return "bob-ci" in unity_cmdlines()


def owner_open():
    """Owner sedang memakai Unity: build APK (berat RAM) ditunda, tes EditMode tetap jalan."""
    out = unity_cmdlines()
    return OWNER.lower() in out or OWNER.lower().replace("\\", "/") in out


def edit_mode(stamp):
    xml_path = os.path.join(LOGS, f"{stamp}-editmode.xml")
    code = unity(os.path.join(LOGS, f"{stamp}-editmode.log"), "-runTests", "-testPlatform", "EditMode", "-testResults", xml_path)
    if not os.path.exists(xml_path):
        return None, f"Unity tidak menghasilkan hasil tes (exit {code}); lihat {stamp}-editmode.log"
    root = ET.parse(xml_path).getroot()
    total, passed, failed = (int(root.get(k, 0)) for k in ("total", "passed", "failed"))
    fails = []
    for case in root.iter("test-case"):
        if case.get("result") == "Failed":
            msg = case.find("./failure/message")
            fails.append(f"{case.get('fullname')}: {(msg.text or '').strip()[:300] if msg is not None else ''}")
    return dict(total=total, passed=passed, failed=failed, fails=fails), None


def build(stamp):
    log_path = os.path.join(LOGS, f"{stamp}-build.log")
    for f in ("audit.txt", "build-exception.txt"):
        p = os.path.join(REPO, "Logs", "ci", f)
        if os.path.exists(p):
            os.remove(p)
    code = unity(log_path, "-quit", "-executeMethod", "BiteOrBetray.EditorTools.CIRunner.Run")
    text = open(log_path, encoding="utf-8", errors="replace").read() if os.path.exists(log_path) else ""
    shader = sorted(set(re.findall(r"Shader error in '[^']+'.*", text)))[:10]
    compile_err = sorted(set(re.findall(r"^.*error CS\d+.*$", text, re.M)))[:20]
    m = re.findall(r"\[Bite or Betray\] (Succeeded|Failed|Cancelled|Unknown);[^\n]*", text)
    result = re.search(r"\[Bite or Betray\] ((Succeeded|Failed|Cancelled|Unknown);[^\n]*)", text)
    audit = os.path.join(REPO, "Logs", "ci", "audit.txt")
    crash = [c for c in CRASH if c in text]
    return dict(exit=code, result=result.group(1) if result else None, succeeded=bool(m and m[-1] == "Succeeded"),
                shader=[x for x in shader if not any(c in x for c in CRASH)], compile=compile_err, crash=crash, skipped=False,
                audit=open(audit, encoding="utf-8").read() if os.path.exists(audit) else "(tidak ada)")


def report(sha, tests, test_err, b):
    build_ok = b.get("skipped") or (b["succeeded"] and not b["shader"] and not b["compile"])
    ok = tests is not None and tests["failed"] == 0 and build_ok
    lines = [f"# CI Unity (runner laptop)", "",
             f"Commit `{sha[:7]}` · {datetime.datetime.now():%Y-%m-%d %H:%M} WIB · **{'LULUS' if ok else 'GAGAL'}**", "",
             "| Cek | Hasil |", "|---|---|"]
    lines.append("| EditMode tests | " + (test_err or f"{tests['passed']}/{tests['total']} lulus, {tests['failed']} gagal") + " |")
    if b.get("skipped"):
        lines.append("| Build APK Development | dilewati: Unity owner sedang terbuka (hemat RAM), dicoba di jalan berikutnya |")
    else:
        lines.append(f"| Build APK Development | {b['result'] or 'tidak selesai (exit %s)' % b['exit']}"
                     + (f" — crash alat build ({', '.join(b['crash'])}), bukan error kode; diulang" if b.get("crash") else "") + " |")
    lines.append(f"| Error shader | {len(b['shader'])} |")
    lines.append(f"| Error compile | {len(b['compile'])} |")
    detail = []
    if tests and tests["fails"]:
        detail += ["## Tes gagal", ""] + [f"- {f}" for f in tests["fails"]] + [""]
    if b["shader"]:
        detail += ["## Error shader", ""] + [f"- `{s}`" for s in b["shader"]] + [""]
    if b["compile"]:
        detail += ["## Error compile", ""] + [f"- `{s}`" for s in b["compile"]] + [""]
    lines += [""] + detail + ["## Budget Audit", "", "```", b["audit"].strip(), "```", ""]
    return ok, "\n".join(lines), detail


def publish(sha, ok, text, detail, infra):
    """CI.md + tugas BOB-CI di develop. Kegagalan infrastruktur (Unity tidak jalan) tidak dijadikan tugas agent."""
    for attempt in range(3):
        git("fetch", "-q", "origin")
        git("checkout", "-q", "-f", "-B", "develop", "origin/develop")
        with open(os.path.join(REPO, REPORT), "w", encoding="utf-8") as f:
            f.write(text)
        path = os.path.join(REPO, TASKS)
        doc = json.load(open(path, encoding="utf-8"))
        task = next((t for t in doc["tasks"] if t["id"] == "BOB-CI"), None)
        if task is None:
            task = dict(id="BOB-CI", title="Perbaiki kegagalan CI Unity (laptop)", priority="P0", lane="agent",
                        assignee="claude", status="done", attempts=0, depends_on=[], files=[REPORT],
                        description="", acceptance_criteria=["CI Unity berikutnya LULUS (lihat Docs/Autopilot/CI.md)."], notes="")
            doc["tasks"].append(task)
        if not infra:
            if ok:
                task["status"] = "done"
            else:
                if task["status"] == "done":
                    task["attempts"] = 0
                task["status"] = "todo"
                task["description"] = (f"CI Unity di laptop GAGAL untuk commit {sha[:7]} (EditMode tests / build APK / shader / "
                                       "compile). Perbaiki penyebabnya tanpa mematikan tes atau menyembunyikan error. "
                                       "Detail:\n" + "\n".join(detail)[:6000])
        with open(path, "w", encoding="utf-8") as f:
            json.dump(doc, f, ensure_ascii=False, indent=1)
            f.write("\n")
        git("add", REPORT, TASKS)
        if not git("status", "--porcelain", check=False):
            return
        git("-c", "user.name=Bite CI (laptop)", "-c", "user.email=252014216+wenn-id@users.noreply.github.com",
            "commit", "-q", "-m", f"CI Unity {sha[:7]}: {'LULUS' if ok else 'GAGAL'}")
        push = subprocess.run(["git", "-C", REPO, "push", "-q", "origin", "develop"], capture_output=True, text=True,
                              creationflags=NO_WINDOW)
        if push.returncode == 0:
            return
        log("push gagal, ulang: " + (push.stderr or push.stdout).strip().replace("\n", " | ")[:400])
    log("push laporan gagal 3x")


def main():
    os.makedirs(LOGS, exist_ok=True)
    try:
        fd = os.open(LOCK, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        os.write(fd, str(os.getpid()).encode())
        os.close(fd)
    except FileExistsError:
        age = datetime.datetime.now().timestamp() - os.path.getmtime(LOCK)
        if age < 4 * 3600:
            log("runner lain masih jalan, lewati")
            return
        os.remove(LOCK)
        return main()
    try:
        if unity_busy():
            log("Unity sedang membuka project CI, lewati")
            return
        git("fetch", "-q", "origin")
        sha = git("rev-parse", "origin/develop")
        last = open(STATE).read().strip() if os.path.exists(STATE) else ""
        if "--force" not in sys.argv and last:
            changed = git("diff", "--name-only", last, sha, check=False).splitlines() if sha != last else []
            # Commit status tugas/log agent (hanya Docs/) tidak perlu build ulang 30 menit.
            if all(f.startswith("Docs/") for f in changed):
                log(f"develop {sha[:7]}: tidak ada perubahan di luar Docs/ sejak {last[:7]}, lewati")
                with open(STATE, "w") as f:
                    f.write(sha)
                return
        # Unity mengubah sendiri setelan/aset saat import dan build; salinan CI tidak boleh punya perubahan lokal.
        git("checkout", "-q", "-f", "-B", "develop", "origin/develop")
        git("clean", "-qfd", "-e", "Library", "-e", "Logs", "-e", "Builds", "-e", "UserSettings")
        subprocess.run(["git", "-C", REPO, "lfs", "pull"], check=False, creationflags=NO_WINDOW)
        stamp = f"{datetime.datetime.now():%Y%m%d-%H%M}-{sha[:7]}"
        log(f"cek develop {sha[:7]}")
        tests, test_err = edit_mode(stamp)
        if owner_open():
            b = dict(exit=None, result=None, succeeded=False, shader=[], compile=[], crash=[], skipped=True, audit="(dilewati)")
        else:
            b = build(stamp)
            # Crash clang/FXC acak (file berbeda tiap kali): build Unity incremental, jadi diulang langsung di sini.
            for retry in (1, 2):
                if b["succeeded"] or not b.get("crash") or b["shader"] or b["compile"]:
                    break
                log(f"build crash ({', '.join(b['crash'])}), ulang {retry}")
                b = build(f"{stamp}-r{retry}")
        # Unity tidak jalan sama sekali, atau build gagal karena crash alat (bukan kode): jangan jadikan tugas agent.
        infra = (tests is None and b["result"] is None) or (bool(b.get("crash")) and not b["shader"] and not b["compile"]
                                                            and tests is not None and tests["failed"] == 0)
        ok, text, detail = report(sha, tests, test_err, b)
        log(f"hasil {sha[:7]}: {'LULUS' if ok else 'GAGAL'}{' (infra)' if infra else ''}")
        publish(sha, ok, text, detail, infra)
        # Build yang dilewati/crash diulang di jalan berikutnya walaupun commit sama.
        if not infra and not b.get("skipped"):
            with open(STATE, "w") as f:
                f.write(git("rev-parse", "HEAD"))     # termasuk commit laporan CI sendiri
    finally:
        if os.path.exists(LOCK):
            os.remove(LOCK)


if __name__ == "__main__":
    main()
