# Roadmap: all Bite Autopilot workers working on Bite or Betray

Version 1 · 16 October 2026 · compiled from the contents of `wenn-id/bite-autopilot-engineering` (main `4800371`) and
`wenn-id/Bite-or-Betray` (main `4a08b0d`). The status below is a plan; nothing has been done yet.

## Summary

Today the worker has proven it can write code, test it, get reviewed by Claude, and then auto-merge. But
the lane is still narrow: **standard Python only, in the trial repo only**. All 25 game tasks (BOB-001 through
BOB-025, plus BOB-026/027 in the proposed queue) are Unity/C#. For the worker to be able to do them,
three new things are needed:

1. **A safe work target for the game repo**: the worker only writes to a staging repo; the game's `main` still
   goes through your approval.
2. **A C#/Unity lane**: Codex sends a C# patch, then a Unity runner checks compile, 42 EditMode tests,
   and the Android build.
3. **A rolling task catalog**: the base follows the tip of the staging branch, so the next task is not held by
   `CATALOG_BASE_ADVANCED` every time there is a merge.

After that, about **19 of 27 tasks can be done by the worker**. The rest (testing on Quest 3, release
decisions) still need a human.

| Phase | Content | Who | Estimate |
|---|---|---|---|
| 0 | Owner decisions and access cleanup | You | 1 day |
| 1 | Warm-up: bring the Python worker back to life | Codex (VPS) | 0.5 day |
| 2 | Onboard the game repo (staging + App + rolling catalog) | Codex | 2–3 days |
| 3 | C#/Unity lane + Unity runner | Codex + laptop | 3–5 days |
| 4 | Execute BOB tasks in waves | Worker, you at the Quest gate | 5–12 days |
| 5 | Scale and daily operations | Codex | ongoing |

Rough time estimates, depending on the daily quota and Unity runner availability.

## Starting conditions (facts)

| Item | Current state | Source |
|---|---|---|
| Worker lanes | `clamp-fix` and `python-repo-fix`; 1–5 `.py`/`.md` files, max 7,200 bytes total | `docs/REPOSITORY-TASKS.md`, `autopilot/repository_task.py` |
| Sandbox | Landlock + seccomp: **no network, no process creation or exec** | `docs/REPOSITORY-TASKS.md` |
| Write target | Only `bite-autopilot-results`; fast-forward auto-merge is enabled there | `AUTO-MERGE.md` |
| Quota | 5 work items per day (WIB), 1 running at a time; coding and review are 1 item each | `AUTO-MERGE.md` |
| Catalog base | Pinned to one SHA; after a merge, new tasks are held by `CATALOG_BASE_ADVANCED` | `AUTO-MERGE.md` |
| Unity | No Unity runner on the VPS. The laptop is already proven: L1 42/42 EditMode tests and L2 staging APK at `07c9f7c` | `evidence/unity-editmode.json`, `evidence/android-build.json`, `docs/SECURITY-BLOCKERS.md` |
| Quest 3 | Never tested on the headset | `evidence/android-build.json` (`quest3_hardware_tested: false`) |
| Game queue | BOB-001..025 in `task-queue.json`, BOB-026/027 in `task-queue.proposed.json`; all `todo` | engineering repo |
| Game repo | Already moved ahead to `4a08b0d` (Meshy full-body characters), on top of the tested `07c9f7c` | `git log` Bite-or-Betray |

## Ground rules

- **The worker never writes to the game repo's `main`.** The worker writes to the staging repo. You
  move the results into `main`, or later via auto-merge restricted to the staging branch.
- **Every task is proven by the Unity runner**, not just by model review. A patch without compile
  and test evidence must not pass.
- **Issue content is still treated as data.** Repo, path, base, test command, and model are still determined by the
  root-owned catalog, exactly as now.
- **Physical Quest 3 tasks are always human.** The worker only prepares the APK and a checklist.
- **All changes to permissions, quotas, and targets need owner approval.** That is what Phase 0 is about.

## Phase 0 — Owner decisions (1 day)

Without these decisions, the next phases must not start.

| # | Decision | Recommendation | Why it matters |
|---|---|---|---|
| D1 | ChatGPT/Claude subscription for automated services, or API key | Check each one's terms. If in doubt, move to API keys via the already-designed `bite-api-broker` (`openai-api-key`, `anthropic-api-key`) | The system's own documents mark subscription permission as "unverified" |
| D2 | Deploy key `bitebot-irengcloud` (write access) on the game repo | Remove it, or make it read-only | Currently the only direct write path to the game repo, outside the gate |
| D3 | Manus Connector (Cue) permissions on the engineering repo | **Decided 16 Oct: keep as is** (risk accepted by owner) | Cue can still change system code; code changes from Cue should still go through a PR read by the owner |
| D4 | Game repo merge policy | Worker → staging repo. PRs to `main` merged manually by you | GitHub Free has no branch protection on private repos, so the only safeguard is in the VPS code |
| D5 | Unity runner | Start with the laptop (already proven), plan a separate machine for when the laptop is off | The VPS does not have a licensed Unity yet |
| D6 | Daily quota | Raise from 5 to 10 items/day during Phase 4, still 1 running at a time | 19 tasks × (coding + review) + retries ≈ 50 items |
| D7 | Package ID and company name (for BOB-003) | E.g. `com.wennid.biteorbetray` | The worker must not guess the app identity |
| D8 | Alert channel | Telegram or email for failed tasks, runner down, and quota exhausted | G7 in `SECURITY-BLOCKERS.md` has no channel yet |

**Done when:** D1–D8 are recorded in an engineering Issue with your decisions, D2 has been applied, and
`SECURITY-BLOCKERS.md` is updated (D3 recorded as an accepted risk).

## Phase 1 — Warm-up: Python worker alive again (0.5 day)

The goal is to prove the cycle runs after `CATALOG_BASE_ADVANCED`, before moving into the game repo.

1. Add a new catalog template in the results repo with base `46bd63d` (current main), for example
   `python-repo-fix-2`: extend `batch_utils` with a new function and a new acceptance test.
2. Cue creates one Issue; worker codes → sandbox test → Claude review → PR → auto-merge.
3. Make sure the Issue status and merge receipt are recorded like PR #3.

**Done when:** one new result PR is auto-merged on the new base, and `CATALOG_BASE_ADVANCED` shows up
again for the old task (correct behavior).

## Phase 2 — Onboard the game repo (2–3 days)

### 2a. Staging repo

- Create the private repo `wenn-id/bite-or-betray-staging`, initial content = mirror of `Bite-or-Betray` at `4a08b0d`
  (including Git LFS).
- Branch `autopilot/integration` as the worker's working base. The staging `main` is only changed by syncing
  from the game repo.
- Two-way sync script run by the owner:
  - **game → staging:** after you push to game `main`, `autopilot/integration` is rebased or merged
    from there;
  - **staging → game:** you pull the worker's results with `git fetch staging` and then merge into game `main`,
    or via a regular PR.

### 2b. GitHub App permissions

- Install the publisher App (or a new App "Bite Publisher Game") **only on the staging repo**, with
  Contents + Pull requests + Checks write permissions.
- The publisher code locks the staging repo ID and only `autopilot/*` branches. `main` and
  `autopilot/integration` only advance through the fast-forward gate.
- The real game repo is not given any App.

### 2c. Rolling catalog

- Catalog templates no longer pin a single SHA. A template pins the **ref** `autopilot/integration`.
  The coordinator records its tip SHA at claim time.
- The merge gate still requires the branch tip to still be the same SHA (already exists: "base changed → hold").
- One template per BOB task: `task_type = "bob-0NN"`. It contains:
  - the paths allowed to be touched, taken from the `files` field in `task-queue.json`;
  - the runner checks that must pass;
  - acceptance criteria as review text.
- The catalog generator (`tools/repository_catalog.py`) reads `task-queue.json` → templates. The output is
  reviewed by the owner before being installed as root.

**Done when:** the worker can claim one dummy task in the staging repo and create a draft PR to
`autopilot/integration`, without a single permission on the game repo.

## Phase 3 — C#/Unity lane (3–5 days)

### 3a. Patch format

The current limit (7,200 bytes total, whole files) is not enough: `CandyTableDirector.cs` alone is 1,311 lines.

- Codex sends a **unified diff** per file, not whole files. Max 8 files and 40 KB of diff per task.
- Allowed extensions: `.cs`, `.asmdef`, `.md`, and `ProjectSettings/*.asset` for settings tasks only.
  `.unity`, `.prefab`, `.mat`, `.fbx`, and images are **forbidden** for the worker. Asset changes like that
  are made by Editor scripts run by the runner (see BOB-011).
- `.meta` files for new scripts are created by the Unity runner on import, then committed by the runner. The worker
  does not create GUIDs itself.
- The diff must apply cleanly (`git apply --check`) at the base SHA. Otherwise, the task fails without an automatic
  retry.

### 3b. Unity runner

| Option | Pros | Cons |
|---|---|---|
| **A. Laptop** (`ci/UnityBuild.cs`, `tools/unity_ci.py`, already proven L1/L2) | Ready now, license already exists | Laptop must be on |
| B. Unity Linux on the VPS | Runs while the laptop is off | Needs RAM ≥16 GB, disk ±30 GB, and headless license activation; check Unity license terms |
| C. Separate Windows machine | Closest to the laptop | Machine and license cost |

Recommendation: **start with A**. The runner pulls jobs from the queue (outbound polling), so the laptop does not need to
open a port. Plan B or C before Phase 5.

**Evidence level per runner job:**

| Level | Content | Used for |
|---|---|---|
| L0 | Clean `git apply` + static scene checker (from BOB-024) | All tasks |
| L1 | Compile all assemblies + 42 EditMode tests (growing per task) | All code tasks |
| L2 | Android APK build (Quest profile) + build report | BOB-004, 006, 015, 025 and settings tasks |
| L3 | Test on Quest 3 | Humans only |

The runner writes evidence as a check on the staging PR (same pattern as "Bite OS verification"):
- Editor log SHA;
- test XML results;
- APK SHA.

### 3c. Pure .NET lane (optional, fast)

`Assets/Scripts/Core` is pure C# without UnityEngine. Core tasks (e.g. BOB-016) can be tested with
`dotnet test` on the VPS without Unity. The problem is that the current sandbox forbids process creation and exec. It needs
a second sandbox profile that only allows the `dotnet` toolchain, still with no network and with
pre-cached NuGet packages. If that is too complicated, just skip it and use the Unity runner.

### 3d. Review

Claude review stays advisory, but for the game repo the task's acceptance criteria are added as a checklist.
"Requested changes" findings hold the merge into `autopilot/integration`.

**Done when:** one real BOB task (recommended BOB-016 or BOB-027) passes L0 + L1 from a Codex patch,
gets reviewed, and then lands in `autopilot/integration`.

## Phase 4 — Executing the BOB tasks

### Lane per task

Lane legend:
- **W**: worker + runner L0/L1;
- **W+B**: worker + APK build (L2);
- **R**: runner only, no model;
- **H**: human on Quest 3;
- **D**: owner decision.

| ID | Task (short) | Lane | Depends on | Notes |
|---|---|---|---|---|
| BOB-005 | Baseline compile + 42 EditMode tests | R | – | Already passed at `07c9f7c`; repeat at `4a08b0d` then close |
| BOB-026 | Fix Quest URP references statically | W+B | – | Automated version of BOB-001 |
| BOB-027 | Configure Android Build Scenes | W+B | – | Automated version of BOB-002 |
| BOB-001 | URP on the Quest quality level | R | 026 | Close with Editor log evidence from the runner |
| BOB-002 | Scene list + Android settings | R | 027 | Close with runner evidence |
| BOB-003 | Change app identity | D → W+B | D7 | Worker applies the name from your decision |
| BOB-016 | RuleConfig validation + edge case tests | W | 005 | First task candidate: pure C#, easy to test |
| BOB-004 | Clean up build script (release, persistent report) | W+B | 001, 002 | |
| BOB-008 | Timer stops when the app is paused/loses focus | W | 005 | Add an EditMode test for the timer |
| BOB-009 | Remove per-frame GC allocations, turn off debug logs | W | 005 | |
| BOB-010 | VR raycast passes through non-interactive colliders | W | 005 | |
| BOB-011 | Replace `Shader.Find` with material assets | W | 001, 005 | Materials are created by an Editor script run by the runner, not model-written YAML |
| BOB-013 | VR input robustness | W | 005 | |
| BOB-014 | CandyTableDirector robustness | W | 005 | Large file, diff format required |
| BOB-017 | Wire up TelemetryRecorder | W | 005, 014 | |
| BOB-018 | Gameplay action audio | D → W | 005, 014 | Needs audio files from you (or an approved SFX source) |
| BOB-020 | RefreshProps optimization + performance harness | W | 009 | |
| BOB-012 | Restart from inside VR | W | 005, 010 | |
| BOB-019 | Short onboarding | D → W | 012 | Needs approval of the onboarding text content |
| BOB-024 | CI draft, static scene checker, documentation | W | 004, 005 | Its static checker is used in the L0 gate |
| BOB-006 | First APK build via the Quest profile | W+B | 001–005 | L2 staging already exists at `07c9f7c` (development, template package ID) |
| BOB-007 | APK smoke test on Quest 3 | H | 006 | Worker prepares the APK + checklist |
| BOB-015 | Integration build + gameplay validation on Quest 3 | W+B → H | 007–014 | |
| BOB-021 | Cartoon face/body in the gameplay scene | **Revise** | 006 | Already superseded by the Meshy full-body cast (`4a08b0d`). Change it to "facial expressions/blinking for the Meshy cast" and do it in an art session, not by the worker |
| BOB-022 | Performance tuning on Quest 3 (72 FPS) | H → W | 015, 020, 021 | You record the profiler, the worker applies the tuning |
| BOB-023 | Full regression + visual sign-off | H | 016–019, 022 | |
| BOB-025 | Feature freeze + release candidate package | D + W+B | 023, 024 | |

### Waves

With 1 concurrent job, the order within a wave follows priority. With 10 items per day,
roughly 4–5 tasks per day get done, if nothing fails.

1. **Wave A (no dependencies):** BOB-005 (R), 026, 027, 016, then 001/002 closed (R) and
   003 after D7.
2. **Wave B:** 004, 008, 009, 010, 011, 013, 014.
3. **Wave C:** 017, 018, 020, 012, 024, then 006 (APK build).
4. **Human gate 1:** 007 smoke test on Quest 3. The result goes into the Issue as L3 evidence.
5. **Wave D:** 015 integration build → human gate (gameplay validation) → 019.
6. **Human gate 2:** 022 profiling on Quest → worker applies tuning → 023 regression → 025 RC.

Every task finishes in `autopilot/integration`. At the end of every wave, you pull
`autopilot/integration` into game `main` after reading the PR summary (Phase 2a).

### Queue revisions before starting

- Use `task-queue.proposed.json` (which has BOB-026/027) as the source, then turn 001/002 into "close with
  runner evidence".
- Mark BOB-005 and part of BOB-006 with the evidence that already exists.
- Rewrite BOB-021 to match the state of `4a08b0d`.
- For every task, add a `lane` field (W, W+B, R, H, D) and `checks` (L0, L1, L2) so the catalog generator
  can read them directly.

## Phase 5 — Scale and operations

- **Daily report:** Cue posts a summary in the engineering Issue: tasks done, failed, held;
  remaining quota; runner status.
- **Alerts (D8):** runner not responding for more than 30 minutes, three consecutive failures on one task,
  quota exhausted, or the gate holding a merge because of an ambiguous result.
- **Failures:**
  - task fails L1 twice → status `needs-human`, not retried automatically;
  - patch cannot be applied → the task is rebased by a new Issue revision (revision +1), not silently retried.
- **Rollback:**
  - stop the worker: `tools/upgrade_repository.py disable` and `tools/upgrade_auto_merge.py disable`
    (already exist);
  - results already in `autopilot/integration`: undone with a revert commit by the owner;
  - game `main` is never touched by the worker.
- **Runner without the laptop:** move to option B or C after 2 weeks of stable operation.

## Capacity

| Component | Calculation |
|---|---|
| Tasks done by the worker | ± 19 |
| Work items per task | 2 (coding + review) + ± 30% retries ≈ 2.6 |
| Total | ± 50 work items |
| Quota of 5 per day | ± 10 working days |
| Quota of 10 per day | ± 5 working days |
| Runner per job | L1 ± 10–15 minutes, L2 ± 20–30 minutes (estimate from laptop evidence) |

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Model changes scene or prefab YAML incorrectly | Broken scene, hard to review | Forbidden in the patch format; assets are changed via runner Editor scripts |
| Laptop runner goes down mid-wave | Queue stops | Jobs held safely; alert; option B/C |
| Base shifts often because you also push to game `main` | Tasks held | Scheduled staging sync; new tasks claim at the latest tip |
| Patch passes tests but behaves wrongly in VR | Bugs only visible on the Quest | Human gates 007/015/023; review with acceptance criteria |
| Subscription permission does not match the terms | Account problems | D1 before scaling up |
| Auto-merge without branch protection | Safeguard only on the VPS | Game repo is never given App access; game `main` is manual |

## What cannot be automated

- **Testing on Quest 3:** BOB-007, 015, 022, 023 need a headset and a person.
- **Content and identity decisions:** BOB-003 (package ID), 018 (audio files), 019 (onboarding text),
  025 (when to freeze and release).
- **Art work:** Meshy models, facial expressions, and textures stay in art sessions (Blender + Unity), not the
  worker lane.

## First steps

1. **You:** decide D1, D2, D4–D8 (D2 most urgent; D3 already decided), then write them in one engineering Issue.
2. **Codex:** Phase 1 (new Python template) to prove the cycle runs again.
3. **Codex:** create the staging repo + restricted App + rolling catalog (Phase 2).
4. **Codex + laptop:** Unity runner polling L0/L1, then run BOB-005 (R) as the runner's first test.
5. **Worker:** BOB-016 as the first C# task; if it passes, open Wave A.
