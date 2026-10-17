"""Runner Unity otomatis di laptop owner (Windows Task Scheduler, tiap 2 jam selama laptop menyala).

Mengecek branch develop yang dikerjakan agent VPS dengan Unity sungguhan: EditMode/PlayMode, Budget Audit, dan build APK
Quest sesuai Docs/Release/build-mode.txt (termasuk error shader Vulkan). Hasil ditulis ke Docs/Autopilot/CI.md dan tugas BOB-CI di
tasks.json pada branch develop, jadi agent memperbaiki kalau ada yang gagal.

Project CI terpisah dari project yang dibuka owner: C:\\Users\\acer\\bob-ci\\repo (lihat install_unity_runner.ps1).

    python unity_runner.py            # satu putaran (dipanggil Task Scheduler)
    python unity_runner.py --force    # jalankan walaupun commit develop sudah pernah dicek
"""
import datetime
import hashlib
import json
import os
import re
import shutil
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
BUILD_MODE = "Docs/Release/build-mode.txt"
NO_WINDOW = 0x08000000                   # CREATE_NO_WINDOW: tanpa jendela console saat dijalankan Task Scheduler


def log(msg):
    line = f"{datetime.datetime.now():%Y-%m-%d %H:%M:%S} {msg}"
    print(line, flush=True)
    with open(os.path.join(LOGS, "runner.log"), "a", encoding="utf-8") as f:
        f.write(line + "\n")


def git(*args, check=True):
    return subprocess.run(["git", "-C", REPO, *args], capture_output=True, text=True, encoding="utf-8", check=check,
                          creationflags=NO_WINDOW).stdout.strip()


def kill_tree(pid):
    subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True, creationflags=NO_WINDOW)


def unity(log_path, *args, timeout=90 * 60):
    """Unity batch dengan batas waktu. Saat macet (mis. sdkmanager Java menggantung di 'Detecting Android SDK'),
    seluruh pohon proses dimatikan supaya tidak ada Unity/Java yatim yang menghalangi jalan berikutnya."""
    cmd = [UNITY, "-batchmode", "-projectPath", REPO, "-buildTarget", "Android", "-logFile", log_path, *args]
    proc = subprocess.Popen(cmd, creationflags=NO_WINDOW)
    try:
        return proc.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        log(f"Unity macet lebih dari {timeout // 60} menit, dimatikan: {os.path.basename(log_path)}")
        kill_tree(proc.pid)
        return -1


def unity_cmdlines():
    out = subprocess.run(["powershell", "-NoProfile", "-Command",
                          "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Select-Object -ExpandProperty CommandLine"],
                         capture_output=True, text=True, creationflags=NO_WINDOW).stdout.lower()
    return out


def kill_orphans():
    """Dipanggil saat memegang lock: Unity yang masih membuka project CI berarti sisa jalan yang terputus (laptop mati,
    Task Scheduler menghentikan runner). Dimatikan beserta anaknya (Java/Gradle/adb)."""
    out = subprocess.run(["powershell", "-NoProfile", "-Command",
                          "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object { $_.CommandLine -like '*bob-ci*' }"
                          " | Select-Object -ExpandProperty ProcessId"],
                         capture_output=True, text=True, creationflags=NO_WINDOW).stdout.split()
    for pid in out:
        log(f"Unity yatim dari jalan sebelumnya dimatikan (pid {pid})")
        kill_tree(pid)


def owner_open():
    """Owner sedang memakai Unity: build APK (berat RAM) ditunda, tes EditMode tetap jalan."""
    out = unity_cmdlines()
    return OWNER.lower() in out or OWNER.lower().replace("\\", "/") in out


def run_tests(stamp, platform):
    """EditMode atau PlayMode (tes PlayMode memuat scene gameplay sungguhan, pengganti uji di headset)."""
    tag = platform.lower()
    xml_path = os.path.join(LOGS, f"{stamp}-{tag}.xml")
    code = unity(os.path.join(LOGS, f"{stamp}-{tag}.log"), "-runTests", "-testPlatform", platform, "-testResults", xml_path)
    if not os.path.exists(xml_path):
        return None, f"Unity tidak menghasilkan hasil tes (exit {code}); lihat {stamp}-{tag}.log"
    root = ET.parse(xml_path).getroot()
    total, passed, failed = (int(root.get(k, 0)) for k in ("total", "passed", "failed"))
    fails, cases = [], []
    for case in root.iter("test-case"):
        cases.append(dict(name=case.get("fullname"), result=case.get("result")))
        if case.get("result") == "Failed":
            msg = case.find("./failure/message")
            fails.append(f"{case.get('fullname')}: {(msg.text or '').strip()[:300] if msg is not None else ''}")
    return dict(total=total, passed=passed, failed=failed, skipped=total - passed - failed,
                fails=fails, cases=cases, exit=code), None


def build_mode():
    path = os.path.join(REPO, BUILD_MODE)
    mode = "Development"
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            mode = f.read().strip()
    if mode not in ("Development", "Release"):
        raise ValueError(f"{BUILD_MODE}: mode harus Development atau Release, bukan {mode!r}")
    return mode


def build(stamp, mode):
    log_path = os.path.join(LOGS, f"{stamp}-build.log")
    for f in ("audit.txt", "build-exception.txt"):
        p = os.path.join(REPO, "Logs", "ci", f)
        if os.path.exists(p):
            os.remove(p)
    code = unity(log_path, "-quit", "-executeMethod", "BiteOrBetray.EditorTools.CIRunner.Run", "-bobBuildMode", mode)
    text = ""
    if os.path.exists(log_path):
        with open(log_path, encoding="utf-8", errors="replace") as f:
            text = f.read()
    shader = sorted(set(re.findall(r"Shader error in '[^']+'.*", text)))[:10]
    compile_err = sorted(set(re.findall(r"^.*error CS\d+.*$", text, re.M)))[:20]
    results = re.findall(r"\[Bite or Betray\] ((Succeeded|Failed|Cancelled|Unknown);[^\n]*)", text)
    result = results[-1][0] if results else None
    report_path = result.rsplit("; laporan=", 1)[-1].strip() if result and "; laporan=" in result else ""
    audit = os.path.join(REPO, "Logs", "ci", "audit.txt")
    audit_text = "(tidak ada)"
    if os.path.exists(audit):
        with open(audit, encoding="utf-8") as f:
            audit_text = f.read()
    crash = [c for c in CRASH if c in text]
    return dict(exit=code, result=result, succeeded=bool(results and results[-1][1] == "Succeeded"
                and f"; mode={mode};" in result), mode=mode,
                report_path=report_path,
                shader=[x for x in shader if not any(c in x for c in CRASH)], compile=compile_err, crash=crash, skipped=False,
                audit=audit_text)


def report(sha, tests, test_err, play, play_err, b):
    audit_ok = "HASIL: LULUS budget statis" in b["audit"]
    build_ok = not b.get("skipped") and b["succeeded"] and b["exit"] == 0 and audit_ok and not b["shader"] and not b["compile"]
    ok = all(t is not None and t["exit"] == 0 and t["total"] > 0 and t["passed"] == t["total"]
             and t["failed"] == 0 for t in (tests, play)) and build_ok
    mode = b["mode"]
    lines = [f"# CI Unity (runner laptop)", "",
             f"Commit `{sha}` · {datetime.datetime.now():%Y-%m-%d %H:%M} WIB · **{'LULUS' if ok else 'GAGAL'}**", "",
             "| Cek | Hasil |", "|---|---|"]
    lines.append("| EditMode tests | " + (test_err or f"{tests['passed']}/{tests['total']} lulus, {tests['failed']} gagal, {tests['skipped']} dilewati; exit={tests['exit']}") + " |")
    lines.append("| PlayMode tests (scene sungguhan) | "
                 + (play_err or f"{play['passed']}/{play['total']} lulus, {play['failed']} gagal, {play['skipped']} dilewati; exit={play['exit']}") + " |")
    if b.get("skipped"):
        lines.append(f"| Build APK {mode} | dilewati: Unity owner sedang terbuka (hemat RAM), dicoba di jalan berikutnya |")
    else:
        lines.append(f"| Build APK {mode} | {b['result'] or 'tidak selesai (exit %s)' % b['exit']}"
                     + (f" — crash alat build ({', '.join(b['crash'])}), bukan error kode; diulang" if b.get("crash") else "") + " |")
    lines.append(f"| Error shader | {len(b['shader'])} |")
    lines.append(f"| Error compile | {len(b['compile'])} |")
    lines.append("| Budget Audit | " + ("dilewati" if b.get("skipped") else "LULUS" if audit_ok else "GAGAL") + " |")
    detail = []
    for name, suite, error in (("EditMode", tests, test_err), ("PlayMode", play, play_err)):
        if error or suite is None or suite["exit"] != 0 or suite["total"] == 0:
            detail += [f"- {name}: {error or ('hasil tidak lengkap / exit Unity ' + str(suite['exit']) if suite else 'XML tidak ada')}", ""]
    if b.get("skipped") or not b["succeeded"] or b["exit"] != 0:
        detail += [f"- Build {mode}: {'ditunda' if b.get('skipped') else b['result'] or 'tidak selesai'}; exit={b['exit']}", ""]
    if not b.get("skipped") and not audit_ok:
        detail += ["## Budget Audit gagal", "", "Lihat laporan Budget Audit di bawah; build APK berhasil belum memenuhi budget gameplay.", ""]
    fails = (tests["fails"] if tests else []) + (play["fails"] if play else [])
    if fails:
        detail += ["## Tes gagal", ""] + [f"- {f}" for f in fails] + [""]
    untested = [c["name"] for t in (tests, play) if t for c in t["cases"] if c["result"] not in ("Passed", "Failed")]
    if untested:
        detail += ["## Tes dilewati / belum lulus", ""] + [f"- {name}" for name in untested] + [""]
    if b["shader"]:
        detail += ["## Error shader", ""] + [f"- `{s}`" for s in b["shader"]] + [""]
    if b["compile"]:
        detail += ["## Error compile", ""] + [f"- `{s}`" for s in b["compile"]] + [""]
    lines += [""] + detail + ["## Budget Audit", "", "```", b["audit"].strip(), "```", ""]
    perf = os.path.join(REPO, "Logs", "ci", "perf.txt")
    if os.path.exists(perf):
        lines += ["## Performa PlayMode (Logs/ci/perf.txt)", "", "```", open(perf, encoding="utf-8").read().strip()[:6000], "```", ""]
    if mode == "Release":
        lines += [f"Arsip RC commit ini: [manifest dan bukti lengkap](../../Builds/reports/RC/{sha}/manifest.json).", ""]
    return ok, "\n".join(lines), detail


def release_evidence(sha, stamp, tests, play, b, ci_text, screens):
    """Ambil bukti sebelum publish mengganti checkout; arsip lama tidak ditimpa bukti commit berikutnya."""
    files = {"CI.md": ci_text.encode("utf-8")}
    paths = {"editmode.xml": os.path.join(LOGS, f"{stamp}-editmode.xml"),
             "playmode.xml": os.path.join(LOGS, f"{stamp}-playmode.xml")}
    for name in ("perf.txt", "perf-telemetry.csv", "regression-telemetry.csv", "audit.txt"):
        paths[name] = os.path.join(REPO, "Logs", "ci", name)
    paths["build-report.txt"] = b.get("report_path", "")
    missing = []
    for name, path in paths.items():
        if os.path.isfile(path):
            with open(path, "rb") as f:
                files[name] = f.read()
        else:
            missing.append(name)
    for name in ("01-menu.png", "02-atur-bot.png", "03-onboarding.png", "04-gameplay.png", "05-spectator.png", "06-hasil-akhir.png"):
        if name not in screens:
            missing.append("screens/" + name)
    # PrepareScenes/import memang dapat mengubah scene. Simpan patch agar suffix -dirty dapat diperiksa.
    files["workspace.diff"] = git("diff", "HEAD", "--", "Assets", "Packages", "ProjectSettings").encode("utf-8")
    files["workspace-status.txt"] = git("status", "--porcelain", "--", "Assets", "Packages", "ProjectSettings").encode("utf-8")
    manifest = dict(source_commit=sha, mode=b["mode"], validation="Unity Editor laptop; tanpa Quest fisik",
                    editmode=tests, playmode=play, build=b, missing=missing,
                    files={name: hashlib.sha256(data).hexdigest() for name, data in files.items()},
                    screenshots={name: hashlib.sha256(data).hexdigest() for name, data in screens.items()})
    files["manifest.json"] = (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    return files


def publish(sha, ok, text, detail, infra, screens=None, evidence=None):
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
        if screens:
            dst = os.path.join(REPO, "Docs", "Autopilot", "screens")
            shutil.rmtree(dst, ignore_errors=True)
            os.makedirs(dst)
            for name, data in screens.items():
                open(os.path.join(dst, name), "wb").write(data)
            git("add", "-A", "Docs/Autopilot/screens")
        if evidence:
            archive = f"Builds/reports/RC/{sha}"
            shutil.rmtree(os.path.join(REPO, archive), ignore_errors=True)
            os.makedirs(os.path.join(REPO, archive), exist_ok=True)
            for name, data in evidence.items():
                with open(os.path.join(REPO, archive, name), "wb") as f:
                    f.write(data)
            # Builds/ di-ignore untuk APK; hanya arsip teks hasil RC yang disimpan.
            git("add", "-f", archive)
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


def boot_time():
    """Waktu Windows terakhir menyala (epoch detik); 0 kalau tidak bisa dibaca."""
    out = subprocess.run(["powershell", "-NoProfile", "-Command",
                          "[int]((Get-Date) - (Get-CimInstance Win32_OperatingSystem).LastBootUpTime).TotalSeconds"],
                         capture_output=True, text=True, creationflags=NO_WINDOW).stdout.strip()
    return datetime.datetime.now().timestamp() - int(out) if out.isdigit() else 0


def main():
    os.makedirs(LOGS, exist_ok=True)
    try:
        fd = os.open(LOCK, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        os.write(fd, str(os.getpid()).encode())
        os.close(fd)
    except FileExistsError:
        age = datetime.datetime.now().timestamp() - os.path.getmtime(LOCK)
        # Lock dari sebelum laptop mati/restart (lebih tua dari waktu boot) sudah pasti basi.
        if age < 4 * 3600 and os.path.getmtime(LOCK) > boot_time():
            log("runner lain masih jalan, lewati")
            return
        os.remove(LOCK)
        return main()
    try:
        kill_orphans()
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
        mode = build_mode()
        stamp = f"{datetime.datetime.now():%Y%m%d-%H%M}-{sha[:7]}"
        log(f"cek develop {sha[:7]}")
        for p in ("perf.txt", "perf-telemetry.csv", "regression-telemetry.csv", "audit.txt", "build-exception.txt"):
            if os.path.exists(os.path.join(REPO, "Logs", "ci", p)):
                os.remove(os.path.join(REPO, "Logs", "ci", p))
        shutil.rmtree(os.path.join(REPO, "Logs", "ci", "screens"), ignore_errors=True)
        tests, test_err = run_tests(stamp, "EditMode")
        play, play_err = run_tests(stamp, "PlayMode")
        # checkout -f di publish tidak menyentuh Logs/, tapi dibaca dulu supaya pasti dari jalan ini.
        shot_dir = os.path.join(REPO, "Logs", "ci", "screens")
        screens = {n: open(os.path.join(shot_dir, n), "rb").read() for n in sorted(os.listdir(shot_dir))
                   if n.lower().endswith(".png")} if os.path.isdir(shot_dir) else {}
        if owner_open():
            b = dict(exit=None, result=None, succeeded=False, mode=mode, shader=[], compile=[], crash=[], skipped=True, audit="(dilewati)")
        else:
            b = build(stamp, mode)
            # Crash clang/FXC acak (file berbeda tiap kali): build Unity incremental (.o yang sudah jadi disimpan), jadi tiap ulangan\n            # melanjutkan progres; diulang langsung di sini sampai 6x.
            for retry in range(1, 7):
                if b["succeeded"] or not b.get("crash") or b["shader"] or b["compile"]:
                    break
                log(f"build crash ({', '.join(b['crash'])}), ulang {retry}")
                b = build(f"{stamp}-r{retry}", mode)
        # Unity tidak jalan sama sekali, atau build gagal karena crash alat (bukan kode): jangan jadikan tugas agent.
        infra = (tests is None and play is None and b["result"] is None) or (
            bool(b.get("crash")) and not b["shader"] and not b["compile"]
            and tests is not None and tests["failed"] == 0 and play is not None and play["failed"] == 0)
        ok, text, detail = report(sha, tests, test_err, play, play_err, b)
        log(f"hasil {sha[:7]}: {'LULUS' if ok else 'GAGAL'}{' (infra)' if infra else ''}")
        evidence = release_evidence(sha, stamp, tests, play, b, text, screens) if mode == "Release" else None
        publish(sha, ok, text, detail, infra, screens, evidence)
        # Build yang dilewati/crash diulang di jalan berikutnya walaupun commit sama.
        if not infra and not b.get("skipped"):
            with open(STATE, "w") as f:
                f.write(git("rev-parse", "HEAD"))     # termasuk commit laporan CI sendiri
    finally:
        if os.path.exists(LOCK):
            os.remove(LOCK)


if __name__ == "__main__":
    main()
