"""Automatic Unity runner on the owner's laptop (Windows Task Scheduler, every 2 hours while the laptop is on).

Checks the develop branch the VPS agents work on with real Unity: EditMode/PlayMode tests, the Budget Audit and a Quest APK
build in the mode from Docs/Release/build-mode.txt (including Vulkan shader errors). Results go to Docs/Autopilot/CI.md and to
task BOB-CI in tasks.json on develop, so an agent fixes whatever fails.

The CI project is separate from the project the owner has open: C:\\Users\\acer\\bob-ci\\repo (see install_unity_runner.ps1).

Trailer clips (BOB-052): only when Docs/Release/record-trailer.txt contains yes, once per code commit. PlayMode tests in the
Trailer category record 1080p30 clips to ~/bob-ci/videos/<commit>/ (outside the repo: they are large). ffmpeg (PATH,
~/bob-ci/bin or the winget link) joins the frames into MP4; the file list and durations go to CI.md.
Install ffmpeg: winget install --id Gyan.FFmpeg -e

    python unity_runner.py            # one run (called by Task Scheduler)
    python unity_runner.py --force    # run even if this develop commit was already checked
    python unity_runner.py --force --trailer   # re-record trailer clips even if this commit already has them
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
OWNER = r"C:\Users\acer\Bite or Betray"   # the project the owner opens
TASKS = "Docs/Autopilot/tasks.json"
# Build tool crashes (usually out of RAM with two Unity instances): not a code error, retried.
CRASH = ("Lost connection with shader compiler", "Suspected crash", "PLEASE submit a bug report", "Stack dump:",
         "out of memory", "OutOfMemory", "std::bad_alloc")
HUNG = "Unity hung and was killed"
REPORT = "Docs/Autopilot/CI.md"
BUILD_MODE = "Docs/Release/build-mode.txt"
TRAILER = "Docs/Release/record-trailer.txt"
VIDEOS = os.path.join(CI, "videos")
KEEP_VIDEOS = 3                          # older commit clip folders are deleted (1080p: several GB per commit without ffmpeg)
FPS = 30
ENCODE = ["-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart"]  # same as the per-clip video script
NO_WINDOW = 0x08000000                   # CREATE_NO_WINDOW: no console window under Task Scheduler
# Unity-side markers in English, plus the older Indonesian ones so commits from before the translation still parse.
AUDIT_PASS = ("RESULT: PASS static budget", "HASIL: LULUS budget statis")
REPORT_MARKERS = ("; report=", "; laporan=")
SCREENS = (("01-menu.png",), ("02-bot-setup.png", "02-atur-bot.png"), ("03-onboarding.png",), ("04-gameplay.png",),
           ("05-spectator.png",), ("06-results.png", "06-hasil-akhir.png"))


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


def file_sha256(path):
    digest = hashlib.sha256()
    if os.path.isfile(path):
        with open(path, "rb") as f:
            for block in iter(lambda: f.read(1 << 20), b""):
                digest.update(block)
    return digest.hexdigest()


def repair_lfs():
    """Every LFS file in the checkout must hash to its object id. A corrupt local copy with the right size survives
    checkout -f and lfs pull (git trusts the stat cache, git-lfs only checks that the object exists); Unity then cannot
    import it (RedGrouch_Full_BaseColor.png: Bot_Red white, PrepareMeshyMaterials failed the audit and PlayMode setup).
    fsck moves corrupt objects to .git/lfs/bad so pull downloads them again; mismatched files are checked out again, after
    deleting a local object that still fails its hash (smudge copies local objects unverified, downloads are verified)."""
    subprocess.run(["git", "-C", REPO, "lfs", "fsck"], capture_output=True, check=False, creationflags=NO_WINDOW)
    subprocess.run(["git", "-C", REPO, "lfs", "pull"], check=False, creationflags=NO_WINDOW)
    # ls-files --long: "<sha256 oid> <*|-> <path>"
    oids = {path: oid for oid, _, path in (line.split(" ", 2) for line in git("lfs", "ls-files", "--long").splitlines())}
    bad = [path for path, oid in oids.items() if file_sha256(os.path.join(REPO, path)) != oid]
    if bad:
        for path in bad:
            obj = os.path.join(REPO, ".git", "lfs", "objects", oids[path][:2], oids[path][2:4], oids[path])
            for f in ([obj] if os.path.exists(obj) and file_sha256(obj) != oids[path] else []) + [os.path.join(REPO, path)]:
                if os.path.exists(f):
                    os.chmod(f, 0o666)
                    os.remove(f)
        git("checkout", "-f", "--", *bad, check=False)
        still = [path for path in bad if file_sha256(os.path.join(REPO, path)) != oids[path]]
        log(f"LFS files not matching their object, checked out again: {', '.join(bad)}"
            + (f"; STILL corrupt: {', '.join(still)}" if still else ""))
    return bad


def unity(log_path, *args, timeout=90 * 60):
    """Unity batch run with a time limit. When it hangs (e.g. the Java sdkmanager stuck at 'Detecting Android SDK'), the
    whole process tree is killed so no orphaned Unity/Java blocks the next run."""
    cmd = [UNITY, "-batchmode", "-projectPath", REPO, "-buildTarget", "Android", "-logFile", log_path, *args]
    proc = subprocess.Popen(cmd, creationflags=NO_WINDOW)
    try:
        return proc.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        log(f"Unity hung for more than {timeout // 60} minutes, killed: {os.path.basename(log_path)}")
        kill_tree(proc.pid)
        return -1


def unity_cmdlines():
    out = subprocess.run(["powershell", "-NoProfile", "-Command",
                          "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Select-Object -ExpandProperty CommandLine"],
                         capture_output=True, text=True, creationflags=NO_WINDOW).stdout.lower()
    return out


def kill_orphans():
    """Called while holding the lock: a Unity that still has the CI project open is left over from an interrupted run
    (laptop shut down, Task Scheduler stopped the runner). Killed together with its children (Java/Gradle/adb)."""
    out = subprocess.run(["powershell", "-NoProfile", "-Command",
                          "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object { $_.CommandLine -like '*bob-ci*' }"
                          " | Select-Object -ExpandProperty ProcessId"],
                         capture_output=True, text=True, creationflags=NO_WINDOW).stdout.split()
    for pid in out:
        log(f"killed orphaned Unity from a previous run (pid {pid})")
        kill_tree(pid)


def owner_open():
    """The owner is using Unity: the APK build (heavy on RAM) is deferred, tests still run."""
    out = unity_cmdlines()
    return OWNER.lower() in out or OWNER.lower().replace("\\", "/") in out


def run_tests(stamp, platform, tag=None, extra=()):
    """EditMode or PlayMode (PlayMode tests load the real gameplay scene, standing in for headset testing).
    The Trailer category (video clips, slow) is excluded from the normal PlayMode run; record_trailer runs it."""
    tag = tag or platform.lower()
    if platform == "PlayMode" and not extra:
        extra = ("-testCategory", "!Trailer")
    xml_path = os.path.join(LOGS, f"{stamp}-{tag}.xml")
    code = unity(os.path.join(LOGS, f"{stamp}-{tag}.log"), "-runTests", "-testPlatform", platform, "-testResults", xml_path, *extra)
    if not os.path.exists(xml_path):
        return None, f"Unity produced no test results (exit {code}); see {stamp}-{tag}.log"
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
        raise ValueError(f"{BUILD_MODE}: mode must be Development or Release, not {mode!r}")
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
    marker = next((m for m in REPORT_MARKERS if result and m in result), None)
    report_path = result.rsplit(marker, 1)[-1].strip() if marker else ""
    audit = os.path.join(REPO, "Logs", "ci", "audit.txt")
    audit_text = "(none)"
    if os.path.exists(audit):
        with open(audit, encoding="utf-8") as f:
            audit_text = f.read()
    crash = [c for c in CRASH if c in text]
    if code == -1:
        crash.append(HUNG)
    # A build tool (il2cpp.exe, clang) that exits non-zero without printing any compiler or IL2CPP error is a tool crash
    # on this laptop (seen as access violations), not a code error.
    elif (results and results[-1][1] == "Failed" and "PrintStdoutOnErrorProcessor" in text and not compile_err
          and not shader and not re.search(r"IL2CPP error|error: |fatal error", text)):
        crash.append("build tool exited without an error message")
    return dict(exit=code, result=result, succeeded=bool(results and results[-1][1] == "Succeeded"
                and f"; mode={mode};" in result), mode=mode,
                report_path=report_path,
                shader=[x for x in shader if not any(c in x for c in CRASH)], compile=compile_err, crash=crash, skipped=False,
                audit=audit_text)


def trailer_requested(rev="HEAD"):
    return git("show", f"{rev}:{TRAILER}", check=False).strip().lower() == "yes"


def code_commit(rev="HEAD"):
    """Last commit that changed anything outside Docs/ and Builds/: CI report and agent status commits do not change the
    clips, so clips are recorded once per code commit, not every 2 hours."""
    return git("log", "-1", "--format=%H", rev, "--", ".", ":(exclude)Docs", ":(exclude)Builds", check=False) or git("rev-parse", rev)


def trailer_done(key):
    return os.path.isfile(os.path.join(VIDEOS, key, "clips.md"))


def ffmpeg_path():
    """ffmpeg on PATH, in ~/bob-ci/bin, or the winget link (the Task Scheduler PATH may not include a new install yet)."""
    found = shutil.which("ffmpeg")
    if found:
        return found
    for path in (os.path.join(CI, "bin", "ffmpeg.exe"),
                 os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "WinGet", "Links", "ffmpeg.exe")):
        if os.path.isfile(path):
            return path
    return None


def encode_clips(out, ffmpeg):
    """clips.txt from TrailerClipTests (name, frames, scene; tab separated) -> one 30 fps MP4 per clip in the commit folder.
    JPG frames are deleted once the MP4 exists; without ffmpeg the frames and the clip's video script are kept."""
    listing = os.path.join(out, "clips.txt")
    clips = []
    if not os.path.isfile(listing):
        return clips
    with open(listing, encoding="utf-8") as f:
        rows = [line.rstrip("\n").split("\t") for line in f if line.strip()]
    for name, frames, scene in (r[:3] for r in rows if len(r) >= 3):
        folder, mp4 = os.path.join(out, name), os.path.join(out, name + ".mp4")
        clip = dict(name=name, frames=int(frames), seconds=int(frames) / FPS, scene=scene, file=name + "/", size=0, error=None)
        if ffmpeg:
            try:
                r = subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-framerate", str(FPS),
                                    "-i", os.path.join(folder, "frame_%06d.jpg"), *ENCODE, mp4],
                                   capture_output=True, text=True, timeout=3600, creationflags=NO_WINDOW)
                error = None if r.returncode == 0 and os.path.isfile(mp4) else (r.stderr or r.stdout or f"exit {r.returncode}").strip()[-300:]
            except (OSError, subprocess.TimeoutExpired) as e:
                error = str(e)[:300]
            if error:
                clip["error"] = error
            else:
                clip.update(file=name + ".mp4", size=os.path.getsize(mp4))
                shutil.rmtree(folder, ignore_errors=True)
        if os.path.isdir(folder):
            clip["size"] = sum(os.path.getsize(os.path.join(folder, n)) for n in os.listdir(folder))
        clips.append(clip)
    return clips


def trailer_section(key, out, suite, error, clips, ffmpeg):
    """CI.md section; also saved as clips.md in the clip folder so later reports can quote it."""
    lines = ["## Trailer clips (BOB-052)", "",
             f"Code commit `{key}` · laptop folder `{out}` (not in the repo) · 1080p {FPS} fps, English UI, no audio.", ""]
    if error or suite is None:
        lines += [f"- Trailer tests: {error or 'no XML'}", ""]
    else:
        lines += [f"- Trailer tests: {suite['passed']}/{suite['total']} passed, {suite['failed']} failed, {suite['skipped']} skipped; exit={suite['exit']}", ""]
        lines += [f"- Failed: {f}" for f in suite["fails"]]
        lines += [f"- Skipped: {c['name']}" for c in suite["cases"] if c["result"] not in ("Passed", "Failed")]
        lines += [""] if suite["fails"] or suite["skipped"] else []
    if clips:
        lines += ["| Clip | Scene | Duration | File | Size |", "|---|---|---|---|---|"]
        for c in clips:
            lines.append(f"| {c['name']} | {c['scene']} | {c['seconds']:.1f} s ({c['frames']} frames) | `{c['file']}` | "
                         f"{c['size'] / 1e6:.1f} MB |" + (f" ffmpeg failed: {c['error']}" if c["error"] else ""))
        lines += ["", f"Total {sum(c['seconds'] for c in clips):.1f} s of video.", ""]
    else:
        lines += ["No clips saved (see the trailer Unity log).", ""]
    if not ffmpeg:
        lines += ["ffmpeg not found: each clip is kept as JPG frames plus a video script. Install it once on the laptop with "
                  "`winget install --id Gyan.FFmpeg -e` (or put ffmpeg.exe in ~/bob-ci/bin), then run the script in the clip "
                  "folder or `python unity_runner.py --force --trailer`.", ""]
    return "\n".join(lines)


def record_trailer(stamp, key):
    """Record trailer clips in Unity (PlayMode tests in the Trailer category), then join them with ffmpeg. Failures here
    do not change the CI PASSED/FAILED status; they are only reported."""
    out = os.path.join(VIDEOS, key)
    shutil.rmtree(out, ignore_errors=True)
    os.makedirs(out)
    log(f"recording trailer clips {key[:7]} to {out}")
    suite, error = run_tests(stamp, "PlayMode", "trailer", ("-testCategory", "Trailer", "-bobTrailerDir", out))
    ffmpeg = ffmpeg_path()
    clips = encode_clips(out, ffmpeg)
    text = trailer_section(key, out, suite, error, clips, ffmpeg)
    with open(os.path.join(out, "clips.md"), "w", encoding="utf-8") as f:
        f.write(text)
    # Older commit clip folders are deleted; the newest ones stay.
    old = sorted((d for d in os.listdir(VIDEOS) if os.path.isdir(os.path.join(VIDEOS, d))),
                 key=lambda d: os.path.getmtime(os.path.join(VIDEOS, d)), reverse=True)[KEEP_VIDEOS:]
    for d in old:
        shutil.rmtree(os.path.join(VIDEOS, d), ignore_errors=True)
    return text


def trailer_report(stamp, force=False):
    """Clip section for CI.md: None when not requested; existing clips for this code commit are not re-recorded."""
    if not trailer_requested():
        return None
    key = code_commit()
    if trailer_done(key) and not force:
        with open(os.path.join(VIDEOS, key, "clips.md"), encoding="utf-8") as f:
            return f.read() + "\n\nAlready recorded in an earlier run for this code commit (re-record: `--force --trailer`)."
    return record_trailer(stamp, key)


def report(sha, tests, test_err, play, play_err, b, trailer=None):
    audit_ok = any(m in b["audit"] for m in AUDIT_PASS)
    build_ok = not b.get("skipped") and b["succeeded"] and b["exit"] == 0 and audit_ok and not b["shader"] and not b["compile"]
    ok = all(t is not None and t["exit"] == 0 and t["total"] > 0 and t["passed"] == t["total"]
             and t["failed"] == 0 for t in (tests, play)) and build_ok
    mode = b["mode"]
    lines = [f"# Unity CI (laptop runner)", "",
             f"Commit `{sha}` · {datetime.datetime.now():%Y-%m-%d %H:%M} WIB · **{'PASSED' if ok else 'FAILED'}**", "",
             "| Check | Result |", "|---|---|"]
    lines.append("| EditMode tests | " + (test_err or f"{tests['passed']}/{tests['total']} passed, {tests['failed']} failed, {tests['skipped']} skipped; exit={tests['exit']}") + " |")
    lines.append("| PlayMode tests (real scene) | "
                 + (play_err or f"{play['passed']}/{play['total']} passed, {play['failed']} failed, {play['skipped']} skipped; exit={play['exit']}") + " |")
    if b.get("skipped"):
        lines.append(f"| Build APK {mode} | skipped: the owner's Unity is open (saving RAM), retried next run |")
    else:
        lines.append(f"| Build APK {mode} | {b['result'] or 'did not finish (exit %s)' % b['exit']}"
                     + (f" — build tool crash ({', '.join(b['crash'])}), not a code error; retried" if b.get("crash") else "") + " |")
    lines.append(f"| Shader errors | {len(b['shader'])} |")
    lines.append(f"| Compile errors | {len(b['compile'])} |")
    lines.append("| Budget Audit | " + ("skipped" if b.get("skipped") else "PASSED" if audit_ok else "FAILED") + " |")
    detail = []
    for name, suite, error in (("EditMode", tests, test_err), ("PlayMode", play, play_err)):
        if error or suite is None or suite["exit"] != 0 or suite["total"] == 0:
            detail += [f"- {name}: {error or ('incomplete results / Unity exit ' + str(suite['exit']) if suite else 'no XML')}", ""]
    if b.get("skipped") or not b["succeeded"] or b["exit"] != 0:
        detail += [f"- Build {mode}: {'deferred' if b.get('skipped') else b['result'] or 'did not finish'}; exit={b['exit']}"
                   + (f" — laptop build tool problem ({', '.join(b['crash'])}), not a code error: do not change code for it"
                      if b.get("crash") else ""), ""]
    if not b.get("skipped") and not audit_ok:
        detail += ["## Budget Audit failed", "", "See the Budget Audit report below; a successful APK build does not yet meet the gameplay budget.", ""]
    fails = (tests["fails"] if tests else []) + (play["fails"] if play else [])
    if fails:
        detail += ["## Failed tests", ""] + [f"- {f}" for f in fails] + [""]
    untested = [c["name"] for t in (tests, play) if t for c in t["cases"] if c["result"] not in ("Passed", "Failed")]
    if untested:
        detail += ["## Skipped / not passed tests", ""] + [f"- {name}" for name in untested] + [""]
    if b["shader"]:
        detail += ["## Shader errors", ""] + [f"- `{s}`" for s in b["shader"]] + [""]
    if b["compile"]:
        detail += ["## Compile errors", ""] + [f"- `{s}`" for s in b["compile"]] + [""]
    lines += [""] + detail + ["## Budget Audit", "", "```", b["audit"].strip(), "```", ""]
    perf = os.path.join(REPO, "Logs", "ci", "perf.txt")
    if os.path.exists(perf):
        lines += ["## PlayMode performance (Logs/ci/perf.txt)", "", "```", open(perf, encoding="utf-8").read().strip()[:6000], "```", ""]
    if trailer:
        lines += [trailer.strip(), ""]
    if mode == "Release":
        lines += [f"RC archive for this commit: [manifest and full evidence](../../Builds/reports/RC/{sha}/manifest.json).", ""]
    return ok, "\n".join(lines), detail


def release_evidence(sha, stamp, tests, play, b, ci_text, screens):
    """Collect evidence before publish replaces the checkout; older archives are not overwritten by later commits."""
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
    for names in SCREENS:
        if not any(n in screens for n in names):
            missing.append("screens/" + names[0])
    # PrepareScenes/import can modify scenes. Keep the patch so the -dirty suffix can be inspected.
    files["workspace.diff"] = git("diff", "HEAD", "--", "Assets", "Packages", "ProjectSettings").encode("utf-8")
    files["workspace-status.txt"] = git("status", "--porcelain", "--", "Assets", "Packages", "ProjectSettings").encode("utf-8")
    manifest = dict(source_commit=sha, mode=b["mode"], validation="Unity Editor on the laptop; no physical Quest",
                    editmode=tests, playmode=play, build=b, missing=missing,
                    files={name: hashlib.sha256(data).hexdigest() for name, data in files.items()},
                    screenshots={name: hashlib.sha256(data).hexdigest() for name, data in screens.items()})
    files["manifest.json"] = (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    return files


def publish(sha, ok, text, detail, infra, screens=None, evidence=None):
    """CI.md + task BOB-CI on develop. Infrastructure failures (Unity did not run) do not become agent tasks."""
    for attempt in range(3):
        git("fetch", "-q", "origin")
        git("checkout", "-q", "-f", "-B", "develop", "origin/develop")
        with open(os.path.join(REPO, REPORT), "w", encoding="utf-8") as f:
            f.write(text)
        path = os.path.join(REPO, TASKS)
        doc = json.load(open(path, encoding="utf-8"))
        task = next((t for t in doc["tasks"] if t["id"] == "BOB-CI"), None)
        if task is None:
            task = dict(id="BOB-CI", title="Fix Unity CI failures (laptop)", priority="P0", lane="agent",
                        assignee="claude", status="done", attempts=0, depends_on=[], files=[REPORT],
                        description="", acceptance_criteria=["The next Unity CI run PASSES (see Docs/Autopilot/CI.md)."], notes="")
            doc["tasks"].append(task)
        if not infra:
            if ok:
                task["status"] = "done"
            else:
                if task["status"] == "done":
                    task["attempts"] = 0
                task["status"] = "todo"
                task["description"] = (f"Unity CI on the laptop FAILED for commit {sha[:7]} (EditMode/PlayMode tests / APK build / "
                                       "shaders / compile / Budget Audit). Fix the cause without disabling tests or hiding "
                                       "errors. Details:\n" + "\n".join(detail)[:6000])
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
            # Builds/ is ignored for APKs; only the text RC archive is committed.
            git("add", "-f", archive)
        git("add", REPORT, TASKS)
        if not git("status", "--porcelain", check=False):
            return
        git("-c", "user.name=Bite CI (laptop)", "-c", "user.email=252014216+wenn-id@users.noreply.github.com",
            "commit", "-q", "-m", f"Unity CI {sha[:7]}: {'PASSED' if ok else 'FAILED'}")
        push = subprocess.run(["git", "-C", REPO, "push", "-q", "origin", "develop"], capture_output=True, text=True,
                              creationflags=NO_WINDOW)
        if push.returncode == 0:
            return
        log("push failed, retrying: " + (push.stderr or push.stdout).strip().replace("\n", " | ")[:400])
    log("pushing the report failed 3 times")


def boot_time():
    """When Windows last booted (epoch seconds); 0 if it cannot be read."""
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
        # A lock from before the laptop shut down or restarted (older than the boot time) is always stale.
        if age < 4 * 3600 and os.path.getmtime(LOCK) > boot_time():
            log("another runner is still running, skipping")
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
            # Agent status/log commits (Docs/ only) do not need a 30-minute rebuild, unless trailer clips are requested
            # (record-trailer.txt = yes) and the latest code commit has no clips yet.
            want_trailer = trailer_requested("origin/develop") and not trailer_done(code_commit("origin/develop"))
            if all(f.startswith("Docs/") for f in changed) and not want_trailer:
                log(f"develop {sha[:7]}: no changes outside Docs/ since {last[:7]}, skipping")
                with open(STATE, "w") as f:
                    f.write(sha)
                return
        # Unity changes settings/assets itself during import and build; the CI copy must not keep local changes.
        git("checkout", "-q", "-f", "-B", "develop", "origin/develop")
        git("clean", "-qfd", "-e", "Library", "-e", "Logs", "-e", "Builds", "-e", "UserSettings")
        repair_lfs()
        mode = build_mode()
        stamp = f"{datetime.datetime.now():%Y%m%d-%H%M}-{sha[:7]}"
        log(f"checking develop {sha[:7]}")
        for p in ("perf.txt", "perf-telemetry.csv", "regression-telemetry.csv", "audit.txt", "build-exception.txt"):
            if os.path.exists(os.path.join(REPO, "Logs", "ci", p)):
                os.remove(os.path.join(REPO, "Logs", "ci", p))
        shutil.rmtree(os.path.join(REPO, "Logs", "ci", "screens"), ignore_errors=True)
        tests, test_err = run_tests(stamp, "EditMode")
        play, play_err = run_tests(stamp, "PlayMode")
        trailer = trailer_report(stamp, "--trailer" in sys.argv)
        # checkout -f in publish does not touch Logs/, but read them now so they are surely from this run.
        shot_dir = os.path.join(REPO, "Logs", "ci", "screens")
        screens = {n: open(os.path.join(shot_dir, n), "rb").read() for n in sorted(os.listdir(shot_dir))
                   if n.lower().endswith(".png")} if os.path.isdir(shot_dir) else {}
        if owner_open():
            b = dict(exit=None, result=None, succeeded=False, mode=mode, shader=[], compile=[], crash=[], skipped=True, audit="(skipped)")
        else:
            b = build(stamp, mode)
            # Random clang/FXC crashes (a different file each time): Unity builds incrementally (finished .o files are
            # kept), so each retry continues the progress; retried right here up to 6 times.
            for retry in range(1, 7):
                if b["succeeded"] or not b.get("crash") or b["shader"] or b["compile"]:
                    break
                # A hang costs 90 minutes: retry it once at most, the next scheduled run tries again.
                if HUNG in b["crash"] and retry > 1:
                    break
                log(f"build crash ({', '.join(b['crash'])}), retry {retry}")
                b = build(f"{stamp}-r{retry}", mode)
        # Unity did not run at all, or the build failed from a tool crash (not code): do not make it an agent task.
        infra = (tests is None and play is None and b["result"] is None) or (
            bool(b.get("crash")) and not b["shader"] and not b["compile"]
            and tests is not None and tests["failed"] == 0 and play is not None and play["failed"] == 0)
        ok, text, detail = report(sha, tests, test_err, play, play_err, b, trailer)
        log(f"result {sha[:7]}: {'PASSED' if ok else 'FAILED'}{' (infra)' if infra else ''}")
        evidence = release_evidence(sha, stamp, tests, play, b, text, screens) if mode == "Release" else None
        publish(sha, ok, text, detail, infra, screens, evidence)
        # Skipped/crashed builds are retried next run even for the same commit.
        if not infra and not b.get("skipped"):
            with open(STATE, "w") as f:
                f.write(git("rev-parse", "HEAD"))     # includes the CI report commit itself
    finally:
        if os.path.exists(LOCK):
            os.remove(LOCK)


if __name__ == "__main__":
    main()
